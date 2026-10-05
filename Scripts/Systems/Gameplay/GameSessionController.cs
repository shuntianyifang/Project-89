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
        private readonly GameDebugInspector _debug;
        private readonly VictoryTracker _victoryTracker = new();
        private readonly FrontlineResolver _frontlineResolver = new();
        private readonly VisionResolver _visionResolver = new();

        private Vector2 _lastMouseScreenPos;
        public CampaignResult Result { get; private set; }
        private float[,] _blueSupply;
        private float[,] _redSupply;

        public void InitializeCampaignPresentation()
        {
            RefreshOccupationFromEntryAndZoc();
            RefreshMissionPanel();
            CheckCampaignEnd();
        }

        public void OnSaveCampaign()
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.EndTurn))
            { _hud.SetInfoText("只能在进行中的战略阶段保存，请先完成移动或战斗"); return; }
            try
            {
                CampaignSave.Capture(_scenario, _turnMgr, _victoryTracker).Write();
                _hud.SetInfoText("对局已保存");
            }
            catch (Exception ex) { _hud.SetInfoText("保存失败：" + ex.Message); }
        }

        public void OnLoadCampaign()
        {
            if (_flow.IsMoving || _combatFlow.IsActive)
            { _hud.SetInfoText("请先完成移动或关闭战斗面板，再读取对局"); return; }
            try
            {
                CampaignSave.Read().Apply(_scenario, _turnMgr, _victoryTracker);
                Result = null;
                _rules.StartMatch(); _rules.FinishPhase(); _flow.StartTurn();
                ClearSelection();
                _hud.ResetCampaignResult();
                RefreshOccupationFromEntryAndZoc();
                RefreshPresentationByVision();
                RefreshMissionPanel();
                _hud.SetStatusText(GetStatusText());
                _hud.SetInfoText("已恢复保存的对局");
                RefreshCampaignCasualtyPanel();
                CheckCampaignEnd();
            }
            catch (Exception ex) { _hud.SetInfoText("读取失败：" + ex.Message); }
        }

        public void OnEndCampaign()
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.EndTurn))
            { if (Result == null) _hud.SetInfoText("请先完成移动或关闭战斗面板，再结束战役"); return; }
            FinishCampaign("双方手动结束战役");
        }

        private void FinishCampaign(string reason)
        {
            if (Result != null) return;
            RefreshOccupationFromEntryAndZoc();
            Result = new CampaignResult(_victoryTracker, _turnMgr.TurnNumber, reason,_scenario.Missions,_scenario.GetOccupationMap());
            _eventHub.Publish(new GameplayEvent(Result.EndEvent));
            ClearSelection();
            _hud.ShowCampaignResult(Result);
        }

        private void CheckCampaignEnd()
        {
            var exited=_scenario.Missions.State.Exits.Select(e=>e.Unit.Restore()).ToList();
            bool blueAlive = CampaignResult.HasLivingBattalions(_scenario.BlueBattalions.Select(u => u.bat).Concat(exited.Where(b=>b.Faction==1)));
            bool redAlive = CampaignResult.HasLivingBattalions(_scenario.RedBattalions.Select(u => u.bat).Concat(exited.Where(b=>b.Faction==2)));
            if (!blueAlive || !redAlive)
                FinishCampaign(!blueAlive && !redAlive ? "双方均无存活营" : !blueAlive ? "北约已无存活营" : "华约已无存活营");
        }
        private SupplyOverlayDisplayMode _supplyOverlayMode = SupplyOverlayDisplayMode.Off;
        public void OnExitSelected()
        {
            if(!_rules.IsActionAllowed(_flow.CurrentState,GameAction.EndTurn) || !_flow.HasSelection) { _hud.SetInfoText("请在战略阶段选择本方出口上的单位"); return; }
            var selected=_flow.CurrentSelection; var unit=selected.Unit;
            if(unit.Faction!=_turnMgr.CurrentFaction) return;
            RefreshOccupationFromEntryAndZoc(); RefreshSupplyVisualization();
            float supply=(unit.Faction==1?_blueSupply:_redSupply)[selected.Pos.X,selected.Pos.Y];
            var error=_scenario.Missions.TryExit(unit,selected.Pos,_turnMgr.TurnNumber,_scenario.Map,_scenario.GetOccupationMap(),supply);
            if(error!=null) { _hud.SetInfoText(error); return; }
            (unit.Faction==1?_scenario.BlueBattalions:_scenario.RedBattalions).RemoveAll(u=>u.bat==unit);
            _turnMgr.ReplaceBattalions(GetAllUnits().Select(u=>u.bat));
            ClearSelection(); RefreshOccupationFromEntryAndZoc(); RefreshPresentationByVision();
            RefreshMissionPanel(); CheckCampaignEnd();
            _hud.SetInfoText(unit.Name+(unit.Faction==1?"已完成有组织撤离":"已完成有效突破"));
        }
        public void ShowMissions()=>_hud.ShowMissions(_scenario.Missions.Summary(_scenario.GetOccupationMap()));
        private void RefreshMissionPanel()=>_hud.UpdateMissionPanel(_scenario.Missions,_scenario.GetOccupationMap(),_turnMgr.TurnNumber);
        private ControlOverlayDisplayMode _controlOverlayMode = ControlOverlayDisplayMode.Off;

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
            _debug = new GameDebugInspector(scenario, turnMgr, renderer, hud.Canvas);
            RefreshPresentationByVision();
            if (OS.GetEnvironment("CW_DEBUG_MODE") == "1") _debug.HandleKey(Key.F3);
        }

        public string GetStatusText() =>
            "1989-07-01 起 · 已过 " + ((_turnMgr.TurnNumber - 1) * 2) + " 小时 · 第 " + _turnMgr.TurnNumber +
            " 回合 · " + (_turnMgr.CurrentFaction == 1 ? "北约" : "华约") + " · " + _turnMgr.PhaseName() + " · 2公里/格";

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
            if (!_flow.CurrentSelection.Unit.CanFillMain())
            {
                _hud.SetInfoText("无法发起战斗：只有主力营可以主动进攻");
                return;
            }
            if (_flow.CurrentSelection.Unit.CurrentAP < 4f)
            {
                _hud.SetInfoText("无法发起战斗：至少需要 4 AP");
                return;
            }

            int dx = Math.Abs(_flow.CurrentSelection.Pos.X - pos.X);
            int dy = Math.Abs(_flow.CurrentSelection.Pos.Y - pos.Y);
            if (Math.Max(dx, dy) > 2)
            {
                _hud.SetInfoText("无法发起战斗：目标须在 2 格内");
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
                var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions);
                var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
                bool isEnemyZOC(Vector2I t) => enemyZOC.Contains(t);
                bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _flow.CurrentSelection.Unit);
                var path = _scenario.Movement.FindPath(_flow.CurrentSelection.Pos, pos, _flow.CurrentSelection.Unit.CurrentAP, isEnemyZOC, occ, _flow.CurrentSelection.Unit);
                if (path == null || path.Count < 2)
                {
                    ClearSelection();
                    _hud.SetInfoText("点击选择己方单位");
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
                    var enemyPositions3 = (enemyFaction3 == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions);
                    var enemyZOC3 = _scenario.ZOC.GetFactionZOC(enemyPositions3);
                    bool isEnemyZOC3(Vector2I t) => enemyZOC3.Contains(t);
                    bool occ3(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _flow.CurrentSelection.Unit);
                    var reachable = _scenario.Movement.GetReachableTiles(pos, _flow.CurrentSelection.Unit.CurrentAP, isEnemyZOC3, occ3, _flow.CurrentSelection.Unit);
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
                if (_flow.HasSelection)
                {
                    _hud.SetInfoText("无法到达：检查 AP、敌方控制区、单位占位或地形阻挡");
                    return;
                }
                ClearSelection();
                _hud.SetInfoText("点击选择己方单位");
            }
        }

        public void OnRightClick()
        {
            if (!_rules.IsActionAllowed(_flow.CurrentState, GameAction.SelectUnit)) return;
            ClearSelection();
            _hud.SetInfoText("点击选择己方单位");
        }

        public void OnHoverChanged(Vector2I? pos)
        {
            if (Result != null) return;
            _debug.Hover(pos);
            if (_debug.Enabled) return;
            _renderer.ClearPath();

            if (pos == null)
            {
                _hud.SetTooltipVisible(false);
                if (_flow.HasSelection)
                    _hud.SetInfoText(BuildSelectedUnitInfo(_flow.CurrentSelection.Unit, _flow.ReachableTiles.Count));
                else
                    _hud.SetInfoText("点击选择己方单位");
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
                var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions);
                var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
                bool isEnemyZOC(Vector2I t) => enemyZOC.Contains(t);
                bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _flow.CurrentSelection.Unit);
                var path = _scenario.Movement.FindPath(_flow.CurrentSelection.Pos, p, _flow.CurrentSelection.Unit.CurrentAP, isEnemyZOC, occ, _flow.CurrentSelection.Unit);
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
            CheckCampaignEnd();
            if (Result != null) return;
            _turnMgr.EndStrategicTurn();
            if(endingFaction==2) _scenario.Missions.CompleteRound(_turnMgr.TurnNumber-1,_scenario.Map,_scenario.GetOccupationMap());
            RefreshPresentationByVision();
            _hud.SetStatusText(GetStatusText());
            _hud.SetInfoText(GetStatusText());
            RefreshCampaignCasualtyPanel();
        }

        public void OnToggleCampaignCasualtyPanel()
        {
            bool visible = !_hud.IsCampaignCasualtyPanelVisible;
            _hud.SetCampaignCasualtyPanelVisible(visible);
            RefreshCampaignCasualtyPanel();
        }

        public void OnMouseMoved(Vector2 position) => _lastMouseScreenPos = position;

        public void HandleKeyboard(InputEventKey key)
        {
            if (!key.Pressed || key.Echo)
                return;
            if (_debug.HandleKey(key.Keycode)) return;

            if (key.Keycode == Key.Space && _rules.IsActionAllowed(_flow.CurrentState, GameAction.EndTurn))
            {
                OnEndTurn();
                return;
            }

            if (key.Keycode == Key.F6)
            {
                CycleSupplyOverlayMode();
                return;
            }

            if (key.Keycode == Key.F7)
            {
                ToggleControlOverlay();
            }
        }

        private void SelectUnit(Battalion bat, Vector2I pos)
        {
            _flow.EnterSelection(bat, pos, null);
            _eventHub.Publish(new GameplayEvent(GameplayEventType.UnitSelected, new SelectionEventData(bat, pos)));
            _renderer.SetSel(pos);
            var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions);
            var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
            bool isEnemyZOC(Vector2I p) => enemyZOC.Contains(p);
            bool occ(Vector2I p) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == p && u.Item1 != bat);
            var reachable = _scenario.Movement.GetReachableTiles(pos, bat.CurrentAP, isEnemyZOC, occ, bat);
            _flow.EnterSelection(bat, pos, reachable);
            _eventHub.Publish(new GameplayEvent(GameplayEventType.UnitSelected, new SelectionEventData(bat, pos, reachable)));
            _renderer.SetReachable(reachable, bat.CurrentAP);
            _hud.SetInfoText(BuildSelectedUnitInfo(bat, reachable.Count));
            _hud.ShowOrgPanel(bat, _owner.GetViewport().GetVisibleRect().Size.Y);
            UpdateArtilleryOverlay(bat, pos);
        }

        private string BuildSelectedUnitInfo(Battalion bat, int reachableCount)
        {
            var (visionRange, visionReason) = bat.GetVisionRuleInfo();
            var supply = bat.Faction == 1 ? _blueSupply : _redSupply;
            var unit = GetAllUnits().FirstOrDefault(u => u.bat == bat);
            string supplyStatus = supply != null && unit.bat != null
                ? (supply[unit.pos.X, unit.pos.Y] > 0 ? "畅通" : "断供") : "未计算";
            return $"已选择：{bat.Name} | AP {bat.CurrentAP:0.0}/{bat.GetMaxAP():0.0} | 疲劳 {bat.Fatigue}" +
                   $" | 当前补给：{supplyStatus} | 连续断供 {bat.TurnsOOS} | 可达 {reachableCount} 格" +
                   $" | 视野 {visionRange}（{visionReason}）";
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
                    GD.Print("=== COMBAT CALLBACK FIRED: VP Blue=" + _victoryTracker.BlueVP + " Red=" + _victoryTracker.RedVP + " ===");
                    _scenario.RemoveDeadBattalions();
                    RefreshOccupationFromEntryAndZoc();
                    RefreshFrontline();
                    ClearSelection();
                    RefreshPresentationByVision();
                    RefreshCampaignCasualtyPanel();
RefreshMissionPanel();
                },
                () =>
                {
                    ClearSelection();
                    _flow.ExitCombat();
                    _turnFlow.CancelCombat();
                    _hud.HideOrgPanel();
            _hud.SetInfoText("战斗已取消");
                },
                () =>
                {
                    _flow.ExitCombat();
                    _turnFlow.FinishPhase();
                    RefreshPresentationByVision();
                    _hud.SetInfoText("点击选择己方单位");
                    CheckCampaignEnd();
                });
        }

        private void RefreshCampaignCasualtyPanel()
        {
            if (_hud == null || !_hud.IsCampaignCasualtyPanelVisible)
                return;

            _hud.SetCampaignCasualtyText(_victoryTracker.BuildCampaignCasualtySummary());
        }

        private void ExecuteEndTurnSettlement(int endingFaction)
        {
            _debug.RecordSettlement(endingFaction, "before");
            var enemyUnits = GetFactionUnits(endingFaction == 1 ? 2 : 1).ToList();
            var enemyPositions = enemyUnits.Select(u => u.pos);
            var enemyOccupied = new HashSet<Vector2I>(enemyPositions);
            var enemyZoc = _scenario.ZOC.GetFactionZOC(enemyUnits);
            var (hubs, airports) = _scenario.GetSupplySpecialNodes();

            _supplyManager.UpdateFactionEndTurn(
                endingFaction,
                _scenario.Map,
                GetAllUnits(),
                enemyOccupied,
                enemyZoc,
                hubs,
                airports,
                _scenario.GetOccupationMap());

            _debug.RecordSettlement(endingFaction, "after_supply_before_control");
            RefreshOccupationFromEntryAndZoc();
            _scenario.SaveOccupationState(_scenario.GetOccupationMap());
            RefreshFrontline();
            RefreshMissionPanel();

            RefreshMissionPanel();
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
            RefreshControlVisualization();
            RefreshFrontline();
            RefreshMissionPanel();
        }

       private void RefreshSupplyVisualization()
       {
           var allUnits = GetAllUnits().ToList();
           var (hubs, airports) = _scenario.GetSupplySpecialNodes();

            _renderer.SetSupplySpecialNodes(hubs, airports);
            float[,] blueSp = ComputeSupplyMapForFaction(1, allUnits, hubs, airports);
            float[,] redSp = ComputeSupplyMapForFaction(2, allUnits, hubs, airports);
            _blueSupply = blueSp;
            _redSupply = redSp;

            var blueOos = new HashSet<Vector2I>(_scenario.BlueBattalions
                .Where(u => blueSp[u.pos.X, u.pos.Y] <= 0f)
                .Select(u => u.pos));

            var redOos = new HashSet<Vector2I>(_scenario.RedBattalions
                .Where(u => redSp[u.pos.X, u.pos.Y] <= 0f)
                .Select(u => u.pos));

            _renderer.SetUnitSupplyStatus(blueOos, redOos);
            _renderer.SetSupplyOverlayData(blueSp, redSp, _supplyOverlayMode);
            _debug.SetDisplayed(blueSp, redSp);
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
            var enemyZoc = _scenario.ZOC.GetFactionZOC(enemyUnits);

            return _supplyManager.ComputeFactionSupplySP(
                faction,
                _scenario.Map,
                allUnits,
                enemyOccupied,
                enemyZoc,
                hubs,
                airports,
                _scenario.GetOccupationMap());
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
            _hud.SetInfoText("补给覆盖 [F6]：" + DescribeSupplyOverlayMode(_supplyOverlayMode));
        }

        private void RefreshControlVisualization()
        {
            _renderer.SetControlOverlayData(_scenario.GetOccupationMap(), _controlOverlayMode);
        }

        private void ToggleControlOverlay()
        {
            _controlOverlayMode = _controlOverlayMode == ControlOverlayDisplayMode.Off
                ? ControlOverlayDisplayMode.On
                : ControlOverlayDisplayMode.Off;

            _renderer.SetControlOverlayMode(_controlOverlayMode);
            _hud.SetInfoText("控制区域 [F7]：" +
                (_controlOverlayMode == ControlOverlayDisplayMode.On ? "开启" : "关闭"));
        }

        private string DescribeSupplyOverlayMode(SupplyOverlayDisplayMode mode)
        {
            return mode switch
            {
                SupplyOverlayDisplayMode.Off => "关闭",
                SupplyOverlayDisplayMode.Friendly => "己方",
                SupplyOverlayDisplayMode.Enemy => "敌方",
                SupplyOverlayDisplayMode.Both => "双方",
                _ => "关闭"
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
                redPathZocTiles,
                _scenario.ZOC.GetFactionZOC(_scenario.BlueBattalions),
                _scenario.ZOC.GetFactionZOC(_scenario.RedBattalions));
            _scenario.ApplyOccupationState(updated);
            RefreshControlVisualization();
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
            _hud.HideOrgPanel();
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
