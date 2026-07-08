using System.Collections.Generic;

namespace ColdWarWargame.Systems.Gameplay
{
    public enum GameplayEventType
    {
        MatchStarted,
        UnitSelected,
        UnitDeselected,
        MovementStarted,
        MovementCompleted,
        CombatStarted,
        CombatResolved,
        CombatCancelled,
        PhaseFinished,
        TurnEnded,
        MatchPaused,
        MatchResumed,
        MatchWon,
        MatchLost,
        MatchDrawn
    }

    public sealed class GameplayEvent
    {
        public GameplayEvent(GameplayEventType type, object data = null)
        {
            Type = type;
            Data = data;
        }

        public GameplayEventType Type { get; }
        public object Data { get; }
    }

    public sealed class SelectionEventData
    {
        public SelectionEventData(ColdWarWargame.Models.Battalion unit, Godot.Vector2I position, Dictionary<Godot.Vector2I, float> reachableTiles = null)
        {
            Unit = unit;
            Position = position;
            ReachableTiles = reachableTiles;
        }

        public ColdWarWargame.Models.Battalion Unit { get; }
        public Godot.Vector2I Position { get; }
        public Dictionary<Godot.Vector2I, float> ReachableTiles { get; }
    }
}
