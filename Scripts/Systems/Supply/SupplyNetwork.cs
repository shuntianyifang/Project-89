using System;
using System.Collections.Generic;
using Godot;

namespace ColdWarWargame.Systems.Supply
{
    /// <summary>
    /// Dynamic supply propagation based on weighted Dijkstra.
    /// - Primary source: map edge with 36 SP.
    /// - Hub reactivation: a hub reached by primary network becomes a new 36 SP source.
    /// - Airport fallback: disconnected airports emit local 18 SP secondary supply.
    /// </summary>
    public class SupplyNetwork
    {
        const float MAX_SP = 36f;
        const float SECONDARY_SP = 18f;
        const float ZOC_PENALTY = 15f;
        const float EPS = 1e-6f;

        public float[,] ComputeSupplySP(
            ColdWarWargame.Systems.Battlefield.GridMap map,
            int faction,
            HashSet<Vector2I> enemyOccupied,
            HashSet<Vector2I> enemyZOC,
            Dictionary<Vector2I, float> enemyAP = null,
            HashSet<Vector2I> hubs = null,
            HashSet<Vector2I> airports = null)
        {
            int w = map.Width;
            int h = map.Height;

            hubs ??= new HashSet<Vector2I>();
            airports ??= new HashSet<Vector2I>();

            var primarySources = BuildPrimarySources(map, faction, enemyOccupied);
            var globalCost = BuildInfiniteGrid(w, h);

            MergeBestCost(
                globalCost,
                RunBoundedDijkstra(map, primarySources, MAX_SP, enemyOccupied, enemyZOC, enemyAP));

            // Re-activate hubs reached by the strategic (primary) network.
            var activatedHubs = new HashSet<Vector2I>();
            var newHubSources = new List<Vector2I>();
            CollectNewActivatedHubs(hubs, globalCost, activatedHubs, newHubSources);

            while (newHubSources.Count > 0)
            {
                var hubCost = RunBoundedDijkstra(map, newHubSources, MAX_SP, enemyOccupied, enemyZOC, enemyAP);
                MergeBestCost(globalCost, hubCost);

                newHubSources = new List<Vector2I>();
                CollectNewActivatedHubs(hubs, globalCost, activatedHubs, newHubSources);
            }

            var result = BuildSpFromCost(globalCost, MAX_SP);

            // Airport fallback: airports disconnected from primary/hub strategic network emit local secondary SP.
            var disconnectedAirports = new List<Vector2I>();
            foreach (var airport in airports)
            {
                if (!map.IsInBounds(airport) || !map.IsPassable(airport) || enemyOccupied.Contains(airport))
                    continue;

                if (float.IsPositiveInfinity(globalCost[airport.X, airport.Y]))
                    disconnectedAirports.Add(airport);
            }

            if (disconnectedAirports.Count > 0)
            {
                var secondaryCost = RunBoundedDijkstra(map, disconnectedAirports, SECONDARY_SP, enemyOccupied, enemyZOC, enemyAP);
                var secondarySp = BuildSpFromCost(secondaryCost, SECONDARY_SP);

                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (secondarySp[x, y] > result[x, y])
                            result[x, y] = secondarySp[x, y];
                    }
                }
            }

            return result;
        }

        private static List<Vector2I> BuildPrimarySources(
            ColdWarWargame.Systems.Battlefield.GridMap map,
            int faction,
            HashSet<Vector2I> enemyOccupied)
        {
            var sources = new List<Vector2I>();
            int sourceY = faction == 1 ? map.Height - 1 : 0;

            if (faction != 1 && faction != 2)
                return sources;

            for (int x = 0; x < map.Width; x++)
            {
                var pos = new Vector2I(x, sourceY);
                if (map.IsPassable(pos) && !enemyOccupied.Contains(pos))
                    sources.Add(pos);
            }

            return sources;
        }

        private static float[,] RunBoundedDijkstra(
            ColdWarWargame.Systems.Battlefield.GridMap map,
            List<Vector2I> sources,
            float budget,
            HashSet<Vector2I> enemyOccupied,
            HashSet<Vector2I> enemyZOC,
            Dictionary<Vector2I, float> enemyAP)
        {
            int w = map.Width;
            int h = map.Height;
            var cost = BuildInfiniteGrid(w, h);
            var frontier = new List<Vector2I>();

            foreach (var src in sources)
            {
                if (!map.IsInBounds(src) || !map.IsPassable(src) || enemyOccupied.Contains(src))
                    continue;

                if (cost[src.X, src.Y] > EPS)
                {
                    cost[src.X, src.Y] = 0f;
                    frontier.Add(src);
                }
            }

            while (frontier.Count > 0)
            {
                int minIdx = 0;
                float minCost = cost[frontier[0].X, frontier[0].Y];
                for (int i = 1; i < frontier.Count; i++)
                {
                    float c = cost[frontier[i].X, frontier[i].Y];
                    if (c < minCost)
                    {
                        minCost = c;
                        minIdx = i;
                    }
                }

                var current = frontier[minIdx];
                frontier.RemoveAt(minIdx);

                if (minCost >= budget - EPS)
                    continue;

                foreach (var nb in map.GetAllNeighbors(current))
                {
                    if (!map.IsPassable(nb) || enemyOccupied.Contains(nb))
                        continue;

                    float tileCost = map.GetTile(nb).GetMovementCost();
                    if (float.IsPositiveInfinity(tileCost))
                        continue;

                    bool zocActive = enemyZOC.Contains(nb);
                    if (zocActive && enemyAP != null && enemyAP.TryGetValue(nb, out float ap) && ap < 4f)
                        zocActive = false;

                    float extra = zocActive ? ZOC_PENALTY : 0f;
                    float newCost = minCost + tileCost + extra;

                    if (newCost < cost[nb.X, nb.Y] - EPS && newCost < budget - EPS)
                    {
                        cost[nb.X, nb.Y] = newCost;
                        frontier.Add(nb);
                    }
                }
            }

            return cost;
        }

        private static void CollectNewActivatedHubs(
            HashSet<Vector2I> hubs,
            float[,] currentBestCost,
            HashSet<Vector2I> activatedHubs,
            List<Vector2I> outputNewHubs)
        {
            int w = currentBestCost.GetLength(0);
            int h = currentBestCost.GetLength(1);
            foreach (var hub in hubs)
            {
                if (activatedHubs.Contains(hub))
                    continue;

                if (hub.X < 0 || hub.X >= w || hub.Y < 0 || hub.Y >= h)
                    continue;

                if (!float.IsPositiveInfinity(currentBestCost[hub.X, hub.Y]))
                {
                    activatedHubs.Add(hub);
                    outputNewHubs.Add(hub);
                }
            }
        }

        private static float[,] BuildInfiniteGrid(int w, int h)
        {
            var grid = new float[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    grid[x, y] = float.PositiveInfinity;
                }
            }
            return grid;
        }

        private static void MergeBestCost(float[,] target, float[,] candidate)
        {
            int w = target.GetLength(0);
            int h = target.GetLength(1);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (candidate[x, y] < target[x, y] - EPS)
                        target[x, y] = candidate[x, y];
                }
            }
        }

        private static float[,] BuildSpFromCost(float[,] costGrid, float budget)
        {
            int w = costGrid.GetLength(0);
            int h = costGrid.GetLength(1);
            var sp = new float[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    sp[x, y] = float.IsPositiveInfinity(costGrid[x, y]) ? 0f : Math.Max(0f, budget - costGrid[x, y]);
                }
            }
            return sp;
        }
    }
}
