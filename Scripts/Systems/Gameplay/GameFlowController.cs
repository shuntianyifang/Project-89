using Godot;
using System.Collections.Generic;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameFlowController
    {
        private readonly Dictionary<Vector2I, float> _reachableTiles = new();

        public bool InCombat { get; private set; }
        public bool IsMoving { get; private set; }
        public SelectionState CurrentSelection { get; private set; }
        public bool HasSelection => CurrentSelection != null;
        public bool CanInteract => !InCombat && !IsMoving;
        public IReadOnlyDictionary<Vector2I, float> ReachableTiles => _reachableTiles;

        public void BeginCombat()
        {
            InCombat = true;
        }

        public void EndCombat()
        {
            InCombat = false;
        }

        public void BeginMovement()
        {
            IsMoving = true;
        }

        public void EndMovement()
        {
            IsMoving = false;
        }

        public void SetSelection(Battalion unit, Vector2I pos, Dictionary<Vector2I, float> reachable)
        {
            CurrentSelection = new SelectionState(unit, pos);
            _reachableTiles.Clear();
            if (reachable == null)
                return;

            foreach (var entry in reachable)
                _reachableTiles[entry.Key] = entry.Value;
        }

        public void ClearSelection()
        {
            CurrentSelection = null;
            _reachableTiles.Clear();
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
