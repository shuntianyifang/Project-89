using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TileData = ColdWarWargame.Models.TileData;

namespace ColdWarWargame.Systems.Battlefield
{
    /// <summary>
    /// 移动判定系统：AP 消耗计算、Corner Clipping、可达范围（Dijkstra）
    /// PRD §2.1: 正交移动基础消耗1，斜向1.4，EPSILON容差
    /// </summary>
    public class MovementResolver
    {
        const float EPSILON = 0.0025f;
        const float ORTH_COST = 1.0f;
        const float DIAG_COST = 1.4f;

        private GridMap _map;

        public MovementResolver(GridMap map)
        {
            _map = map;
        }

        /// <summary>AP 容差判定：剩余 AP + EPSILON >= 消耗</summary>
        public static bool CanAfford(float currentAP, float cost) =>
            currentAP + EPSILON >= cost;

        /// <summary>
        /// Corner Clipping 检测（PRD §2.1）：
        /// 斜向移动时，若两侧翻越网格均不可通行或被敌军占据，则阻断
        /// </summary>
        public bool CanMoveDiagonal(Vector2I from, Vector2I to, Func<Vector2I, bool> isBlocked)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (Mathf.Abs(dx) != 1 || Mathf.Abs(dy) != 1)
                return true; // 非斜向，无需检测

            Vector2I n1 = new Vector2I(from.X + dx, from.Y);
            Vector2I n2 = new Vector2I(from.X, from.Y + dy);

            // 两侧翻越网格均被阻挡 → 斜向不可通行
            if (isBlocked(n1) && isBlocked(n2))
                return false;

            return true;
        }

        /// <summary>
        /// 计算从 from 到 to（相邻格子）的完整移动 AP 消耗
        /// 含方向基础消耗 × 目的地地形成本
        /// </summary>
        public float GetMoveCost(Vector2I from, Vector2I to, Func<Vector2I, bool> isBlocked)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            float baseCost;
        

            if (Mathf.Abs(dx) == 1 && dy == 0)
                baseCost = ORTH_COST;
            else if (dx == 0 && Mathf.Abs(dy) == 1)
                baseCost = ORTH_COST;
            else if (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 1)
            {
                if (!CanMoveDiagonal(from, to, isBlocked))
                    return float.PositiveInfinity;
                baseCost = DIAG_COST;
        
            }
            else
                return float.PositiveInfinity; // 不相邻

            float tileCost = _map.GetTile(to).GetMovementCost();
            if (float.IsPositiveInfinity(tileCost))
                return float.PositiveInfinity;

            return baseCost * tileCost;
        }

        /// <summary>
        /// Dijkstra 搜索可达网格。
        /// 返回 dict: grid -> 到达该格所需累计AP
        /// </summary>
        public Dictionary<Vector2I, float> GetReachableTiles(
            Vector2I start,
            float maxAP,
            Func<Vector2I, bool> isEnemyZOC,
            Func<Vector2I, bool> isOccupied)
        {
            var costSoFar = new Dictionary<Vector2I, float>();
            costSoFar[start] = 0f;

            // 简单的贪心队列（可按性能需求换为优先队列）
            var frontier = new List<Vector2I> { start };

            while (frontier.Count > 0)
            {
                var current = frontier[0];
                frontier.RemoveAt(0);
                float currentCost = costSoFar[current];

                foreach (var neighbor in _map.GetAllNeighbors(current))
                {
                    // 不能进入敌方 ZOC
                    if (isEnemyZOC(neighbor)) continue;

                    // 不能进入已被占据的格子
                    if (isOccupied(neighbor)) continue;

                    // 不能进入不可通行格子
                    if (!_map.IsPassable(neighbor)) continue;

                    float moveCost = GetMoveCost(current, neighbor,
                        p => isEnemyZOC(p) || isOccupied(p) || !_map.IsPassable(p));
                    if (float.IsPositiveInfinity(moveCost)) continue;

                    float newCost = currentCost + moveCost;

                    // 超出 AP 预算？
                    if (!CanAfford(maxAP, newCost)) continue;

                    // 已经有更优路径？
                    if (costSoFar.TryGetValue(neighbor, out var existingCost) && existingCost <= newCost + EPSILON)
                        continue;

                    costSoFar[neighbor] = newCost;
                    frontier.Add(neighbor);
                }
            }

            // 去掉起点本身（起点不算"可达格子"）
            costSoFar.Remove(start);
            return costSoFar;
        }

        /// <summary>计算从 start 到 target 沿已知路径的精确 AP 消耗</summary>
        public float CalculatePathCost(Vector2I start, Vector2I target,
            Func<Vector2I, bool> isBlocked)
        {
            var costSoFar = new Dictionary<Vector2I, float>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            costSoFar[start] = 0f;

            var frontier = new List<Vector2I> { start };

            while (frontier.Count > 0)
            {
                var current = frontier[0];
                frontier.RemoveAt(0);
                float currentCost = costSoFar[current];

                if (current == target)
                    return currentCost;

                foreach (var neighbor in _map.GetAllNeighbors(current))
                {
                    if (!_map.IsPassable(neighbor)) continue;

                    float moveCost = GetMoveCost(current, neighbor, isBlocked);
                    if (float.IsPositiveInfinity(moveCost)) continue;

                    float newCost = currentCost + moveCost;

                    if (costSoFar.TryGetValue(neighbor, out var existingCost) && existingCost <= newCost + EPSILON)
                        continue;

                    costSoFar[neighbor] = newCost;
                    cameFrom[neighbor] = current;
                    frontier.Add(neighbor);
                }
            }

            return float.PositiveInfinity; // 不可达
        }
    }
}
