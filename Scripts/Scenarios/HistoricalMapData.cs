using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ColdWarWargame.Systems.Battlefield;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;

namespace ColdWarWargame.Scenarios
{
    public sealed class HistoricalMapData
    {
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("terrain_rows")] public string[] Terrain { get; set; }
        [JsonPropertyName("infra_rows")] public string[] Infra { get; set; }
        [JsonPropertyName("blocked_river_edges")] public int[][] RiverEdges { get; set; }
        [JsonPropertyName("blue_supply_sources")] public int[][] BlueSources { get; set; }
        [JsonPropertyName("red_supply_sources")] public int[][] RedSources { get; set; }
        [JsonPropertyName("hubs")] public int[][] Hubs { get; set; }
        [JsonPropertyName("airports")] public int[][] Airports { get; set; }
        public static HistoricalMapData Load(string path = "res://Scripts/Data/Scenarios/Fulda_Gap/historical_map.json")
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var data = file == null ? null : JsonSerializer.Deserialize<HistoricalMapData>(file.GetAsText());
            if (data == null || data.Width != 50 || data.Height != 30 ||
                data.Terrain?.Length != 30 || data.Infra?.Length != 30 ||
                data.Terrain.Any(r => r == null || r.Length != 50 || r.Any(c => c < '0' || c > '3')) ||
                data.Infra.Any(r => r == null || r.Length != 50 || r.Any(c => c < '0' || c > '2')))
                throw new InvalidOperationException("历史地图尺寸或图层无效");
            foreach (var points in new[] { data.BlueSources, data.RedSources, data.Hubs, data.Airports })
                if (points == null || points.Any(p => p.Length != 2 || !InBounds(p[0], p[1])))
                    throw new InvalidOperationException("历史地图补给节点无效");
            if (data.RiverEdges == null || data.RiverEdges.Any(p => p.Length != 4 ||
                !InBounds(p[0], p[1]) || !InBounds(p[2], p[3]) ||
                Math.Abs(p[0]-p[2]) + Math.Abs(p[1]-p[3]) != 1))
                throw new InvalidOperationException("历史地图河流边无效");
            return data;
        }
        private static bool InBounds(int x, int y) => x >= 0 && x < 50 && y >= 0 && y < 30;
        public static HashSet<Vector2I> Points(int[][] values) => values.Select(p => new Vector2I(p[0], p[1])).ToHashSet();
        public GridMap Build()
        {
            var terrain = new int[Height,Width]; var infra = new int[Height,Width];
            for (int y=0; y<Height; y++) for (int x=0; x<Width; x++)
            { terrain[y,x] = Terrain[y][x]-'0'; infra[y,x] = Infra[y][x]-'0'; }
            var map = GridMap.FromLayers(terrain,infra);
            map.PrimarySupplySources[1] = Points(BlueSources);
            map.PrimarySupplySources[2] = Points(RedSources);
            foreach (var e in RiverEdges) map.BlockCrossing(new(e[0],e[1]),new(e[2],e[3]));
            return map;
        }
    }
}
