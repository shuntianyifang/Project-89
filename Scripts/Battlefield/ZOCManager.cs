using System.Collections.Generic;
using Godot;

namespace ColdWarWargame.Battlefield
{
    /// <summary>
    /// ZOC（Zone of Control）控制区管理器
    /// PRD §2.1: 每个存活单位对其所在的网格及周围8格（3x3区域）投射ZOC
    /// </summary>
    public class ZOCManager
    {
        private GridMap _map;

        public ZOCManager(GridMap map)
        {
            _map = map;
        }

        /// <summary>
        /// 给定一方的所有单位位置，计算该方的 ZOC 覆盖网格集合。
        /// 每个单位向其所在网格 + 周围 8 格（Chebyshev 距离 ≤ 1）投射 ZOC。
        /// </summary>
        public HashSet<Vector2I> GetFactionZOC(IEnumerable<Vector2I> unitPositions)
        {
            var zocTiles = new HashSet<Vector2I>();
            foreach (var pos in unitPositions)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var tile = new Vector2I(pos.X + dx, pos.Y + dy);
                        if (_map.IsInBounds(tile))
                            zocTiles.Add(tile);
                    }
                }
            }
            return zocTiles;
        }

        /// <summary>检查某个网格是否在任一敌方单位的 ZOC 中</summary>
        public bool IsInEnemyZOC(Vector2I tile, IEnumerable<Vector2I> enemyPositions)
        {
            if (!_map.IsInBounds(tile)) return true;

            foreach (var ep in enemyPositions)
            {
                int dx = System.Math.Abs(tile.X - ep.X);
                int dy = System.Math.Abs(tile.Y - ep.Y);
                if (dx <= 1 && dy <= 1)
                    return true;
            }
            return false;
        }

        /// <summary>调试：打印 ZOC 覆盖图</summary>
        public void PrintZOCGrid(HashSet<Vector2I> zocTiles, IEnumerable<Vector2I> unitPositions)
        {
            var unitSet = new HashSet<Vector2I>(unitPositions);
            GD.Print("=== ZOC Grid ===");
            for (int y = 0; y < _map.Height; y++)
            {
                var row = new System.Text.StringBuilder();
                for (int x = 0; x < _map.Width; x++)
                {
                    var pos = new Vector2I(x, y);
                    if (unitSet.Contains(pos))
                        row.Append("U ");
                    else if (zocTiles.Contains(pos))
                        row.Append("Z ");
                    else
                        row.Append(". ");
                }
                GD.Print(row.ToString());
            }
        }
    }
}
