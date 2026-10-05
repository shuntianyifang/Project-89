using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    public enum CombatAssignment { Frontline, Reserve, Support, HoldOut }

    // The company index identifies the original roster slot, never a copied force.
    public readonly record struct CombatElementKey(string BattalionId, int CompanyIndex);

    public sealed record CompanyCombatOrder(CombatElementKey Element, CombatAssignment Assignment);

    public sealed class CombatCommandPlan
    {
        public int Faction { get; }
        public IReadOnlyList<Battalion> Roster { get; }
        public IReadOnlyList<CompanyCombatOrder> Orders { get; }

        public CombatCommandPlan(int faction, IEnumerable<Battalion> roster, IEnumerable<CompanyCombatOrder> orders)
        {
            if (faction is not (1 or 2) || roster == null || orders == null)
                throw new ArgumentException("命令计划缺少阵营、兵力或命令");
            var units = roster.ToArray();
            var assignments = orders.ToArray();
            if (units.Any(b => b == null || b.Faction != faction || string.IsNullOrWhiteSpace(b.InstanceId)) ||
                units.Select(b => b.InstanceId).Distinct().Count() != units.Length)
                throw new ArgumentException("命令兵力包含重复番号或错误阵营");
            var expected = units.SelectMany(b => b.Companies.Select((c,i) => new CombatElementKey(b.InstanceId,i))).ToHashSet();
            if (assignments.Any(o => o == null || !Enum.IsDefined(o.Assignment) || !expected.Contains(o.Element)) ||
                assignments.Select(o => o.Element).Distinct().Count() != assignments.Length || assignments.Length != expected.Count)
                throw new ArgumentException("每个连必须有且只有一条有效命令");
            if (units.SelectMany(b=>b.GetAllSubUnits()).Distinct().Count() != units.Sum(b=>b.GetAllSubUnits().Count()))
                throw new ArgumentException("同一个子单位不能属于多个命令元素");
            Faction = faction;
            Roster = Array.AsReadOnly(units);
            Orders = Array.AsReadOnly(assignments);
        }

        public Company Resolve(CombatElementKey key)
        {
            var battalion = Roster.Single(b => b.InstanceId == key.BattalionId);
            return battalion.Companies[key.CompanyIndex];
        }

        public Battalion Owner(CombatElementKey key) => Roster.Single(b => b.InstanceId == key.BattalionId);
    }
}
