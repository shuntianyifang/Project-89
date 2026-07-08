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
using ColdWarWargame.UI;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSessionController
    {
        private readonly global::GameManager _owner;
        private readonly FuldaGapScenario _scenario;
        private readonly TurnManager _turnMgr;
        private readonly Grid3DRenderer _renderer;
        private readonly Label _infoLabel;
        private readonly Label _statusLabel;
        private readonly CanvasLayer _ui;
        private readonly Panel _tooltipPanel;
        private readonly Label _tooltipLabel;
        private readonly CombatResolver _resolver = new();

        private CombatDeploymentPanel _combatPanel;
        private CombatForce _attackerStored;
        private CombatForce _defenderStored;
        private bool _inCombat;
        private bool _isMoving;
        private SelectionState _sel;
        private Dictionary<Vector2I, float> _currentReachable = new();
        private Vector2 _lastMouseScreenPos;

        private static readonly string[] TerrainNames = { "平原", "森林", "半城镇", "城镇" };
        private static readonly string[] InfraNames = { "", "支线公路", "高速公路" };

        private sealed class SelectionState
        {
            public Battalion Unit;
            public Vector2I Pos;
        }

        public GameSessionController(
            global::GameManager owner,
            FuldaGapScenario scenario,
            TurnManager turnMgr,
            Grid3DRenderer renderer,
            Label infoLabel,
            Label statusLabel,
            CanvasLayer ui,
            Panel tooltipPanel,
            Label tooltipLabel)
        {
            _owner = owner;
            _scenario = scenario;
            _turnMgr = turnMgr;
            _renderer = renderer;
            _infoLabel = infoLabel;
            _statusLabel = statusLabel;
            _ui = ui;
            _tooltipPanel = tooltipPanel;
            _tooltipLabel = tooltipLabel;
        }

        public string GetStatusText() =>
            "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact") + " - " + _turnMgr.PhaseName();

        public void OnUnitClicked(int faction, Battalion bat, Vector2I pos)
        {
            if (_inCombat) return;
            if (faction == _turnMgr.CurrentFaction)
            {
                SelectUnit(bat, pos);
                return;
            }

            if (_sel == null) return;
            if (_inCombat) return;
            if (_sel.Unit.CurrentAP < 4f)
            {
                _infoLabel.Text = "AP too low, need 4";
                return;
            }

            int dx = Math.Abs(_sel.Pos.X - pos.X);
            int dy = Math.Abs(_sel.Pos.Y - pos.Y);
            if (Math.Max(dx, dy) > 2)
            {
                _infoLabel.Text = "Target too far, max 2";
                return;
            }

            StartCombat(bat, pos);
        }

        public void OnTileClicked(Vector2I pos)
        {
            if (_inCombat || _isMoving) return;
            if (_sel != null && _currentReachable != null && _currentReachable.ContainsKey(pos))
            {
                float cost = _currentReachable[pos];
                var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
                bool isEnemyZOC(Vector2I t) => enemyZOC.Contains(t);
                bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _sel.Unit);
                var path = _scenario.Movement.FindPath(_sel.Pos, pos, _sel.Unit.CurrentAP, isEnemyZOC, occ);
                if (path == null || path.Count < 2)
                {
                    ClearSelection();
                    _infoLabel.Text = "Click to select";
                    return;
                }

                _isMoving = true;
                _renderer.ClearPath();
                _renderer.StartMoveAnimation(path, _sel.Unit);

                _renderer.OnMoveFinished = () =>
                {
                    _sel.Unit.CurrentAP = Math.Max(0, _sel.Unit.CurrentAP - cost);
                    _sel.Pos = pos;
                    for (int i = 0; i < _scenario.BlueBattalions.Count; i++)
                        if (_scenario.BlueBattalions[i].bat == _sel.Unit)
                            _scenario.BlueBattalions[i] = (_sel.Unit, pos);
                    for (int i = 0; i < _scenario.RedBattalions.Count; i++)
                        if (_scenario.RedBattalions[i].bat == _sel.Unit)
                            _scenario.RedBattalions[i] = (_sel.Unit, pos);
                    _renderer.SetBlueUnits(_scenario.BlueBattalions);
                    _renderer.SetRedUnits(_scenario.RedBattalions);

                    var enemyFaction3 = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                    var enemyPositions3 = (enemyFaction3 == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                    var enemyZOC3 = _scenario.ZOC.GetFactionZOC(enemyPositions3);
                    bool isEnemyZOC3(Vector2I t) => enemyZOC3.Contains(t);
                    bool occ3(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _sel.Unit);
                    _currentReachable = _scenario.Movement.GetReachableTiles(pos, _sel.Unit.CurrentAP, isEnemyZOC3, occ3);
                    _renderer.SetReachable(_currentReachable, _sel.Unit.CurrentAP);
                    _renderer.SetSel(pos);
                    UpdateArtilleryOverlay(_sel.Unit, pos);
                    _infoLabel.Text = "Moved to (" + pos.X + "," + pos.Y + ") AP=" + _sel.Unit.CurrentAP.ToString("0.0");
                    _isMoving = false;
                };
            }
            else
            {
                ClearSelection();
                _infoLabel.Text = "Click to select";
            }
        }

        public void OnRightClick()
        {
            if (_inCombat || _isMoving) return;
            ClearSelection();
            _infoLabel.Text = "Click to select";
        }

        public void OnHoverChanged(Vector2I? pos)
        {
            _renderer.ClearPath();

            if (pos == null)
            {
                _tooltipPanel.Visible = false;
                if (_sel != null)
                    _infoLabel.Text = "Selected: " + _sel.Unit.Name + " reachable " + _currentReachable.Count + " tiles";
                else
                    _infoLabel.Text = "Click to select";
                return;
            }

            var p = pos.Value;
            _infoLabel.Text = "坐标: (" + p.X + ", " + p.Y + ")";

            var tile = _scenario.Map.GetTile(p);
            string terrainName = TerrainNames[tile.TerrainType];
            string info = "地形: " + terrainName;

            if (tile.InfraType > 0)
                info += " (" + InfraNames[tile.InfraType] + ")";

            if (!tile.IsPassable)
            {
                info += " [不可通行]";
                if (_sel != null) info += " | 到达剩余AP: 0";
            }
            else
            {
                float moveCost = tile.GetMovementCost();
                if (!float.IsPositiveInfinity(moveCost))
                    info += " 消耗" + moveCost.ToString("0.0");

                if (_sel != null)
                {
                    if (p == _sel.Pos)
                        info += " | 当前所在";
                    else if (_currentReachable.TryGetValue(p, out float totalCost))
                    {
                        float remaining = _sel.Unit.CurrentAP - totalCost;
                        info += " | 到达剩余AP: " + remaining.ToString("0.0");
                    }
                    else
                        info += " | 到达剩余AP: 0";
                }
            }

            if (_sel != null && _currentReachable.ContainsKey(p))
            {
                var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
                bool isEnemyZOC(Vector2I t) => enemyZOC.Contains(t);
                bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _sel.Unit);
                var path = _scenario.Movement.FindPath(_sel.Pos, p, _sel.Unit.CurrentAP, isEnemyZOC, occ);
                if (path != null) _renderer.ShowPath(path);
            }

            _tooltipLabel.Text = info;
            _tooltipLabel.Size = new Vector2(504, 24);
            _tooltipPanel.Size = new Vector2(520, 32);

            Vector2 tipPos = _lastMouseScreenPos + new Vector2(20, 20);
            var vpSize = _owner.GetViewport().GetVisibleRect().Size;
            tipPos.X = Mathf.Clamp(tipPos.X, 0, vpSize.X - _tooltipPanel.Size.X);
            tipPos.Y = Mathf.Clamp(tipPos.Y, 0, vpSize.Y - _tooltipPanel.Size.Y);
            _tooltipPanel.Position = tipPos;
            _tooltipPanel.Visible = true;
        }

        public void OnEndTurn()
        {
            if (_inCombat || _isMoving) return;
            ClearSelection();
            _turnMgr.EndStrategicTurn();
            _renderer.SetBlueUnits(_scenario.BlueBattalions);
            _renderer.SetRedUnits(_scenario.RedBattalions);
            _statusLabel.Text = GetStatusText();
            _infoLabel.Text = "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact");
        }

        public void OnMouseMoved(Vector2 position) => _lastMouseScreenPos = position;

        public void HandleKeyboard(InputEventKey key)
        {
            if (key.Pressed && !key.Echo && key.Keycode == Key.Space && !_inCombat && !_isMoving)
                OnEndTurn();
        }

        private void SelectUnit(Battalion bat, Vector2I pos)
        {
            _sel = new SelectionState { Unit = bat, Pos = pos };
            _renderer.SetSel(pos);
            var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
            var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
            bool isEnemyZOC(Vector2I p) => enemyZOC.Contains(p);
            bool occ(Vector2I p) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == p && u.Item1 != bat);
            _currentReachable = _scenario.Movement.GetReachableTiles(pos, bat.CurrentAP, isEnemyZOC, occ);
            _renderer.SetReachable(_currentReachable, bat.CurrentAP);
            _infoLabel.Text = "Selected: " + bat.Name + " reachable " + _currentReachable.Count + " tiles";
            UpdateArtilleryOverlay(bat, pos);
        }

        private void StartCombat(Battalion defBat, Vector2I defPos)
        {
            _inCombat = true;

            var friendlyUnits = (_turnMgr.CurrentFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions)
                .Where(u => u.Item1 != _sel.Unit)
                .ToList();
            var eligible = EngagementResolver.GetEligibleUnits(defPos, friendlyUnits, 2);
            eligible = eligible.Where(u => u.bat.CurrentAP >= 4).ToList();
            eligible.Insert(0, (_sel.Unit, _sel.Pos));

            var artySupports = friendlyUnits
                .Where(u => u.bat.GetArtilleryRange() > 0
                    && (u.Item2.X - defPos.X) * (u.Item2.X - defPos.X) + (u.Item2.Y - defPos.Y) * (u.Item2.Y - defPos.Y) <= u.bat.GetArtilleryRange() * u.bat.GetArtilleryRange()
                    && u.bat.CurrentAP >= 4
                    && !eligible.Any(e => e.bat == u.bat))
                .ToList();
            eligible.InsertRange(0, artySupports);

            float terrainBonus = _scenario.Map.GetTile(defPos).TerrainType switch { 1 => 0.1f, 2 => 0.3f, 3 => 0.4f, _ => 0f };
            string[] terrainNames = { "Plains", "Forest", "Semi-Urban", "Urban" };
            int terrainType = _scenario.Map.GetTile(defPos).TerrainType;
            string tName = terrainType >= 0 && terrainType < terrainNames.Length ? terrainNames[terrainType] : "??";
            int tBonus = (int)(terrainBonus * 10);

            _combatPanel = new CombatDeploymentPanel();
            _ui.AddChild(_combatPanel);
            _combatPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _combatPanel.OnAttackerConfirmed = (CombatForce attackerForce) =>
            {
                _attackerStored = attackerForce;
                var defEligible = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)
                    .Where(u => u.Item1 != defBat)
                    .ToList();
                defEligible = defEligible.Where(u => Math.Max(Math.Abs(u.Item2.X - defPos.X), Math.Abs(u.Item2.Y - defPos.Y)) <= 2 && u.Item1.CurrentAP >= 4).ToList();
                var defArtySupports = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)
                    .Where(u => u.Item1 != defBat && u.Item1.GetArtilleryRange() > 0
                        && (u.Item2.X - defPos.X) * (u.Item2.X - defPos.X) + (u.Item2.Y - defPos.Y) * (u.Item2.Y - defPos.Y) <= u.Item1.GetArtilleryRange() * u.Item1.GetArtilleryRange()
                        && u.Item1.CurrentAP >= 4
                        && !defEligible.Any(e => e.Item1 == u.Item1))
                    .ToList();
                defEligible.AddRange(defArtySupports);
                _defenderStored = CombatAutoDeployer.AutoFillForce(defEligible, defBat);
                _combatPanel.RemoveContent();
                _combatPanel.ShowDefenderPreview(_defenderStored);
            };

            _combatPanel.OnResolvePressed = () =>
            {
                var ctx = new CombatContext
                {
                    DefenderTerrainBonus = terrainBonus,
                    AttackerOOSTurns = _sel.Unit.TurnsOOS,
                    DefenderOOSTurns = defBat.TurnsOOS
                };
                var result = _resolver.ResolveCombat(
                    _attackerStored.GetAllBattalions(),
                    _defenderStored.GetAllBattalions(),
                    ctx);
                foreach (var b in _attackerStored.GetAllBattalions())
                {
                    b.Fatigue = Math.Min(10, b.Fatigue + result.AttackerFatigueGained);
                    b.CurrentAP = Math.Max(0, b.CurrentAP - 4);
                }
                foreach (var b in _defenderStored.GetAllBattalions())
                {
                    b.Fatigue = Math.Min(10, b.Fatigue + result.DefenderFatigueGained);
                    b.CurrentAP = Math.Max(0, b.CurrentAP - 4);
                }
                _combatPanel.RemoveContent();
                _combatPanel.ShowResult(result);
                ClearSelection();
            };

            _combatPanel.OnResultDismissed = () =>
            {
                if (_combatPanel != null)
                {
                    _combatPanel.Dismiss();
                    _combatPanel = null;
                }
                _inCombat = false;
                _renderer.SetBlueUnits(_scenario.BlueBattalions);
                _renderer.SetRedUnits(_scenario.RedBattalions);
                _infoLabel.Text = "Click to select";
            };

            _combatPanel.OnCancel = () =>
            {
                if (_combatPanel != null)
                {
                    _combatPanel.Dismiss();
                    _combatPanel = null;
                }
                ClearSelection();
                _inCombat = false;
                _infoLabel.Text = "Combat cancelled";
            };

            _combatPanel.ShowAttackerPhase(_sel.Unit, defBat, eligible, tBonus, tName);
        }

        private void ClearSelection()
        {
            _sel = null;
            _renderer.ClearSel();
            _currentReachable.Clear();
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
