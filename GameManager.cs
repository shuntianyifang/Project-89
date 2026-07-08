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
    private Dictionary<Vector2I, float> _currentReachable = new();

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
        _ui.AddChild(_endTurnBtn);
    }

    string GetStatusText() => "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact") + " - " + _turnMgr.PhaseName();

    void OnUnitClicked(int faction, Battalion bat, Vector2I pos)
    {
        if (_inCombat) return;
        if (faction == _turnMgr.CurrentFaction)
        {
            _sel = new SelState { Unit = bat, Pos = pos };
            _renderer.SetSel(pos);
            bool noZOC(Vector2I p) => false;
            bool occ(Vector2I p) => _scenario.BlueBattalions.Concat(_scenario.RedBattalions).Any(u => u.Item2 == p && u.Item1 != bat);
            _currentReachable = _scenario.Movement.GetReachableTiles(pos, bat.CurrentAP, noZOC, occ);
            _renderer.SetReachable(_currentReachable);
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
        if (_inCombat) return;
        if (_sel != null && _currentReachable != null && _currentReachable.ContainsKey(pos))
        {
            float cost = _currentReachable[pos];
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
            _infoLabel.Text = "Moved to (" + pos.X + "," + pos.Y + ") AP=" + _sel.Unit.CurrentAP.ToString("0.0");
            _renderer.ClearSel(); _sel = null; _currentReachable.Clear();
        }
        else { _sel = null; _renderer.ClearSel(); _currentReachable.Clear(); _infoLabel.Text = "Click to select"; }
    }

    void OnEndTurn()
    {
        if (_inCombat) return;
        _sel = null; _renderer.ClearSel(); _currentReachable.Clear();
        _turnMgr.EndStrategicTurn();
        _statusLabel.Text = GetStatusText();
        _infoLabel.Text = "Turn " + _turnMgr.TurnNumber + " - " + (_turnMgr.CurrentFaction == 1 ? "NATO" : "Warsaw Pact");
    }

    public override void _Process(double delta)
    {
        if (Input.IsKeyPressed(Key.Space) && !_inCombat) OnEndTurn();
    }
}
