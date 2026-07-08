using System.Collections.Generic;
using Godot;
using ColdWarWargame.Systems.Battlefield;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;

namespace ColdWarWargame.Tests.Battlefield
{
    public static class FrontlineTests
    {
        static int _fails = 0;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[FL FAIL] " + msg); }
            else { GD.Print("[FL PASS] " + msg); }
        }

        static void Test_MergeCollinearBoundarySegments()
        {
            var resolver = new FrontlineResolver();
            int[,] ownership =
            {
                { 1, 1 },
                { 1, 1 },
                { 2, 2 }
            };

            var segments = resolver.ExtractBoundarySegments(ownership);

            Assert(segments.Count == 1, "Merged boundary should produce one segment");
            Assert(Mathf.IsEqualApprox(segments[0].Start.X, 2f), "Boundary X = 2");
            Assert(Mathf.IsEqualApprox(segments[0].Start.Y, 0f), "Boundary starts at Y = 0");
            Assert(Mathf.IsEqualApprox(segments[0].End.Y, 2f), "Boundary ends at Y = 2");
        }

        static void Test_ResolveFrontlineFromOwnershipMap()
        {
            var resolver = new FrontlineResolver();

            int[,] controlMap =
            {
                { 1, 1, 1, 1, 1 },
                { 1, 1, 1, 1, 1 },
                { 1, 1, 1, 1, 1 },
                { 2, 2, 2, 2, 2 },
                { 2, 2, 2, 2, 2 }
            };

            var segments = resolver.ResolveFrontline(controlMap);

            Assert(segments.Count == 1, "Frontline should come from occupied ownership map");
            Assert(Mathf.IsEqualApprox(segments[0].Start.X, 3f), "Ownership frontline X = 3");
        }

        static void Test_ResolveFrontlineFromOccupationOnly()
        {
            var resolver = new FrontlineResolver();

            int[,] controlMap =
            {
                { 1, 1, 1, 1, 1 },
                { 1, 1, 1, 1, 1 },
                { 1, 1, 1, 1, 1 },
                { 2, 2, 2, 2, 2 },
                { 2, 2, 2, 2, 2 }
            };

            var segments = resolver.ResolveFrontline(controlMap);

            Assert(segments.Count == 1, "Frontline should come only from occupation state");
            Assert(Mathf.IsEqualApprox(segments[0].Start.X, 3f), "Occupation-only frontline X = 3");
        }

        static void Test_ResolveFrontlineClosedLoop()
        {
            var resolver = new FrontlineResolver();

            int[,] controlMap =
            {
                { 2, 2, 2, 2, 2, 2 },
                { 2, 1, 1, 1, 1, 2 },
                { 2, 1, 1, 1, 1, 2 },
                { 2, 1, 1, 1, 1, 2 },
                { 2, 1, 1, 1, 1, 2 },
                { 2, 2, 2, 2, 2, 2 }
            };

            var chains = resolver.ResolveFrontlineChains(controlMap);

            Assert(chains.Count == 1, "Encirclement should produce one dominant loop chain");
            Assert(chains[0].Points.Count > 8, "Closed loop should contain enough smoothed points");
            Assert(chains[0].Points[0].DistanceTo(chains[0].Points[^1]) < 0.5f, "Loop chain should close back on itself");
        }

        public static void RunAll()
        {
            _fails = 0;

            Test_MergeCollinearBoundarySegments();
            Test_ResolveFrontlineFromOwnershipMap();
            Test_ResolveFrontlineFromOccupationOnly();
            Test_ResolveFrontlineClosedLoop();

            if (_fails == 0)
                GD.Print("All FrontlineTests passed");
            else
                GD.PrintErr(_fails + " FrontlineTests FAILED");
        }
    }
}