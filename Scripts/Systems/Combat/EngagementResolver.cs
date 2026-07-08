using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    /// <summary>
    /// 交战区域检测（PRD §2.3）：以防守方为中心，切比雪夫距离 ≤ 2（5×5）
    /// </summary>
    public static class EngagementResolver
    {
        public static List<(Battalion bat, Vector2I pos)> GetEligibleUnits(
            Vector2I defenderPos,
            IEnumerable<(Battalion bat, Vector2I pos)> allUnits,
            int maxDistance = 2)
        {
            return allUnits
                .Where(u => u.pos != defenderPos)
                .Where(u => Math.Max(Math.Abs(u.pos.X - defenderPos.X), Math.Abs(u.pos.Y - defenderPos.Y)) <= maxDistance)
                .ToList();
        }
    }
}
