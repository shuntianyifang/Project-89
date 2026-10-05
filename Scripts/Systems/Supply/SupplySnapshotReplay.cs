using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Battlefield;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;
using TileData = ColdWarWargame.Models.TileData;

namespace ColdWarWargame.Systems.Supply
{
    public static class SupplySnapshotReplay
    {
        // A diagnostic snapshot reconstructs supply inputs only; it is not a game save.
        public static int Compare(JsonElement state)
        {
            int w = state.GetProperty("width").GetInt32(), h = state.GetProperty("height").GetInt32();
            var map = new GridMap(w,h); var owners = new int[w,h];
            Vector2I ReadPoint(JsonElement p) => new(p.GetProperty("x").GetInt32(),p.GetProperty("y").GetInt32());
            if (state.TryGetProperty("primary_sources",out var sources))
                foreach(var entry in sources.EnumerateArray())
                {
                    var points=new HashSet<Vector2I>();
                    foreach(var p in entry.GetProperty("points").EnumerateArray()) points.Add(ReadPoint(p));
                    map.PrimarySupplySources[entry.GetProperty("faction").GetInt32()] = points;
                }
            if (state.TryGetProperty("blocked_crossings",out var crossings))
                foreach(var edge in crossings.EnumerateArray()) map.BlockCrossing(ReadPoint(edge.GetProperty("a")),ReadPoint(edge.GetProperty("b")));
            var hubs = new HashSet<Vector2I>(); var airports = new HashSet<Vector2I>();
            foreach (var cell in state.GetProperty("cells").EnumerateArray())
            {
                var p = new Vector2I(cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32());
                map.SetTile(p, new TileData(cell.GetProperty("terrain").GetInt32(),
                    cell.GetProperty("infra").GetInt32(), cell.GetProperty("passable").GetBoolean()));
                owners[p.X,p.Y] = cell.GetProperty("owner").GetInt32();
                if (cell.GetProperty("hub").GetBoolean()) hubs.Add(p);
                if (cell.GetProperty("airport").GetBoolean()) airports.Add(p);
            }
            var units = new List<(Battalion bat, Vector2I pos)>();
            foreach (var unit in state.GetProperty("units").EnumerateArray())
                units.Add((new Battalion { InstanceId = unit.GetProperty("id").GetString(),
                    Faction = unit.GetProperty("faction").GetInt32(), CurrentAP = unit.GetProperty("ap").GetSingle() },
                    new Vector2I(unit.GetProperty("x").GetInt32(), unit.GetProperty("y").GetInt32())));
            int mismatches = 0;
            for (int faction = 1; faction <= 2; faction++)
            {
                var occupied = new HashSet<Vector2I>();
                foreach (var unit in units) if (unit.bat.Faction != faction) occupied.Add(unit.pos);
                var sp = new SupplyManager().ComputeFactionSupplySP(faction, map, units,
                    occupied, new HashSet<Vector2I>(), hubs, airports, owners);
                foreach (var cell in state.GetProperty("cells").EnumerateArray())
                {
                    int x = cell.GetProperty("x").GetInt32(), y = cell.GetProperty("y").GetInt32();
                    float exported = cell.GetProperty(faction == 1 ? "blue" : "red").GetProperty("sp").GetSingle();
                    if (Math.Abs(sp[x,y] - exported) > 0.001f)
                    {
                        mismatches++;
                        GD.PrintErr($"[SUPPLY REPLAY] faction={faction} ({x},{y}) exported={exported} recomputed={sp[x,y]}");
                    }
                }
            }
            return mismatches;
        }

        public static int Run(string path)
        {
            using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var root = document.RootElement;
            if (root.GetProperty("schema_version").GetInt32() != 1) throw new InvalidOperationException("Unsupported snapshot version");
            int differences = Compare(root.GetProperty("current"));
            foreach (var settlement in root.GetProperty("settlements").EnumerateArray())
                differences += Compare(settlement);
            GD.Print($"[SUPPLY REPLAY] SP differences={differences}; snapshot={path}");
            return differences;
        }
    }
}
