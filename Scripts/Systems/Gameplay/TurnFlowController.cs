namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class TurnFlowController
    {
        private readonly GameplayEventHub _eventHub;

        public TurnFlowController(GameplayEventHub eventHub)
        {
            _eventHub = eventHub;
        }

        public void StartMatch()
        {
            _eventHub.Publish(new GameplayEvent(GameplayEventType.MatchStarted));
            _eventHub.Publish(new GameplayEvent(GameplayEventType.TurnEnded));
        }

        public void EndTurn()
        {
            _eventHub.Publish(new GameplayEvent(GameplayEventType.TurnEnded));
        }

        public void StartCombat()
        {
            _eventHub.Publish(new GameplayEvent(GameplayEventType.CombatStarted));
        }

        public void ResolveCombat()
        {
            _eventHub.Publish(new GameplayEvent(GameplayEventType.CombatResolved));
        }

        public void CancelCombat()
        {
            _eventHub.Publish(new GameplayEvent(GameplayEventType.CombatCancelled));
        }

        public void FinishPhase()
        {
            _eventHub.Publish(new GameplayEvent(GameplayEventType.PhaseFinished));
        }
    }
}
