using System.Linq;
using System.Text;

namespace ColdWarWargame.Models
{
    public static class BattalionOrgReporter
    {
        const int ColWidth = 16;

        public static string BuildOrganizationSummary(Battalion bat)
        {
            if (bat?.Companies == null || !bat.Companies.Any()) return "";
            var comps = bat.Companies.Where(c => c.Platoons != null && c.Platoons.Any()).ToList();
            if (!comps.Any()) return "";

            var sb = new StringBuilder();
            foreach (var c in comps) sb.Append(PadR(Short(c.Name ?? c.CompanyId), ColWidth));
            sb.AppendLine();

            int maxP = comps.Max(cc => cc.Platoons.Count);
            for (int pi = 0; pi < maxP; pi++)
            {
                // platoon ID row
                foreach (var c in comps)
                    sb.Append(PadR(pi < c.Platoons.Count ? Short(c.Platoons[pi].PlatoonId) : "", ColWidth));
                sb.AppendLine();

                bool composite = comps.Any(c2 => pi < c2.Platoons.Count && HasBoth(c2.Platoons[pi]));
                if (composite)
                {
                    foreach (var c in comps) sb.Append(PadR(InfLabel(c, pi), ColWidth));
                    sb.AppendLine();
                    foreach (var c in comps) sb.Append(PadR(VehLabel(c, pi), ColWidth));
                    sb.AppendLine();
                }
                else
                {
                    foreach (var c in comps) sb.Append(PadR(UniLabel(c, pi), ColWidth));
                    sb.AppendLine();
                }
            }
            return sb.ToString().TrimEnd();
        }

        static bool HasBoth(Platoon pl) =>
            pl.Units.Any(u => IsInf(u)) && pl.Units.Any(u => !IsInf(u));

        static bool IsInf(SubUnitInstance u)
        {
            var cat = (u.Category ?? "").ToLowerInvariant();
            if (cat == "infantry") return true;
            if (cat == "vehicle") return false;
            var ct = (u.Template?.ClassType ?? "").ToLowerInvariant();
            return ct.StartsWith("inf") || ct.Contains("infantry");
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (Encoding.UTF8.GetByteCount(s) <= 14) return s;
            var r = ""; int bc = 0;
            foreach (char ch in s)
            {
                int cb = Encoding.UTF8.GetByteCount(ch.ToString());
                if (bc + cb > 14) break;
                r += ch; bc += cb;
            }
            return r;
        }

        static string PadR(string s, int w)
        {
            int p = w - Encoding.UTF8.GetByteCount(s);
            return p > 0 ? s + new string(' ', p) : s;
        }

        static string InfLabel(Company c, int pi)
        {
            if (pi >= c.Platoons.Count) return "";
            var pl = c.Platoons[pi];
            int a = pl.Units.Count(u => IsInf(u) && u.SurvivalState == 1);
            int t = pl.Units.Count(u => IsInf(u));
            return t == 0 ? "步兵(空)" : (a == 0 ? "步兵(阵亡)" : "步兵x" + a);
        }

        static string VehLabel(Company c, int pi)
        {
            if (pi >= c.Platoons.Count) return "";
            var pl = c.Platoons[pi];
            int a = pl.Units.Count(u => !IsInf(u) && u.SurvivalState == 1);
            int t = pl.Units.Count(u => !IsInf(u));
            return t == 0 ? "载具(空)" : (a == 0 ? "载具(阵亡)" : "载具x" + a);
        }

        static string UniLabel(Company c, int pi)
        {
            if (pi >= c.Platoons.Count) return "";
            var pl = c.Platoons[pi];
            int a = pl.Units.Count(u => u.SurvivalState == 1);
            int t = pl.Units.Count;
            return t == 0 ? "(空)" : (a == 0 ? "全部阵亡" : "单位x" + a);
        }
    }
}