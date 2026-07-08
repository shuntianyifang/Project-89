using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Models;

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

        public static bool IsInfantry(SubUnitInstance u)
        {
            var ct = u.Template?.ClassType ?? "";
            ct = Normalize(ct);
            return ct.StartsWith("inf") || ct.Contains("infantry");
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