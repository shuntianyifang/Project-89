using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace ColdWarWargame.Scenarios
{
    public static class OccupationStateCodec
    {
        private sealed class OccupationStateFile
        {
            public int width { get; set; }
            public int height { get; set; }
            public List<string> rows { get; set; } = new();
        }

        public static int[,] CreateDefaultHalfHalf(int width, int height)
        {
            var map = new int[width, height];
            int split = height / 2;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    map[x, y] = y < split ? 2 : 1;
            }

            return map;
        }

        public static bool TryLoad(string path, int expectedWidth, int expectedHeight, out int[,] controlMap)
        {
            controlMap = new int[0, 0];

            if (!Godot.FileAccess.FileExists(path))
                return false;

            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
                return false;

            string json = file.GetAsText();
            var state = JsonSerializer.Deserialize<OccupationStateFile>(json);
            if (state == null || state.rows == null || state.rows.Count != expectedHeight)
                return false;
            if (state.width != expectedWidth || state.height != expectedHeight)
                return false;

            var parsed = new int[expectedWidth, expectedHeight];
            for (int y = 0; y < expectedHeight; y++)
            {
                string row = state.rows[y] ?? string.Empty;
                if (row.Length < expectedWidth)
                    row = row.PadRight(expectedWidth, '0');

                for (int x = 0; x < expectedWidth; x++)
                {
                    char ch = row[x];
                    parsed[x, y] = ch switch
                    {
                        '1' => 1,
                        '2' => 2,
                        _ => 0
                    };
                }
            }

            controlMap = parsed;
            return true;
        }

        public static void Save(string path, int[,] controlMap)
        {
            int width = controlMap.GetLength(0);
            int height = controlMap.GetLength(1);
            var state = new OccupationStateFile
            {
                width = width,
                height = height,
                rows = new List<string>(height)
            };

            for (int y = 0; y < height; y++)
            {
                var row = new StringBuilder(width);
                for (int x = 0; x < width; x++)
                {
                    row.Append(controlMap[x, y] switch
                    {
                        1 => '1',
                        2 => '2',
                        _ => '0'
                    });
                }
                state.rows.Add(row.ToString());
            }

            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
            if (file == null)
                throw new IOException("Unable to open occupation state file for write: " + path);

            file.StoreString(json);
        }

        public static int[,] CloneMap(int[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            var clone = new int[width, height];

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    clone[x, y] = source[x, y];

            return clone;
        }
    }
}