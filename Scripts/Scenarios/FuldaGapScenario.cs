using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Godot;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Models;
using ColdWarWargame.Factories;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Scenarios;

namespace ColdWarWargame.Scenarios
{
    /// <summary>
    /// Fulda Gap 1989 场景 — 50x30 网格，红军从东进攻，蓝军在西防守。
    /// 地形数据直接嵌入 C# 代码（纯文本字符串矩阵），无需外部文件 IO。
    /// </summary>
    public class FuldaGapScenario
    {
        const int MAP_W = 50;
        const int MAP_H = 30;
        public const string DefaultOccupationStatePath = "res://Scripts/Data/Scenarios/Fulda_Gap/occupation_state.json";
        public const string SavedOccupationStatePath = "user://Fulda_Gap_occupation_state.json";

        static readonly string[] TerrainRows = {
            "00000000000001100000000000000000000011111110000000", // 0
            "00000000000001110000000000000000000011111111000000", // 1
            "00000000000011110000000000000000000001111110000000", // 2
            "00000000000011111000000000000000000000111100000000", // 3
            "00000000000111111000000000000000000000011000000000", // 4
            "00000300000111110000000000220000000000000000000000", // 5
            "00000000001111110000000000000000000000000000000000", // 6
            "00000000001111100000000000000000000000000000000000", // 7
            "00000000011111100000000000000000000000000000000000", // 8
            "00000000011111000000000000000000000220000000200000", // 9
            "00000000111111000000000000000000000000000000333000", // 10
            "00000001111111000000000000000000000000000000002000", // 11
            "00000001111110000000000000000000003200000000000000", // 12
            "00000011111110000000000000000000011100000000000000", // 13
            "00000011111100000000000330000000011110000000000000", // 14
            "00000011111100000000000330000000111110000000000000", // 15
            "00000011111000000000000000000000111111000000000000", // 16
            "00000001111000000000000000000001111111000000000000", // 17
            "00000000110000000000000000000011111111100000000000", // 18
            "00000000000000000000000000000111111111110000000000", // 19
            "00000000000000000000000000000111111111111000000000", // 20
            "00000000000000000000000000001111111111111100000000", // 21
            "00000000000000000000111000001111111111111110000000", // 22
            "00000000000000000011111000001111111111111110000000", // 23
            "00330000000000000111111000001111111111111111000000", // 24
            "00330000000000001111111100001111111111111111000000", // 25
            "00000000000000011111111100000111111111111111000000", // 26
            "00000000000000111111111100000011111111111110000000", // 27
            "00002000000001111111111100000001111111111000000000", // 28
            "00000000000001111111111000000000111111110000000000", // 29
        };

        static readonly string[] InfraRows = {
            "2200110011000000000000000000200000000000000000000000", // 0
            "2200101100100000000000000000200000000000000000000000", // 1
            "2200100000011000000000000000200000000000000000000000", // 2
            "2200200000000111000000000000200000000000000000000000", // 3
            "2200200000000000111000000000200000000000000000000000", // 4
            "2200200100000000000111111111222222221100000000000000", // 5
            "2200200100000000000000000001200000000220000000000000", // 6
            "2200200100000000000000000001200000000022000000000000", // 7
            "2200200010000000000000000001200000000002000000000000", // 8
            "2200200010000000000000000001200000000002200000010000", // 9
            "2200200001000000000000000001200000000000222222211112", // 10
            "2200200001000000000000000001200000000011100000010000", // 11
            "2200200000100000000000000001200000001100000000010000", // 12
            "2200200000100000000000000001200000010000000000010000", // 13
            "2200200000010000000000000011211111100000000000010000", // 14
            "2200200000001000000000001100200000000000000000010000", // 15
            "2200200000000111000000020000200000000000000000100000", // 16
            "2200200000000000111111200000200000000000000000100000", // 17
            "2200200000000000000002000000200000000000000001000000", // 18
            "2200200000000000000020000000200000000000000010000000", // 19
            "2200200000000000000200000000200000000000000010000000", // 20
            "2200200000000000002000000000200000000000000100000000", // 21
            "2200200000000000020000000000200000000000001000000000", // 22
            "2200200000000000200000000000200000000000010000000000", // 23
            "2200222222222222000000000000200000000000100000000000", // 24
            "2200200000000000000000000000200000000001000000000000", // 25
            "2200200000000000000000000000200000000010000000000000", // 26
            "2200200000000000000000000000200000000100000000000000", // 27
            "2200200000000000000000000000200000001000000000000000", // 28
            "2200200000000000000000000000200000010000000000000000", // 29
        };

        // Supply special layer (independent from infra/road layer):
        // '0' = none, 'H' = hub, 'A' = airport, 'B' = both hub + airport on same tile.
        static readonly string[] SupplySpecialRows = {
            "2200000000000000000000000000000000000000000000000000", // 0
            "2200000000000000000000000000000000000000000000000000", // 1
            "2200000000000000000000000000000000000000000000000000", // 2
            "2200000000000000000000000000000000000000000000000000", // 3
            "2200000000000000000000000000000000000000000000000000", // 4
            "2200000H00000000000000000000000000000000000000000000", // 5
            "2200000000000000000000000000000000000000000000000000", // 6
            "2200000000000000000000000000000000000000000000000000", // 7
            "2200000000000000000000000000000000000000000000000000", // 8
            "2200000000000000000000000000000000000000000000000000", // 9
            "22000000000000000000000000000000000000000000000B0000", // 10
            "22000000000000000000000000000000000000000000000000H0", // 11
            "220000000000000000000000000000000000A000000000000000", // 12
            "2200000000000000000000000000000000000000000000000000", // 13
            "2200000000000000000000000A00000000000000000000000000", // 14
            "2200000000000000000000000000000000000000000000000000", // 15
            "2200000000000000000000000000000000000000000000000000", // 16
            "2200000000000000000000000000000000000000000000000000", // 17
            "220000000000000000A000000000000000000000000000000000", // 18
            "2200000000000000000000000000000000000000000000000000", // 19
            "2200000000000000000000000000000000000000000000000000", // 20
            "2200000000000000000000000000000000000000000000000000", // 21
            "2200000000000000000000000000000000000000000000000000", // 22
            "2200000000000000000000000000000000000000000000000000", // 23
            "22000B0000000000000000000000000000000000000000000000", // 24
            "2200000000000000000000000000000000000000000000000000", // 25
            "2200000000000000000000000000000000000000000000000000", // 26
            "2200000000000000000000000000000000000000000000000000", // 27
            "2200000000000000000000000000000000000000000000000000", // 28
            "2200000000000000000000000000000000000000000000000000", // 29
        };

        public Systems.Battlefield.GridMap Map { get; private set; }
        public MovementResolver Movement { get; private set; }
        public ZOCManager ZOC { get; private set; }
        public int[,] OccupationMap { get; private set; }
        public HashSet<Vector2I> SupplyHubs { get; private set; } = new();
        public HashSet<Vector2I> SupplyAirports { get; private set; } = new();

        public List<(Battalion bat, Vector2I pos)> BlueBattalions { get; private set; } = new();
        public List<(Battalion bat, Vector2I pos)> RedBattalions { get; private set; } = new();

        public FuldaGapScenario()
        {
            BuildMap();
            BuildSupplySpecialNodes();
            Movement = new MovementResolver(Map);
            ZOC = new ZOCManager(Map);
            LoadOccupationState();
        }

        void BuildMap()
        {
            var terrain = new int[MAP_H, MAP_W];
            var infra = new int[MAP_H, MAP_W];

            for (int y = 0; y < MAP_H; y++)
            {
                string row = TerrainRows[y].PadRight(MAP_W, '0');
                string infraRow = InfraRows[y].PadRight(MAP_W, '0');
                for (int x = 0; x < MAP_W; x++)
                {
                    terrain[y, x] = row[x] - '0';
                    infra[y, x] = infraRow[x] - '0';
                }
            }

            Map = Systems.Battlefield.GridMap.FromLayers(terrain, infra);
        }

        void BuildSupplySpecialNodes()
        {
            SupplyHubs.Clear();
            SupplyAirports.Clear();

            for (int y = 0; y < MAP_H; y++)
            {
                string row = SupplySpecialRows[y].PadRight(MAP_W, '0');
                for (int x = 0; x < MAP_W; x++)
                {
                    var pos = new Vector2I(x, y);
                    switch (char.ToUpperInvariant(row[x]))
                    {
                        case 'H':
                            SupplyHubs.Add(pos);
                            break;
                        case 'A':
                            SupplyAirports.Add(pos);
                            break;
                        case 'B':
                            SupplyHubs.Add(pos);
                            SupplyAirports.Add(pos);
                            break;
                    }
                }
            }
        }

        public (HashSet<Vector2I> hubs, HashSet<Vector2I> airports) GetSupplySpecialNodes()
        {
            return (new HashSet<Vector2I>(SupplyHubs), new HashSet<Vector2I>(SupplyAirports));
        }

        public void LoadOOB(string bluePath, string redPath)
        {
            BlueBattalions.Clear();
            RedBattalions.Clear();

            using var blueFile = FileAccess.Open(bluePath, FileAccess.ModeFlags.Read);
            if (blueFile != null)
            {
                var json = blueFile.GetAsText();
                var doc = JsonDocument.Parse(json);
                var entries = doc.RootElement.GetProperty("faction_blue").EnumerateArray();
                foreach (var entry in entries)
                {
                    var id = entry.GetProperty("instance_id").GetString();
                    var tid = entry.GetProperty("template_id").GetString();
                    int x = entry.GetProperty("x").GetInt32();
                    int y = entry.GetProperty("y").GetInt32();
                    var bat = BattalionFactory.CreateFullBattalion(id, tid, 1);

                    if (entry.TryGetProperty("structure_overrides", out JsonElement structureOverrides))
                        BattalionFactory.ApplyStructureOverrides(bat, structureOverrides);

                    if (entry.TryGetProperty("state_overrides", out JsonElement stateOverrides))
                        BattalionFactory.ApplyStateOverrides(bat, stateOverrides);

                    BlueBattalions.Add((bat, new Vector2I(x, y)));
                }
            }

            using var redFile = FileAccess.Open(redPath, FileAccess.ModeFlags.Read);
            if (redFile != null)
            {
                var json = redFile.GetAsText();
                var doc = JsonDocument.Parse(json);
                var entries = doc.RootElement.GetProperty("faction_red").EnumerateArray();
                foreach (var entry in entries)
                {
                    var id = entry.GetProperty("instance_id").GetString();
                    var tid = entry.GetProperty("template_id").GetString();
                    int x = entry.GetProperty("x").GetInt32();
                    int y = entry.GetProperty("y").GetInt32();
                    var bat = BattalionFactory.CreateFullBattalion(id, tid, 2);

                    if (entry.TryGetProperty("structure_overrides", out JsonElement structureOverrides))
                        BattalionFactory.ApplyStructureOverrides(bat, structureOverrides);

                    if (entry.TryGetProperty("state_overrides", out JsonElement stateOverrides))
                        BattalionFactory.ApplyStateOverrides(bat, stateOverrides);

                    RedBattalions.Add((bat, new Vector2I(x, y)));
                }
            }

            GD.Print("场景 OOB 加载完成：蓝军 " + BlueBattalions.Count + " 个营，红军 " + RedBattalions.Count + " 个营");
        }

        public void LoadOccupationState()
        {
            if (OccupationStateCodec.TryLoad(DefaultOccupationStatePath, MAP_W, MAP_H, out var savedMap))
            {
                OccupationMap = savedMap;
                return;
            }

            OccupationMap = OccupationStateCodec.CreateDefaultHalfHalf(MAP_W, MAP_H);
        }

        public void SaveOccupationState(int[,] controlMap)
        {
            OccupationMap = OccupationStateCodec.CloneMap(controlMap);
            OccupationStateCodec.Save(SavedOccupationStatePath, OccupationMap);
        }

        public void ApplyOccupationState(int[,] controlMap)
        {
            OccupationMap = OccupationStateCodec.CloneMap(controlMap);
        }

        public int[,] GetOccupationMap()
        {
            if (OccupationMap == null || OccupationMap.GetLength(0) != MAP_W || OccupationMap.GetLength(1) != MAP_H)
                LoadOccupationState();

            return OccupationStateCodec.CloneMap(OccupationMap);
        }

        public void PrintSummary()
        {
            GD.Print("========== Fulda Gap 1985 场景摘要 ==========");
            GD.Print("地图尺寸: " + MAP_W + " x " + MAP_H + " (共 " + (MAP_W * MAP_H) + " 格)");

            Map.PrintCostGrid();

            GD.Print("--- 蓝军（NATO） ---");
            foreach (var (bat, pos) in BlueBattalions)
            {
                var tile = Map.GetTile(pos);
                GD.Print("  " + bat.Name + " @ (" + pos.X + "," + pos.Y + ") 地形=" + tile.TerrainType + " 移动成本=" + tile.GetMovementCost());
            }

            GD.Print("--- 红军（Warsaw Pact） ---");
            foreach (var (bat, pos) in RedBattalions)
            {
                var tile = Map.GetTile(pos);
                GD.Print("  " + bat.Name + " @ (" + pos.X + "," + pos.Y + ") 地形=" + tile.TerrainType + " 移动成本=" + tile.GetMovementCost());
            }

            var terrainCount = new int[5];
            for (int y = 0; y < MAP_H; y++)
                for (int x = 0; x < MAP_W; x++)
                {
                    int t = Map.GetTile(new Vector2I(x, y)).TerrainType;
                    if (t >= 0 && t <= 4) terrainCount[t]++;
                }
            GD.Print("--- 地形统计 ---");
            GD.Print("  平原: " + terrainCount[0] + " 格");
            GD.Print("  森林: " + terrainCount[1] + " 格");
            GD.Print("  半城镇: " + terrainCount[2] + " 格");
            GD.Print("  城镇: " + terrainCount[3] + " 格");
            GD.Print("--- 补给设施统计 ---");
            GD.Print("  Hub: " + SupplyHubs.Count + " 格");
            GD.Print("  Airport: " + SupplyAirports.Count + " 格");

            GD.Print("============================================");
        }


        /// <summary>缁ｉ崜宕遍悽銊﹀灇鏉╂瑤绠炴禒銉ㄧ箾閻ㄥ嫯骞忛崣鏍灟闁偀濮為柅婊堟▔缂?/summary>
        public void RemoveDeadBattalions()
        {
            BlueBattalions.RemoveAll(u => !u.bat.HasSurvivingSubUnits);
            RedBattalions.RemoveAll(u => !u.bat.HasSurvivingSubUnits);
        }

        public void PrintReachableFor(string label, Vector2I pos, float ap)
        {
            bool noBlock(Vector2I p) => false;
            var reachable = Movement.GetReachableTiles(pos, ap, noBlock, noBlock);
            GD.Print(label + " @" + pos + " 在 " + ap + " AP 下可达 " + reachable.Count + " 格");
        }

        public void RunDemoCombat(Battalion atk, Vector2I atkPos, Battalion def, Vector2I defPos)
        {
            var defTerrain = Map.GetTile(defPos);
            float bonus = defTerrain.TerrainType switch { 1 => 0.1f, 2 => 0.3f, 3 => 0.4f, _ => 0f };

            var ctx = new CombatContext { DefenderTerrainBonus = bonus, AttackerOOSTurns = 0, DefenderOOSTurns = 0 };
            var resolver = new CombatResolver();
            var result = resolver.ComputeAdvantage(atk, def, ctx);
            var fullResult = resolver.ResolveCombat(atk, def, ctx, 42);

            GD.Print("--- 演示战斗 ---");
            GD.Print("  " + atk.Name + "(" + atkPos + ") -> " + def.Name + "(" + defPos + ")");
            GD.Print("  防御方地形: " + defTerrain.TerrainType + " -> 加成 +" + bonus);
            GD.Print("  战斗优势分 V = " + result.Value.ToString("0.00"));
            GD.Print("  进攻方损失: " + fullResult.AttackerHpLost + " HP");
            GD.Print("  防御方损失: " + fullResult.DefenderHpLost + " HP");
            foreach (var m in result.Modifiers)
            {
                if (m.Source.StartsWith("Terrain") || m.Source.StartsWith("HeavyArmor") || m.Source.StartsWith("Armor"))
                    GD.Print("    " + m.Source + ": " + m.Value.ToString("0.0") + " (" + m.Reason + ")");
            }
            GD.Print("----------------");
        }
    }
}
