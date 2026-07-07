using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Core.Entities;

namespace ColdWarWargame.Core.Combat
{
    public class CombatResolver
    {
        const float EPS = 1e-6f;

        public CombatResolutionResult ResolveCombat(Battalion attacker, Battalion defender, CombatContext ctx, ulong? randomSeed = null)
        {
            var advantage = ComputeAdvantage(attacker, defender, ctx);
            var (attackerRate, defenderRate) = GetCasualtyRates(advantage.Value);

            int attackerDamagePool = CalculateDamagePool(attacker, attackerRate);
            int defenderDamagePool = CalculateDamagePool(defender, defenderRate);

            var rng = CreateRandom(randomSeed);
            var attackerCasualties = ApplyDamagePool(attacker, attackerDamagePool, rng);
            var defenderCasualties = ApplyDamagePool(defender, defenderDamagePool, rng);

            return new CombatResolutionResult
            {
                Advantage = advantage,
                AttackerDamagePool = attackerDamagePool,
                DefenderDamagePool = defenderDamagePool,
                AttackerHpLost = attackerCasualties.Sum(c => c.HpLost),
                DefenderHpLost = defenderCasualties.Sum(c => c.HpLost),
                AttackerCasualties = attackerCasualties,
                DefenderCasualties = defenderCasualties
            };
        }

        System.Random CreateRandom(ulong? seed)
        {
            if (seed.HasValue)
            {
                return new System.Random((int)(seed.Value & 0x7FFFFFFF));
            }
            return new System.Random();
        }

        (float attackerRate, float defenderRate) GetCasualtyRates(float advantage)
        {
            if (advantage >= 1.5f) return (0.03f, 0.60f);
            if (advantage >= 1.0f) return (0.10f, 0.35f);
            if (advantage >= 0.5f) return (0.15f, 0.25f);
            if (advantage >= 0f) return (0.20f, 0.20f);
            if (advantage >= -0.5f) return (0.25f, 0.15f);
            if (advantage >= -1.0f) return (0.35f, 0.10f);
            return (0.60f, 0.03f);
        }

        int CalculateDamagePool(Battalion battalion, float casualtyRate)
        {
            if (battalion == null || casualtyRate <= 0f) return 0;
            int totalHp = battalion.GetTotalCurrentHp();
            return (int)Math.Round(totalHp * casualtyRate);
        }

        List<CasualtyRecord> ApplyDamagePool(Battalion battalion, int damagePool, System.Random rng)
        {
            var casualties = new List<CasualtyRecord>();
            if (battalion == null || damagePool <= 0) return casualties;

            var aliveUnits = battalion.GetAllSubUnits().Where(u => u.SurvivalState == 1).ToList();
            if (!aliveUnits.Any()) return casualties;

            var recordByUnit = new Dictionary<SubUnitInstance, CasualtyRecord>();

            while (damagePool > 0)
            {
                aliveUnits = battalion.GetAllSubUnits().Where(u => u.SurvivalState == 1).ToList();
                if (!aliveUnits.Any()) break;

                float totalWeight = aliveUnits.Sum(u => Math.Max(1, u.BaseWeight));
                double pick = rng.NextDouble() * totalWeight;
                float accumulator = 0f;
                SubUnitInstance target = aliveUnits.Last();

                foreach (var unit in aliveUnits)
                {
                    accumulator += Math.Max(1, unit.BaseWeight);
                    if (pick <= accumulator)
                    {
                        target = unit;
                        break;
                    }
                }

                int beforeHp = target.CurrentHp;
                target.CurrentHp = Math.Max(0, target.CurrentHp - 1);
                int hpLost = beforeHp - target.CurrentHp;
                if (hpLost <= 0) break;

                if (!recordByUnit.TryGetValue(target, out var entry))
                {
                    entry = new CasualtyRecord
                    {
                        Unit = target,
                        HpLost = 0,
                        IsDestroyed = false,
                        RemainingHp = target.CurrentHp
                    };
                    recordByUnit[target] = entry;
                    casualties.Add(entry);
                }

                entry.HpLost += hpLost;
                entry.IsDestroyed = target.CurrentHp == 0;
                entry.RemainingHp = target.CurrentHp;
                damagePool--;
            }

            return casualties;
        }

        public AdvantageResult ComputeAdvantage(Battalion attacker, Battalion defender, CombatContext ctx)
        {
            var res = new AdvantageResult();

            // 准备基础值
            float A_base = attacker?.GetActualAttack() ?? 0f;
            float D_base = defender?.GetActualDefense() ?? 0f;
            float A_org = attacker?.GetOrganizationalDebuff() ?? 1f;
            float D_org = defender?.GetOrganizationalDebuff() ?? 1f;

            float numer = A_base * A_org;
            float denom = D_base * D_org;
            float baseRatio = denom > EPS ? numer / denom : (numer > EPS ? 10f : 1f); // 防止除0
            float baseV = baseRatio - 1.0f;

            // 准备双方 modifier 列表（对称累加）
            var attackerMods = new List<ModifierEntry>();
            var defenderMods = new List<ModifierEntry>();

            // Armor/HeavyArmor rules (对称检查)
            EvaluateArmorRules(attacker, defender, attackerMods, defenderMods);

            // AntiTank vs HeavyArmor (累加)
            EvaluateAntiTankVsHeavyArmor(attacker, defender, attackerMods, defenderMods);

            // 指挥网络（对各自适用）
            if (!CombatUtils.HasCommandNetwork(attacker))
                attackerMods.Add(new ModifierEntry { Source = "CommandNetworkMissing", Value = -2.0f, Reason = "No Command units", Target = "attacker" });
            if (!CombatUtils.HasCommandNetwork(defender))
                defenderMods.Add(new ModifierEntry { Source = "CommandNetworkMissing", Value = -2.0f, Reason = "No Command units", Target = "defender" });

            // OOS（断联）惩罚（对称）
            ApplyOosPenalty(ctx?.AttackerOOSTurns ?? 0, "attacker", attackerMods);
            ApplyOosPenalty(ctx?.DefenderOOSTurns ?? 0, "defender", defenderMods);

            // 步兵缺失
            if (!attacker.GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u)))
                attackerMods.Add(new ModifierEntry { Source = "NoInfantry", Value = -1.0f, Reason = "No infantry present", Target = "attacker" });
            if (!defender.GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u)))
                defenderMods.Add(new ModifierEntry { Source = "NoInfantry", Value = -1.0f, Reason = "No infantry present", Target = "defender" });

            // 侦察缺失
            if (!CombatUtils.HasAnyCapability(attacker, "Recon"))
                attackerMods.Add(new ModifierEntry { Source = "NoRecon", Value = -1.0f, Reason = "No Recon units", Target = "attacker" });
            if (!CombatUtils.HasAnyCapability(defender, "Recon"))
                defenderMods.Add(new ModifierEntry { Source = "NoRecon", Value = -1.0f, Reason = "No Recon units", Target = "defender" });

            // 炮兵差异（参战子单元级别）
            bool aHasArt = attacker.GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));
            bool dHasArt = defender.GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));
            if (aHasArt && !dHasArt) defenderMods.Add(new ModifierEntry { Source = "NoArtilleryAgainstArtillery", Value = -1.0f, Reason = "Opponent has artillery", Target = "defender" });
            if (dHasArt && !aHasArt) attackerMods.Add(new ModifierEntry { Source = "NoArtilleryAgainstArtillery", Value = -1.0f, Reason = "Opponent has artillery", Target = "attacker" });

            // 空地对抗（直升机 vs 无防空）
            bool aHasHeli = CombatUtils.HasHeliDomain(attacker);
            bool dHasHeli = CombatUtils.HasHeliDomain(defender);
            bool aHasAA = CombatUtils.HasAnyAA(attacker);
            bool dHasAA = CombatUtils.HasAnyAA(defender);
            if (aHasHeli && !dHasAA) defenderMods.Add(new ModifierEntry { Source = "NoAAAgainstHeli", Value = -1.0f, Reason = "No AA vs Heli", Target = "defender" });
            if (dHasHeli && !aHasAA) attackerMods.Add(new ModifierEntry { Source = "NoAAAgainstHeli", Value = -1.0f, Reason = "No AA vs Heli", Target = "attacker" });

            // 其它：地形、ZOC、疲劳等（占位 - 可扩展）
            // 这里把 ctx.TerrainModifiers 简单加入到双方（示例：绝对值）
            if (ctx?.TerrainModifiers != null)
            {
                if (ctx.TerrainModifiers.TryGetValue("atk", out var atkMod))
                    attackerMods.Add(new ModifierEntry { Source = "TerrainAtk", Value = atkMod, Reason = "Terrain atk modifier", Target = "attacker" });
                if (ctx.TerrainModifiers.TryGetValue("def", out var defMod))
                    defenderMods.Add(new ModifierEntry { Source = "TerrainDef", Value = defMod, Reason = "Terrain def modifier", Target = "defender" });
            }

            // 汇总
            float attackerPenalty = attackerMods.Sum(m => m.Value);
            float defenderPenalty = defenderMods.Sum(m => m.Value);

            res.Value = baseV + attackerPenalty - defenderPenalty;

            // 合并所有 ModifierEntry 到返回列表
            res.Modifiers.AddRange(attackerMods);
            res.Modifiers.AddRange(defenderMods);

            // 钳位
            res.Value = Math.Clamp(res.Value, -10f, 10f);

            return res;
        }

        void EvaluateArmorRules(Battalion a, Battalion b, List<ModifierEntry> aMods, List<ModifierEntry> bMods)
        {
            // Symmetric checks: if party X has HeavyArmor/Armor and the opponent lacks the required anti capability, apply penalty to the opponent.
            // Check a -> b
            if (CombatUtils.HasAnyCapability(a, "HeavyArmor"))
            {
                bool bHasHeavyAnti = CombatUtils.HasAnyCapability(b, "HeavyAntiTank");
                bool bHasAnti = CombatUtils.HasAnyCapability(b, "AntiTank");
                if (!bHasHeavyAnti && !bHasAnti)
                {
                    bMods.Add(new ModifierEntry { Source = "HeavyArmorOverride", Value = -1.0f, Reason = "Opponent has HeavyArmor and you lack AntiTank", Target = "defender" });
                }
            }
            if (CombatUtils.HasAnyCapability(a, "Armor"))
            {
                if (!CombatUtils.HasAnyCapability(b, "LightAntiTank"))
                {
                    bMods.Add(new ModifierEntry { Source = "ArmorOverride", Value = -1.0f, Reason = "Opponent has Armor and you lack LightAntiTank", Target = "defender" });
                }
            }

            // Check b -> a (symmetric)
            if (CombatUtils.HasAnyCapability(b, "HeavyArmor"))
            {
                bool aHasHeavyAnti = CombatUtils.HasAnyCapability(a, "HeavyAntiTank");
                bool aHasAnti = CombatUtils.HasAnyCapability(a, "AntiTank");
                if (!aHasHeavyAnti && !aHasAnti)
                {
                    aMods.Add(new ModifierEntry { Source = "HeavyArmorOverride", Value = -1.0f, Reason = "Opponent has HeavyArmor and you lack AntiTank", Target = "attacker" });
                }
            }
            if (CombatUtils.HasAnyCapability(b, "Armor"))
            {
                if (!CombatUtils.HasAnyCapability(a, "LightAntiTank"))
                {
                    aMods.Add(new ModifierEntry { Source = "ArmorOverride", Value = -1.0f, Reason = "Opponent has Armor and you lack LightAntiTank", Target = "attacker" });
                }
            }
        }

        void EvaluateAntiTankVsHeavyArmor(Battalion a, Battalion b, List<ModifierEntry> aMods, List<ModifierEntry> bMods)
        {
            // If a only has AntiTank (no HeavyAntiTank/LightAntiTank) and b has HeavyArmor => a gets -0.5
            if (CombatUtils.HasAnyCapability(a, "AntiTank") && !CombatUtils.HasAnyCapability(a, "HeavyAntiTank") && !CombatUtils.HasAnyCapability(a, "LightAntiTank"))
            {
                if (CombatUtils.HasAnyCapability(b, "HeavyArmor"))
                {
                    aMods.Add(new ModifierEntry { Source = "AntiTankVsHeavyArmor", Value = -0.5f, Reason = "Limited AntiTank vs HeavyArmor", Target = "attacker" });
                }
            }

            // Do symmetric check
            if (CombatUtils.HasAnyCapability(b, "AntiTank") && !CombatUtils.HasAnyCapability(b, "HeavyAntiTank") && !CombatUtils.HasAnyCapability(b, "LightAntiTank"))
            {
                if (CombatUtils.HasAnyCapability(a, "HeavyArmor"))
                {
                    bMods.Add(new ModifierEntry { Source = "AntiTankVsHeavyArmor", Value = -0.5f, Reason = "Limited AntiTank vs HeavyArmor", Target = "defender" });
                }
            }
        }

        void ApplyOosPenalty(int oosTurns, string target, List<ModifierEntry> mods)
        {
            if (oosTurns <= 0) return;
            if (oosTurns == 1) mods.Add(new ModifierEntry { Source = "OOS_Turn1", Value = -0.5f, Reason = "Out of supply turn 1", Target = target });
            else mods.Add(new ModifierEntry { Source = "OOS_Turn2Plus", Value = -1.0f, Reason = "Out of supply >=2", Target = target });
        }
    }
}