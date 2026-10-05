using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Systems.Victory;

namespace ColdWarWargame.Scenarios
{
    public sealed class CampaignSave
    {
        public int Version { get; set; } = 1;
        public string ScenarioId { get; set; } = "fulda_gap_1989";
        public int Faction { get; set; }
        public int Turn { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Control { get; set; }
        public int[] Statistics { get; set; }
        public List<SavedBattalion> Units { get; set; } = new();
        public const string DefaultPath = "user://Fulda_Gap_campaign.json";

        public static CampaignSave Capture(FuldaGapScenario scenario, TurnManager turns, VictoryTracker victory)
        {
            var control = scenario.GetOccupationMap();
            return new CampaignSave {
                Faction = turns.CurrentFaction, Turn = turns.TurnNumber,
                Width = scenario.Map.Width, Height = scenario.Map.Height,
                Control = control.Cast<int>().ToArray(), Statistics = victory.CaptureStatistics(),
                Units = scenario.BlueBattalions.Concat(scenario.RedBattalions)
                    .Select(u => SavedBattalion.Capture(u.bat, u.pos)).ToList()
            };
        }

        public void Write(string path = DefaultPath)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null) throw new InvalidOperationException("无法写入存档：" + FileAccess.GetOpenError());
            file.StoreString(JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static CampaignSave Read(string path = DefaultPath)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) throw new InvalidOperationException("存档不存在或无法读取");
            return JsonSerializer.Deserialize<CampaignSave>(file.GetAsText()) ?? throw new InvalidOperationException("存档为空");
        }

        public void Apply(FuldaGapScenario scenario, TurnManager turns, VictoryTracker victory)
        {
            if (Version != 1 || ScenarioId != "fulda_gap_1989" || Width != scenario.Map.Width || Height != scenario.Map.Height ||
                Control == null || Control.Length != Width * Height || Control.Any(v => v < 0 || v > 2) ||
                Faction is not (1 or 2) || Turn < 1 || Statistics == null || Statistics.Length != 7 || Statistics.Any(v => v < 0) || Units == null)
                throw new InvalidOperationException("存档版本、场景或状态不匹配");
            // Construct and validate everything before mutating the live scenario.
            var restored = Units.Select(u => (bat: u.Restore(), pos: new Vector2I(u.X, u.Y))).ToList();
            if (restored.Any(u => !scenario.Map.IsInBounds(u.pos) || !scenario.Map.GetTile(u.pos).IsPassable) ||
                restored.Select(u => u.pos).Distinct().Count() != restored.Count ||
                restored.Select(u => u.bat.InstanceId).Distinct().Count() != restored.Count)
                throw new InvalidOperationException("存档单位坐标或 ID 无效");
            var control = new int[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++) control[x, y] = Control[x * Height + y];
            scenario.BlueBattalions.Clear(); scenario.RedBattalions.Clear();
            foreach (var u in restored)
            {
                (u.bat.Faction == 1 ? scenario.BlueBattalions : scenario.RedBattalions).Add(u);
            }
            turns.ReplaceBattalions(restored.Select(u => u.bat));
            scenario.ApplyOccupationState(control);
            turns.RestoreStrategicState(Faction, Turn);
            victory.RestoreStatistics(Statistics);
        }
    }

    public sealed class SavedBattalion
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public string Role { get; set; }
        public int Faction { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public float AP { get; set; }
        public int Fatigue { get; set; }
        public int OOS { get; set; }
        public bool WasOOS { get; set; }
        public bool Recon { get; set; }
        public HashSet<string> Tags { get; set; }
        public List<SavedCompany> Companies { get; set; }
        public static SavedBattalion Capture(Battalion b, Vector2I p) => new() {
            Id = b.InstanceId, Name = b.Name, Template = b.TemplateId, Role = b.TemplateRole,
            Faction = b.Faction, X = p.X, Y = p.Y, AP = b.CurrentAP, Fatigue = b.Fatigue,
            OOS = b.TurnsOOS, WasOOS = b.WasOOSLastTurn, Recon = b.IsAdvancedReconBattalion,
            Tags = new HashSet<string>(b.BattalionTags), Companies = b.Companies.Select(c => new SavedCompany {
                Id = c.CompanyId, Name = c.Name, Platoons = c.Platoons.Select(p => new SavedPlatoon {
                    Id = p.PlatoonId, Type = p.Type, Units = p.Units.Select(u => new SavedUnit {
                        Id = u.UnitId, Node = u.NodeId, Category = u.Category, HP = u.CurrentHp
                    }).ToList()
                }).ToList()
            }).ToList()
        };
        public Battalion Restore()
        {
            if (string.IsNullOrWhiteSpace(Id) || Faction is not (1 or 2) || !float.IsFinite(AP) || AP < 0 || AP > 12 ||
                Fatigue < 0 || Fatigue > Battalion.FatigueOverflowCap || OOS < 0 || Companies == null || Tags == null)
                throw new InvalidOperationException("存档营状态无效");
            ColdWarWargame.Data.TOE.TemplateDatabase.GetTemplate(Template);
            var b = new Battalion { InstanceId = Id, Name = Name, TemplateId = Template, TemplateRole = Role,
                Faction = Faction, CurrentAP = AP, Fatigue = Fatigue, TurnsOOS = OOS, WasOOSLastTurn = WasOOS,
                IsAdvancedReconBattalion = Recon, BattalionTags = new HashSet<string>(Tags, StringComparer.OrdinalIgnoreCase) };
            foreach (var c in Companies)
            {
                var company = new Company { CompanyId = c.Id, Name = c.Name };
                foreach (var p in c.Platoons)
                {
                    var platoon = new Platoon { PlatoonId = p.Id, Type = p.Type };
                    foreach (var u in p.Units)
                    {
                        var unit = new SubUnitInstance(u.Id) { NodeId = u.Node, Category = u.Category };
                        if (u.HP < 0 || u.HP > unit.Template.CombatStats.MaxHp) throw new InvalidOperationException("存档 HP 无效");
                        unit.CurrentHp = u.HP; platoon.Units.Add(unit);
                    }
                    company.Platoons.Add(platoon);
                }
                b.Companies.Add(company);
            }
            return b;
        }
    }
    public sealed class SavedCompany { public string Id { get; set; } public string Name { get; set; } public List<SavedPlatoon> Platoons { get; set; } }
    public sealed class SavedPlatoon { public string Id { get; set; } public string Type { get; set; } public List<SavedUnit> Units { get; set; } }
    public sealed class SavedUnit { public string Id { get; set; } public string Node { get; set; } public string Category { get; set; } public int HP { get; set; } }
}
