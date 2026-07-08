using System.Collections.Generic;
using System.Linq;
using Godot;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;
using MovementResolver = ColdWarWargame.Systems.Battlefield.MovementResolver;
using ZOCManager = ColdWarWargame.Systems.Battlefield.ZOCManager;
using TileData = ColdWarWargame.Models.TileData;
using Battalion = ColdWarWargame.Models.Battalion;

namespace ColdWarWargame.Tests.Battlefield
{
    public static class GridTests
    {
        static int _fails = 0;
        static int _passes = 0;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[GRID FAIL] " + msg); }
            else { _passes++; GD.Print("[GRID PASS] " + msg); }
        }

        static void AssertFloat(float actual, float expected, string msg, float eps = 0.01f)
        {
            bool ok = System.Math.Abs(actual - expected) < eps;
            if (!ok) { _fails++; GD.PrintErr("[GRID FAIL] " + msg + ": expected " + expected + ", got " + actual); }
            else { _passes++; GD.Print("[GRID PASS] " + msg + ": " + actual); }
        }

        static void Test_TerrainCosts()
        {
            AssertFloat(TileData.GetTerrainCost(0), 2f, "Plain terrain cost = 2");
            AssertFloat(TileData.GetTerrainCost(1), 4f, "Forest terrain cost = 4");
            AssertFloat(TileData.GetTerrainCost(2), 1f, "Semi-urban terrain cost = 1");
            AssertFloat(TileData.GetTerrainCost(3), 1f, "Urban terrain cost = 1");
            Assert(float.IsPositiveInfinity(TileData.GetTerrainCost(99)), "Unknown terrain = impassable");
        }

        static void Test_InfraCosts()
        {
            Assert(float.IsPositiveInfinity(TileData.GetInfraCost(0)), "No infra = no bonus");
            AssertFloat(TileData.GetInfraCost(1), 1f, "Road infra cost = 1");
            AssertFloat(TileData.GetInfraCost(2), 0.5f, "Highway infra cost = 0.5");
        }

        static void Test_TileMovementCosts()
        {
            var plain = new TileData(0, 0);
            AssertFloat(plain.GetMovementCost(), 2f, "Plain+noInfra cost = 2");

            var plainRoad = new TileData(0, 1);
            AssertFloat(plainRoad.GetMovementCost(), 1f, "Plain+Road cost = min(2,1) = 1");

            var plainHighway = new TileData(0, 2);
            AssertFloat(plainHighway.GetMovementCost(), 0.5f, "Plain+Highway cost = min(2,0.5) = 0.5");

            var forestRoad = new TileData(1, 1);
            AssertFloat(forestRoad.GetMovementCost(), 1f, "Forest+Road cost = min(4,1) = 1");

            var forestHighway = new TileData(1, 2);
            AssertFloat(forestHighway.GetMovementCost(), 0.5f, "Forest+Highway cost = min(4,0.5) = 0.5");

            var blocked = new TileData(0, 0, false);
            Assert(float.IsPositiveInfinity(blocked.GetMovementCost()), "Impassable tile cost = +inf");
        }

        static void Test_GridMapCreation()
        {
            // C# 2D array: [row, col]. Row0 = {0,1,2}, Row1 = {3,0,1}
            int[,] terrain = {
                { 0, 1, 2 },
                { 3, 0, 1 }
            };
            var map = GridMap.FromTerrainArray(terrain);
            Assert(map.Width == 3, "Map width = 3 (3 cols)");
            Assert(map.Height == 2, "Map height = 2 (2 rows)");
            AssertFloat(map.GetTile(new Vector2I(0, 0)).TerrainType, 0, "Tile (0,0) terrain = terrain[0,0] = 0");
            AssertFloat(map.GetTile(new Vector2I(2, 1)).TerrainType, 1, "Tile (2,1) terrain = terrain[1,2] = 1");
        }

        static void Test_GridMapBounds()
        {
            var map = new GridMap(5, 5);
            Assert(map.IsInBounds(new Vector2I(0, 0)), "(0,0) in bounds");
            Assert(map.IsInBounds(new Vector2I(4, 4)), "(4,4) in bounds");
            Assert(!map.IsInBounds(new Vector2I(-1, 0)), "(-1,0) out of bounds");
            Assert(!map.IsInBounds(new Vector2I(0, 5)), "(0,5) out of bounds");
            Assert(!map.IsInBounds(new Vector2I(5, 0)), "(5,0) out of bounds");
        }

        static void Test_GridMapNeighbors()
        {
            var map = new GridMap(5, 5);
            var center = new Vector2I(2, 2);

            var orth = map.GetOrthogonalNeighbors(center);
            Assert(orth.Count == 4, "Center has 4 orthogonal neighbors");

            var all = map.GetAllNeighbors(center);
            Assert(all.Count == 8, "Center has 8 total neighbors");

            var corner = new Vector2I(4, 4);
            var cornerOrth = map.GetOrthogonalNeighbors(corner);
            Assert(cornerOrth.Count == 2, "Corner (4,4) has 2 orthogonal neighbors");

            var cornerAll = map.GetAllNeighbors(corner);
            Assert(cornerAll.Count == 3, "Corner (4,4) has 3 total neighbors");
        }

        static void Test_FromLayers()
        {
            int[,] terrain = {
                { 0, 0, 1 },
                { 2, 3, 0 }
            };
            int[,] infra = {
                { 2, 0, 1 },
                { 0, 2, 0 }
            };
            var map = GridMap.FromLayers(terrain, infra);

            // (0,0): row0,col0=0(terrain),row0,col0=2(infra) => highway+plain = 0.5
            AssertFloat(map.GetTile(new Vector2I(0, 0)).GetMovementCost(), 0.5f, "Layer (0,0) highway+plain = 0.5");

            // (1,0): row0,col1=0(terrain),row0,col1=0(infra) => plain alone = 2.0
            AssertFloat(map.GetTile(new Vector2I(1, 0)).GetMovementCost(), 2f, "Layer (1,0) plain alone = 2");

            // (2,1): row1,col2=0(terrain),row1,col2=0(infra) => plain alone = 2.0
            AssertFloat(map.GetTile(new Vector2I(2, 1)).GetMovementCost(), 2f, "Layer (2,1) plain alone = 2");

            // (2,0): row0,col2=1(terrain),row0,col2=1(infra) => forest+road = min(4,1) = 1
            AssertFloat(map.GetTile(new Vector2I(2, 0)).GetMovementCost(), 1f, "Layer (2,0) forest+road = 1");
        }

        static void Test_MovementCosts()
        {
            var map = new GridMap(10, 10);
            var resolver = new MovementResolver(map);
            bool noBlock(Vector2I p) => false;

            float orthCost = resolver.GetMoveCost(new Vector2I(3, 3), new Vector2I(4, 3), noBlock);
            AssertFloat(orthCost, 2.0f, "Orthogonal move on plain = 2.0 AP");

            float diagCost = resolver.GetMoveCost(new Vector2I(3, 3), new Vector2I(4, 4), noBlock);
            AssertFloat(diagCost, 2.8f, "Diagonal move on plain = 2.8 AP");
        }

        static void Test_MovementOnHighway()
        {
            var map = new GridMap(10, 10);
            map.SetTile(new Vector2I(5, 5), new TileData(0, 2));
            var resolver = new MovementResolver(map);
            bool noBlock(Vector2I p) => false;

            float orthCost = resolver.GetMoveCost(new Vector2I(4, 5), new Vector2I(5, 5), noBlock);
            AssertFloat(orthCost, 0.5f, "Orthogonal onto highway = 0.5 AP");

            float diagCost = resolver.GetMoveCost(new Vector2I(4, 4), new Vector2I(5, 5), noBlock);
            AssertFloat(diagCost, 0.7f, "Diagonal onto highway = 0.7 AP");
        }

        static void Test_HeliBattalionMovementCost()
        {
            var map = new GridMap(10, 10);
            map.SetTile(new Vector2I(5, 5), new TileData(1, 0)); // forest, expensive for normal units
            var resolver = new MovementResolver(map);
            bool noBlock(Vector2I p) => false;

            var heli = new Battalion { Name = "Heli", Faction = 1 };
            heli.BattalionTags.Add("Heli_Battalion");

            float orthCost = resolver.GetMoveCost(new Vector2I(4, 5), new Vector2I(5, 5), noBlock, heli);
            AssertFloat(orthCost, 0.5f, "Heli_Battalion orthogonal move = 0.5 AP regardless of terrain");

            float diagCost = resolver.GetMoveCost(new Vector2I(4, 4), new Vector2I(5, 5), noBlock, heli);
            AssertFloat(diagCost, 0.7f, "Heli_Battalion diagonal move = 0.7 AP regardless of terrain");

            float normalOrthCost = resolver.GetMoveCost(new Vector2I(4, 5), new Vector2I(5, 5), noBlock);
            AssertFloat(normalOrthCost, 4.0f, "Non-heli unit still uses terrain-based orthogonal cost");
        }

        static void Test_CornerClipping()
        {
            var map = new GridMap(5, 5);
            var resolver = new MovementResolver(map);
            var from = new Vector2I(1, 1);
            var target = new Vector2I(2, 2);

            bool bothBlocked(Vector2I p) =>
                p == new Vector2I(2, 1) || p == new Vector2I(1, 2);

            bool canClipBoth = resolver.CanMoveDiagonal(from, target, bothBlocked);
            Assert(!canClipBoth, "Corner clipping: both sides blocked -> diagonal blocked");

            bool oneBlocked(Vector2I p) => p == new Vector2I(2, 1);
            bool canClipOne = resolver.CanMoveDiagonal(from, target, oneBlocked);
            Assert(canClipOne, "Corner clipping: one side blocked -> diagonal OK");

            float cost = resolver.GetMoveCost(from, target, oneBlocked);
            AssertFloat(cost, 2.8f, "Diagonal with one blocked side still costs 2.8 AP");

            float blockedCost = resolver.GetMoveCost(from, target, bothBlocked);
            Assert(float.IsPositiveInfinity(blockedCost), "Diagonal cost = +inf when both flanks blocked");
        }

        static void Test_CanAfford()
        {
            Assert(MovementResolver.CanAfford(12f, 12f), "Exactly 12 AP can afford 12 cost");
            Assert(MovementResolver.CanAfford(11.96f, 12f), "11.96 + EPS(0.05) >= 12 -> can afford");
            Assert(!MovementResolver.CanAfford(10f, 12f), "10 AP cannot afford 12 cost");
            Assert(MovementResolver.CanAfford(0.03f, 0.05f), "0.03 + EPS(0.05) >= 0.05 -> can afford ultra-small");
        }

        static void Test_ReachableTiles_Plain()
        {
            var map = new GridMap(10, 10);
            var resolver = new MovementResolver(map);
            var start = new Vector2I(3, 3);

            bool noZOC(Vector2I p) => false;
            bool empty(Vector2I p) => false;

            var reachable = resolver.GetReachableTiles(start, 12f, noZOC, empty);

            Assert(reachable.ContainsKey(new Vector2I(4, 3)), "Reachable: (4,3)");
            Assert(!reachable.ContainsKey(new Vector2I(10, 3)), "Not reachable: (10,3)");
            Assert(reachable.ContainsKey(new Vector2I(4, 4)), "Reachable: (4,4) diagonal");
            Assert(reachable.ContainsKey(new Vector2I(9, 3)), "Reachable: (9,3) at 12.0 AP on plain");
        }

        static void Test_ReachableTiles_ZOCBlocks()
        {
            var map = new GridMap(10, 10);
            var resolver = new MovementResolver(map);
            var start = new Vector2I(3, 3);
            var blockedByZOC = new HashSet<Vector2I>
            {
                new Vector2I(4, 3),
                new Vector2I(4, 4),
            };
            bool isEnemyZOC(Vector2I p) => blockedByZOC.Contains(p);
            bool empty(Vector2I p) => false;

            var reachable = resolver.GetReachableTiles(start, 12f, isEnemyZOC, empty);

            Assert(!reachable.ContainsKey(new Vector2I(4, 3)), "ZOC blocks orth: (4,3) not reachable");
            Assert(!reachable.ContainsKey(new Vector2I(4, 4)), "ZOC blocks diag: (4,4) not reachable");
            Assert(reachable.ContainsKey(new Vector2I(3, 2)), "Not in ZOC: (3,2) reachable");
            Assert(reachable.ContainsKey(new Vector2I(2, 3)), "Not in ZOC: (2,3) reachable");
            Assert(!reachable.ContainsKey(start), "Start tile not in reachable set");
        }

        static void Test_ZOC_SingleUnit()
        {
            var map = new GridMap(10, 10);
            var zocMgr = new ZOCManager(map);

            var unitPos = new Vector2I(4, 5);
            var units = new List<Vector2I> { unitPos };

            var zoc = zocMgr.GetFactionZOC(units);

            Assert(zoc.Count == 9, "Single unit ZOC covers 9 tiles (3x3)");
            Assert(zoc.Contains(new Vector2I(3, 4)), "ZOC includes top-left (3,4)");
            Assert(zoc.Contains(new Vector2I(4, 5)), "ZOC includes the unit's own tile (4,5)");
            Assert(zoc.Contains(new Vector2I(5, 6)), "ZOC includes bottom-right (5,6)");
            Assert(!zoc.Contains(new Vector2I(6, 5)), "ZOC does not include (6,5)");
            Assert(!zoc.Contains(new Vector2I(4, 7)), "ZOC does not include (4,7)");
        }

        static void Test_ZOC_MultipleUnits()
        {
            var map = new GridMap(10, 10);
            var zocMgr = new ZOCManager(map);

            var units = new List<Vector2I>
            {
                new Vector2I(4, 5),
                new Vector2I(7, 3)
            };

            var zoc = zocMgr.GetFactionZOC(units);

            Assert(zoc.Contains(new Vector2I(4, 5)), "Combined ZOC: unit1 position");
            Assert(zoc.Contains(new Vector2I(7, 3)), "Combined ZOC: unit2 position");
            Assert(zoc.Contains(new Vector2I(6, 4)), "Combined ZOC: overlap region");
        }

        static void Test_ZOC_IsInEnemyZOC()
        {
            var map = new GridMap(10, 10);
            var zocMgr = new ZOCManager(map);

            var enemyPositions = new List<Vector2I> { new Vector2I(5, 5) };

            Assert(zocMgr.IsInEnemyZOC(new Vector2I(5, 5), enemyPositions), "Enemy own tile is in ZOC");
            Assert(zocMgr.IsInEnemyZOC(new Vector2I(4, 4), enemyPositions), "Adjacent tile in ZOC");
            Assert(zocMgr.IsInEnemyZOC(new Vector2I(6, 6), enemyPositions), "Adjacent tile in ZOC");
            Assert(!zocMgr.IsInEnemyZOC(new Vector2I(8, 5), enemyPositions), "Far tile not in ZOC (2 tiles away)");
            Assert(!zocMgr.IsInEnemyZOC(new Vector2I(0, 0), enemyPositions), "Far tile (0,0) not in ZOC");
        }

        public static void RunAll()
        {
            _fails = 0;
            _passes = 0;

            Test_TerrainCosts();
            Test_InfraCosts();
            Test_TileMovementCosts();

            Test_GridMapCreation();
            Test_GridMapBounds();
            Test_GridMapNeighbors();
            Test_FromLayers();

            Test_MovementCosts();
            Test_MovementOnHighway();
            Test_HeliBattalionMovementCost();
            Test_CornerClipping();
            Test_CanAfford();
            Test_ReachableTiles_Plain();
            Test_ReachableTiles_ZOCBlocks();

            Test_ZOC_SingleUnit();
            Test_ZOC_MultipleUnits();
            Test_ZOC_IsInEnemyZOC();

            if (_fails == 0)
                GD.Print("All GridTests passed (" + _passes + " tests)");
            else
                GD.PrintErr(_fails + "/" + (_passes + _fails) + " GridTests FAILED (" + _passes + " passed)");
        }
    }
}
