namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSituationRules
    {
        public GameSituation CurrentSituation { get; private set; } = GameSituation.InProgress;

        public void StartGame()
        {
            CurrentSituation = GameSituation.InProgress;
        }

        public void DeclareVictory(bool playerVictory)
        {
            CurrentSituation = playerVictory ? GameSituation.Victory : GameSituation.Defeat;
        }

        public void DeclareDraw()
        {
            CurrentSituation = GameSituation.Draw;
        }

        public void Pause()
        {
            CurrentSituation = GameSituation.Paused;
        }

        public void Resume()
        {
            CurrentSituation = GameSituation.InProgress;
        }

        public bool IsGameOver() => CurrentSituation is GameSituation.Victory or GameSituation.Defeat or GameSituation.Draw;
        public bool IsInProgress() => CurrentSituation == GameSituation.InProgress;
    }

    public enum GameSituation
    {
        InProgress,
        Paused,
        Victory,
        Defeat,
        Draw
    }
}
