using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;

namespace ColdWarWargame.Systems.Combat
{
    public static class CombatUtils
    {
        static string Normalize(string s) => s?.ToLowerInvariant() ?? string.Empty;

        public static bool HasCommandNetwork(Battalion b)
        {
            if (b == null) return false;
            return b.GetAllSubUnits().Any(u => u.Template?.TacticalTags?.Capabilities?.Any(cap => Normalize(cap) == "command") == true);
        }

        public static int CountCapability(Battalion b, string cap)
        {
            if (b == null || string.IsNullOrEmpty(cap)) return 0;
            string want = Normalize(cap);
            return b.GetAllSubUnits()
                    .Count(u => u.Template?.TacticalTags?.Capabilities?.Any(c => Normalize(c) == want) == true);
        }

        public static bool HasAnyCapability(Battalion b, params string[] caps)
        {
            if (b == null || caps == null || caps.Length == 0) return false;
            var lowers = caps.Select(c => Normalize(c)).ToHashSet();
            return b.GetAllSubUnits().Any(u =>
                u.Template?.TacticalTags?.Capabilities?.Any(c => lowers.Contains(Normalize(c))) == true);
        }

        public static bool HasBattalionTag(Battalion b, string tag)
        {
            return b != null && b.HasBattalionTag(tag);
        }

        public static bool IsInfantry(SubUnitInstance u)
        {
            var ct = u.Template?.ClassType ?? "";
            ct = Normalize(ct);
            return ct.StartsWith("inf") || ct.Contains("infantry");
        }

        public static bool IsSoldier(SubUnitInstance u)
        {
            if (u == null) return false;

            var category = Normalize(u.Category);
            if (category == "infantry")
                return true;
            if (category == "vehicle")
                return false;

            var ct = Normalize(u.Template?.ClassType);
            if (ct.StartsWith("inf") || ct.Contains("infantry"))
                return true;

            return false;
        }

        public static bool IsVehicle(SubUnitInstance u)
        {
            return u != null && !IsSoldier(u);
        }

        public static (int soldiers, int vehicles) CountDestroyedUnitLosses(IEnumerable<CasualtyRecord> casualties)
        {
            int soldiers = 0;
            int vehicles = 0;

            if (casualties == null)
                return (0, 0);

            foreach (var casualty in casualties)
            {
                if (casualty == null)
                    continue;

                if (IsSoldier(casualty.Unit))
                    soldiers += Math.Max(0, casualty.HpLost);
                else
                {
                    if (casualty.IsDestroyed)
                        vehicles++;
                }
            }

            return (soldiers, vehicles);
        }

        public static bool IsArtillery(SubUnitInstance u)
        {
            var ct = Normalize(u.Template?.ClassType);
            return ct.Contains("arty") || ct.Contains("rocket") || ct.Contains("mortar");
        }

        public static bool HasHeliDomain(Battalion b)
        {
            return b.GetAllSubUnits().Any(u => Normalize(u.Template?.TacticalTags?.Domain) == "heli");
        }

        public static bool HasAnyAA(Battalion b)
        {
            // detect AA by class_type keywords (spaag, sam) or capabilities if provided
            return b.GetAllSubUnits().Any(u =>
            {
                var ct = Normalize(u.Template?.ClassType);
                if (ct.Contains("spaag") || ct.Contains("sam") || ct.Contains("inf_aa")) return true;
                var caps = u.Template?.TacticalTags?.Capabilities;
                return caps != null && caps.Any(c => Normalize(c).Contains("aa"));
            });
        }
    }
}