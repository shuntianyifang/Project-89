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
    /// 地图来自带来源记录的历史地图 JSON，东西方向补给。
    /// </summary>
    public class FuldaGapScenario
    {
        const int MAP_W = 50;
        const int MAP_H = 30;
        public const string DefaultOccupationStatePath = "res://Scripts/Data/Scenarios/Fulda_Gap/occupation_state.json";
        public const string SavedOccupationStatePath = "user://Fulda_Gap_occupation_state.json";

        private HistoricalMapData _historicalMap;

        public Systems.Battlefield.GridMap Map { get; private set; }
        public MovementResolver Movement { get; private set; }
        public ZOCManager ZOC { get; private set; }
        public int[,] OccupationMap { get; private set; }
        public HashSet<Vector2I> SupplyHubs { get; private set; } = new();
        public HashSet<Vector2I> SupplyAirports { get; private set; } = new();

        public List<(Battalion bat, Vector2I pos)> BlueBattalions { get; private set; } = new();
        public List<(Battalion bat, Vector2I pos)> RedBattalions { get; private set; } = new();

        public FuldaGapScenario(string mapPath = "res://Scripts/Data/Scenarios/Fulda_Gap/historical_map.json", string occupationPath = DefaultOccupationStatePath)
        {
            _historicalMap = HistoricalMapData.Load(mapPath);
            BuildMap();
            BuildSupplySpecialNodes();
            Movement = new MovementResolver(Map);
            ZOC = new ZOCManager(Map);
            LoadOccupationState(occupationPath);
        }

        void BuildMap()
        {
            Map = _historicalMap.Build();
        }

        void BuildSupplySpecialNodes()
        {
            SupplyHubs = HistoricalMapData.Points(_historicalMap.Hubs);
            SupplyAirports = HistoricalMapData.Points(_historicalMap.Airports);
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
                    if (entry.TryGetProperty("name", out var name)) bat.Name = name.GetString();

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
                    if (entry.TryGetProperty("name", out var name)) bat.Name = name.GetString();

                    if (entry.TryGetProperty("structure_overrides", out JsonElement structureOverrides))
                        BattalionFactory.ApplyStructureOverrides(bat, structureOverrides);

                    if (entry.TryGetProperty("state_overrides", out JsonElement stateOverrides))
                        BattalionFactory.ApplyStateOverrides(bat, stateOverrides);

                    RedBattalions.Add((bat, new Vector2I(x, y)));
                }
            }

            GD.Print("场景 OOB 加载完成：蓝军 " + BlueBattalions.Count + " 个营，红军 " + RedBattalions.Count + " 个营");
        }

        public void LoadOccupationState(string path = DefaultOccupationStatePath)
        {
            if (OccupationStateCodec.TryLoad(path, MAP_W, MAP_H, out var savedMap))
            {
                OccupationMap = savedMap;
                return;
            }

            throw new InvalidOperationException("历史场景缺少有效初始控制图");
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
            GD.Print("========== Fulda Gap 1989 场景摘要 ==========");
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
