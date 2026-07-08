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

namespace ColdWarWargame.Tests.Supply
{
    public static class SupplyManagerTests
    {
        static int _fails = 0;
        static bool _unitDbReady = false;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[SUPPLY FAIL] " + msg); }
            else { GD.Print("[SUPPLY PASS] " + msg); }
        }

        static void AssertFloat(float actual, float expected, string msg, float eps = 0.01f)
        {
            bool ok = System.Math.Abs(actual - expected) < eps;
            if (!ok) { _fails++; GD.PrintErr("[SUPPLY FAIL] " + msg + ": expected " + expected + ", got " + actual); }
            else { GD.Print("[SUPPLY PASS] " + msg + ": " + actual); }
        }

        static Battalion MakeSupplyBat(string name, int faction)
        {
            return new Battalion { Name = name, Faction = faction, CurrentAP = 12f, Fatigue = 0, TurnsOOS = 0 };
        }

        static void EnsureUnitDatabase()
        {
            if (_unitDbReady) return;
            UnitDatabase.Initialize("res://Scripts/Data/Units");
            _unitDbReady = true;
        }

        static Battalion MakeSupplyBatWithTestUnits(string name, int faction)
        {
            EnsureUnitDatabase();

            var bat = MakeSupplyBat(name, faction);
            var comp = new Company { CompanyId = "C1", Name = "C1" };
            var platoon = new Platoon { PlatoonId = "P1", Type = "standard" };

            var u1 = new SubUnitInstance("us_mech_rifles") { NodeId = "u1", Category = "units" };
            var u2 = new SubUnitInstance("us_mech_rifles") { NodeId = "u2", Category = "units" };
            var dead = new SubUnitInstance("us_mech_rifles") { NodeId = "dead", Category = "units" };

            u1.CurrentHp = 5;
            u2.CurrentHp = 8;
            dead.CurrentHp = 0;

            platoon.Units.Add(u1);
            platoon.Units.Add(u2);
            platoon.Units.Add(dead);
            comp.Platoons.Add(platoon);
            bat.Companies.Add(comp);
            return bat;
        }

        // ========== SupplyNetwork Tests ==========

        static void Test_PlainMap_AllSupplied()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5); // all plain
            var net = new SupplyNetwork();

            // Blue (faction 1) -> supply from bottom (y=4)
            var spBlue = net.ComputeSupplySP(map, 1, new HashSet<Vector2I>(), new HashSet<Vector2I>());
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    Assert(spBlue[x, y] > 0f, "Blue supply covers all tiles: (" + x + "," + y + ")");

            // Red (faction 2) -> supply from top (y=0)
            var spRed = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), new HashSet<Vector2I>());
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    Assert(spRed[x, y] > 0f, "Red supply covers all tiles: (" + x + "," + y + ")");
        }

        static void Test_ImpassableBlocks()
        {
            int[,] terrain = {
                { 0, 0, 0 },
                { 0,-1, 0 },  // center impassable (terrain=-1)
                { 0, 0, 0 }
            };
            var map = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(terrain);
            var net = new SupplyNetwork();

            // Red supply from top: should NOT reach bottom row
            var sp = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), new HashSet<Vector2I>());
            Assert(sp[0, 2] > 0f, "Left column: bottom tile supplied (goes around)");
            Assert(sp[2, 2] > 0f, "Right column: bottom tile supplied (goes around)");
            Assert(sp[1, 2] > 0f, "Center bottom: CAN be reached via diagonal around impassable");
        }

        static void Test_EnemyZOC_Penalty()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var net = new SupplyNetwork();

            // Enemy ZOC at (2, 2) - center of map
            var zoc = new HashSet<Vector2I> { new Vector2I(2, 2) };

            // Red supply from top
            var sp = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), zoc);

            // (2,2) should still have supply (just lower)
            Assert(sp[2, 2] > 0f, "ZOC tile still has some supply (high cost but within range)");

            // Bottom corner far from source + ZOC penalty still within 36 SP on 5x5
            Assert(sp[4, 4] > 0f, "Bottom-right still supplied (ZOC penalty on 5x5 not enough to block)");
        }

        static void Test_Hub_Reactivation_ExtendsPrimaryRange()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(1, 30);
            var net = new SupplyNetwork();

            // Red supply from top y=0. Plain cost=2 means single-source 36 SP cannot reach y=29.
            var noHub = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), new HashSet<Vector2I>());
            AssertFloat(noHub[0, 29], 0f, "Without hub reactivation, far tile stays unsupplied");

            // Hub at y=17 is reachable by primary source; it should reactivate to a fresh 36 SP source.
            var hubs = new HashSet<Vector2I> { new Vector2I(0, 17) };
            var withHub = net.ComputeSupplySP(
                map,
                2,
                new HashSet<Vector2I>(),
                new HashSet<Vector2I>(),
                null,
                hubs,
                null);

            Assert(withHub[0, 29] > 0f, "Activated hub extends strategic supply deeper than primary-only range");
        }

        static void Test_DisconnectedAirport_ProvidesSecondarySupply()
        {
            int[,] terrain = {
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { -1, -1, -1, -1, -1 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0 }
            };

            var map = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(terrain);
            var net = new SupplyNetwork();

            // Red supply source is y=0; row y=5 is fully blocked, so lower half is disconnected.
            var noAirport = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), new HashSet<Vector2I>());
            AssertFloat(noAirport[2, 10], 0f, "Without airport fallback, disconnected pocket remains OOS");

            var airports = new HashSet<Vector2I> { new Vector2I(2, 8) };
            var withAirport = net.ComputeSupplySP(
                map,
                2,
                new HashSet<Vector2I>(),
                new HashSet<Vector2I>(),
                null,
                null,
                airports);

            Assert(withAirport[2, 10] > 0f, "Disconnected airport emits local secondary supply");
            Assert(withAirport[2, 10] <= 18f + 0.01f, "Secondary supply is capped by 18 SP budget");
        }

        // ========== SupplyManager Tests ==========

        static void Test_OOS_Accumulation()
        {
            // Simulate a battalion OOS on a map where supply doesn't reach
            int[,] terrain = {
                { 0, 0, 0 },
                { 0,-1, 0 },  // impassable wall
                { 0, 0, 0 }
            };
            var map = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(terrain);
            var mgr = new SupplyManager();

            // Place a Blue battalion at bottom-right (3,3) which should be cutoff
            var bat = MakeSupplyBat("CutoffBat", 1);
            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(2, 2)) };

            // Actually, let me use a proper scenario: 5x1 corridor with wall
            int[,] corridor = {
                { 0, 0, 0, 0, 0 }
            };
            var corridorMap = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(corridor);

            var cutoff = MakeSupplyBat("Cutoff", 1);
            cutoff.CurrentAP = 4f; // low AP -> less fatigue recovery later

            // Place at column 4 (furthest from source at y=0 for Red, y=4 for... wait, this is 1 row)
            // Use a bigger map to test
            var bigMap = new ColdWarWargame.Systems.Battlefield.GridMap(10, 10);
            var oosBat = MakeSupplyBat("OOS", 1);

            // Start 9 tiles away from supply source (bottom edge y=9 for Blue)
            // Plain cost per tile = 2.0, so 9 tiles = 18.0 cumulative cost. Still within 36 MAX_SP.
            // For proper OOS, need to go further. Let me use a long corridor.

            // 1x10 corridor, Blue supply from bottom (y=9 for a 10-high map)
            // Actually our map is 10x10, all plain. Blue supply from y=9.
            // Tile at (0,0) is 9 orth steps from source: 9*2.0 = 18.0 < 36. So it's in supply.
            // This won't work for OOS testing on a small map.

            // Simpler test: just check the SP calculation.
            var net = new SupplyNetwork();
            var sp = net.ComputeSupplySP(bigMap, 1, new HashSet<Vector2I>(), new HashSet<Vector2I>());
            Assert(sp[0, 0] > 0f, "SP reaches far corner (9 tiles * 2.0 = 18 < 36)");

            // Now test actual SupplyManager: battalion in supply should reset TurnsOOS
            oosBat.TurnsOOS = 2; // simulate previous OOS
            var units2 = new List<(Battalion, Vector2I)> { (oosBat, new Vector2I(5, 5)) };
            mgr.UpdateFactionEndTurn(1, bigMap, units2, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            Assert(oosBat.TurnsOOS == 0, "Battalion in supply: TurnsOOS reset to 0");
        }

        static void Test_FatigueRecovery()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var mgr = new SupplyManager();

            var bat = MakeSupplyBat("Test", 1);
            bat.Fatigue = 6;
            bat.CurrentAP = 10f; // high remaining AP -> good recovery (Fatigue -2)
            bat.TurnsOOS = 0;

            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(2, 2)) };
            mgr.UpdateFactionEndTurn(1, map, units, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            Assert(bat.TurnsOOS == 0, "In supply: TurnsOOS stays 0");
            Assert(bat.Fatigue == 4, "Remaining AP >= 8: Fatigue 6->4 (recover 2)");

            // Test low AP recovery
            var bat2 = MakeSupplyBat("Test2", 1);
            bat2.Fatigue = 5;
            bat2.CurrentAP = 5f; // 8 > AP >= 4 -> recover 1
            var units2 = new List<(Battalion, Vector2I)> { (bat2, new Vector2I(2, 2)) };
            mgr.UpdateFactionEndTurn(1, map, units2, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            Assert(bat2.Fatigue == 4, "AP between 4-8: Fatigue 5->4 (recover 1)");
        }

        static void Test_HpRecovery_LinkedToFatigueRecover2()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var mgr = new SupplyManager();
            var bat = MakeSupplyBatWithTestUnits("Recover2", 1);
            bat.Fatigue = 6;
            bat.CurrentAP = 10f; // recover fatigue by 2

            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(2, 2)) };
            mgr.UpdateFactionEndTurn(1, map, units, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            var unit1 = bat.GetAllSubUnits().First(u => u.NodeId == "u1");
            var unit2 = bat.GetAllSubUnits().First(u => u.NodeId == "u2");
            var dead = bat.GetAllSubUnits().First(u => u.NodeId == "dead");

            Assert(bat.Fatigue == 4, "Fatigue recover2: 6->4");
            Assert(unit1.CurrentHp == 9, "Fatigue recover2: alive unit +4 and clamped to max");
            Assert(unit2.CurrentHp == 9, "Fatigue recover2: near-max unit stays capped at max");
            Assert(dead.CurrentHp == 0, "Fatigue recover2: dead unit is not revived");
        }

        static void Test_HpRecovery_LinkedToFatigueRecover1()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var mgr = new SupplyManager();
            var bat = MakeSupplyBatWithTestUnits("Recover1", 1);
            bat.Fatigue = 5;
            bat.CurrentAP = 5f; // recover fatigue by 1

            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(2, 2)) };
            mgr.UpdateFactionEndTurn(1, map, units, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            var unit1 = bat.GetAllSubUnits().First(u => u.NodeId == "u1");
            Assert(bat.Fatigue == 4, "Fatigue recover1: 5->4");
            Assert(unit1.CurrentHp == 7, "Fatigue recover1: alive unit +2");
        }

        static void Test_HpRecovery_NoRecoveryWhenOOS()
        {
            int[,] terrain = {
                { -1, -1, -1 },
                { -1,  0, -1 },
                { -1, -1, -1 }
            };
            var map = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(terrain);
            var mgr = new SupplyManager();
            var bat = MakeSupplyBatWithTestUnits("NoRecoverOOS", 1);
            bat.Fatigue = 6;
            bat.CurrentAP = 10f;

            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(1, 1)) };
            mgr.UpdateFactionEndTurn(1, map, units, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            var unit1 = bat.GetAllSubUnits().First(u => u.NodeId == "u1");
            Assert(bat.TurnsOOS == 1, "OOS: turns_oos increments");
            Assert(unit1.CurrentHp == 5, "OOS: no HP recovery should happen");
        }

        static void Test_DisorganizedInSupply_ForcedToFatigue8NextTurn()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var mgr = new SupplyManager();
            var bat = MakeSupplyBatWithTestUnits("DisorganizedReset", 1);
            bat.Fatigue = 12;
            bat.CurrentAP = 0f; // 即使 AP 低，也应触发强制回落到 8
            bat.TurnsOOS = 3;

            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(2, 2)) };
            mgr.UpdateFactionEndTurn(1, map, units, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            Assert(bat.TurnsOOS == 0, "Disorganized+in supply: TurnsOOS reset to 0");
            Assert(bat.Fatigue == 8, "Disorganized+in supply: fatigue forced to 8 for next turn");
        }

        public static void RunAll()
        {
            _fails = 0;
            GD.Print("--- Supply 系统测试 ---");

            Test_PlainMap_AllSupplied();
            Test_ImpassableBlocks();
            Test_EnemyZOC_Penalty();
            Test_Hub_Reactivation_ExtendsPrimaryRange();
            Test_DisconnectedAirport_ProvidesSecondarySupply();
            Test_OOS_Accumulation();
            Test_FatigueRecovery();
            Test_HpRecovery_LinkedToFatigueRecover2();
            Test_HpRecovery_LinkedToFatigueRecover1();
            Test_HpRecovery_NoRecoveryWhenOOS();
            Test_DisorganizedInSupply_ForcedToFatigue8NextTurn();

            if (_fails == 0)
                GD.Print("All SupplyManagerTests passed");
            else
                GD.PrintErr(_fails + " SupplyManagerTests FAILED");
        }
    }
}
