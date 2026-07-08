using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;

namespace ColdWarWargame.Systems.Victory
{
    // ========== 胜利等级枚举 ==========

    public enum VictoryLevel
    {
        CrushingDefeat,     // R < 0.25
        MajorDefeat,        // 0.25 <= R < 0.5
        MarginalDefeat,     // 0.5 <= R < 0.8
        Stalemate,          // 0.8 <= R < 1.25
        MarginalVictory,    // 1.25 <= R < 2.0
        MajorVictory,       // 2.0 <= R < 4.0
        DecisiveVictory     // R >= 4.0
    }

    public static class VictoryLevelExtensions
    {
        public static string DisplayName(this VictoryLevel l) => l switch
        {
            VictoryLevel.CrushingDefeat => "一败涂地",
            VictoryLevel.MajorDefeat => "重大失利",
            VictoryLevel.MarginalDefeat => "边缘失利",
            VictoryLevel.Stalemate => "血腥僵局",
            VictoryLevel.MarginalVictory => "边缘胜利",
            VictoryLevel.MajorVictory => "重大胜利",
            VictoryLevel.DecisiveVictory => "决定性胜利",
            _ => "未知"
        };
    }

    // ========== 评估结果 ==========

    public class VictoryAssessment
    {
        public float Ratio { get; set; }
        public int BlueVP { get; set; }
        public int RedVP { get; set; }
        public VictoryLevel BlueLevel { get; set; }
        public int BlueControlled { get; set; }
        public int RedControlled { get; set; }
        public int TurnNumber { get; set; }

        public VictoryLevel RedLevel => BlueLevel switch
        {
            VictoryLevel.CrushingDefeat => VictoryLevel.DecisiveVictory,
            VictoryLevel.MajorDefeat => VictoryLevel.MajorVictory,
            VictoryLevel.MarginalDefeat => VictoryLevel.MarginalVictory,
            VictoryLevel.Stalemate => VictoryLevel.Stalemate,
            VictoryLevel.MarginalVictory => VictoryLevel.MarginalDefeat,
            VictoryLevel.MajorVictory => VictoryLevel.MajorDefeat,
            VictoryLevel.DecisiveVictory => VictoryLevel.CrushingDefeat,
            _ => VictoryLevel.Stalemate
        };
    }

    // ========== 胜利追踪器 ==========

    public class VictoryTracker
    {
        public int BlueVP { get; internal set; }
        public int RedVP { get; internal set; }
        public int BlueSoldierLossTotal { get; private set; }
        public int BlueVehicleLossTotal { get; private set; }
        public int RedSoldierLossTotal { get; private set; }
        public int RedVehicleLossTotal { get; private set; }
        public int CombatCount { get; private set; }

        public int BlueControlledCount => _blueControlled.Count;
        public int RedControlledCount => _redControlled.Count;
        public int[,] ControlMap => _controlMap;

        private HashSet<Vector2I> _blueControlled = new();
        private HashSet<Vector2I> _redControlled = new();
        private int[,] _controlMap = new int[0, 0];

        // ---- 1. 战损VP ----

        /// <summary>
        /// 记录一次战斗的VP。
        /// 攻击方获得防御方被摧毁单位的Cost作为VP，反之亦然。
        /// </summary>
        public void RecordCombatResult(CombatResolutionResult result, int attackerFaction)
        {
            CombatCount++;

            var attackerLosses = CombatUtils.CountDestroyedUnitLosses(result?.AttackerCasualties);
            var defenderLosses = CombatUtils.CountDestroyedUnitLosses(result?.DefenderCasualties);

            if (attackerFaction == 1)
            {
                BlueSoldierLossTotal += attackerLosses.soldiers;
                BlueVehicleLossTotal += attackerLosses.vehicles;
                RedSoldierLossTotal += defenderLosses.soldiers;
                RedVehicleLossTotal += defenderLosses.vehicles;
            }
            else
            {
                RedSoldierLossTotal += attackerLosses.soldiers;
                RedVehicleLossTotal += attackerLosses.vehicles;
                BlueSoldierLossTotal += defenderLosses.soldiers;
                BlueVehicleLossTotal += defenderLosses.vehicles;
            }

            int atkVP = result.DefenderCasualties
                .Where(c => c.IsDestroyed)
                .Sum(c => c.Unit.Cost);

            int defVP = result.AttackerCasualties
                .Where(c => c.IsDestroyed)
                .Sum(c => c.Unit.Cost);

            if (attackerFaction == 1)
            {
                BlueVP += atkVP;   // 蓝军摧毁红军单位
                RedVP += defVP;    // 红军摧毁蓝军单位
            }
            else
            {
                RedVP += atkVP;    // 红军摧毁蓝军单位
                BlueVP += defVP;   // 蓝军摧毁红军单位
            }
        }

        public string BuildCampaignCasualtySummary()
        {
            return string.Join("\n", new[]
            {
                "Campaign Casualty Stats",
                "Battles resolved: " + CombatCount,
                "Blue losses: soldiers " + BlueSoldierLossTotal + ", vehicles " + BlueVehicleLossTotal,
                "Red losses: soldiers " + RedSoldierLossTotal + ", vehicles " + RedVehicleLossTotal,
                "Blue total losses: " + (BlueSoldierLossTotal + BlueVehicleLossTotal),
                "Red total losses: " + (RedSoldierLossTotal + RedVehicleLossTotal)
            });
        }

        // ---- 2. 地理控制 ----

        /// <summary>
        /// 基于各单位位置和 ZOC 更新控制区。
        /// 网格被一方ZOC覆盖且不被对方ZOC覆盖时，由该方控制。
        /// 双方ZOC重叠区域为中立区。
        /// </summary>
        public void UpdateControl(Battlefield.GridMap map,
            HashSet<Vector2I> blueUnitPositions,
            HashSet<Vector2I> redUnitPositions,
            ZOCManager zocMgr)
        {
            _blueControlled.Clear();
            _redControlled.Clear();
            _controlMap = new int[map.Width, map.Height];

            var blueZOC = zocMgr.GetFactionZOC(blueUnitPositions);
            var redZOC = zocMgr.GetFactionZOC(redUnitPositions);

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var p = new Vector2I(x, y);
                    bool inBlue = blueZOC.Contains(p);
                    bool inRed = redZOC.Contains(p);

                    if (inBlue && !inRed)
                    {
                        _blueControlled.Add(p);
                        _controlMap[x, y] = 1;
                    }
                    else if (inRed && !inBlue)
                    {
                        _redControlled.Add(p);
                        _controlMap[x, y] = 2;
                    }
                    else
                    {
                        _controlMap[x, y] = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 按 PRD 占领规则更新控制图：
        /// 1) 单位进入格子立即覆写控制权；
        /// 2) 自身 ZOC 且不受敌方 ZOC 影响的格子覆写控制权；
        /// 3) 双方 ZOC 冲突格子变为中立；
        /// 4) 非影响区域保持原控制权不变。
        /// </summary>
        public int[,] UpdateOccupationFromEntryAndZOC(
            Battlefield.GridMap map,
            int[,] currentControlMap,
            HashSet<Vector2I> blueUnitPositions,
            HashSet<Vector2I> redUnitPositions,
            ZOCManager zocMgr,
            IEnumerable<Vector2I> blueEnteredTiles = null,
            IEnumerable<Vector2I> redEnteredTiles = null,
            IEnumerable<Vector2I> bluePathZocTiles = null,
            IEnumerable<Vector2I> redPathZocTiles = null)
        {
            int width = map.Width;
            int height = map.Height;

            var updated = new int[width, height];
            if (currentControlMap != null &&
                currentControlMap.GetLength(0) == width &&
                currentControlMap.GetLength(1) == height)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                        updated[x, y] = currentControlMap[x, y];
                }
            }

            var blueZOC = zocMgr.GetFactionZOC(blueUnitPositions);
            var redZOC = zocMgr.GetFactionZOC(redUnitPositions);

            var blueEntered = blueEnteredTiles != null
                ? new HashSet<Vector2I>(blueEnteredTiles)
                : new HashSet<Vector2I>();
            var redEntered = redEnteredTiles != null
                ? new HashSet<Vector2I>(redEnteredTiles)
                : new HashSet<Vector2I>();

            if (bluePathZocTiles != null)
                blueZOC.UnionWith(bluePathZocTiles);
            if (redPathZocTiles != null)
                redZOC.UnionWith(redPathZocTiles);

            foreach (var p in blueUnitPositions)
                if (map.IsInBounds(p))
                    updated[p.X, p.Y] = 1;

            foreach (var p in blueEntered)
                if (map.IsInBounds(p))
                    updated[p.X, p.Y] = 1;

            foreach (var p in redUnitPositions)
                if (map.IsInBounds(p))
                    updated[p.X, p.Y] = 2;

            foreach (var p in redEntered)
                if (map.IsInBounds(p))
                    updated[p.X, p.Y] = 2;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var p = new Vector2I(x, y);
                    bool inBlue = blueZOC.Contains(p);
                    bool inRed = redZOC.Contains(p);

                    if (inBlue && inRed)
                    {
                        updated[x, y] = 0;
                    }
                    else if (inBlue)
                    {
                        updated[x, y] = 1;
                    }
                    else if (inRed)
                    {
                        updated[x, y] = 2;
                    }
                }
            }

            _controlMap = updated;
            RebuildControlSetsFromMap(updated);
            return CloneMap(updated);
        }

        private void RebuildControlSetsFromMap(int[,] controlMap)
        {
            _blueControlled.Clear();
            _redControlled.Clear();

            int width = controlMap.GetLength(0);
            int height = controlMap.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var p = new Vector2I(x, y);
                    if (controlMap[x, y] == 1)
                        _blueControlled.Add(p);
                    else if (controlMap[x, y] == 2)
                        _redControlled.Add(p);
                }
            }
        }

        private static int[,] CloneMap(int[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            var cloned = new int[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    cloned[x, y] = source[x, y];
            }

            return cloned;
        }

        /// <summary>回合结束时根据控制区数量计分</summary>
        public void ScoreControlVP()
        {
            BlueVP += _blueControlled.Count;
            RedVP += _redControlled.Count;
        }

        // ---- 3. 胜负判定 ----

        /// <summary>
        /// 评估当前战局（PRD §2.6 战役胜负判定标准）
        /// </summary>
        public VictoryAssessment Evaluate(int turnNumber = 0)
        {
            float R;
            if (RedVP == 0 && BlueVP == 0)
                R = 1.0f;
            else if (RedVP == 0)
                R = 10.0f;
            else if (BlueVP == 0)
                R = 0.1f;
            else
                R = Math.Clamp((float)BlueVP / RedVP, 0.1f, 10.0f);

            var level = R switch
            {
                >= 4.0f => VictoryLevel.DecisiveVictory,
                >= 2.0f => VictoryLevel.MajorVictory,
                >= 1.25f => VictoryLevel.MarginalVictory,
                >= 0.8f => VictoryLevel.Stalemate,
                >= 0.5f => VictoryLevel.MarginalDefeat,
                >= 0.25f => VictoryLevel.MajorDefeat,
                _ => VictoryLevel.CrushingDefeat
            };

            return new VictoryAssessment
            {
                Ratio = R,
                BlueVP = BlueVP,
                RedVP = RedVP,
                BlueLevel = level,
                BlueControlled = _blueControlled.Count,
                RedControlled = _redControlled.Count,
                TurnNumber = turnNumber
            };
        }
    }
}
