using Godot;
using System;
using System.Collections.Generic;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Systems.Gameplay;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Tests.Battlefield;
using ColdWarWargame.Tests.Combat;
using ColdWarWargame.Tests.Supply;
using ColdWarWargame.Tests.Turns;
using ColdWarWargame.Tests.Victory;
using ColdWarWargame.UI;

public partial class GameManager : Node
{
    private Grid3DRenderer _renderer;
    private GameCamera _camCtrl;
    private CanvasLayer _ui;
    private FuldaGapScenario _scenario;
    private TurnManager _turnMgr;
    private GameSessionController _session;
    private GameHud _hud;
    private GameSceneBootstrapper _sceneBootstrapper;

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

    private void SetupScene3D()
    {
        _ui = new CanvasLayer();
        AddChild(_ui);

        _hud = new GameHud(_ui, OnEndTurn);
        _sceneBootstrapper = new GameSceneBootstrapper(
            this,
            _scenario,
            _ui,
            _hud,
            OnUnitClicked,
            OnTileClicked,
            OnRightClick,
            OnHoverChanged);
        _sceneBootstrapper.Initialize();

        _camCtrl = _sceneBootstrapper.Camera;
        _renderer = _sceneBootstrapper.Renderer;

        _session = new GameSessionController(this, _scenario, _turnMgr, _renderer, _hud);
        _hud.SetStatusText(GetStatusText());
    }

    private string GetStatusText() => _session?.GetStatusText() ?? "Turn 1";

    private void OnUnitClicked(int faction, Battalion bat, Vector2I pos) => _session?.OnUnitClicked(faction, bat, pos);

    private void OnTileClicked(Vector2I pos) => _session?.OnTileClicked(pos);

    private void OnRightClick() => _session?.OnRightClick();

    private void OnHoverChanged(Vector2I? pos) => _session?.OnHoverChanged(pos);

    private void OnEndTurn() => _session?.OnEndTurn();

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
            _session?.OnMouseMoved(mm.Position);

        if (@event is InputEventKey key)
            _session?.HandleKeyboard(key);
    }
}
