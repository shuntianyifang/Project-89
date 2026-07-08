using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Supply;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Systems.Victory;

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
        private readonly GameplayEventHub _eventHub = new();
        private readonly TurnFlowController _turnFlow;
        private readonly SupplyManager _supplyManager = new();
        private readonly VictoryTracker _victoryTracker = new();
        private readonly FrontlineResolver _frontlineResolver = new();
        private readonly VisionResolver _visionResolver = new();

        private Vector2 _lastMouseScreenPos;
        private SupplyOverlayDisplayMode _supplyOverlayMode = SupplyOverlayDisplayMode.Off;

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
            _rules.Bind(_eventHub);
            _turnFlow = new TurnFlowController(_eventHub);
            _turnFlow.StartMatch();
            _combatFlow = new CombatFlowController(
                hud.Canvas,
                hud,
                renderer,
                scenario,
                turnMgr,
                _resolver);
            RefreshPresentationByVision();
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
                _eventHub.Publish(new GameplayEvent(GameplayEventType.MovementStarted));
                _renderer.ClearPath();
                _renderer.StartMoveAnimation(path, _flow.CurrentSelection.Unit);

                _renderer.OnMoveFinished = () =>
                {
                    float remainingAp = _flow.CurrentSelection.Unit.CurrentAP - cost;
                    _flow.CurrentSelection.Unit.CurrentAP = Math.Max(0f, (float)Math.Round(remainingAp, 1));
                    _flow.CurrentSelection.Pos = pos;
                    for (int i = 0; i < _scenario.BlueBattalions.Count; i++)
                        if (_scenario.BlueBattalions[i].bat == _flow.CurrentSelection.Unit)
                            _scenario.BlueBattalions[i] = (_flow.CurrentSelection.Unit, pos);
                    for (int i = 0; i < _scenario.RedBattalions.Count; i++)
                        if (_scenario.RedBattalions[i].bat == _flow.CurrentSelection.Unit)
                            _scenario.RedBattalions[i] = (_flow.CurrentSelection.Unit, pos);

                    HashSet<Vector2I> blueEntered = null;
                    HashSet<Vector2I> redEntered = null;
                    HashSet<Vector2I> bluePathZoc = null;
                    HashSet<Vector2I> redPathZoc = null;
                    var traversedTiles = path.Skip(1).ToHashSet();
                    if (_turnMgr.CurrentFaction == 1)
                    {
                        blueEntered = traversedTiles;
                        bluePathZoc = _scenario.ZOC.GetFactionZOC(traversedTiles);
                    }
                    else
                    {
                        redEntered = traversedTiles;
                        redPathZoc = _scenario.ZOC.GetFactionZOC(traversedTiles);
                    }

                    RefreshOccupationFromEntryAndZoc(blueEntered, redEntered, bluePathZoc, redPathZoc);
                    RefreshFrontline();
                    RefreshPresentationByVision();

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
                    _eventHub.Publish(new GameplayEvent(GameplayEventType.MovementCompleted));
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
                    _hud.SetInfoText(BuildSelectedUnitInfo(_flow.CurrentSelection.Unit, _flow.ReachableTiles.Count));
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
            int endingFaction = _turnMgr.CurrentFaction;
            ClearSelection();
            _flow.EndTurn();
            _turnFlow.EndTurn();
            ExecuteEndTurnSettlement(endingFaction);
            _turnMgr.EndStrategicTurn();
            RefreshPresentationByVision();
            _hud.SetStatusText(GetStatusText());
            _hud.SetInfoText("Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact"));
        }

        public void OnMouseMoved(Vector2 position) => _lastMouseScreenPos = position;

        public void HandleKeyboard(InputEventKey key)
        {
            if (!key.Pressed || key.Echo)
                return;

            if (key.Keycode == Key.Space && _rules.IsActionAllowed(_flow.CurrentState, GameAction.EndTurn))
            {
                OnEndTurn();
                return;
            }

            if (key.Keycode == Key.F6)
            {
                CycleSupplyOverlayMode();
            }
        }

        private void SelectUnit(Battalion bat, Vector2I pos)
        {
            _flow.EnterSelection(bat, pos, null);
            _eventHub.Publish(new GameplayEvent(GameplayEventType.UnitSelected, new SelectionEventData(bat, pos)));
            _renderer.SetSel(pos);
            var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
            var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
            bool isEnemyZOC(Vector2I p) => enemyZOC.Contains(p);
            bool occ(Vector2I p) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == p && u.Item1 != bat);
            var reachable = _scenario.Movement.GetReachableTiles(pos, bat.CurrentAP, isEnemyZOC, occ);
            _flow.EnterSelection(bat, pos, reachable);
            _eventHub.Publish(new GameplayEvent(GameplayEventType.UnitSelected, new SelectionEventData(bat, pos, reachable)));
            _renderer.SetReachable(reachable, bat.CurrentAP);
            _hud.SetInfoText(BuildSelectedUnitInfo(bat, reachable.Count));
            UpdateArtilleryOverlay(bat, pos);
        }

        private static string BuildSelectedUnitInfo(Battalion bat, int reachableCount)
        {
            var (visionRange, visionReason) = bat.GetVisionRuleInfo();
            return "Selected: " + bat.Name +
                   " reachable " + reachableCount + " tiles" +
                   " | Vision " + visionRange + " (" + visionReason + ")";
        }

        private void StartCombat(Battalion defBat, Vector2I defPos)
        {
            _flow.EnterCombat();
            _turnFlow.StartCombat();

            _combatFlow.StartCombat(
                _flow.CurrentSelection.Unit,
                defBat,
                defPos,
                (attackerForce, defenderForce, result) =>
                {
                    _flow.ExitCombat();
                    _turnFlow.ResolveCombat();
                    _victoryTracker.RecordCombatResult(result, _turnMgr.CurrentFaction);
                    RefreshOccupationFromEntryAndZoc();
                    RefreshFrontline();
                    ClearSelection();
                    RefreshPresentationByVision();
                },
                () =>
                {
                    ClearSelection();
                    _flow.ExitCombat();
                    _turnFlow.CancelCombat();
                    _hud.SetInfoText("Combat cancelled");
                },
                () =>
                {
                    _flow.ExitCombat();
                    _turnFlow.FinishPhase();
                    RefreshPresentationByVision();
                    _hud.SetInfoText("Click to select");
                });
        }

        private void ExecuteEndTurnSettlement(int endingFaction)
        {
            var enemyPositions = GetFactionUnits(endingFaction == 1 ? 2 : 1).Select(u => u.pos);
            var enemyOccupied = new HashSet<Vector2I>(enemyPositions);
            var enemyZoc = _scenario.ZOC.GetFactionZOC(enemyPositions);
            var (hubs, airports) = _scenario.GetSupplySpecialNodes();

            _supplyManager.UpdateFactionEndTurn(
                endingFaction,
                _scenario.Map,
                GetAllUnits(),
                enemyOccupied,
                enemyZoc,
                hubs,
                airports);

            RefreshOccupationFromEntryAndZoc();
            _scenario.SaveOccupationState(_scenario.GetOccupationMap());
            RefreshFrontline();
            _victoryTracker.ScoreControlVP();

            var assessment = _victoryTracker.Evaluate(_turnMgr.TurnNumber);
            _hud.SetInfoText("VP Blue:" + assessment.BlueVP + " Red:" + assessment.RedVP + " 结果:" + assessment.BlueLevel.DisplayName());
        }

        private void RefreshPresentationByVision()
        {
            var visible = _visionResolver.UpdateGlobalVision(_turnMgr.CurrentFaction, GetAllUnits());

            var blueVisible = _turnMgr.CurrentFaction == 1
                ? _scenario.BlueBattalions
                : _scenario.BlueBattalions.Where(u => visible.Contains(u.pos)).ToList();

            var redVisible = _turnMgr.CurrentFaction == 2
                ? _scenario.RedBattalions
                : _scenario.RedBattalions.Where(u => visible.Contains(u.pos)).ToList();

            _renderer.SetBlueUnits(blueVisible);
            _renderer.SetRedUnits(redVisible);
            _renderer.SetActiveFaction(_turnMgr.CurrentFaction);
            RefreshSupplyVisualization();
            RefreshFrontline();
        }

        private void RefreshSupplyVisualization()
        {
            var allUnits = GetAllUnits().ToList();
            var (hubs, airports) = _scenario.GetSupplySpecialNodes();

            float[,] blueSp = ComputeSupplyMapForFaction(1, allUnits, hubs, airports);
            float[,] redSp = ComputeSupplyMapForFaction(2, allUnits, hubs, airports);

            var blueOos = new HashSet<Vector2I>(_scenario.BlueBattalions
                .Where(u => blueSp[u.pos.X, u.pos.Y] <= 0f)
                .Select(u => u.pos));

            var redOos = new HashSet<Vector2I>(_scenario.RedBattalions
                .Where(u => redSp[u.pos.X, u.pos.Y] <= 0f)
                .Select(u => u.pos));

            _renderer.SetUnitSupplyStatus(blueOos, redOos);
            _renderer.SetSupplyOverlayData(blueSp, redSp, _supplyOverlayMode);
        }

        private float[,] ComputeSupplyMapForFaction(
            int faction,
            List<(Battalion bat, Vector2I pos)> allUnits,
            HashSet<Vector2I> hubs,
            HashSet<Vector2I> airports)
        {
            int enemyFaction = faction == 1 ? 2 : 1;
            var enemyUnits = allUnits.Where(u => u.bat.Faction == enemyFaction).ToList();
            var enemyOccupied = enemyUnits.Select(u => u.pos).ToHashSet();
            var enemyZoc = _scenario.ZOC.GetFactionZOC(enemyOccupied);

            return _supplyManager.ComputeFactionSupplySP(
                faction,
                _scenario.Map,
                allUnits,
                enemyOccupied,
                enemyZoc,
                hubs,
                airports);
        }

        private void CycleSupplyOverlayMode()
        {
            _supplyOverlayMode = _supplyOverlayMode switch
            {
                SupplyOverlayDisplayMode.Off => SupplyOverlayDisplayMode.Friendly,
                SupplyOverlayDisplayMode.Friendly => SupplyOverlayDisplayMode.Enemy,
                SupplyOverlayDisplayMode.Enemy => SupplyOverlayDisplayMode.Both,
                _ => SupplyOverlayDisplayMode.Off
            };

            _renderer.SetSupplyOverlayMode(_supplyOverlayMode);
            _hud.SetInfoText("Supply Overlay [F6]: " + DescribeSupplyOverlayMode(_supplyOverlayMode));
        }

        private string DescribeSupplyOverlayMode(SupplyOverlayDisplayMode mode)
        {
            return mode switch
            {
                SupplyOverlayDisplayMode.Off => "OFF",
                SupplyOverlayDisplayMode.Friendly => "Friendly",
                SupplyOverlayDisplayMode.Enemy => "Enemy",
                SupplyOverlayDisplayMode.Both => "Both",
                _ => "OFF"
            };
        }

        private void RefreshFrontline()
        {
            var chains = _frontlineResolver.ResolveFrontlineChains(_scenario.GetOccupationMap());
            _renderer.SetFrontlineChains(chains);
        }

        private void RefreshOccupationFromEntryAndZoc(
            IEnumerable<Vector2I> blueEnteredTiles = null,
            IEnumerable<Vector2I> redEnteredTiles = null,
            IEnumerable<Vector2I> bluePathZocTiles = null,
            IEnumerable<Vector2I> redPathZocTiles = null)
        {
            var bluePositions = new HashSet<Vector2I>(_scenario.BlueBattalions.Select(u => u.pos));
            var redPositions = new HashSet<Vector2I>(_scenario.RedBattalions.Select(u => u.pos));
            var updated = _victoryTracker.UpdateOccupationFromEntryAndZOC(
                _scenario.Map,
                _scenario.GetOccupationMap(),
                bluePositions,
                redPositions,
                _scenario.ZOC,
                blueEnteredTiles,
                redEnteredTiles,
                bluePathZocTiles,
                redPathZocTiles);
            _scenario.ApplyOccupationState(updated);
        }

        private IEnumerable<(Battalion bat, Vector2I pos)> GetAllUnits()
        {
            return _scenario.BlueBattalions.Concat(_scenario.RedBattalions);
        }

        private IEnumerable<(Battalion bat, Vector2I pos)> GetFactionUnits(int faction)
        {
            return faction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions;
        }

        private void ClearSelection()
        {
            _flow.ClearSelection();
            _renderer.ClearSel();
            _eventHub.Publish(new GameplayEvent(GameplayEventType.UnitDeselected));
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
