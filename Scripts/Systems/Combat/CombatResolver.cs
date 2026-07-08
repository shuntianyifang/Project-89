using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    public class CombatResolver
    {
        const float EPS = 1e-6f;

        /// <summary>
        /// 非破坏性预览：用于部署阶段实时估算战果，不会修改任何单位 HP。
        /// </summary>
        public CombatResolutionResult PreviewCombat(
            List<Battalion> attackerBattalions,
            List<Battalion> defenderBattalions,
            CombatContext ctx)
        {
            var leadA = attackerBattalions.FirstOrDefault();
            var leadD = defenderBattalions.FirstOrDefault();
            if (leadA == null || leadD == null)
                return new CombatResolutionResult { Advantage = new AdvantageResult { Value = 0 } };

            var advantage = ComputeAdvantage(attackerBattalions, defenderBattalions, ctx);
            float forceBonus = (attackerBattalions.Count - 1) * 0.15f -
                               (defenderBattalions.Count - 1) * 0.15f;
            advantage.Value = Math.Clamp(advantage.Value + forceBonus, -10f, 10f);

            var (atkRate, defRate, atkFat, defFat) = GetCasualtyRates(advantage.Value);
            int atkTotalHp = attackerBattalions.Sum(b => b.GetTotalCurrentHp());
            int defTotalHp = defenderBattalions.Sum(b => b.GetTotalCurrentHp());

            int atkHpLost = (int)Math.Round(atkTotalHp * atkRate);
            int defHpLost = (int)Math.Round(defTotalHp * defRate);

            return new CombatResolutionResult
            {
                Advantage = advantage,
                AttackerDamagePool = atkHpLost,
                DefenderDamagePool = defHpLost,
                AttackerHpLost = atkHpLost,
                DefenderHpLost = defHpLost,
                AttackerCasualties = new List<CasualtyRecord>(),
                DefenderCasualties = new List<CasualtyRecord>(),
                AttackerFatigueGained = atkFat,
                DefenderFatigueGained = defFat
            };
        }

        /// <summary>多营战斗结算：合并各营所有子单位进行统一结算（PRD 搂2.3插槽系统）</summary>
        public CombatResolutionResult ResolveCombat(
            List<Battalion> attackerBattalions,
            List<Battalion> defenderBattalions,
            CombatContext ctx,
            ulong? randomSeed = null)
        {
            var leadA = attackerBattalions.FirstOrDefault();
            var leadD = defenderBattalions.FirstOrDefault();
            if (leadA == null || leadD == null)
                return new CombatResolutionResult { Advantage = new AdvantageResult { Value = 0 } };
            var advantage = ComputeAdvantage(attackerBattalions, defenderBattalions, ctx);
            float forceBonus = (attackerBattalions.Count - 1) * 0.15f -
                               (defenderBattalions.Count - 1) * 0.15f;
            advantage.Value = Math.Clamp(advantage.Value + forceBonus, -10f, 10f);
            var (atkRate, defRate, atkFat, defFat) = GetCasualtyRates(advantage.Value);
            int atkTotalHp = attackerBattalions.Sum(b => b.GetTotalCurrentHp());
            int defTotalHp = defenderBattalions.Sum(b => b.GetTotalCurrentHp());
            int atkDmgPool = (int)Math.Round(atkTotalHp * atkRate);
            int defDmgPool = (int)Math.Round(defTotalHp * defRate);
            var atkAllUnits = attackerBattalions.SelectMany(b => b.GetAllSubUnits()).ToList();
            var defAllUnits = defenderBattalions.SelectMany(b => b.GetAllSubUnits()).ToList();
            var rng = CreateRandom(randomSeed);
            var atkCas = ApplyDamageToUnits(atkAllUnits, atkDmgPool, rng);
            var defCas = ApplyDamageToUnits(defAllUnits, defDmgPool, rng);
            return new CombatResolutionResult
            {
                Advantage = advantage,
                AttackerDamagePool = atkDmgPool,
                DefenderDamagePool = defDmgPool,
                AttackerHpLost = atkCas.Sum(c => c.HpLost),
                DefenderHpLost = defCas.Sum(c => c.HpLost),
                AttackerCasualties = atkCas,
                DefenderCasualties = defCas,
                AttackerFatigueGained = atkFat,
                DefenderFatigueGained = defFat
            };
        }

        /// <summary>在任意子单位列表上执行权重随机伤亡分摊</summary>
        List<CasualtyRecord> ApplyDamageToUnits(List<SubUnitInstance> allUnits, int damagePool, System.Random rng)
        {
            var casualties = new List<CasualtyRecord>();
            if (damagePool <= 0 || !allUnits.Any()) return casualties;
            var recordByUnit = new Dictionary<SubUnitInstance, CasualtyRecord>();
            while (damagePool > 0)
            {
                var alive = allUnits.Where(u => u.SurvivalState == 1).ToList();
                if (!alive.Any()) break;
                float totalW = alive.Sum(u => Math.Max(1, u.BaseWeight));
                double pick = rng.NextDouble() * totalW;
                float acc = 0f;
                SubUnitInstance target = alive.Last();
                foreach (var u in alive)
                {
                    acc += Math.Max(1, u.BaseWeight);
                    if (pick <= acc) { target = u; break; }
                }
                int beforeHp = target.CurrentHp;
                target.CurrentHp = Math.Max(0, target.CurrentHp - 1);
                int lost = beforeHp - target.CurrentHp;
                if (lost <= 0) break;
                if (!recordByUnit.TryGetValue(target, out var entry))
                {
                    entry = new CasualtyRecord { Unit = target, HpLost = 0, IsDestroyed = false, RemainingHp = target.CurrentHp };
                    recordByUnit[target] = entry;
                    casualties.Add(entry);
                }
                entry.HpLost += lost;
                entry.IsDestroyed = target.CurrentHp == 0;
                entry.RemainingHp = target.CurrentHp;
                damagePool--;
            }
            return casualties;
        }


        public CombatResolutionResult ResolveCombat(Battalion attacker, Battalion defender, CombatContext ctx, ulong? randomSeed = null)
        {
            var advantage = ComputeAdvantage(attacker, defender, ctx);
            var (attackerRate, defenderRate, atkFat, defFat) = GetCasualtyRates(advantage.Value);

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
                DefenderCasualties = defenderCasualties,
                AttackerFatigueGained = atkFat,
                DefenderFatigueGained = defFat
            };
        }

        System.Random CreateRandom(ulong? seed)
        {
            if (seed.HasValue)
                return new System.Random((int)(seed.Value & 0x7FFFFFFF));
            return new System.Random();
        }

        (float attackerRate, float defenderRate, int attackerFatigue, int defenderFatigue) GetCasualtyRates(float advantage)
        {
            if (advantage >= 1.5f) return (0.03f, 0.60f, 2, 5);
            if (advantage >= 1.0f) return (0.10f, 0.35f, 2, 4);
            if (advantage >= 0.5f) return (0.15f, 0.25f, 2, 3);
            if (advantage >= 0f) return (0.20f, 0.20f, 2, 1);
            if (advantage >= -0.5f) return (0.25f, 0.15f, 3, 1);
            if (advantage >= -1.0f) return (0.35f, 0.10f, 4, 1);
            return (0.60f, 0.03f, 5, 1);
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

            float attackerOosScale = GetOosCombatPowerScale(ctx?.AttackerOOSTurns ?? 0);
            float defenderOosScale = GetOosCombatPowerScale(ctx?.DefenderOOSTurns ?? 0);

            float A_base = (attacker?.GetActualAttack() ?? 0f) * attackerOosScale;
            float D_base = (defender?.GetActualDefense() ?? 0f) * defenderOosScale;
            float A_org = 1f;
            float D_org = 1f;

            float numer = A_base * A_org;
            float denom = D_base * D_org;
            float baseRatio = denom > EPS ? numer / denom : (numer > EPS ? 10f : 1f);
            float baseV = baseRatio - 1.0f;

            var attackerMods = new List<ModifierEntry>();
            var defenderMods = new List<ModifierEntry>();

            // Armor/HeavyArmor rules
            EvaluateArmorRules(attacker, defender, attackerMods, defenderMods);
            EvaluateAntiTankVsHeavyArmor(attacker, defender, attackerMods, defenderMods);

            // Command network
            if (!CombatUtils.HasCommandNetwork(attacker))
                attackerMods.Add(new ModifierEntry { Source = "CommandNetworkMissing", Value = -2.0f, Reason = "No Command units", Target = "attacker" });
            if (!CombatUtils.HasCommandNetwork(defender))
                defenderMods.Add(new ModifierEntry { Source = "CommandNetworkMissing", Value = -2.0f, Reason = "No Command units", Target = "defender" });

            // Infantry
            if (!attacker.GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u)))
                attackerMods.Add(new ModifierEntry { Source = "NoInfantry", Value = -1.0f, Reason = "No infantry present", Target = "attacker" });
            if (!defender.GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u)))
                defenderMods.Add(new ModifierEntry { Source = "NoInfantry", Value = -1.0f, Reason = "No infantry present", Target = "defender" });

            // Recon
            if (!CombatUtils.HasAnyCapability(attacker, "Recon"))
                attackerMods.Add(new ModifierEntry { Source = "NoRecon", Value = -1.0f, Reason = "No Recon units", Target = "attacker" });
            if (!CombatUtils.HasAnyCapability(defender, "Recon"))
                defenderMods.Add(new ModifierEntry { Source = "NoRecon", Value = -1.0f, Reason = "No Recon units", Target = "defender" });

            // Artillery difference
            bool aHasArt = attacker.GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));
            bool dHasArt = defender.GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));
            if (aHasArt && !dHasArt) defenderMods.Add(new ModifierEntry { Source = "NoArtilleryAgainstArtillery", Value = -1.0f, Reason = "Opponent has artillery", Target = "defender" });
            if (dHasArt && !aHasArt) attackerMods.Add(new ModifierEntry { Source = "NoArtilleryAgainstArtillery", Value = -1.0f, Reason = "Opponent has artillery", Target = "attacker" });

            // Heli vs AA
            bool aHasHeli = CombatUtils.HasHeliDomain(attacker);
            bool dHasHeli = CombatUtils.HasHeliDomain(defender);
            bool aHasAA = CombatUtils.HasAnyAA(attacker);
            bool dHasAA = CombatUtils.HasAnyAA(defender);
            if (aHasHeli && !dHasAA) defenderMods.Add(new ModifierEntry { Source = "NoAAAgainstHeli", Value = -1.0f, Reason = "No AA vs Heli", Target = "defender" });
            if (dHasHeli && !aHasAA) attackerMods.Add(new ModifierEntry { Source = "NoAAAgainstHeli", Value = -1.0f, Reason = "No AA vs Heli", Target = "attacker" });

            // Terrain: defender's position grants a defense advantage
            // T_atk = 0 for all terrain types, only T_def applies per Terrain_Combat_Effects.md
            if (ctx != null && ctx.DefenderTerrainBonus > 0f)
            {
                attackerMods.Add(new ModifierEntry { Source = "TerrainDefenderBonus", Value = -ctx.DefenderTerrainBonus, Reason = "Defender terrain +" + ctx.DefenderTerrainBonus, Target = "attacker" });
            }

            // Aggregate
            float attackerPenalty = attackerMods.Sum(m => m.Value);
            float defenderPenalty = defenderMods.Sum(m => m.Value);

            res.Value = baseV + attackerPenalty - defenderPenalty;

            res.Modifiers.AddRange(attackerMods);
            res.Modifiers.AddRange(defenderMods);

            res.Value = Math.Clamp(res.Value, -10f, 10f);

            return res;
        }

        AdvantageResult ComputeAdvantage(List<Battalion> attackerBattalions, List<Battalion> defenderBattalions, CombatContext ctx)
        {
            var res = new AdvantageResult();
            var leadA = attackerBattalions.FirstOrDefault();
            var leadD = defenderBattalions.FirstOrDefault();
            if (leadA == null || leadD == null)
            {
                res.Value = 0f;
                return res;
            }

            float A_base = 0f;
            for (int i = 0; i < attackerBattalions.Count; i++)
            {
                var bat = attackerBattalions[i];
                int oosTurns = GetPerBattalionOosTurns(ctx?.AttackerBattalionOOSTurns, i, ctx?.AttackerOOSTurns ?? 0);
                A_base += bat.GetActualAttack() * GetOosCombatPowerScale(oosTurns);
            }

            float D_base = 0f;
            for (int i = 0; i < defenderBattalions.Count; i++)
            {
                var bat = defenderBattalions[i];
                int oosTurns = GetPerBattalionOosTurns(ctx?.DefenderBattalionOOSTurns, i, ctx?.DefenderOOSTurns ?? 0);
                D_base += bat.GetActualDefense() * GetOosCombatPowerScale(oosTurns);
            }

            float numer = A_base;
            float denom = D_base;
            float baseRatio = denom > EPS ? numer / denom : (numer > EPS ? 10f : 1f);
            float baseV = baseRatio - 1.0f;

            var attackerMods = new List<ModifierEntry>();
            var defenderMods = new List<ModifierEntry>();

            EvaluateArmorRules(leadA, leadD, attackerMods, defenderMods);
            EvaluateAntiTankVsHeavyArmor(leadA, leadD, attackerMods, defenderMods);

            if (!CombatUtils.HasCommandNetwork(leadA))
                attackerMods.Add(new ModifierEntry { Source = "CommandNetworkMissing", Value = -2.0f, Reason = "No Command units", Target = "attacker" });
            if (!CombatUtils.HasCommandNetwork(leadD))
                defenderMods.Add(new ModifierEntry { Source = "CommandNetworkMissing", Value = -2.0f, Reason = "No Command units", Target = "defender" });

            if (!leadA.GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u)))
                attackerMods.Add(new ModifierEntry { Source = "NoInfantry", Value = -1.0f, Reason = "No infantry present", Target = "attacker" });
            if (!leadD.GetAllSubUnits().Any(u => CombatUtils.IsInfantry(u)))
                defenderMods.Add(new ModifierEntry { Source = "NoInfantry", Value = -1.0f, Reason = "No infantry present", Target = "defender" });

            if (!CombatUtils.HasAnyCapability(leadA, "Recon"))
                attackerMods.Add(new ModifierEntry { Source = "NoRecon", Value = -1.0f, Reason = "No Recon units", Target = "attacker" });
            if (!CombatUtils.HasAnyCapability(leadD, "Recon"))
                defenderMods.Add(new ModifierEntry { Source = "NoRecon", Value = -1.0f, Reason = "No Recon units", Target = "defender" });

            bool aHasArt = leadA.GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));
            bool dHasArt = leadD.GetAllSubUnits().Any(u => CombatUtils.IsArtillery(u));
            if (aHasArt && !dHasArt) defenderMods.Add(new ModifierEntry { Source = "NoArtilleryAgainstArtillery", Value = -1.0f, Reason = "Opponent has artillery", Target = "defender" });
            if (dHasArt && !aHasArt) attackerMods.Add(new ModifierEntry { Source = "NoArtilleryAgainstArtillery", Value = -1.0f, Reason = "Opponent has artillery", Target = "attacker" });

            bool aHasHeli = CombatUtils.HasHeliDomain(leadA);
            bool dHasHeli = CombatUtils.HasHeliDomain(leadD);
            bool aHasAA = CombatUtils.HasAnyAA(leadA);
            bool dHasAA = CombatUtils.HasAnyAA(leadD);
            if (aHasHeli && !dHasAA) defenderMods.Add(new ModifierEntry { Source = "NoAAAgainstHeli", Value = -1.0f, Reason = "No AA vs Heli", Target = "defender" });
            if (dHasHeli && !aHasAA) attackerMods.Add(new ModifierEntry { Source = "NoAAAgainstHeli", Value = -1.0f, Reason = "No AA vs Heli", Target = "attacker" });

            if (ctx != null && ctx.DefenderTerrainBonus > 0f)
            {
                attackerMods.Add(new ModifierEntry { Source = "TerrainDefenderBonus", Value = -ctx.DefenderTerrainBonus, Reason = "Defender terrain +" + ctx.DefenderTerrainBonus, Target = "attacker" });
            }

            float attackerPenalty = attackerMods.Sum(m => m.Value);
            float defenderPenalty = defenderMods.Sum(m => m.Value);

            res.Value = Math.Clamp(baseV + attackerPenalty - defenderPenalty, -10f, 10f);
            res.Modifiers.AddRange(attackerMods);
            res.Modifiers.AddRange(defenderMods);
            return res;
        }

        void EvaluateArmorRules(Battalion a, Battalion b, List<ModifierEntry> aMods, List<ModifierEntry> bMods)
        {
            if (CombatUtils.HasAnyCapability(a, "HeavyArmor"))
            {
                bool bHasHeavyAnti = CombatUtils.HasAnyCapability(b, "HeavyAntiTank");
                bool bHasAnti = CombatUtils.HasAnyCapability(b, "AntiTank");
                if (!bHasHeavyAnti && !bHasAnti)
                    bMods.Add(new ModifierEntry { Source = "HeavyArmorOverride", Value = -1.0f, Reason = "Opponent has HeavyArmor and you lack AntiTank", Target = "defender" });
            }
            if (CombatUtils.HasAnyCapability(a, "Armor"))
            {
                if (!CombatUtils.HasAnyCapability(b, "LightAntiTank"))
                    bMods.Add(new ModifierEntry { Source = "ArmorOverride", Value = -1.0f, Reason = "Opponent has Armor and you lack LightAntiTank", Target = "defender" });
            }

            if (CombatUtils.HasAnyCapability(b, "HeavyArmor"))
            {
                bool aHasHeavyAnti = CombatUtils.HasAnyCapability(a, "HeavyAntiTank");
                bool aHasAnti = CombatUtils.HasAnyCapability(a, "AntiTank");
                if (!aHasHeavyAnti && !aHasAnti)
                    aMods.Add(new ModifierEntry { Source = "HeavyArmorOverride", Value = -1.0f, Reason = "Opponent has HeavyArmor and you lack AntiTank", Target = "attacker" });
            }
            if (CombatUtils.HasAnyCapability(b, "Armor"))
            {
                if (!CombatUtils.HasAnyCapability(a, "LightAntiTank"))
                    aMods.Add(new ModifierEntry { Source = "ArmorOverride", Value = -1.0f, Reason = "Opponent has Armor and you lack LightAntiTank", Target = "attacker" });
            }
        }

        void EvaluateAntiTankVsHeavyArmor(Battalion a, Battalion b, List<ModifierEntry> aMods, List<ModifierEntry> bMods)
        {
            if (CombatUtils.HasAnyCapability(a, "AntiTank") && !CombatUtils.HasAnyCapability(a, "HeavyAntiTank") && !CombatUtils.HasAnyCapability(a, "LightAntiTank"))
            {
                if (CombatUtils.HasAnyCapability(b, "HeavyArmor"))
                    aMods.Add(new ModifierEntry { Source = "AntiTankVsHeavyArmor", Value = -0.5f, Reason = "Limited AntiTank vs HeavyArmor", Target = "attacker" });
            }

            if (CombatUtils.HasAnyCapability(b, "AntiTank") && !CombatUtils.HasAnyCapability(b, "HeavyAntiTank") && !CombatUtils.HasAnyCapability(b, "LightAntiTank"))
            {
                if (CombatUtils.HasAnyCapability(a, "HeavyArmor"))
                    bMods.Add(new ModifierEntry { Source = "AntiTankVsHeavyArmor", Value = -0.5f, Reason = "Limited AntiTank vs HeavyArmor", Target = "defender" });
            }
        }

        float GetOosCombatPowerScale(int oosTurns)
        {
            if (oosTurns <= 0) return 1.0f;
            if (oosTurns == 1) return 0.8f;
            return 0.5f;
        }

        int GetPerBattalionOosTurns(List<int> turnsByBattalion, int index, int fallbackTurns)
        {
            if (turnsByBattalion != null && index >= 0 && index < turnsByBattalion.Count)
                return turnsByBattalion[index];
            return fallbackTurns;
        }
    }
}
