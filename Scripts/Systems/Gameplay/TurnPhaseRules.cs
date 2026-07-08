namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class TurnPhaseRules
    {
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Strategic;

        public void StartStrategicPhase()
        {
            CurrentPhase = TurnPhase.Strategic;
        }

        public void StartCombatPhase()
        {
            CurrentPhase = TurnPhase.Combat;
        }

        public void StartResolutionPhase()
        {
            CurrentPhase = TurnPhase.Resolution;
        }

        public void FinishPhase()
        {
            CurrentPhase = TurnPhase.Strategic;
        }

        public bool CanIssueOrders() => CurrentPhase == TurnPhase.Strategic;
        public bool IsCombatPhase() => CurrentPhase == TurnPhase.Combat;
        public bool IsResolutionPhase() => CurrentPhase == TurnPhase.Resolution;
    }

    public enum TurnPhase
    {
        Strategic,
        Combat,
        Resolution
    }
}
