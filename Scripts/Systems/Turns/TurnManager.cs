using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;
using Godot;

namespace ColdWarWargame.Systems.Turns
{
    /// <summary>
    /// 回合与战斗阶段状态机（PRD §2.4 / §4.1）
    /// 管理蓝/红双方交替行动、战斗部署流程。
    /// </summary>
    public class TurnManager
    {
        /// <summary>行动阶段枚举</summary>
        public enum GamePhase
        {
            StrategicMovement,       // 大地图机动
            CombatDeployment_Attacker,  // 进攻方部署
            CombatDeployment_Defender,  // 防御方部署
            CombatResolution          // 数值结算
        }

        // ------ 公开状态 ------
        public int CurrentFaction { get; private set; } = 1;  // 1=Blue, 2=Red
        public GamePhase CurrentPhase { get; private set; } = GamePhase.StrategicMovement;
        public int TurnNumber { get; private set; } = 1;

        // ------ 内部状态 ------
        private List<Battalion> _battalions = new();
        private Battalion _combatAttacker;
        private Battalion _combatDefender;
        private CombatContext _combatCtx;
        private int _combatStartFaction;
        private int _switchCount; // 累计阵营切换次数

        /// <summary>注册战场上的营（用于 AP 重置）</summary>
        public void RegisterBattalion(Battalion b) => _battalions.Add(b);

        /// <summary>结束当前阵营的机动阶段，切换为对方</summary>
        public void EndStrategicTurn()
        {
            if (CurrentPhase != GamePhase.StrategicMovement)
                throw new InvalidOperationException("EndStrategicTurn 只能在 StrategicMovement 阶段调用");

            CurrentFaction = CurrentFaction == 1 ? 2 : 1;
            _switchCount++;

            // 每 2 次切换（双方各一次）为完整一轮
            if (CurrentFaction == 1)
                TurnNumber++;

            CurrentPhase = GamePhase.StrategicMovement;
            ResetAP(CurrentFaction);
        }

        /// <summary>发起战斗：从 StrategicMovement 进入 CombatDeployment_Attacker</summary>
        public void InitiateCombat(Battalion attacker, Battalion defender, CombatContext ctx)
        {
            if (CurrentPhase != GamePhase.StrategicMovement)
                throw new InvalidOperationException("只能在 StrategicMovement 阶段发起战斗");

            if (attacker.Faction != CurrentFaction)
                throw new InvalidOperationException("只能使用当前阵营的单位发起进攻");

            if (defender.Faction == attacker.Faction)
                throw new InvalidOperationException("不能攻击己方单位");

            _combatAttacker = attacker;
            _combatDefender = defender;
            _combatCtx = ctx;
            _combatStartFaction = CurrentFaction;

            ctx.AttackerFaction = attacker.Faction;
            ctx.DefenderFaction = defender.Faction;

            CurrentPhase = GamePhase.CombatDeployment_Attacker;
        }

        /// <summary>进攻方完成部署 -> 切换为防御方部署阶段</summary>
        public void FinishAttackerDeployment()
        {
            if (CurrentPhase != GamePhase.CombatDeployment_Attacker)
                throw new InvalidOperationException("必须在 AttackerDeployment 阶段调用");

            CurrentPhase = GamePhase.CombatDeployment_Defender;
            CurrentFaction = _combatCtx.DefenderFaction;
        }

        /// <summary>防御方完成部署 -> 执行战斗结算 -> 回到战略机动阶段</summary>
        public CombatResolutionResult FinishDefenderDeployment(CombatResolver resolver)
        {
            if (CurrentPhase != GamePhase.CombatDeployment_Defender)
                throw new InvalidOperationException("必须在 DefenderDeployment 阶段调用");

            CurrentPhase = GamePhase.CombatResolution;

            var result = resolver.ResolveCombat(_combatAttacker, _combatDefender, _combatCtx);

            // 回到发起方继续战略机动
            CurrentFaction = _combatStartFaction;
            CurrentPhase = GamePhase.StrategicMovement;

            _combatAttacker = null;
            _combatDefender = null;
            _combatCtx = null;

            return result;
        }

        /// <summary>
        /// 双阶段部署由外部系统自行结算后，调用此方法切回发起方战略阶段。
        /// </summary>
        public void CompleteCombatResolution()
        {
            if (CurrentPhase != GamePhase.CombatDeployment_Defender && CurrentPhase != GamePhase.CombatResolution)
                throw new InvalidOperationException("必须在 DefenderDeployment/CombatResolution 阶段调用");

            CurrentFaction = _combatStartFaction;
            CurrentPhase = GamePhase.StrategicMovement;

            _combatAttacker = null;
            _combatDefender = null;
            _combatCtx = null;
        }

        /// <summary>
        /// 取消当前战斗部署流程，恢复到发起方战略阶段。
        /// </summary>
        public void CancelCombat()
        {
            if (CurrentPhase == GamePhase.StrategicMovement)
                return;

            CurrentFaction = _combatStartFaction;
            CurrentPhase = GamePhase.StrategicMovement;

            _combatAttacker = null;
            _combatDefender = null;
            _combatCtx = null;
        }

        /// <summary>为指定阵营所有单位重置 AP</summary>
        private void ResetAP(int faction)
        {
            foreach (var b in _battalions)
                if (b.Faction == faction)
                    b.CurrentAP = b.GetMaxAP();
        }

        // ---------- 便利方法 ----------
        public IEnumerable<Battalion> GetBattalionsByFaction(int faction) =>
            _battalions.Where(b => b.Faction == faction);

        public string PhaseName() => CurrentPhase switch
        {
            GamePhase.StrategicMovement => "战略机动",
            GamePhase.CombatDeployment_Attacker => "进攻方部署",
            GamePhase.CombatDeployment_Defender => "防御方部署",
            GamePhase.CombatResolution => "战斗结算",
            _ => "未知"
        };
    }
}
