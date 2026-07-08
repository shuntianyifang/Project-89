using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    /// <summary>
    /// 自动部署：贪心算法填充敌方插槽（PRD §2.3-4）
    /// </summary>
    public static class CombatAutoDeployer
    {
        public static CombatForce AutoFillForce(
            List<(Battalion bat, Vector2I pos)> eligibleUnits,
            Battalion primaryDefender)
        {
            var result = new CombatForce();
            var available = new List<Battalion>();
            if (primaryDefender != null) available.Add(primaryDefender);
            foreach (var e in eligibleUnits)
                if (e.bat != primaryDefender) available.Add(e.bat);
            if (available.Count == 0) return result;

            result.LeadBattalion = primaryDefender ?? available[0];
            var used = new HashSet<Battalion> { result.LeadBattalion };

            var mainPool = available.Where(b => !used.Contains(b) && b.CanFillMain()).OrderByDescending(b => b.GetActualAttack()).ToList();
            var second = mainPool.FirstOrDefault();
            if (second != null) { result.MainSlot2 = second; used.Add(second); }

            var supPool = available.Where(b => !used.Contains(b) && b.CanFillSupport()).ToList();
            var cmdB = supPool.FirstOrDefault(b => CombatUtils.HasAnyCapability(b, "Command"));
            var reconB = supPool.FirstOrDefault(b => CombatUtils.HasAnyCapability(b, "Recon"));
            var defB = supPool.OrderByDescending(b => b.GetActualDefense()).FirstOrDefault();
            Battalion support = cmdB ?? reconB ?? defB;
            if (support != null) { result.SupportSlot = support; used.Add(support); }

            var artyB = available.FirstOrDefault(b =>
                !used.Contains(b) && b.CanFillArtillery());
            if (artyB != null) { result.ArtillerySlot = artyB; used.Add(artyB); }

            return result;
        }
    }
}
