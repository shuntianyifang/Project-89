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
    private Label _infoLabel;
    private Label _statusLabel;
    private Button _endTurnBtn;
    private FuldaGapScenario _scenario;
    private TurnManager _turnMgr;
    private GameSessionController _session;
    private Panel _tooltipPanel;
    private Label _tooltipLabel;

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

        _session = new GameSessionController(
            this,
            _scenario,
            _turnMgr,
            _renderer,
            _infoLabel,
            _statusLabel,
            _ui,
            _tooltipPanel,
            _tooltipLabel);

        _statusLabel.Text = GetStatusText();
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
