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

        public int BlueControlledCount => _blueControlled.Count;
        public int RedControlledCount => _redControlled.Count;

        private HashSet<Vector2I> _blueControlled = new();
        private HashSet<Vector2I> _redControlled = new();

        // ---- 1. 战损VP ----

        /// <summary>
        /// 记录一次战斗的VP。
        /// 攻击方获得防御方被摧毁单位的Cost作为VP，反之亦然。
        /// </summary>
        public void RecordCombatResult(CombatResolutionResult result, int attackerFaction)
        {
            int defenderFaction = attackerFaction == 1 ? 2 : 1;

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

            var blueZOC = zocMgr.GetFactionZOC(blueUnitPositions);
            var redZOC = zocMgr.GetFactionZOC(redUnitPositions);

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var p = new Vector2I(x, y);
                    bool inBlue = blueZOC.Contains(p);
                    bool inRed = redZOC.Contains(p);

                    if (inBlue && !inRed) _blueControlled.Add(p);
                    if (inRed && !inBlue) _redControlled.Add(p);
                }
            }
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
