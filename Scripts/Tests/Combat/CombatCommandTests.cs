using System;
using System.Linq;
using Godot;
using ColdWarWargame.Factories;
using ColdWarWargame.Systems.Combat;

namespace ColdWarWargame.Tests.Combat
{
    public static class CombatCommandTests
    {
        public static int RunAll()
        {
            int fails=0;
            void Check(bool ok,string message) {
                if(ok) GD.Print("[COMMAND PASS] "+message);
                else { fails++; GD.PrintErr("[COMMAND FAIL] "+message); }
            }
            var a=BattalionFactory.CreateFullBattalion("command-a","fg_cav_squadron",1);
            var d=BattalionFactory.CreateFullBattalion("command-d","fg_tank_bn",2);
            a.Fatigue=5; a.CurrentAP=8; d.TurnsOOS=1;
            var order=a.Companies.Select((c,i)=>new CompanyCombatOrder(new(a.InstanceId,i),CombatAssignment.Frontline)).ToArray();
            var plan=new CombatCommandPlan(1,new[]{a},order);
            Check(ReferenceEquals(plan.Resolve(order[0].Element),a.Companies[0]),"Orders reference original company roster, not duplicated troops");
            bool duplicate=false,missing=false,wrongFaction=false;
            try { new CombatCommandPlan(1,new[]{a},order.Concat(new[]{order[0]})); } catch(ArgumentException) { duplicate=true; }
            try { new CombatCommandPlan(1,new[]{a},order.Skip(1)); } catch(ArgumentException) { missing=true; }
            try { new CombatCommandPlan(1,new[]{d},Array.Empty<CompanyCombatOrder>()); } catch(ArgumentException) { wrongFaction=true; }
            Check(duplicate&&missing&&wrongFaction,"Duplicate, missing and wrong-faction orders are rejected");
            var parameters=new CommandExperimentParameters();
            float stage=parameters.StageLossRate(.2f);
            Check(Math.Abs(1-MathF.Pow(1-stage,parameters.Phases)-.2f)<.00001f,
                "Duration-normalized stages do not apply a full 20 percent CRT loss each time");
            var reserveOrders=order.Select((o,i)=>o with { Assignment=i==0?CombatAssignment.Frontline:CombatAssignment.Reserve }).ToArray();
            var reservePlan=new CombatCommandPlan(1,new[]{a},reserveOrders);
            var selector=new CommandPhaseSelection(reservePlan,parameters);
            var phase1=selector.Next();
            var input1=CommandPhaseReport.Capture(reservePlan,phase1);
            float firstAttack=a.Companies[0].Platoons.SelectMany(p=>p.Units).Where(u=>u.SurvivalState==1)
                .Sum(u=>u.Template.CombatStats.Attack)*a.GetOrganizationalDebuff()*a.GetFatigueCombatMultiplier()/10f;
            Check(input1.Contributions.Count==1 && Math.Abs(input1.Attack-firstAttack)<.0001f,
                "Phase report aggregates only committed companies with original owner organization and fatigue");
            Check(phase1.Frontline.Count==1 && phase1.Reserve.Count==a.Companies.Count-1,"First phase excludes waiting reserves from direct fire");
            var firstUnits=a.Companies[0].Platoons.SelectMany(p=>p.Units).ToArray();
            int[] before=firstUnits.Select(u=>u.CurrentHp).ToArray();
            foreach(var unit in firstUnits) unit.CurrentHp=0;
            var phase2=selector.Next();
            var input2=CommandPhaseReport.Capture(reservePlan,phase2);
            Check(input2.Contributions.All(c=>c.Assignment==CombatAssignment.Frontline) &&
                input2.Contributions.All(c=>c.Element.CompanyIndex!=0) && input1.Contributions[0].LivingUnits>0,
                "Committed reserves become frontline; previous phase report remains a value snapshot");
            Check(phase2.Frontline.Count==a.Companies.Count-1 && phase2.Reserve.Count==0 && phase2.Reason.Contains("上一阶段"),
                "Reserve commitment reads the previous phase state and replaces the original frontline");
            for(int i=0;i<firstUnits.Length;i++) firstUnits[i].CurrentHp=before[i];
            var allInput=CommandPhaseReport.Capture(plan,new CommandPhaseSelection(plan,parameters).Next());
            Check(Math.Abs(allInput.Attack-a.GetActualAttack())<.0001f && Math.Abs(allInput.Defense-a.GetActualDefense())<.0001f,
                "All-company input equals existing effective power without adding a battalion-count bonus");
            var withheldPlan=new CombatCommandPlan(1,new[]{a},order.Select(o=>o with { Assignment=CombatAssignment.HoldOut }));
            var withheldInput=CommandPhaseReport.Capture(withheldPlan,new CommandPhaseSelection(withheldPlan,parameters).Next());
            Check(withheldInput.Contributions.Count==0 && withheldInput.Attack==0 && withheldInput.Defense==0,
                "Withheld companies contribute no fire or capability entries to the input audit");
            var ctx=new CombatContext { DefenderTerrainBonus=.4f, AttackerFaction=1, DefenderFaction=2 };
            var aHp=a.GetAllSubUnits().Select(u=>u.CurrentHp).ToArray(); var dHp=d.GetAllSubUnits().Select(u=>u.CurrentHp).ToArray();
            var baseline=LegacyCombatComparison.Evaluate(new(){LeadBattalion=a},new(){LeadBattalion=d},ctx,42);
            Check(aHp.SequenceEqual(a.GetAllSubUnits().Select(u=>u.CurrentHp)) && dHp.SequenceEqual(d.GetAllSubUnits().Select(u=>u.CurrentHp)) &&
                a.CurrentAP==8 && a.Fatigue==5 && d.TurnsOOS==1,"Legacy comparison leaves live HP, AP, fatigue and supply unchanged");
            Check(baseline.Result.AttackerCasualties.All(c=>!a.GetAllSubUnits().Contains(c.Unit)) &&
                baseline.Result.DefenderCasualties.All(c=>!d.GetAllSubUnits().Contains(c.Unit)),"Legacy casualty records belong only to snapshot copies");
            var repeat=LegacyCombatComparison.Evaluate(new(){LeadBattalion=a},new(){LeadBattalion=d},ctx,42);
            Check(baseline.Summary==repeat.Summary && baseline.Result.AttackerCasualties.Select(c=>c.HpLost).SequenceEqual(
                repeat.Result.AttackerCasualties.Select(c=>c.HpLost)),"Fixed-seed comparison is repeatable and documents its assumptions");
            return fails;
        }
    }
}
