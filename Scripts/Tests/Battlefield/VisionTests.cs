using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Models;

namespace ColdWarWargame.Tests.Battlefield
{
    public static class VisionTests
    {
        static int _fails = 0;
        static void Assert(bool c, string m) { if (!c) { _fails++; GD.PrintErr("[VISION FAIL] " + m); } else GD.Print("[VISION PASS] " + m); }
        public static void RunAll()
        {
            _fails = 0; GD.Print("--- Vision 测试 ---");
            var pos = new Vector2I(5, 5);
            var grids = GridHelpers.GetGridsInChebyshevDistance(pos, 2);
            Assert(grids.Count == 25, "Range 2 from (5,5) -> 25 tiles (5x5)");
            var pos2 = new Vector2I(0, 0);
            var grids2 = GridHelpers.GetGridsInChebyshevDistance(pos2, 1);
            Assert(grids2.Count == 9, "Range 1 from (0,0) -> 9 tiles (3x3)");
            if (_fails == 0) GD.Print("All VisionTests passed");
            else GD.PrintErr(_fails + " VisionTests FAILED");
        }
    }
}
