using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Turns;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSessionController
    {
        private readonly global::GameManager _owner;
        private readonly FuldaGapScenario _scenario;
        private readonly TurnManager _turnMgr;
        private readonly Grid3DRenderer _renderer;
        private readonly GameHud _hud;
        private readonly CombatFlowController _combatFlow;
        private readonly CombatResolver _resolver = new();
        private readonly GameFlowController _flow;
        private readonly GameSessionRules _rules = new();

        private Vector2 _lastMouseScreenPos;

        private static readonly string[] TerrainNames = { "平原", "森林", "半城镇", "城镇" };
        private static readonly string[] InfraNames = { "", "支线公路", "高速公路" };

        public GameSessionController(
            global::GameManager owner,
            FuldaGapScenario scenario,
            TurnManager turnMgr,
            Grid3DRenderer renderer,
            GameHud hud)
        {
            _owner = owner;
            _scenario = scenario;
            _turnMgr = turnMgr;
            _renderer = renderer;
            _hud = hud;
            _flow = new GameFlowController();
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.MatchStarted));
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.TurnEnded));
            _combatFlow = new CombatFlowController(
                hud.Canvas,
                hud,
                renderer,
                scenario,
                turnMgr,
                _resolver);
        }

        public string GetStatusText() =>
            "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact") + " - " + _turnMgr.PhaseName();

        public void OnUnitClicked(int faction, Battalion bat, Vector2I pos)
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.SelectUnit)) return;
            if (faction == _turnMgr.CurrentFaction)
            {
                SelectUnit(bat, pos);
                return;
            }

            if (!_flow.HasSelection) return;
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.EnterCombat)) return;
            if (_flow.CurrentSelection.Unit.CurrentAP < 4f)
            {
                _hud.SetInfoText("AP too low, need 4");
                return;
            }

            int dx = Math.Abs(_flow.CurrentSelection.Pos.X - pos.X);
            int dy = Math.Abs(_flow.CurrentSelection.Pos.Y - pos.Y);
            if (Math.Max(dx, dy) > 2)
            {
                _hud.SetInfoText("Target too far, max 2");
                return;
            }

            StartCombat(bat, pos);
        }

        public void OnTileClicked(Vector2I pos)
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.MoveUnit)) return;
            if (_flow.HasSelection && _flow.ReachableTiles.ContainsKey(pos))
            {
                float cost = _flow.ReachableTiles[pos];
                var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
                bool isEnemyZOC(Vector2I t) => enemyZOC.Contains(t);
                bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _flow.CurrentSelection.Unit);
                var path = _scenario.Movement.FindPath(_flow.CurrentSelection.Pos, pos, _flow.CurrentSelection.Unit.CurrentAP, isEnemyZOC, occ);
                if (path == null || path.Count < 2)
                {
                    ClearSelection();
                    _hud.SetInfoText("Click to select");
                    return;
                }

                _flow.BeginMovement();
                _rules.RaiseEvent(new GameplayEvent(GameplayEventType.MovementStarted));
                _renderer.ClearPath();
                _renderer.StartMoveAnimation(path, _flow.CurrentSelection.Unit);

                _renderer.OnMoveFinished = () =>
                {
                    _flow.CurrentSelection.Unit.CurrentAP = Math.Max(0, _flow.CurrentSelection.Unit.CurrentAP - cost);
                    _flow.CurrentSelection.Pos = pos;
                    for (int i = 0; i < _scenario.BlueBattalions.Count; i++)
                        if (_scenario.BlueBattalions[i].bat == _flow.CurrentSelection.Unit)
                            _scenario.BlueBattalions[i] = (_flow.CurrentSelection.Unit, pos);
                    for (int i = 0; i < _scenario.RedBattalions.Count; i++)
                        if (_scenario.RedBattalions[i].bat == _flow.CurrentSelection.Unit)
                            _scenario.RedBattalions[i] = (_flow.CurrentSelection.Unit, pos);
                    _renderer.SetBlueUnits(_scenario.BlueBattalions);
                    _renderer.SetRedUnits(_scenario.RedBattalions);

                    var enemyFaction3 = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                    var enemyPositions3 = (enemyFaction3 == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                    var enemyZOC3 = _scenario.ZOC.GetFactionZOC(enemyPositions3);
                    bool isEnemyZOC3(Vector2I t) => enemyZOC3.Contains(t);
                    bool occ3(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _flow.CurrentSelection.Unit);
                    var reachable = _scenario.Movement.GetReachableTiles(pos, _flow.CurrentSelection.Unit.CurrentAP, isEnemyZOC3, occ3);
                    _flow.EnterSelection(_flow.CurrentSelection.Unit, pos, reachable);
                    _renderer.SetReachable(reachable, _flow.CurrentSelection.Unit.CurrentAP);
                    _renderer.SetSel(pos);
                    UpdateArtilleryOverlay(_flow.CurrentSelection.Unit, pos);
                    _hud.SetInfoText("Moved to (" + pos.X + "," + pos.Y + ") AP=" + _flow.CurrentSelection.Unit.CurrentAP.ToString("0.0"));
                    _flow.CompleteMovement();
                    _rules.RaiseEvent(new GameplayEvent(GameplayEventType.MovementCompleted));
                };
            }
            else
            {
                ClearSelection();
                _hud.SetInfoText("Click to select");
            }
        }

        public void OnRightClick()
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.SelectUnit)) return;
            ClearSelection();
            _hud.SetInfoText("Click to select");
        }

        public void OnHoverChanged(Vector2I? pos)
        {
            _renderer.ClearPath();

            if (pos == null)
            {
                _hud.SetTooltipVisible(false);
                if (_flow.HasSelection)
                    _hud.SetInfoText("Selected: " + _flow.CurrentSelection.Unit.Name + " reachable " + _flow.ReachableTiles.Count + " tiles");
                else
                    _hud.SetInfoText("Click to select");
                return;
            }

            var p = pos.Value;
            _hud.SetInfoText("坐标: (" + p.X + ", " + p.Y + ")");

            var tile = _scenario.Map.GetTile(p);
            string terrainName = TerrainNames[tile.TerrainType];
            string info = "地形: " + terrainName;

            if (tile.InfraType > 0)
                info += " (" + InfraNames[tile.InfraType] + ")";

            if (!tile.IsPassable)
            {
                info += " [不可通行]";
                if (_flow.HasSelection) info += " | 到达剩余AP: 0";
            }
            else
            {
                float moveCost = tile.GetMovementCost();
                if (!float.IsPositiveInfinity(moveCost))
                    info += " 消耗" + moveCost.ToString("0.0");

                if (_flow.HasSelection)
                {
                    if (p == _flow.CurrentSelection.Pos)
                        info += " | 当前所在";
                    else if (_flow.ReachableTiles.TryGetValue(p, out float totalCost))
                    {
                        float remaining = _flow.CurrentSelection.Unit.CurrentAP - totalCost;
                        info += " | 到达剩余AP: " + remaining.ToString("0.0");
                    }
                    else
                        info += " | 到达剩余AP: 0";
                }
            }

            if (_flow.HasSelection && _flow.ReachableTiles.ContainsKey(p))
            {
                var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
                bool isEnemyZOC(Vector2I t) => enemyZOC.Contains(t);
                bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _flow.CurrentSelection.Unit);
                var path = _scenario.Movement.FindPath(_flow.CurrentSelection.Pos, p, _flow.CurrentSelection.Unit.CurrentAP, isEnemyZOC, occ);
                if (path != null) _renderer.ShowPath(path);
            }

            _hud.SetTooltipText(info, _lastMouseScreenPos, _owner.GetViewport().GetVisibleRect().Size);
        }

        public void OnEndTurn()
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.EndTurn)) return;
            ClearSelection();
            _flow.EndTurn();
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.TurnEnded));
            _turnMgr.EndStrategicTurn();
            _renderer.SetBlueUnits(_scenario.BlueBattalions);
            _renderer.SetRedUnits(_scenario.RedBattalions);
            _hud.SetStatusText(GetStatusText());
            _hud.SetInfoText("Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact"));
        }

        public void OnMouseMoved(Vector2 position) => _lastMouseScreenPos = position;

        public void HandleKeyboard(InputEventKey key)
        {
            if (key.Pressed && !key.Echo && key.Keycode == Key.Space && _rules.IsActionAllowed(_flow.CurrentState, GameAction.EndTurn))
                OnEndTurn();
        }

        private void SelectUnit(Battalion bat, Vector2I pos)
        {
            _flow.EnterSelection(bat, pos, null);
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.UnitSelected, new SelectionEventData(bat, pos)));
            _renderer.SetSel(pos);
            var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
            var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
            bool isEnemyZOC(Vector2I p) => enemyZOC.Contains(p);
            bool occ(Vector2I p) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == p && u.Item1 != bat);
            var reachable = _scenario.Movement.GetReachableTiles(pos, bat.CurrentAP, isEnemyZOC, occ);
            _flow.EnterSelection(bat, pos, reachable);
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.UnitSelected, new SelectionEventData(bat, pos, reachable)));
            _renderer.SetReachable(reachable, bat.CurrentAP);
            _hud.SetInfoText("Selected: " + bat.Name + " reachable " + reachable.Count + " tiles");
            UpdateArtilleryOverlay(bat, pos);
        }

        private void StartCombat(Battalion defBat, Vector2I defPos)
        {
            _flow.EnterCombat();
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.CombatStarted));

            _combatFlow.StartCombat(
                _flow.CurrentSelection.Unit,
                defBat,
                defPos,
                (attackerForce, defenderForce, result) =>
                {
                    _flow.ExitCombat();
                    _rules.RaiseEvent(new GameplayEvent(GameplayEventType.CombatResolved));
                    ClearSelection();
                },
                () =>
                {
                    ClearSelection();
                    _flow.ExitCombat();
                    _rules.RaiseEvent(new GameplayEvent(GameplayEventType.CombatCancelled));
                    _hud.SetInfoText("Combat cancelled");
                },
                () =>
                {
                    _flow.ExitCombat();
                    _rules.RaiseEvent(new GameplayEvent(GameplayEventType.PhaseFinished));
                    _hud.SetInfoText("Click to select");
                });
        }

        private void ClearSelection()
        {
            _flow.ClearSelection();
            _renderer.ClearSel();
            _rules.RaiseEvent(new GameplayEvent(GameplayEventType.UnitDeselected));
        }

        private void UpdateArtilleryOverlay(Battalion unit, Vector2I pos)
        {
            int artyRange = unit.GetArtilleryRange();
            if (artyRange > 0)
            {
                var tiles = new HashSet<Vector2I>();
                int r2 = artyRange * artyRange;
                int inner2 = (artyRange - 1) * (artyRange - 1);
                for (int dx = -artyRange; dx <= artyRange; dx++)
                    for (int dy = -artyRange; dy <= artyRange; dy++)
                    {
                        int d2 = dx * dx + dy * dy;
                        if (d2 <= r2 && d2 >= inner2)
                        {
                            var p = new Vector2I(pos.X + dx, pos.Y + dy);
                            if (_scenario.Map.IsInBounds(p)) tiles.Add(p);
                        }
                    }
                _renderer.SetArtilleryRange(tiles);
            }
            else _renderer.ClearArtilleryRange();
        }
    }
}
