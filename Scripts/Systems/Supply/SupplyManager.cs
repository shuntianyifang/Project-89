using System;
using System.Collections.Generic;
using Godot;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Supply
{
    public class SupplyManager
    {
        private SupplyNetwork _network = new();

        public void UpdateFactionEndTurn(
            int faction,
            ColdWarWargame.Systems.Battlefield.GridMap map,
            IEnumerable<(Battalion bat, Vector2I pos)> battalions,
            HashSet<Vector2I> enemyOccupied,
            HashSet<Vector2I> enemyZOC)
        {
            var sp = _network.ComputeSupplySP(map, faction, enemyOccupied, enemyZOC, GetEnemyAP(battalions, faction));

            foreach (var (bat, pos) in battalions)
            {
                if (bat.Faction != faction) continue;
                int fatigueBefore = bat.Fatigue;
                bool inSupply = sp[pos.X, pos.Y] > 0f;
                if (!inSupply)
                {
                    bat.TurnsOOS++;
                    if (bat.TurnsOOS == 1)
                        bat.Fatigue = Math.Min(bat.Fatigue + 1, Battalion.FatigueOverflowCap);
                    else
                        bat.Fatigue = Math.Min(bat.Fatigue + 2, Battalion.FatigueOverflowCap);
                }
                else
                {
                    bat.TurnsOOS = 0;
                    if (bat.CurrentAP >= 8f)
                        bat.Fatigue = Math.Max(0, bat.Fatigue - 2);
                    else if (bat.CurrentAP >= 4f)
                        bat.Fatigue = Math.Max(0, bat.Fatigue - 1);

                    int fatigueRecovered = Math.Max(0, fatigueBefore - bat.Fatigue);
                    RecoverHpFromFatigue(bat, fatigueRecovered);
                }
                bat.Fatigue = Math.Clamp(bat.Fatigue, 0, Battalion.FatigueOverflowCap);
            }
        }

        static void RecoverHpFromFatigue(Battalion bat, int fatigueRecovered)
        {
            if (fatigueRecovered <= 0) return;

            int hpRecovered = fatigueRecovered * 2;
            foreach (var unit in bat.GetAllSubUnits())
            {
                if (unit.SurvivalState != 1) continue;
                int maxHp = unit.Template.CombatStats.MaxHp;
                unit.CurrentHp = Math.Clamp(unit.CurrentHp + hpRecovered, 0, maxHp);
            }
        }

        Dictionary<Vector2I, float> GetEnemyAP(IEnumerable<(Battalion bat, Vector2I pos)> battalions, int faction)
        {
            var result = new Dictionary<Vector2I, float>();
            foreach (var (bat, pos) in battalions)
                if (bat.Faction != faction)
                    result[pos] = bat.CurrentAP;
            return result;
        }

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
