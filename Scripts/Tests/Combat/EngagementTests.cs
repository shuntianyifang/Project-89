using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Models;

namespace ColdWarWargame.Tests.Combat
{
    public static class EngagementTests
    {
        static int _fails = 0;
        static void Assert(bool c, string m) { if (!c) { _fails++; GD.PrintErr("[ENGAGE FAIL] " + m); } else GD.Print("[ENGAGE PASS] " + m); }
        public static void RunAll()
        {
            _fails = 0; GD.Print("--- Engagement 测试 ---");
            var defPos = new Vector2I(10, 10);
            var units = new List<(Battalion, Vector2I)>();
            for (int i = 0; i < 5; i++) units.Add((new Battalion { Name = "U" + i, Faction = 1 }, new Vector2I(10 + i, 10)));
            var eligible = EngagementResolver.GetEligibleUnits(defPos, units, 2);
            Assert(eligible.Count == 2, "5 units in line, maxDist=2 from index 0 -> 2 eligible");
            var farUnits = new List<(Battalion, Vector2I)> { (new Battalion(), new Vector2I(0, 0)) };
            var far = EngagementResolver.GetEligibleUnits(defPos, farUnits, 2);
            Assert(far.Count == 0, "Unit at (0,0) too far from (10,10) with maxDist=2");
            if (_fails == 0) GD.Print("All EngagementTests passed");
            else GD.PrintErr(_fails + " EngagementTests FAILED");
        }
    }
}
