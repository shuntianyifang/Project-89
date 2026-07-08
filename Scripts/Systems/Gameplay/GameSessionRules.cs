using ColdWarWargame.Models;
using Godot;
using System.Collections.Generic;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSessionRules
    {
        private readonly TurnPhaseRules _turnPhaseRules = new();
        private readonly GameSituationRules _situationRules = new();
        private readonly List<GameplayEvent> _eventLog = new();

        public TurnPhase CurrentPhase => _turnPhaseRules.CurrentPhase;
        public GameSituation CurrentSituation => _situationRules.CurrentSituation;
        public IReadOnlyList<GameplayEvent> EventLog => _eventLog;

        public bool CanSelectUnit(GameFlowController.GameState state) => _turnPhaseRules.CanIssueOrders() && state is GameFlowController.GameState.Idle or GameFlowController.GameState.Selecting;

        public bool CanMove(GameFlowController.GameState state) => _turnPhaseRules.CanIssueOrders() && state == GameFlowController.GameState.Selecting;

        public bool CanEnterCombat(GameFlowController.GameState state) => _turnPhaseRules.CanIssueOrders() && state == GameFlowController.GameState.Selecting;

        public bool CanEndTurn(GameFlowController.GameState state) => _turnPhaseRules.CanIssueOrders() && state is GameFlowController.GameState.Idle or GameFlowController.GameState.Selecting;

        public bool RequiresSelection(GameFlowController.GameState state) => state is GameFlowController.GameState.Selecting or GameFlowController.GameState.Moving or GameFlowController.GameState.Combat;

        public string DescribeState(GameFlowController.GameState state) => state switch
        {
            GameFlowController.GameState.Idle => "等待指令",
            GameFlowController.GameState.Selecting => "已选中单位",
            GameFlowController.GameState.Moving => "单位移动中",
            GameFlowController.GameState.Combat => "战斗中",
            _ => "未知状态"
        };

        public bool IsActionAllowed(GameFlowController.GameState state, GameAction action) => action switch
        {
            GameAction.SelectUnit => CanSelectUnit(state),
            GameAction.MoveUnit => CanMove(state),
            GameAction.EnterCombat => CanEnterCombat(state),
            GameAction.EndTurn => CanEndTurn(state),
            _ => false
        };

        public void RaiseEvent(GameplayEvent gameplayEvent)
        {
            _eventLog.Add(gameplayEvent);

            switch (gameplayEvent.Type)
            {
                case GameplayEventType.MatchStarted:
                    StartMatch();
                    break;
                case GameplayEventType.UnitSelected:
                    break;
                case GameplayEventType.UnitDeselected:
                    break;
                case GameplayEventType.MovementStarted:
                    break;
                case GameplayEventType.MovementCompleted:
                    break;
                case GameplayEventType.CombatStarted:
                    BeginCombatPhase();
                    break;
                case GameplayEventType.CombatResolved:
                    BeginResolutionPhase();
                    break;
                case GameplayEventType.CombatCancelled:
                    FinishPhase();
                    break;
                case GameplayEventType.PhaseFinished:
                    FinishPhase();
                    break;
                case GameplayEventType.TurnEnded:
                    BeginStrategicPhase();
                    break;
                case GameplayEventType.MatchPaused:
                    PauseMatch();
                    break;
                case GameplayEventType.MatchResumed:
                    ResumeMatch();
                    break;
                case GameplayEventType.MatchWon:
                    DeclareVictory(true);
                    break;
                case GameplayEventType.MatchLost:
                    DeclareVictory(false);
                    break;
                case GameplayEventType.MatchDrawn:
                    DeclareDraw();
                    break;
            }
        }

        public void BeginStrategicPhase() => _turnPhaseRules.StartStrategicPhase();
        public void BeginCombatPhase() => _turnPhaseRules.StartCombatPhase();
        public void BeginResolutionPhase() => _turnPhaseRules.StartResolutionPhase();
        public void FinishPhase() => _turnPhaseRules.FinishPhase();
        public void StartMatch() => _situationRules.StartGame();
        public void PauseMatch() => _situationRules.Pause();
        public void ResumeMatch() => _situationRules.Resume();
        public void DeclareVictory(bool playerVictory) => _situationRules.DeclareVictory(playerVictory);
        public void DeclareDraw() => _situationRules.DeclareDraw();
    }

    public enum GameAction
    {
        SelectUnit,
        MoveUnit,
        EnterCombat,
        EndTurn
    }
}
