using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    /// <summary>
    /// 战场战斗体：将多个营合并为一个战斗群。
    /// 用于插槽式战斗部署，合并所有子单位的攻防数据。
    /// </summary>
    public class CombatForce
    {
        public Battalion LeadBattalion { get; set; }
        public Battalion MainSlot2 { get; set; }
        public Battalion SupportSlot { get; set; }
        public Battalion ArtillerySlot { get; set; }

        public List<Battalion> GetAllBattalions()
        {
            var list = new List<Battalion>();
            if (LeadBattalion != null) list.Add(LeadBattalion);
            if (MainSlot2 != null) list.Add(MainSlot2);
            if (SupportSlot != null) list.Add(SupportSlot);
            if (ArtillerySlot != null) list.Add(ArtillerySlot);
            return list;
        }

        /// <summary>获取合并后的总子单位列表</summary>
        public List<SubUnitInstance> GetAllSubUnits() =>
            GetAllBattalions().SelectMany(b => b.GetAllSubUnits()).ToList();

        /// <summary>仅存活的子单位</summary>
        public List<SubUnitInstance> GetAliveSubUnits() =>
            GetAllSubUnits().Where(u => u.SurvivalState == 1).ToList();

        /// <summary>以 LeadBattalion 为主体的总攻击力 + 额外加成</summary>
        public float GetCombinedAttack()
        {
            float baseAtk = LeadBattalion?.GetActualAttack() ?? 0f;
            float bonus = 0f;
            if (MainSlot2 != null) bonus += MainSlot2.GetActualAttack() * 0.5f;
            if (SupportSlot != null) bonus += SupportSlot.GetActualAttack() * 0.3f;
            if (ArtillerySlot != null) bonus += ArtillerySlot.GetActualAttack() * 0.2f;
            return baseAtk + bonus;
        }

        /// <summary>以 LeadBattalion 为主体的总防御力 + 额外加成</summary>
        public float GetCombinedDefense()
        {
            float baseDef = LeadBattalion?.GetActualDefense() ?? 0f;
            float bonus = 0f;
            if (MainSlot2 != null) bonus += MainSlot2.GetActualDefense() * 0.5f;
            if (SupportSlot != null) bonus += SupportSlot.GetActualDefense() * 0.3f;
            if (ArtillerySlot != null) bonus += ArtillerySlot.GetActualDefense() * 0.2f;
            return baseDef + bonus;
        }

        public int GetTotalCurrentHp() => GetAllSubUnits().Sum(u => u.CurrentHp);

        /// <summary>简化模式：以 LeadBattalion 为基准的组织度惩罚</summary>
        public float GetOrganizationalDebuff() =>
            LeadBattalion?.GetOrganizationalDebuff() ?? 1f;

        /// <summary>合并检测能力标签（任一营有即算有）</summary>
        public bool HasAnyCapability(string cap) =>
            GetAllBattalions().Any(b => CombatUtils.HasAnyCapability(b, cap));

        public bool HasCommandNetwork() =>
            GetAllBattalions().Any(b => CombatUtils.HasCommandNetwork(b));

        public bool HasArtillery() =>
            GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));

        public bool HasHeliDomain() =>
            GetAllBattalions().Any(b => CombatUtils.HasHeliDomain(b));

        public bool HasAnyAA() =>
            GetAllBattalions().Any(b => CombatUtils.HasAnyAA(b));

        public bool HasInfantry() =>
            GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u));

        public int CountCapability(string cap) =>
            GetAllBattalions().Sum(b => CombatUtils.CountCapability(b, cap));
    }
}
