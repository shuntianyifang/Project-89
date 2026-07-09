using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Systems.Combat;

namespace ColdWarWargame.Models
{
    public class Battalion
    {
        // Deliberately set higher than gameplay thresholds as a numeric safety buffer.
        // Gameplay effects still collapse once Fatigue > 8.
        public const int FatigueOverflowCap = 20;

        public string InstanceId { get; set; }
        public string Name { get; set; }
        public int Faction { get; set; } // 0: Neutral, 1: Blue, 2: Red
        
        // 战役属性
        public float CurrentAP { get; set; }
        public int Fatigue { get; set; }
        /// <summary>断联回合数（PRD §2.5.3）</summary>
        public int TurnsOOS { get; set; } = 0;
        /// <summary>编制模板中写死的营种类: main/support/artillery</summary>
        public string TemplateRole { get; set; } = "main";
        /// <summary>编制模板ID（用于规则判定与调试）</summary>
        public string TemplateId { get; set; } = string.Empty;
        /// <summary>是否为专业侦察营（PRD §2.7 Advanced Vision）</summary>
        public bool IsAdvancedReconBattalion { get; set; } = false;
        /// <summary>营级标签（如 Engineer, Heli_Battalion）</summary>
        public HashSet<string> BattalionTags { get; set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public List<Company> Companies { get; set; } = new List<Company>();

        // 展平获取全营所有子单位 (方便 LINQ 遍历)
        public IEnumerable<SubUnitInstance> GetAllSubUnits() =>
            Companies.SelectMany(c => c.Platoons).SelectMany(p => p.Units);

        // --- 核心算法：底层数据注入与聚合[cite: 3] ---
        
        // 1. 计算战斗效能比 (Combat Effectiveness, CE)[cite: 3]
        public float CalculateCE()
        {
            if (!GetAllSubUnits().Any()) return 0f;
            float maxCost = GetAllSubUnits().Sum(u => u.Cost);
            if (maxCost == 0) return 0f;
            
            float aliveCost = GetAllSubUnits().Where(u => u.SurvivalState == 1).Sum(u => u.Cost);
            return aliveCost / maxCost;
        }

        // 2. 获取组织度惩罚系数 (M_org)[cite: 3]
        public float GetOrganizationalDebuff()
        {
            float ce = CalculateCE();
            if (ce >= 0.8f) return 1.0f;
            if (ce >= 0.5f) return 0.8f;
            return 0.5f;
        }

        /// <summary>是否有存活的子单位（CE > 0）</summary>
        public bool HasSurvivingSubUnits => CalculateCE() > 0f;

        // 3. 计算面板攻击力：底层真实聚合 x 顶层组织度惩罚[cite: 3]
        public float GetActualAttack()
        {
            float baseAttack = GetAllSubUnits()
                .Where(u => u.SurvivalState == 1)
                .Sum(u => u.Template.CombatStats.Attack * u.SurvivalState); // 也可以直接乘以 1
            
            return (baseAttack * GetOrganizationalDebuff() * GetFatigueCombatMultiplier()) / 10f; // 聚合缩放常量 K=10[cite: 3]
        }
        
        // 计算面板防御力：与攻击力对称实现
        public float GetActualDefense()
        {
            float baseDef = GetAllSubUnits()
                .Where(u => u.SurvivalState == 1)
                .Sum(u => u.Template.CombatStats.Defense * u.SurvivalState);

            return (baseDef * GetOrganizationalDebuff() * GetFatigueCombatMultiplier()) / 10f; // 使用相同的聚合缩放常量 K=10
        }

        public int GetTotalCurrentHp()
        {
            return GetAllSubUnits().Sum(u => u.CurrentHp);
        }

        public int GetTotalMaxHp()
        {
            return GetAllSubUnits().Sum(u => u.Template.CombatStats.MaxHp);
        }
        
        // 4. 视野聚合规则[cite: 3]
        /// <summary>考虑疲劳度的最大 AP（PRD §2.5.1）</summary>
        public float GetFatigueCombatMultiplier() { if (Fatigue >= 7) return 0.5f; if (Fatigue >= 5) return 0.9f; return 1.0f; }
        public float GetMaxAP()
        {
            float baseAP = 12f;
            if (Fatigue >= 7) return Fatigue > 8 ? 0f : baseAP * 0.5f;
            if (Fatigue >= 5) return baseAP * 0.8f;
            return baseAP;
        }

        public (int range, string reason) GetVisionRuleInfo()
        {
            var aliveUnits = GetAllSubUnits().Where(u => u.SurvivalState == 1).ToList();
            if (aliveUnits.Count == 0) return (0, "全灭");

            // 专业侦察营：Advanced Vision = 12
            if (IsAdvancedReconBattalion) return (12, "专业侦察营");

            // 如果包含存活的 Recon 标签，视野为 8，否则瞎子基线为 6[cite: 3]
            if (aliveUnits.Any(u => u.HasCapability("Recon"))) return (8, "建制内侦察激活");

            return (6, "瞎子基线");
        }

        public int CalculateVisionRange() => GetVisionRuleInfo().range;

        /// <summary>营战斗角色：主力(可填主战插槽)、辅助、炮兵</summary>
        public enum BattalionRole { Main, Support, Artillery }
        public BattalionRole GetRole()
        {
            return TemplateRole switch { "support" => BattalionRole.Support, "artillery" => BattalionRole.Artillery, _ => BattalionRole.Main };
        }

        /// <summary>获取营内所有炮兵单位的最大支援半径。非炮兵类营（没有）和无炮兵的炮兵类营（打光了是这样的）则返回0。</summary>
        public int GetArtilleryRange()
        {
            //这不大规范，但是没时间优雅修bug了
            if (GetRole() != BattalionRole.Artillery)
                return 0;
            return GetAllSubUnits()
                .Where(u => u.SurvivalState == 1)
                .Select(u => u.Template.ArtyArea ?? 0)
                .DefaultIfEmpty(0)
                .Max();
        }

        public bool CanFillMain() => GetRole() == BattalionRole.Main;
        public bool CanFillSupport() => GetRole() == BattalionRole.Support;
        public bool CanFillArtillery() => GetRole() == BattalionRole.Artillery;

        public bool HasBattalionTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || BattalionTags == null)
                return false;
            return BattalionTags.Contains(tag);
        }
    }
}
