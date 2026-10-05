using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    public sealed record CommandContribution(CombatElementKey Element, string CompanyName,
        CombatAssignment Assignment, int LivingUnits, float Attack, float Defense,
        IReadOnlyList<string> Capabilities);

    // This is an input audit, not a combat forecast or a casualty exposure decision.
    public sealed record CommandPhaseReport(int Phase, string Reason,
        IReadOnlyList<CommandContribution> Contributions,
        IReadOnlyList<CombatElementKey> WaitingReserve,
        IReadOnlyList<CombatElementKey> Relieved)
    {
        public float Attack => Contributions.Sum(c => c.Attack);
        public float Defense => Contributions.Sum(c => c.Defense);

        public static CommandPhaseReport Capture(CombatCommandPlan plan, CommandSelection selection)
        {
            var contributions = selection.Frontline.Concat(selection.Support).Select(order =>
            {
                var owner = plan.Owner(order.Element);
                var company = plan.Resolve(order.Element);
                var living = company.Platoons.SelectMany(p => p.Units).Where(u => u.SurvivalState == 1).ToArray();
                float factor = owner.GetOrganizationalDebuff() * owner.GetFatigueCombatMultiplier() / 10f;
                var capabilities = living.SelectMany(u => u.Template.TacticalTags.Capabilities)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                // A committed reserve acts as frontline regardless of its original order.
                var role = selection.Frontline.Contains(order) ? CombatAssignment.Frontline : CombatAssignment.Support;
                return new CommandContribution(order.Element, company.Name, role, living.Length,
                    living.Sum(u => u.Template.CombatStats.Attack) * factor,
                    living.Sum(u => u.Template.CombatStats.Defense) * factor, Array.AsReadOnly(capabilities));
            }).ToArray();
            return new(selection.Phase, selection.Reason, Array.AsReadOnly(contributions),
                Array.AsReadOnly(selection.Reserve.Select(o => o.Element).ToArray()),
                Array.AsReadOnly(selection.Relieved.Select(o => o.Element).ToArray()));
        }
    }
}
