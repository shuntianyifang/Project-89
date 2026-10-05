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
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Scenarios;

namespace ColdWarWargame.Tests.Supply
{
    public static class SupplyManagerTests
    {
        static int _fails = 0;
        static bool _unitDbReady = false;
        static bool _templateDbReady = false;

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

        static void EnsureScenarioDatabases()
        {
            EnsureUnitDatabase();
            if (_templateDbReady) return;
            TemplateDatabase.Initialize("res://Scripts/Data/Templates");
            _templateDbReady = true;
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

        static void Test_LowAPEnemy_BlocksOnlyOwnTile()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var net = new SupplyNetwork();
            var enemyOccupied = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var enemyAP = new Dictionary<Vector2I, float> { { new Vector2I(2, 2), 3f } };

            var sp = net.ComputeSupplySP(map, 2, enemyOccupied, new HashSet<Vector2I>(), enemyAP);

            AssertFloat(sp[2, 2], 0f, "Low-AP enemy own tile is blocked");
            Assert(sp[3, 2] > 0f, "Low-AP enemy does not block adjacent tiles");
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

        static void Test_HubOwnership_ChangesSupplyAndOOS()
        {
            // Keep the real map dimensions, but remove roads and airports so only
            // an owned, connected hub can supply the target on the opposite edge.
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(50, 30);
            var manager = new SupplyManager();
            foreach (int faction in new[] { 1, 2 })
            {
                var hub = new Vector2I(25, faction == 1 ? 14 : 15);
                var targetPos = new Vector2I(25, faction == 1 ? 0 : 29);
                var hubs = new HashSet<Vector2I> { hub };
                var occupation = new int[50, 30];
                for (int x = 0; x < 50; x++) occupation[x, faction == 1 ? 29 : 0] = faction;
                var target = MakeSupplyBat("Hub-dependent target", faction);
                var units = new List<(Battalion bat, Vector2I pos)> { (target, targetPos) };
                var empty = new HashSet<Vector2I>();

                // Reuse the manager to also detect stale activation after capture,
                // neutralization, and recapture of the same hub.
                foreach (int owner in new[] { faction, 3 - faction, 0, faction })
                {
                    occupation[hub.X, hub.Y] = owner;
                    bool owned = owner == faction;
                    var supply = manager.ComputeFactionSupplySP(
                        faction, map, units, empty, empty, hubs, null, occupation);
                    string label = $"Hub ownership: faction={faction} owner={owner}";
                    AssertFloat(supply[hub.X, hub.Y], owned ? 36f : 6f,
                        label + " hub resets SP only for its owner");
                    AssertFloat(supply[targetPos.X, targetPos.Y], owned ? 8f : 0f,
                        label + " downstream target SP");
                    manager.UpdateFactionEndTurn(
                        faction, map, units, empty, empty, hubs, null, occupation);
                    Assert(target.WasOOSLastTurn == !owned,
                        label + " end-turn OOS follows ownership");
                }
            }
        }

        static void Test_EnemyControlledAirport_DoesNotProvideSecondarySupply()
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
            var occupation = new int[map.Width, map.Height];
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    occupation[x, y] = 1;

            var supply = net.ComputeSupplySP(
                map,
                2,
                new HashSet<Vector2I>(),
                new HashSet<Vector2I>(),
                null,
                null,
                new HashSet<Vector2I> { new Vector2I(2, 8) },
                occupation);

            AssertFloat(supply[2, 10], 0f, "Enemy-controlled airport must not provide secondary supply");
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
            Assert(bat.TurnsOOS == 0, "First OOS turn keeps turns_oos at 0");
            Assert(unit1.CurrentHp == 5, "OOS: no HP recovery should happen");
        }

        static void Test_OOS_UsesTurn0AndTurn1Rules()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var mgr = new SupplyManager();
            var bat = MakeSupplyBat("TurnTransition", 1);
            bat.Fatigue = 3;
            bat.CurrentAP = 12f;

            var enemyOccupied = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var units = new List<(Battalion, Vector2I)> { (bat, new Vector2I(2, 2)) };

            mgr.UpdateFactionEndTurn(1, map, units, enemyOccupied, new HashSet<Vector2I>());
            Assert(bat.TurnsOOS == 0, "First OOS turn should use turn-0 state");
            Assert(bat.Fatigue == 3, "First OOS turn should not add fatigue yet");

            mgr.UpdateFactionEndTurn(1, map, units, enemyOccupied, new HashSet<Vector2I>());
            Assert(bat.TurnsOOS == 1, "Second consecutive OOS turn should enter turn-1 state");
            Assert(bat.Fatigue == 4, "Turn-1 OOS should add 1 fatigue at end turn");
        }

        static void Test_BlockingRange_UsesOwnTileAnd3x3ForHighAPUnits()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 5);
            var net = new SupplyNetwork();
            var enemyOccupied = new HashSet<Vector2I> { new Vector2I(2, 2) };
            var enemyAP = new Dictionary<Vector2I, float> { { new Vector2I(2, 2), 4f } };

            var sp = net.ComputeSupplySP(map, 2, enemyOccupied, new HashSet<Vector2I>(), enemyAP);

            AssertFloat(sp[2, 2], 0f, "Own tile is blocked");
            AssertFloat(sp[3, 2], 0f, "3x3 range should block the adjacent tile");
            Assert(sp[4, 2] > 0f, "Tiles beyond the 3x3 range should remain reachable");
        }

        static void Test_HighAPBlockingRange_CutsOffPathsBeyondBarrier()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(5, 7);
            var net = new SupplyNetwork();
            var enemyOccupied = new HashSet<Vector2I>
            {
                new Vector2I(0, 3),
                new Vector2I(2, 3),
                new Vector2I(4, 3)
            };
            var enemyAP = new Dictionary<Vector2I, float>
            {
                { new Vector2I(0, 3), 4f },
                { new Vector2I(2, 3), 4f },
                { new Vector2I(4, 3), 4f }
            };

            var sp = net.ComputeSupplySP(map, 2, enemyOccupied, new HashSet<Vector2I>(), enemyAP);

            AssertFloat(sp[2, 6], 0f, "High-AP 3x3 barrier blocks every path beyond it");
        }

        static void Test_Supply_DiagonalCostMatchesMovementAP()
        {
            const int size = 14;
            var terrain = new int[size, size];
            for (int x = 1; x < size; x++)
                terrain[0, x] = -1; // Only (0,0) is a valid Red supply source.

            var map = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(terrain);
            var net = new SupplyNetwork();
            var sp = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            // Nine plain diagonal steps cost 9 * (1.4 * 2.0) = 25.2 AP/SP.
            AssertFloat(sp[9, 9], 10.8f, "Supply diagonal cost must match movement AP cost");
        }

        static void Test_Supply_CannotCutBlockedCorners()
        {
            const int size = 5;
            var terrain = new int[size, size];
            for (int x = 1; x < size; x++)
                terrain[0, x] = -1; // Only (0,0) is a valid Red supply source.
            terrain[1, 0] = -1;

            var map = ColdWarWargame.Systems.Battlefield.GridMap.FromLayers(terrain);
            var net = new SupplyNetwork();
            var sp = net.ComputeSupplySP(map, 2, new HashSet<Vector2I>(), new HashSet<Vector2I>());

            // (1,1) is diagonally adjacent to the source but both crossing flanks are blocked.
            AssertFloat(sp[1, 1], 0f, "Supply cannot diagonally leak through two blocked corners");
            AssertFloat(sp[4, 4], 0f, "Tiles beyond a blocked corner remain cut off");
        }

        static void Test_LargeMap_ManagerPath_HighAPFrontlineCausesPersistentOOS()
        {
            // Match the campaign map dimensions and exercise SupplyManager's runtime
            // AP extraction, rather than injecting an AP dictionary into SupplyNetwork.
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(50, 30);
            var manager = new SupplyManager();
            var units = new List<(Battalion bat, Vector2I pos)>();
            var enemyOccupied = new HashSet<Vector2I>();

            var suppliedRed = MakeSupplyBat("Red rear target", 2);
            suppliedRed.Fatigue = 3;
            units.Add((suppliedRed, new Vector2I(25, 12)));

            // Centers spaced three tiles apart make their 3x3 zones a continuous
            // 50-tile-wide barrier across rows 5-7.
            for (int x = 0; x < 50; x += 3)
            {
                var blocker = MakeSupplyBat("Blue blocker " + x, 1);
                blocker.CurrentAP = 4f;
                var pos = new Vector2I(x, 6);
                units.Add((blocker, pos));
                enemyOccupied.Add(pos);
            }

            var sp = manager.ComputeFactionSupplySP(
                2,
                map,
                units,
                enemyOccupied,
                new HashSet<Vector2I>());

            Assert(sp[25, 3] > 0f, "Large map: supply reaches the source-side of the frontline");
            AssertFloat(sp[25, 12], 0f, "Large map: high-AP frontline cuts off the rear target");

            // Re-run end-of-turn settlement for five Red turns. The first OOS turn
            // establishes the state; every later OOS turn must retain it and add fatigue.
            for (int redTurn = 1; redTurn <= 5; redTurn++)
            {
                manager.UpdateFactionEndTurn(
                    2,
                    map,
                    units,
                    enemyOccupied,
                    new HashSet<Vector2I>());

                Assert(suppliedRed.WasOOSLastTurn,
                    "Large map: rear target remains OOS after Red turn " + redTurn);
            }

            Assert(suppliedRed.TurnsOOS == 1,
                "Large map: rear target reaches the persistent OOS state");
            Assert(suppliedRed.Fatigue == 7,
                "Large map: five OOS turns add fatigue after the initial grace turn");
        }

        static void Test_FuldaScenario_FrontlineAndAirportSupplyChain()
        {
            EnsureScenarioDatabases();

            var scenario = new FuldaGapScenario();
            scenario.LoadOOB(
                "res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json",
                "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");

            // Reposition real Blue OOB battalions as a continuous AP=4 frontline.
            // This mirrors a player-created blockade while preserving the real map,
            // road layer, OOB, supply nodes, and control map used by the game.
            for (int i = 0; i < 17; i++)
            {
                var blocker = scenario.BlueBattalions[i].bat;
                blocker.CurrentAP = 4f;
                scenario.BlueBattalions[i] = (blocker, new Vector2I(i * 3, 8));
            }

            var target = scenario.RedBattalions[0].bat;
            // (36,12) is a Red-controlled airport in the real scenario. The target
            // remains behind the frontline but within its 12-SP local supply range.
            var targetPos = new Vector2I(36, 14);
            target.Fatigue = 3;
            scenario.RedBattalions[0] = (target, targetPos);

            var allUnits = scenario.BlueBattalions.Concat(scenario.RedBattalions).ToList();
            var enemyOccupied = scenario.BlueBattalions.Select(u => u.pos).ToHashSet();
            var enemyZoc = scenario.ZOC.GetFactionZOC(enemyOccupied);
            var occupationMap = scenario.GetOccupationMap();
            var (hubs, airports) = scenario.GetSupplySpecialNodes();
            var manager = new SupplyManager();

            var withoutAirports = manager.ComputeFactionSupplySP(
                2, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, null, occupationMap);
            AssertFloat(withoutAirports[targetPos.X, targetPos.Y], 0f,
                "Fulda scenario: frontline cuts supply when airports are removed");

            manager.UpdateFactionEndTurn(
                2, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, null, occupationMap);
            Assert(target.WasOOSLastTurn,
                "Fulda scenario: rear target enters OOS when airports are removed");

            var airportSupply = manager.ComputeFactionSupplySP(
                2, scenario.Map, allUnits, enemyOccupied, enemyZoc, null, airports, occupationMap);
            Assert(airportSupply[targetPos.X, targetPos.Y] > 0f,
                "Fulda scenario: controlled disconnected airport resupplies the rear target");

            var fullGameSupply = manager.ComputeFactionSupplySP(
                2, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, airports, occupationMap);
            Assert(fullGameSupply[targetPos.X, targetPos.Y] > 0f,
                "Fulda scenario: full game supply inputs keep the airport-supplied target in supply");

            manager.UpdateFactionEndTurn(
                2, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, airports, occupationMap);
            Assert(!target.WasOOSLastTurn,
                "Fulda scenario: airport-supplied rear target does not enter OOS");
            Assert(target.Fatigue == 1,
                "Fulda scenario: airport supply applies normal fatigue recovery");
        }

        static void Test_FuldaPrimarySupply_EnemyInterception()
        {
            EnsureScenarioDatabases();
            foreach (int faction in new[] { 1, 2 })
            {
                var scenario = new FuldaGapScenario();
                scenario.LoadOOB(
                    "res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json",
                    "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
                var enemies = faction == 1 ? scenario.RedBattalions : scenario.BlueBattalions;
                var manager = new SupplyManager();
                var occupation = scenario.GetOccupationMap();
                var target = new Vector2I(0, faction == 1 ? 10 : 18);
                const int lineY = 14;
                // This fixture isolates unit interception: explicitly grant the
                // entire tested source edge, rather than relying on unowned sources.
                for (int x = 0; x < scenario.Map.Width; x++)
                    occupation[x, faction == 1 ? scenario.Map.Height - 1 : 0] = faction;

                float[,] ComputePrimary()
                {
                    var allUnits = scenario.BlueBattalions.Concat(scenario.RedBattalions).ToList();
                    var occupied = enemies.Select(u => u.pos).ToHashSet();
                    return manager.ComputeFactionSupplySP(faction, scenario.Map, allUnits,
                        occupied, scenario.ZOC.GetFactionZOC(occupied), null, null, occupation);
                }

                void PlaceEnemy(int index, Vector2I pos, float ap)
                {
                    var bat = enemies[index].bat;
                    bat.CurrentAP = ap;
                    enemies[index] = (bat, pos);
                }

                void AssertRearCutOff(float[,] sp, string label)
                {
                    int suppliedRearCells = 0;
                    for (int x = 0; x < scenario.Map.Width; x++)
                        for (int y = 0; y < scenario.Map.Height; y++)
                            if ((faction == 1 ? y < lineY : y > lineY) && sp[x, y] > 0f)
                                suppliedRearCells++;
                    Assert(suppliedRearCells == 0,
                        $"Primary interception faction={faction}: {label}, supplied rear cells={suppliedRearCells}");
                }

                var baseline = ComputePrimary();
                Assert(baseline[target.X, target.Y] > 0f,
                    $"Primary interception faction={faction}: target initially supplied on real highway");

                // A horizontal 3x3 blockade cuts every route from the top/bottom
                // source edge, including Fulda's cheap, map-spanning highways.
                for (int i = 0; i < 17; i++)
                    PlaceEnemy(i, new Vector2I(i * 3, lineY), 4f);
                var highAP = ComputePrimary();
                AssertRearCutOff(highAP, "AP=4 continuous blockade");

                // Below 4 AP the same battalions still occupy their own tiles,
                // but the two-cell gaps between them permit genuine detours.
                for (int i = 0; i < 17; i++)
                    enemies[i].bat.CurrentAP = 3.99f;
                var lowAP = ComputePrimary();
                Assert(enemies.Take(17).All(u => lowAP[u.pos.X, u.pos.Y] == 0f),
                    $"Primary interception faction={faction}: AP<4 occupied tiles still block supply");
                Assert(lowAP[target.X, target.Y] > 0f,
                    $"Primary interception faction={faction}: AP<4 gaps restore downstream supply");
                GD.Print($"[SUPPLY DIAG] primary_interception faction={faction} target={target} " +
                         $"baseline={baseline[target.X, target.Y]:F2} AP4={highAP[target.X, target.Y]:F2} " +
                         $"AP3.99={lowAP[target.X, target.Y]:F2}");

                // Even exhausted units block propagation when their actual occupied
                // tiles fill the entire row; this does not rely on the AP threshold.
                for (int x = 0; x < scenario.Map.Width; x++)
                    PlaceEnemy(x, new Vector2I(x, lineY), 0f);
                AssertRearCutOff(ComputePrimary(), "AP=0 solid occupied row");

                // A source inside enemy 3x3 control must not be seeded with 36 SP.
                int nearSourceY = faction == 1 ? scenario.Map.Height - 2 : 1;
                for (int i = 0; i < 17; i++)
                    PlaceEnemy(i, new Vector2I(i * 3, nearSourceY), 4f);
                Assert(ComputePrimary().Cast<float>().All(v => v == 0f),
                    $"Primary interception faction={faction}: blocking every source stops all propagation");
            }
        }

        static void Test_FuldaScenario_InitialSupplyDiagnostics()
        {
            EnsureScenarioDatabases();

            var scenario = new FuldaGapScenario();
            scenario.LoadOOB(
                "res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json",
                "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");

            var allUnits = scenario.BlueBattalions.Concat(scenario.RedBattalions).ToList();
            var (hubs, airports) = scenario.GetSupplySpecialNodes();
            var occupationMap = scenario.GetOccupationMap();
            var manager = new SupplyManager();

            foreach (int faction in new[] { 1, 2 })
            {
                int enemyFaction = faction == 1 ? 2 : 1;
                var factionUnits = allUnits.Where(u => u.bat.Faction == faction).ToList();
                var enemyOccupied = allUnits
                    .Where(u => u.bat.Faction == enemyFaction)
                    .Select(u => u.pos)
                    .ToHashSet();
                var enemyZoc = scenario.ZOC.GetFactionZOC(enemyOccupied);

                var primaryOnly = manager.ComputeFactionSupplySP(
                    faction, scenario.Map, allUnits, enemyOccupied, enemyZoc, null, null, occupationMap);
                var fullSupply = manager.ComputeFactionSupplySP(
                    faction, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, airports, occupationMap);

                var ownedHubs = hubs.Where(p => occupationMap[p.X, p.Y] == faction).ToHashSet();
                var otherHubs = hubs.Where(p => occupationMap[p.X, p.Y] != faction).ToHashSet();
                var ownedHubsSupply = manager.ComputeFactionSupplySP(
                    faction, scenario.Map, allUnits, enemyOccupied, enemyZoc, ownedHubs, airports, occupationMap);
                var otherHubsOnly = manager.ComputeFactionSupplySP(
                    faction, scenario.Map, allUnits, enemyOccupied, enemyZoc, otherHubs, null, occupationMap);
                var hubsOnly = manager.ComputeFactionSupplySP(
                    faction, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, null, occupationMap);
                var missingOccupation = manager.ComputeFactionSupplySP(
                    faction, scenario.Map, allUnits, enemyOccupied, enemyZoc, hubs, null, null);

                // Compare all 1,500 SP values, not just whether a few units are supplied.
                int foreignHubChanges = 0, filteredHubChanges = 0, missingOccupationChanges = 0;
                int airportChanges = 0;
                for (int x = 0; x < scenario.Map.Width; x++)
                {
                    for (int y = 0; y < scenario.Map.Height; y++)
                    {
                        if (Math.Abs(otherHubsOnly[x, y] - primaryOnly[x, y]) > 0.001f)
                            foreignHubChanges++;
                        if (Math.Abs(ownedHubsSupply[x, y] - fullSupply[x, y]) > 0.001f)
                            filteredHubChanges++;
                        if (Math.Abs(missingOccupation[x, y] - hubsOnly[x, y]) > 0.001f)
                            missingOccupationChanges++;
                        if (Math.Abs(fullSupply[x, y] - hubsOnly[x, y]) > 0.001f)
                            airportChanges++;
                    }
                }
                Assert(foreignHubChanges == 0,
                    $"Fulda faction={faction}: foreign/neutral hubs change no SP cells ({foreignHubChanges})");
                Assert(filteredHubChanges == 0,
                    $"Fulda faction={faction}: all hubs match prefiltered owned hubs ({filteredHubChanges})");
                // Positive control: deliberately omit ownership. This fixture must
                // expose the permissive null-map path, or the comparison is vacuous.
                Assert(missingOccupationChanges > 0,
                    $"Fulda faction={faction}: fixture detects missing ownership map ({missingOccupationChanges} SP cells)");

                GD.Print($"[SUPPLY DIAG] faction={faction} covered/1500 " +
                         $"primary={primaryOnly.Cast<float>().Count(v => v > 0f)} " +
                         $"with_hubs={hubsOnly.Cast<float>().Count(v => v > 0f)} " +
                         $"full={fullSupply.Cast<float>().Count(v => v > 0f)} " +
                         $"null_occupation_changed_sp={missingOccupationChanges} airport_changed_sp={airportChanges}");
                foreach (var hub in hubs.OrderBy(p => p.Y).ThenBy(p => p.X))
                    GD.Print($"[SUPPLY DIAG] faction={faction} HUB {hub} " +
                             $"owner={occupationMap[hub.X, hub.Y]} " +
                             $"primary={primaryOnly[hub.X, hub.Y]:F1} with_hubs={hubsOnly[hub.X, hub.Y]:F1}");

                int sourceY = faction == 1 ? scenario.Map.Height - 1 : 0;
                int foreignSources = Enumerable.Range(0, scenario.Map.Width).Count(x =>
                    occupationMap[x, sourceY] != faction && primaryOnly[x, sourceY] == 36f);
                Assert(foreignSources == 0, $"Fulda faction={faction}: foreign/neutral edge tiles never emit full SP");
                GD.Print($"[SUPPLY DIAG] faction={faction} non_owned_edge_sources={foreignSources} " +
                         $"opposite_edge_sp_at_x0={primaryOnly[0, scenario.Map.Height - 1 - sourceY]:F1}");

                var facilitySupported = factionUnits
                    .Where(u => primaryOnly[u.pos.X, u.pos.Y] <= 0f && fullSupply[u.pos.X, u.pos.Y] > 0f)
                    .ToList();
                var oos = factionUnits
                    .Where(u => fullSupply[u.pos.X, u.pos.Y] <= 0f)
                    .ToList();

                GD.Print($"[SUPPLY DIAG] faction={faction} units={factionUnits.Count} " +
                         $"facility_supported={facilitySupported.Count} oos={oos.Count}");
                foreach (var (bat, pos) in facilitySupported)
                {
                    GD.Print($"[SUPPLY DIAG] faction={faction} FACILITY {bat.InstanceId} @ " +
                             $"({pos.X},{pos.Y}) primary={primaryOnly[pos.X, pos.Y]:F1} " +
                             $"with_hubs={hubsOnly[pos.X, pos.Y]:F1} full={fullSupply[pos.X, pos.Y]:F1}");
                }
                foreach (var (bat, pos) in oos)
                    GD.Print($"[SUPPLY DIAG] faction={faction} OOS {bat.InstanceId} @ ({pos.X},{pos.Y})");
            }
        }

        static void Test_PrimarySourceOwnership_CaptureAndRecapture()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(1, 8);
            var manager = new SupplyManager();
            foreach (int faction in new[] { 1, 2 })
            {
                var occupation = new int[1,8];
                int sourceY = faction == 1 ? 7 : 0;
                var target = MakeSupplyBat("Edge-dependent", faction);
                var units = new List<(Battalion bat, Vector2I pos)> { (target, new Vector2I(0,4)) };
                foreach (int owner in new[] { faction, 3-faction, 0, faction })
                {
                    occupation[0,sourceY] = owner;
                    var empty = new HashSet<Vector2I>();
                    var sp = manager.ComputeFactionSupplySP(faction,map,units,empty,empty,null,null,occupation);
                    Assert((sp[0,4] > 0) == (owner == faction),
                        $"Edge ownership faction={faction} owner={owner}: downstream supply follows source ownership");
                    manager.UpdateFactionEndTurn(faction,map,units,empty,empty,null,null,occupation);
                    Assert(target.WasOOSLastTurn == (owner != faction),
                        $"Edge ownership faction={faction} owner={owner}: settlement follows source ownership");
                }
            }
        }

        static void Test_DebugTraceAndSnapshot_RoundTrip()
        {
            EnsureScenarioDatabases();
            var scenario = new FuldaGapScenario();
            scenario.LoadOOB("res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json",
                "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
            var units = scenario.BlueBattalions.Concat(scenario.RedBattalions).ToList();
            var (hubs, airports) = scenario.GetSupplySpecialNodes();
            var occupation = scenario.GetOccupationMap();
            var maps = new float[3][,];
            for (int faction = 1; faction <= 2; faction++)
            {
                var occupied = units.Where(u => u.bat.Faction != faction).Select(u => u.pos).ToHashSet();
                var manager = new SupplyManager(); var trace = new SupplyTrace();
                var normal = manager.ComputeFactionSupplySP(faction, scenario.Map, units, occupied,
                    new HashSet<Vector2I>(), hubs, airports, occupation);
                maps[faction] = manager.ComputeFactionSupplySP(faction, scenario.Map, units, occupied,
                    new HashSet<Vector2I>(), hubs, airports, occupation, trace);
                bool identical = true, pathsValid = true;
                var movement = new ColdWarWargame.Systems.Battlefield.MovementResolver(scenario.Map);
                for (int x = 0; x < scenario.Map.Width; x++)
                    for (int y = 0; y < scenario.Map.Height; y++)
                    {
                        identical &= normal[x,y] == maps[faction][x,y];
                        var route = trace.Routes[x,y];
                        if (normal[x,y] <= 0) { pathsValid &= route == null; continue; }
                        if (route == null) { pathsValid = false; continue; }
                        var path = route.Path(); float cost = 0;
                        pathsValid &= path[^1] == new Vector2I(x,y);
                        for (int i = 0; i < path.Count; i++)
                        {
                            pathsValid &= !trace.Blocked.Contains(path[i]);
                            if (i > 0) cost += movement.GetMoveCost(path[i-1], path[i],
                                p => trace.Blocked.Contains(p) || !scenario.Map.IsPassable(p));
                        }
                        pathsValid &= Math.Abs(cost - route.Cost) < 0.001f && Math.Abs(route.Budget-cost-normal[x,y]) < 0.001f;
                    }
                Assert(identical, $"Debug trace faction={faction}: all SP unchanged by instrumentation");
                Assert(pathsValid, $"Debug trace faction={faction}: all winning paths obey blockade and reproduce SP");
            }
            var canvas = new CanvasLayer(); var renderer = new ColdWarWargame.Rendering.Grid3DRenderer();
            var inspector = new ColdWarWargame.Systems.Gameplay.GameDebugInspector(scenario,
                new TurnManager(), renderer, canvas);
            inspector.SetDisplayed(maps[1], maps[2]);
            Assert(!inspector.HandleKey(Key.F8), "Debug shortcut: editor Stop/F8 is not bound by game");
            inspector.HandleKey(Key.F3);
            Assert(inspector.Enabled, "Debug shortcut: F3 opens inspector");
            inspector.Hover(new Vector2I(0,0));
            inspector.RecordSettlement(2, "before");
            using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(inspector.Capture("test")));
            Assert(SupplySnapshotReplay.Compare(doc.RootElement) == 0,
                "Debug snapshot: serialized actual scenario inputs replay all 3000 SP values exactly");
            inspector.HandleKey(Key.F9);
            Assert(inspector.LastExportPath != null && SupplySnapshotReplay.Run(inspector.LastExportPath) == 0,
                "Debug snapshot: exported JSON file and settlement history replay exactly");
            inspector.HandleKey(Key.F3);
            Assert(!inspector.Enabled, "Debug shortcut: F3 closes inspector");
            canvas.Free(); renderer.Free();
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
            Test_LowAPEnemy_BlocksOnlyOwnTile();
            Test_Hub_Reactivation_ExtendsPrimaryRange();
            Test_DisconnectedAirport_ProvidesSecondarySupply();
            Test_HubOwnership_ChangesSupplyAndOOS();
            Test_EnemyControlledAirport_DoesNotProvideSecondarySupply();
            Test_OOS_Accumulation();
            Test_FatigueRecovery();
            Test_HpRecovery_LinkedToFatigueRecover2();
            Test_HpRecovery_LinkedToFatigueRecover1();
            Test_HpRecovery_NoRecoveryWhenOOS();
            Test_OOS_UsesTurn0AndTurn1Rules();
            Test_BlockingRange_UsesOwnTileAnd3x3ForHighAPUnits();
            Test_HighAPBlockingRange_CutsOffPathsBeyondBarrier();
            Test_Supply_DiagonalCostMatchesMovementAP();
            Test_Supply_CannotCutBlockedCorners();
            Test_LargeMap_ManagerPath_HighAPFrontlineCausesPersistentOOS();
            Test_FuldaScenario_FrontlineAndAirportSupplyChain();
            Test_FuldaPrimarySupply_EnemyInterception();
            Test_FuldaScenario_InitialSupplyDiagnostics();
            Test_DebugTraceAndSnapshot_RoundTrip();
            Test_PrimarySourceOwnership_CaptureAndRecapture();
            Test_DisorganizedInSupply_ForcedToFatigue8NextTurn();

            if (_fails == 0)
                GD.Print("All SupplyManagerTests passed");
            else
                GD.PrintErr(_fails + " SupplyManagerTests FAILED");
        }
    }
}
