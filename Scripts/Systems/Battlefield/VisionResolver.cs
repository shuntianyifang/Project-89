using System;
using System.Collections.Generic;
using Godot;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Battlefield
{
    public static class GridHelpers
    {
        /// <summary>获取以 center 为中心，Chebyshev 距离 ≤ range 的所有网格坐标</summary>
        public static List<Vector2I> GetGridsInChebyshevDistance(Vector2I center, int range)
        {
            var result = new List<Vector2I>();
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    result.Add(new Vector2I(center.X + dx, center.Y + dy));
            return result;
        }
    }

    /// <summary>视野计算（PRD §2.7）</summary>
    public class VisionResolver
    {
        public HashSet<Vector2I> UpdateGlobalVision(
            int currentFaction,
            IEnumerable<(Battalion bat, Vector2I pos)> allBattalions)
        {
            var visibleGrids = new HashSet<Vector2I>();
            foreach (var (bat, pos) in allBattalions)
            {
                if (bat.Faction == currentFaction)
                {
                    int range = bat.CalculateVisionRange();
                    var grids = GridHelpers.GetGridsInChebyshevDistance(pos, range);
                    visibleGrids.UnionWith(grids);
                }
            }
            return visibleGrids;
        }
    }
}
