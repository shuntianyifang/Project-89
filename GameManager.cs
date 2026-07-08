using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Factories;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Rendering;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Victory;
using ColdWarWargame.Tests.Combat;
using ColdWarWargame.Tests.Battlefield;
using ColdWarWargame.Tests.Supply;
using ColdWarWargame.Tests.Turns;
using ColdWarWargame.Tests.Victory;

public partial class GameManager : Node
{
    private Grid3DRenderer _renderer;
    private GameCamera _camCtrl;
    private CanvasLayer _ui;
    private Label _infoLabel;
    private Label _statusLabel;
    private Button _endTurnBtn;
    private FuldaGapScenario _scenario;
    private TurnManager _turnMgr;
    private class SelState { public Battalion Unit; public Vector2I Pos; }
    private SelState _sel;
    private CombatResolver _resolver = new();
    private bool _inCombat = false;
    private bool _isMoving = false;
    private Dictionary<Vector2I, float> _currentReachable = new();
    private Panel _tooltipPanel;
    private Label _tooltipLabel;
    private Vector2 _lastMouseScreenPos;

    public override void _Ready()
    {
        UnitDatabase.Initialize("res://Scripts/Data/Units");
        TemplateDatabase.Initialize("res://Scripts/Data/Templates");
        GD.Print("System initialized.");
        CombatResolverTests.RunAll();
        GridTests.RunAll();
        TurnManagerTests.RunAll();
        SupplyManagerTests.RunAll();
        VictoryTrackerTests.RunAll();
        VisionTests.RunAll();
        EngagementTests.RunAll();
        GD.Print("========== Fulda Gap 1985 ==========");
        _scenario = new FuldaGapScenario();
        _scenario.LoadOOB("res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json", "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
        _scenario.PrintSummary();
        _turnMgr = new TurnManager();
        foreach (var u in _scenario.BlueBattalions) _turnMgr.RegisterBattalion(u.Item1);
        foreach (var u in _scenario.RedBattalions) _turnMgr.RegisterBattalion(u.Item1);
        SetupScene3D();
        GD.Print("3D scene ready.");
    }

    void SetupScene3D()
    {
        float gw = 30f, gh = 20f;
        _camCtrl = new GameCamera();
        _camCtrl.Target = new Vector3(gw / 2, 0, gh / 2);
        AddChild(_camCtrl);
        _renderer = new Grid3DRenderer();
        _renderer.CellSize = 1.0f;
        _renderer.SetGrid(_scenario.Map);
        _renderer.SetBlueUnits(_scenario.BlueBattalions);
        _renderer.SetRedUnits(_scenario.RedBattalions);
       _renderer.OnUnitClicked = OnUnitClicked;
       _renderer.OnTileClicked = OnTileClicked;
        _renderer.OnRightClick = OnRightClick;
        _renderer.OnHoverChanged = OnHoverChanged;
       _renderer.SetCameraRef(_camCtrl.Cam);
        AddChild(_renderer);
        _ui = new CanvasLayer();
        AddChild(_ui);
        _infoLabel = new Label();
        _infoLabel.Position = new Vector2(10, 10);
        _infoLabel.AddThemeFontSizeOverride("font_size", 16);
        _infoLabel.Text = "Fulda Gap 1985 - Click to select, reachable tile to move";
        _ui.AddChild(_infoLabel);
        _statusLabel = new Label();
        _statusLabel.Position = new Vector2(10, 34);
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        _statusLabel.Text = GetStatusText();
        _ui.AddChild(_statusLabel);
        _endTurnBtn = new Button();
        _endTurnBtn.Position = new Vector2(10, 60);
        _endTurnBtn.Text = "End Turn [Space]";
       _endTurnBtn.Pressed += OnEndTurn;
        _endTurnBtn.FocusMode = Control.FocusModeEnum.None;
       _ui.AddChild(_endTurnBtn);
        // Tooltip near cursor
        _tooltipPanel = new Panel();
        var tipStyle = new StyleBoxFlat();
        tipStyle.BgColor = new Color(0, 0, 0, 0.85f);
        tipStyle.CornerRadiusTopLeft = 4; tipStyle.CornerRadiusTopRight = 4;
        tipStyle.CornerRadiusBottomLeft = 4; tipStyle.CornerRadiusBottomRight = 4;
        _tooltipPanel.AddThemeStyleboxOverride("panel", tipStyle);
        _tooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tooltipPanel.Visible = false;
        _tooltipPanel.Size = new Vector2(520, 32);
        _ui.AddChild(_tooltipPanel);

        _tooltipLabel = new Label();
        _tooltipLabel.AddThemeFontSizeOverride("font_size", 14);
        _tooltipLabel.AddThemeColorOverride("font_color", Colors.White);
        _tooltipLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tooltipLabel.Position = new Vector2(8, 4);
        _tooltipLabel.Size = new Vector2(504, 24);
        _tooltipPanel.AddChild(_tooltipLabel);
    }

    string GetStatusText() => "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact") + " - " + _turnMgr.PhaseName();

    void OnUnitClicked(int faction, Battalion bat, Vector2I pos)
    {
        if (_inCombat) return;
        if (faction == _turnMgr.CurrentFaction)
        {
            _sel = new SelState { Unit = bat, Pos = pos };
            _renderer.SetSel(pos);
            var enemyFaction = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var enemyPositions = (enemyFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
            var enemyZOC = _scenario.ZOC.GetFactionZOC(enemyPositions);
            bool isEnemyZOC(Vector2I p) => enemyZOC.Contains(p);
            bool occ(Vector2I p) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == p && u.Item1 != bat);
            _currentReachable = _scenario.Movement.GetReachableTiles(pos, bat.CurrentAP, isEnemyZOC, occ);
            _renderer.SetReachable(_currentReachable, bat.CurrentAP);
            _infoLabel.Text = "Selected: " + bat.Name + " reachable " + _currentReachable.Count + " tiles";
        }
        else if (_sel != null)
        {
            _inCombat = true;
            var ctx = new CombatContext { DefenderTerrainBonus = _scenario.Map.GetTile(pos).TerrainType switch { 1 => 0.1f, 2 => 0.3f, 3 => 0.4f, _ => 0f } };
            _turnMgr.InitiateCombat(_sel.Unit, bat, ctx);
            _turnMgr.FinishAttackerDeployment();
            var result = _turnMgr.FinishDefenderDeployment(_resolver);
            _infoLabel.Text = "Combat: " + _sel.Unit.Name + " -> " + bat.Name + " V=" + result.Advantage.Value.ToString("0.00");
            _sel = null; _renderer.ClearSel(); _currentReachable.Clear();
            _inCombat = false;
        }
    }

   void OnTileClicked(Vector2I pos)
    {
        if (_inCombat || _isMoving) return;
        if (_sel != null && _currentReachable != null && _currentReachable.ContainsKey(pos))
        {
            float cost = _currentReachable[pos];
            var ef2 = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var ep2 = (ef2 == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
            var ez2 = _scenario.ZOC.GetFactionZOC(ep2);
            bool isEZ(Vector2I t) => ez2.Contains(t);
            bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _sel.Unit);
            var path = _scenario.Movement.FindPath(_sel.Pos, pos, _sel.Unit.CurrentAP, isEZ, occ);
            if (path == null || path.Count < 2) { _sel = null; _renderer.ClearSel(); _currentReachable.Clear(); return; }

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

                var ef3 = _turnMgr.CurrentFaction == 1 ? 2 : 1;
                var ep3 = (ef3 == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
                var ez3 = _scenario.ZOC.GetFactionZOC(ep3);
                bool isEZ3(Vector2I t) => ez3.Contains(t);
                bool oc3(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _sel.Unit);
                _currentReachable = _scenario.Movement.GetReachableTiles(pos, _sel.Unit.CurrentAP, isEZ3, oc3);
                _renderer.SetReachable(_currentReachable, _sel.Unit.CurrentAP);
                _renderer.SetSel(pos);
                _infoLabel.Text = "Moved to (" + pos.X + "," + pos.Y + ") AP=" + _sel.Unit.CurrentAP.ToString("0.0");
                _isMoving = false;
            };
        }
        else { _sel = null; _renderer.ClearSel(); _currentReachable.Clear(); _infoLabel.Text = "Click to select"; }
    }

    void OnRightClick()
    {
        if (_inCombat || _isMoving) return;
        _sel = null;
        _renderer.ClearSel();
        _currentReachable.Clear();
        _infoLabel.Text = "Click to select";
    }

    static readonly string[] TerrainNames = { "平原", "森林", "半城镇", "城镇" };
    static readonly string[] InfraNames = { "", "支线公路", "高速公路" };

    void OnHoverChanged(Vector2I? pos)
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

        // 有选中单位时显示路径
        if (_sel != null && _currentReachable.ContainsKey(p))
        {
            var ef = _turnMgr.CurrentFaction == 1 ? 2 : 1;
            var ep = (ef == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions).Select(u => u.Item2);
            var ez = _scenario.ZOC.GetFactionZOC(ep);
            bool isEZ(Vector2I t) => ez.Contains(t);
            bool occ(Vector2I t) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == t && u.Item1 != _sel.Unit);
            var path = _scenario.Movement.FindPath(_sel.Pos, p, _sel.Unit.CurrentAP, isEZ, occ);
            if (path != null) _renderer.ShowPath(path);
        }

        _tooltipLabel.Text = info;
        _tooltipLabel.Size = new Vector2(504, 24);
        _tooltipPanel.Size = new Vector2(520, 32);

        Vector2 tipPos = _lastMouseScreenPos + new Vector2(20, 20);
        var vpSize = GetViewport().GetVisibleRect().Size;
        tipPos.X = Mathf.Clamp(tipPos.X, 0, vpSize.X - _tooltipPanel.Size.X);
        tipPos.Y = Mathf.Clamp(tipPos.Y, 0, vpSize.Y - _tooltipPanel.Size.Y);
        _tooltipPanel.Position = tipPos;
        _tooltipPanel.Visible = true;
    }

   void OnEndTurn()
    {
        if (_inCombat || _isMoving) return;
        _sel = null; _renderer.ClearSel(); _currentReachable.Clear();
        _turnMgr.EndStrategicTurn();
        _renderer.SetBlueUnits(_scenario.BlueBattalions);
        _renderer.SetRedUnits(_scenario.RedBattalions);
        _statusLabel.Text = GetStatusText();
        _infoLabel.Text = "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
        {
            _lastMouseScreenPos = mm.Position;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Space && !_inCombat && !_isMoving)
            OnEndTurn();
    }
}
