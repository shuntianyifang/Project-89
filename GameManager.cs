using Godot;
using System;
using System.Collections.Generic;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Systems.Gameplay;
using ColdWarWargame.Tests;

public partial class GameManager : Node
{
    private GameApplication _application;
    private Grid3DRenderer _renderer;
    private GameCamera _camCtrl;

    public override void _Ready()
    {
        if (OS.GetEnvironment("CW_RUN_TESTS") == "1")
        {
            int fails = AllTestsRunner.RunAll();
            GetTree().Quit(fails > 0 ? 1 : 0);
            return;
        }

        GD.Print($"========== {"Fulda Gap 1989"} ==========");
        _application = new GameApplication(this, this);
        _application.Initialize();
        _renderer = _application.Renderer;
        _camCtrl = _application.Camera;
    }

    private string GetStatusText() => _application?.GetStatusText() ?? "Turn 1";

    private void OnUnitClicked(int faction, Battalion bat, Vector2I pos) => _application?.HandleUnitClicked(faction, bat, pos);

    private void OnTileClicked(Vector2I pos) => _application?.HandleTileClicked(pos);

    private void OnRightClick() => _application?.HandleRightClick();

    private void OnHoverChanged(Vector2I? pos) => _application?.HandleHoverChanged(pos);

    private void OnEndTurn() => _application?.Session?.OnEndTurn();

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
            _application?.HandleMouseMoved(mm.Position);

        if (@event is InputEventKey key)
            _application?.HandleKeyboard(key);
    }
}
