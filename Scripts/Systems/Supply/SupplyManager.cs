using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Supply
{
    /// <summary>
    /// 鍚庡嫟琛ョ粰绠＄悊鍣紙PRD 搂2.5锛?
    /// 鍦ㄦ瘡涓€鏂瑰洖鍚堢粨鏉熸椂璐熻矗锛?
    ///   1. 璁＄畻琛ョ粰缃戠粶
    ///   2. 鏇存柊鍚勮惀鏂仈锛圤OS锛夊洖鍚堟暟
    ///   3. 搴旂敤鐤插姵鎭㈠ / 鏂仈鐤插姵鎯╃綒
    /// </summary>
    public class SupplyManager
    {
        private SupplyNetwork _network = new();

        /// <summary>
        /// 瀵规寚瀹氶樀钀ユ墽琛屽洖鍚堟湯琛ョ粰鏇存柊銆?
        /// 璋冪敤鏃舵満锛氳闃佃惀瀹屾垚鎵€鏈夎鍔ㄥ悗锛孍ndStrategicTurn 涔嬪墠銆?
        /// </summary>
        public void UpdateFactionEndTurn(
            int faction,
            ColdWarWargame.Systems.Battlefield.GridMap map,
            IEnumerable<(Battalion bat, Vector2I pos)> battalions,
            HashSet<Vector2I> enemyOccupied,
            HashSet<Vector2I> enemyZOC)
        {
            var sp = _network.ComputeSupplySP(map, faction, enemyOccupied, enemyZOC);

            foreach (var (bat, pos) in battalions)
            {
                if (bat.Faction != faction) continue;

                bool inSupply = sp[pos.X, pos.Y] > 0f;

                if (!inSupply)
                {
                    // 鏂仈锛圥RD 搂2.5.3锛?
                    bat.TurnsOOS++;

                    if (bat.TurnsOOS == 1)
                        bat.Fatigue = Math.Min(bat.Fatigue + 1, 10);
                    else if (bat.TurnsOOS >= 2)
                        bat.Fatigue = Math.Min(bat.Fatigue + 2, 10);
                }
                else
                {
                    // 琛ョ粰姝ｅ父
                    bat.TurnsOOS = 0;

                    // 鐤插姵鎭㈠锛圥RD 搂2.5.1锛夛細鍩轰簬鍓╀綑 AP
                    if (bat.CurrentAP >= 8f)
                        bat.Fatigue = Math.Max(0, bat.Fatigue - 2);
                    else if (bat.CurrentAP >= 4f)
                        bat.Fatigue = Math.Max(0, bat.Fatigue - 1);
                }

                bat.Fatigue = Math.Clamp(bat.Fatigue, 0, 10);
            }
        }

        /// <summary>璋冭瘯锛氭墦鍗拌ˉ缁欏娍鑳界綉鏍?/summary>
        public void PrintSupplyGrid(float[,] sp, int w, int h)
        {
            GD.Print("=== Supply SP Grid ===");
            for (int y = 0; y < h; y++)
            {
                var row = new System.Text.StringBuilder();
                for (int x = 0; x < w; x++)
                {
                    float val = sp[x, y];
                    if (val <= 0f) row.Append(" ..");
                    else row.Append(val.ToString("0").PadLeft(3));
                }
                GD.Print(row.ToString());
            }
        }
    }
}
