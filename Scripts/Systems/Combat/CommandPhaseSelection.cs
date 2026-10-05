using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    public sealed class CommandExperimentParameters
    {
        public string Version { get; init; }="command-experiment-v1";
        public int Phases { get; init; }=3;
        public float ReserveThreshold { get; init; }=.8f;
        public float APCost { get; init; }=4;
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(Version) || Phases<1 || Phases>100 || !float.IsFinite(ReserveThreshold) ||
                ReserveThreshold<0 || ReserveThreshold>1 || !float.IsFinite(APCost) || APCost<0 || APCost>12)
                throw new ArgumentException("实验参数无效");
        }
        public float StageLossRate(float wholeBattleRate)
        {
            Validate();
            if(!float.IsFinite(wholeBattleRate) || wholeBattleRate<0 || wholeBattleRate>1)
                throw new ArgumentException("CRT损失率无效");
            return 1-MathF.Pow(1-wholeBattleRate,1f/Phases);
        }
    }

    public sealed record CommandSelection(int Phase, IReadOnlyList<CompanyCombatOrder> Frontline,
        IReadOnlyList<CompanyCombatOrder> Support, IReadOnlyList<CompanyCombatOrder> Reserve,
        IReadOnlyList<CompanyCombatOrder> Relieved, string Reason);

    // Selection reads the state left by the previous phase, never the current phase outcome.
    public sealed class CommandPhaseSelection
    {
        private readonly CombatCommandPlan _plan;
        private readonly CommandExperimentParameters _parameters;
        private readonly HashSet<CombatElementKey> _relieved=new();
        private bool _reserveCommitted;
        private int _phase;
        public CommandPhaseSelection(CombatCommandPlan plan,CommandExperimentParameters parameters)
        {
            _plan=plan ?? throw new ArgumentNullException(nameof(plan));
            _parameters=parameters ?? throw new ArgumentNullException(nameof(parameters)); parameters.Validate();
        }
        private IEnumerable<SubUnitInstance> Units(CompanyCombatOrder order)=>_plan.Resolve(order.Element).Platoons.SelectMany(p=>p.Units);
        private bool Alive(CompanyCombatOrder order)=>Units(order).Any(u=>u.SurvivalState==1);
        public CommandSelection Next()
        {
            if(_phase>=_parameters.Phases) throw new InvalidOperationException("实验阶段已经结束");
            _phase++;
            var original=_plan.Orders.Where(o=>o.Assignment==CombatAssignment.Frontline).ToArray();
            var reserve=_plan.Orders.Where(o=>o.Assignment==CombatAssignment.Reserve && Alive(o)).ToArray();
            float maxCost=original.SelectMany(Units).Sum(u=>u.Cost);
            float aliveCost=original.SelectMany(Units).Where(u=>u.SurvivalState==1).Sum(u=>u.Cost);
            float ce=maxCost>0?aliveCost/maxCost:0;
            string reason="前沿按计划投入；预备等待接替条件";
            if(!_reserveCommitted && _phase>1 && ce<_parameters.ReserveThreshold && reserve.Length>0)
            {
                _reserveCommitted=true;
                foreach(var order in original) _relieved.Add(order.Element);
                reason=$"上一阶段前沿CE {ce:P0}低于{_parameters.ReserveThreshold:P0}，预备接替，原前沿转入后方";
            }
            else if(_reserveCommitted) reason="已投入预备继续执行前沿任务";
            var front=(_reserveCommitted?reserve:original.Where(Alive)).ToArray();
            var support=_plan.Orders.Where(o=>o.Assignment==CombatAssignment.Support && Alive(o)).ToArray();
            var relieved=original.Where(o=>_relieved.Contains(o.Element) && Alive(o)).ToArray();
            return new(_phase,Array.AsReadOnly(front),Array.AsReadOnly(support),
                Array.AsReadOnly(_reserveCommitted?Array.Empty<CompanyCombatOrder>():reserve),Array.AsReadOnly(relieved),reason);
        }
    }
}
