using Godot;
using System.Collections.Generic;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameFlowController
    {
        private readonly Dictionary<Vector2I, float> _reachableTiles = new();

        public GameState CurrentState { get; private set; } = GameState.Idle;
        public bool InCombat => CurrentState == GameState.Combat;
        public bool IsMoving => CurrentState == GameState.Moving;
        public SelectionState CurrentSelection { get; private set; }
        public bool HasSelection => CurrentSelection != null;
        public bool CanInteract => CurrentState is GameState.Idle or GameState.Selecting;
        public IReadOnlyDictionary<Vector2I, float> ReachableTiles => _reachableTiles;

        public void StartTurn()
        {
            TransitionTo(GameState.Idle);
            CurrentSelection = null;
            _reachableTiles.Clear();
        }

        public void EnterSelection(Battalion unit, Vector2I pos, Dictionary<Vector2I, float> reachable)
        {
            CurrentSelection = new SelectionState(unit, pos);
            _reachableTiles.Clear();
            if (reachable != null)
            {
                foreach (var entry in reachable)
                    _reachableTiles[entry.Key] = entry.Value;
            }

            TransitionTo(GameState.Selecting);
        }

        public void EnterCombat()
        {
            if (CurrentState == GameState.Selecting)
                TransitionTo(GameState.Combat);
        }

        public void ExitCombat()
        {
            if (CurrentState == GameState.Combat)
                TransitionTo(HasSelection ? GameState.Selecting : GameState.Idle);
        }

        public void BeginMovement()
        {
            if (CurrentState == GameState.Selecting)
                TransitionTo(GameState.Moving);
        }

        public void CompleteMovement()
        {
            if (CurrentState == GameState.Moving)
                TransitionTo(HasSelection ? GameState.Selecting : GameState.Idle);
        }

        public void EndTurn()
        {
            CurrentSelection = null;
            _reachableTiles.Clear();
            TransitionTo(GameState.Idle);
        }

        public void ClearSelection()
        {
            CurrentSelection = null;
            _reachableTiles.Clear();
            TransitionTo(GameState.Idle);
        }

        private void TransitionTo(GameState nextState)
        {
            CurrentState = (CurrentState, nextState) switch
            {
                (GameState.Idle, GameState.Selecting) => GameState.Selecting,
                (GameState.Selecting, GameState.Moving) => GameState.Moving,
                (GameState.Moving, GameState.Selecting) => GameState.Selecting,
                (GameState.Selecting, GameState.Combat) => GameState.Combat,
                (GameState.Combat, GameState.Selecting) => GameState.Selecting,
                (GameState.Combat, GameState.Idle) => GameState.Idle,
                (GameState.Selecting, GameState.Idle) => GameState.Idle,
                (GameState.Moving, GameState.Idle) => GameState.Idle,
                (GameState.Idle, GameState.Idle) => GameState.Idle,
                _ => CurrentState
            };
        }

        public enum GameState
        {
            Idle,
            Selecting,
            Moving,
            Combat
        }

        public sealed class SelectionState
        {
            public SelectionState(Battalion unit, Vector2I pos)
            {
                Unit = unit;
                Pos = pos;
            }

            public Battalion Unit { get; set; }
            public Vector2I Pos { get; set; }
        }
    }
}
