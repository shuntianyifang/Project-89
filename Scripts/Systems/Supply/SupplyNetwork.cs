using System;
using System.Collections.Generic;
using Godot;


namespace ColdWarWargame.Systems.Supply
{
    /// <summary>
    /// 鍩轰簬 Dijkstra 娉涙椽绠楁硶鐨勫姩鎬佽ˉ缁欑綉锛圥RD 搂2.5.2锛?    /// 浠庨樀钀ュ悗鏂瑰湴鍥捐竟缂樺嚭鍙戯紝娌垮彲閫氳缃戞牸浼犳挱琛ョ粰鍔胯兘锛圫P锛夈€?    /// 鏁屾柟鍗犳嵁鏍间笉鍙€氳锛屾晫鏂?ZOC 澧炲姞 15 SP 娑堣€椼€?    /// </summary>
    public class SupplyNetwork
    {
        /// <summary>缁濆琛ョ粰婧愬娍鑳斤紙PRD 搂2.5.2锛?/summary>
        const float MAX_SP = 36f;

        /// <summary>ZOC 闃绘柇闄勫姞娑堣€楋紙PRD 搂2.5.2锛?/summary>
        const float ZOC_PENALTY = 15f;

        const float EPS = 1e-6f;

        /// <summary>
        /// 璁＄畻鎸囧畾闃佃惀鐨勮ˉ缁欏娍鑳界綉鏍笺€?        /// 钃濆啗锛?锛夎ˉ缁欐簮涓哄湴鍥惧簳杈癸紝绾㈠啗锛?锛変负鍦板浘椤惰竟銆?        /// 杩斿洖 float[width, height]锛屽€?> 0 琛ㄧず鏈夎ˉ缁欍€?        /// </summary>
        public float[,] ComputeSupplySP(
            ColdWarWargame.Systems.Battlefield.GridMap map,
            int faction,
            HashSet<Vector2I> enemyOccupied,
            HashSet<Vector2I> enemyZOC, Dictionary<Vector2I, float> enemyAP = null)
        {
            int w = map.Width, h = map.Height;
            var cost = new float[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    cost[x, y] = float.PositiveInfinity;

            // 纭畾琛ョ粰婧愯竟
            int sourceY = faction == 1 ? h - 1 : 0;
            if (faction == 1 || faction == 2)
            {
                for (int x = 0; x < w; x++)
                {
                    var pos = new Vector2I(x, sourceY);
                    if (map.IsPassable(pos) && !enemyOccupied.Contains(pos))
                        cost[x, sourceY] = 0f;
                }
            }

            // Dijkstra
            var frontier = new List<Vector2I>();
            for (int x = 0; x < w; x++)
                if (!float.IsPositiveInfinity(cost[x, sourceY]))
                    frontier.Add(new Vector2I(x, sourceY));

            while (frontier.Count > 0)
            {
                // 鎵炬渶灏?cost 鑺傜偣
                int minIdx = 0;
                float minCost = cost[frontier[0].X, frontier[0].Y];
                for (int i = 1; i < frontier.Count; i++)
                {
                    float c = cost[frontier[i].X, frontier[i].Y];
                    if (c < minCost) { minCost = c; minIdx = i; }
                }

                var current = frontier[minIdx];
                frontier.RemoveAt(minIdx);

                if (minCost >= MAX_SP - EPS) continue;

                foreach (var nb in map.GetAllNeighbors(current))
                {
                    if (!map.IsPassable(nb)) continue;
                    if (enemyOccupied.Contains(nb)) continue;

                    float tileCost = map.GetTile(nb).GetMovementCost();
                    if (float.IsPositiveInfinity(tileCost)) continue;

                    bool zocActive = enemyZOC.Contains(nb); if (zocActive && enemyAP != null && enemyAP.TryGetValue(nb, out float ap) && ap < 4f) zocActive = false; float extra = zocActive ? ZOC_PENALTY : 0f;
                    float newCost = minCost + tileCost + extra;

                    if (newCost < cost[nb.X, nb.Y] - EPS && newCost < MAX_SP - EPS)
                    {
                        cost[nb.X, nb.Y] = newCost;
                        frontier.Add(nb);
                    }
                }
            }

            // 杞崲涓轰緵缁欏娍鑳?SP = max(0, MAX_SP - cost)
            var sp = new float[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    sp[x, y] = float.IsPositiveInfinity(cost[x, y]) ? 0f : Math.Max(0f, MAX_SP - cost[x, y]);

            return sp;
        }
    }
}
