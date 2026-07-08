using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using TileData = ColdWarWargame.Models.TileData;

namespace ColdWarWargame.Systems.Battlefield
{
    public class GridMap
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        private TileData[,] _tiles;

        /// <summary>
        /// 构造空网格（默认填充平原/无基础设施）
        /// 注意：C# 2D 数组索引为 [row, col]，但本系统使用 (x, y) 其中 x=列, y=行
        /// </summary>
        public GridMap(int width, int height)
        {
            Width = width;
            Height = height;
            _tiles = new TileData[width, height];

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _tiles[x, y] = new TileData(0, 0, true);
        }

        /// <summary>从地形矩阵创建（纯文本 -> 地形层）</summary>
        public static GridMap FromTerrainArray(int[,] terrainGrid)
        {
            int rows = terrainGrid.GetLength(0); // 行数 = Height
            int cols = terrainGrid.GetLength(1); // 列数 = Width
            var map = new GridMap(cols, rows);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    map._tiles[x, y] = new TileData(terrainGrid[y, x], 0, true);
            return map;
        }

        /// <summary>从地形+基础设施双层矩阵创建</summary>
        public static GridMap FromLayers(int[,] terrainGrid, int[,] infraGrid = null)
        {
            int rows = terrainGrid.GetLength(0);
            int cols = terrainGrid.GetLength(1);
            var map = new GridMap(cols, rows);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int t = terrainGrid[y, x];
                    int inf = infraGrid?[y, x] ?? 0;
                    bool passable = t >= 0;
                    map._tiles[x, y] = new TileData(Math.Max(0, t), inf, passable);
                }
            }
            return map;
        }

        public TileData GetTile(Vector2I pos)
        {
            if (!IsInBounds(pos))
                return new TileData(0, 0, false);
            return _tiles[pos.X, pos.Y];
        }

        public void SetTile(Vector2I pos, TileData data)
        {
            if (!IsInBounds(pos)) return;
            _tiles[pos.X, pos.Y] = data;
        }

        public bool IsInBounds(Vector2I pos) =>
            pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        public bool IsPassable(Vector2I pos)
        {
            if (!IsInBounds(pos)) return false;
            return _tiles[pos.X, pos.Y].IsPassable;
        }

        public List<Vector2I> GetOrthogonalNeighbors(Vector2I pos)
        {
            var result = new List<Vector2I>();
            foreach (var dir in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up })
            {
                var n = pos + dir;
                if (IsInBounds(n)) result.Add(n);
            }
            return result;
        }

        public List<Vector2I> GetAllNeighbors(Vector2I pos)
        {
            var result = new List<Vector2I>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = new Vector2I(pos.X + dx, pos.Y + dy);
                    if (IsInBounds(n)) result.Add(n);
                }
            }
            return result;
        }

        public void PrintTerrainGrid()
        {
            GD.Print("=== Terrain Grid ===");
            for (int y = 0; y < Height; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < Width; x++)
                {
                    var t = _tiles[x, y];
                    char ch = t.IsPassable ? t.TerrainType.ToString()[0] : '#';
                    row.Append(ch);
                    row.Append(' ');
                }
                GD.Print(row.ToString());
            }
        }

        public void PrintCostGrid()
        {
            GD.Print("=== Movement Cost Grid ===");
            for (int y = 0; y < Height; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < Width; x++)
                {
                    float cost = _tiles[x, y].GetMovementCost();
                    if (float.IsPositiveInfinity(cost))
                        row.Append(" ## ");
                    else
                        row.Append(cost.ToString("0.0").PadLeft(4));
                }
                GD.Print(row.ToString());
            }
        }
    }
}
