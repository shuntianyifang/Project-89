using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    public sealed record LegacyComparisonResult(
        CombatResolutionResult Result, string[] AttackerNames, string[] DefenderNames, ulong Seed)
    {
        public string Summary => "旧系统独立对照（战前快照，不影响实际对局）\n" +
            "假设：所列完整营同时参战，使用旧攻防、CRT、伤亡与歼灭规则；不执行新命令。\n" +
            "进攻：" + string.Join("、",AttackerNames) + "\n防守：" + string.Join("、",DefenderNames) +
            $"\n优势 {Result.Advantage.Value:0.00}；HP损失 进攻 {Result.AttackerHpLost} / 防守 {Result.DefenderHpLost}" +
            $"\n疲劳增量 进攻 {Result.AttackerFatigueGained} / 防守 {Result.DefenderFatigueGained}；各参战营原规则消耗4 AP" +
            $"\n装备损失 进攻 {Result.AttackerCasualties.Count(c=>c.IsDestroyed && CombatUtils.IsVehicle(c.Unit))} / " +
            $"防守 {Result.DefenderCasualties.Count(c=>c.IsDestroyed && CombatUtils.IsVehicle(c.Unit))}；种子 {Seed}" +
            "\n差异同时包含投入名单、时间与规则变化，不能直接称为命令收益。";
    }

    public static class CombatStateCopy
    {
        public static Battalion Clone(Battalion b) => new()
        {
            InstanceId=b.InstanceId, Name=b.Name, Faction=b.Faction, TemplateId=b.TemplateId,
            TemplateRole=b.TemplateRole, CurrentAP=b.CurrentAP, Fatigue=b.Fatigue, TurnsOOS=b.TurnsOOS,
            WasOOSLastTurn=b.WasOOSLastTurn, ScenarioVisionRange=b.ScenarioVisionRange,
            IsAdvancedReconBattalion=b.IsAdvancedReconBattalion,
            BattalionTags=new HashSet<string>(b.BattalionTags,StringComparer.OrdinalIgnoreCase),
            Companies=b.Companies.Select(c=>new Company {
                CompanyId=c.CompanyId, Name=c.Name, Platoons=c.Platoons.Select(p=>new Platoon {
                    PlatoonId=p.PlatoonId, Type=p.Type, Units=p.Units.Select(u=>new SubUnitInstance(u.UnitId) {
                        NodeId=u.NodeId, Category=u.Category, CurrentHp=u.CurrentHp
                    }).ToList()
                }).ToList()
            }).ToList()
        };
    }

    public static class LegacyCombatComparison
    {
        public static LegacyComparisonResult Evaluate(CombatForce attacker, CombatForce defender, CombatContext context, ulong seed)
        {
            if(attacker==null || defender==null || context==null) throw new ArgumentNullException("旧对照输入为空");
            var a=attacker.GetAllBattalions(); var d=defender.GetAllBattalions();
            if(a.Count==0 || d.Count==0 || a.Concat(d).Distinct().Count()!=a.Count+d.Count)
                throw new ArgumentException("旧对照必须包含双方不同的参战单位");
            var copyContext=new CombatContext {
                DefenderTerrainBonus=context.DefenderTerrainBonus, AttackerFaction=context.AttackerFaction,
                DefenderFaction=context.DefenderFaction, AttackerOOSTurns=context.AttackerOOSTurns,
                DefenderOOSTurns=context.DefenderOOSTurns, RoundId=context.RoundId,
                AttackerBattalionOOSTurns=context.AttackerBattalionOOSTurns?.ToList(),
                DefenderBattalionOOSTurns=context.DefenderBattalionOOSTurns?.ToList()
            };
            var result=new CombatResolver().ResolveCombat(a.Select(CombatStateCopy.Clone).ToList(),
                d.Select(CombatStateCopy.Clone).ToList(),copyContext,seed);
            return new(result,a.Select(b=>b.Name).ToArray(),d.Select(b=>b.Name).ToArray(),seed);
        }
    }
}
