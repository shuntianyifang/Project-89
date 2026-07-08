using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Supply;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Systems.Victory;
using ColdWarWargame.Data;

namespace ColdWarWargame.Tests.Victory
{
    public static class VictoryTrackerTests
    {
        static int _fails = 0;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[VP FAIL] " + msg); }
            else { GD.Print("[VP PASS] " + msg); }
        }

        static void AssertFloat(float actual, float expected, string msg, float eps = 0.01f)
        {
            bool ok = System.Math.Abs(actual - expected) < eps;
            if (!ok) { _fails++; GD.PrintErr("[VP FAIL] " + msg + ": expected " + expected + ", got " + actual); }
            else { GD.Print("[VP PASS] " + msg + ": " + actual); }
        }

        static Battalion MakeVPBat(int faction, params string[] unitIds)
        {
            var bat = new Battalion { Name = "VPBat", Faction = faction, CurrentAP = 12f };
            var comp = new Company { Name = "C1" };
            var pl = new Platoon { PlatoonId = "P1" };
            foreach (var id in unitIds)
                pl.Units.Add(new SubUnitInstance(id));
            comp.Platoons.Add(pl);
            bat.Companies.Add(comp);
            return bat;
        }

        static void Test_InitialStalemate()
        {
            var vt = new VictoryTracker();
            var r = vt.Evaluate();
            AssertFloat(r.Ratio, 1.0f, "Initial ratio = 1.0 (stalemate)");
            Assert(r.BlueLevel == VictoryLevel.Stalemate, "Initial: Stalemate");
        }

        static void Test_CombatVP()
        {
            var vt = new VictoryTracker();

            // Create a combat result manually
            var atk = MakeVPBat(1, "us_mech_rifles");    // cost=25
            var def = MakeVPBat(2, "sov_motostrelkovy");  // cost=45

            // Simulate: attacker destroyed defender's unit
            var defUnit = def.GetAllSubUnits().First();
            defUnit.CurrentHp = 0;

            var result = new CombatResolutionResult
            {
                AttackerCasualties = new List<CasualtyRecord>(),
                DefenderCasualties = new List<CasualtyRecord>
                {
                    new CasualtyRecord { Unit = defUnit, HpLost = 9, IsDestroyed = true, RemainingHp = 0 }
                }
            };

            vt.RecordCombatResult(result, 1); // Blue attacked

            // Blue should get defender's cost = 45, Red gets 0
            Assert(vt.BlueVP == 45, "Blue gains 45 VP from destroying cost-45 unit");
            Assert(vt.RedVP == 0, "Red gains 0 VP");

            var r = vt.Evaluate();
            AssertFloat(r.Ratio, 10.0f, "Blue 45:0 -> max ratio 10.0");
            Assert(r.BlueLevel == VictoryLevel.DecisiveVictory, "Blue 45:0 -> Decisive Victory");
        }

        static void Test_GeographicalControl()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(10, 10);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            // Blue at (2,2), no red units
            var bluePos = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var redPos = new HashSet<Vector2I>();

            vt.UpdateControl(map, bluePos, redPos, zoc);
            Assert(vt.BlueControlledCount == 9, "Single blue unit: controls 9 tiles (3x3 ZOC)");
            Assert(vt.RedControlledCount == 0, "No red: controls 0 tiles");

            // After scoring
            vt.ScoreControlVP();
            Assert(vt.BlueVP == 9, "Blue scores 9 VP from control");
        }

        static void Test_ZOCBlocksControl()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(10, 10);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            // Blue at (2,2), Red at (2,4) 鈥?ZOCs overlap in between
            var bluePos = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var redPos = new HashSet<Vector2I> { new Vector2I(2, 4) };

            vt.UpdateControl(map, bluePos, redPos, zoc);

            // Check: (2,3) is in both ZOCs -> neutral, not counted for either
            Assert(vt.BlueControlledCount < 9, "ZOC overlap: Blue loses some control tiles");
            Assert(vt.RedControlledCount < 9, "ZOC overlap: Red loses some control tiles");

            // (2,2) is blue's own tile -> blue controls
            Assert(vt.BlueControlledCount > 0, "Blue controls some tiles");
            Assert(vt.RedControlledCount > 0, "Red controls some tiles");
        }

        static void Test_Occupation_EntryAndUncontestedZocOverwrite()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(8, 8);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            var initial = new int[8, 8];
            initial[2, 2] = 2;

            var bluePos = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var redPos = new HashSet<Vector2I>();

            var updated = vt.UpdateOccupationFromEntryAndZOC(map, initial, bluePos, redPos, zoc);

            Assert(updated[2, 2] == 1, "Entry tile should be overwritten by occupying faction");
            Assert(updated[1, 1] == 1, "Uncontested blue ZOC tile should become blue");
            Assert(updated[7, 7] == 0, "Far tile outside influence remains unchanged");
        }

        static void Test_Occupation_ConflictBecomesNeutral()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(10, 10);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            var initial = new int[10, 10];
            initial[4, 4] = 1;

            var bluePos = new HashSet<Vector2I> { new Vector2I(4, 4) };
            var redPos = new HashSet<Vector2I> { new Vector2I(4, 6) };

            var updated = vt.UpdateOccupationFromEntryAndZOC(map, initial, bluePos, redPos, zoc);

            Assert(updated[4, 5] == 0, "Overlapped ZOC tile should become neutral");
            Assert(updated[4, 4] == 1, "Blue occupied tile remains blue");
            Assert(updated[4, 6] == 2, "Red occupied tile remains red");
        }

        static void Test_Occupation_PreservePreviousOutsideInfluence()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(8, 8);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            var initial = new int[8, 8];
            initial[0, 0] = 2;
            initial[7, 7] = 1;

            var bluePos = new HashSet<Vector2I> { new Vector2I(3, 3) };
            var redPos = new HashSet<Vector2I>();

            var updated = vt.UpdateOccupationFromEntryAndZOC(map, initial, bluePos, redPos, zoc);

            Assert(updated[0, 0] == 2, "Previous control should persist outside any influence");
            Assert(updated[7, 7] == 1, "Previous friendly control should persist outside any influence");
        }

        static void Test_Occupation_PathTilesAreCaptured()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(12, 12);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            var initial = new int[12, 12];
            var bluePos = new HashSet<Vector2I> { new Vector2I(6, 6) };
            var redPos = new HashSet<Vector2I>();
            var blueEntered = new HashSet<Vector2I>
            {
                new Vector2I(2, 2),
                new Vector2I(3, 2),
                new Vector2I(4, 2)
            };

            var updated = vt.UpdateOccupationFromEntryAndZOC(map, initial, bluePos, redPos, zoc, blueEntered);

            Assert(updated[2, 2] == 1, "Path tile should be captured even outside final ZOC");
            Assert(updated[3, 2] == 1, "Intermediate path tile should be captured");
            Assert(updated[4, 2] == 1, "Last traversed path tile should be captured");
        }

        static void Test_Occupation_PathZocTilesAreCaptured()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(12, 12);
            var zoc = new ColdWarWargame.Systems.Battlefield.ZOCManager(map);
            var vt = new VictoryTracker();

            var initial = new int[12, 12];
            var bluePos = new HashSet<Vector2I> { new Vector2I(10, 10) };
            var redPos = new HashSet<Vector2I>();
            var blueEntered = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var bluePathZoc = zoc.GetFactionZOC(blueEntered);

            var updated = vt.UpdateOccupationFromEntryAndZOC(
                map,
                initial,
                bluePos,
                redPos,
                zoc,
                blueEntered,
                null,
                bluePathZoc);

            Assert(updated[1, 2] == 1, "Path ZOC tile should be captured");
            Assert(updated[2, 1] == 1, "Another path ZOC tile should be captured");
            Assert(updated[2, 2] == 1, "Path-entered tile remains captured");
        }

        static void Test_VictoryLevels()
        {
            var vt = new VictoryTracker();

            // Test each threshold
            void AssertLevel(int b, int r, VictoryLevel expected)
            {
                vt.BlueVP = b;
                vt.RedVP = r;
                var assessment = vt.Evaluate();
                Assert(assessment.BlueLevel == expected,
                    "Blue " + b + ":" + r + " -> " + expected.DisplayName() + " (got " + assessment.BlueLevel.DisplayName() + ")");
            }

            AssertLevel(0, 1, VictoryLevel.CrushingDefeat);    // R=0.1
            AssertLevel(1, 5, VictoryLevel.CrushingDefeat);    // R=0.2
            AssertLevel(1, 3, VictoryLevel.MajorDefeat);       // R=0.33

            // R = 1/2 = 0.5: 0.25 <= 0.5 < 0.5 鈫?this is exactly at the boundary
            // Let me check: 0.5 is >= 0.5, so it falls into MarginalDefeat
            // No wait: R >= 0.5 鈫?first matching case
            // >= 4.0: no
            // >= 2.0: no
            // >= 1.25: no
            // >= 0.8: no
            // >= 0.5: yes 鈫?MarginalDefeat
            // So 1:2 gives R=0.5 鈫?MarginalDefeat 鉁?
            AssertLevel(1, 2, VictoryLevel.MarginalDefeat);    // R=0.5
            AssertLevel(1, 1, VictoryLevel.Stalemate);         // R=1.0
            AssertLevel(3, 2, VictoryLevel.MarginalVictory);   // R=1.5
            AssertLevel(5, 2, VictoryLevel.MajorVictory);      // R=2.5
            AssertLevel(10, 2, VictoryLevel.DecisiveVictory);  // R=5.0
        }

        public static void RunAll()
        {
            _fails = 0;
            GD.Print("--- Victory 绯荤粺娴嬭瘯 ---");

            Test_InitialStalemate();
            Test_CombatVP();
            Test_GeographicalControl();
            Test_ZOCBlocksControl();
            Test_Occupation_EntryAndUncontestedZocOverwrite();
            Test_Occupation_ConflictBecomesNeutral();
            Test_Occupation_PreservePreviousOutsideInfluence();
            Test_Occupation_PathTilesAreCaptured();
            Test_Occupation_PathZocTilesAreCaptured();
            Test_VictoryLevels();

            if (_fails == 0)
                GD.Print("All VictoryTrackerTests passed");
            else
                GD.PrintErr(_fails + " VictoryTrackerTests FAILED");
        }
    }
}
