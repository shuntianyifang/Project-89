using Godot;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Tests.Battlefield;
using ColdWarWargame.Tests.Combat;
using ColdWarWargame.Tests.Supply;
using ColdWarWargame.Tests.Turns;
using ColdWarWargame.Tests.Victory;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSessionHost
    {
        private readonly Node _root;
        private readonly GameManager _owner;
        private readonly CanvasLayer _canvasLayer;
        private readonly GameHud _hud;
        private readonly GameSceneBootstrapper _bootstrapper;

        public GameSessionHost(Node root, GameManager owner)
        {
            _root = root;
            _owner = owner;
            _canvasLayer = new CanvasLayer();
            _root.AddChild(_canvasLayer);

            _hud = new GameHud(_canvasLayer, () => Session?.OnEndTurn());
            _bootstrapper = new GameSceneBootstrapper(root, Scenario, _canvasLayer, _hud, HandleUnitClicked, HandleTileClicked, HandleRightClick, HandleHoverChanged);
        }

        public FuldaGapScenario Scenario { get; private set; }
        public TurnManager TurnManager { get; private set; }
        public Grid3DRenderer Renderer { get; private set; }
        public GameCamera Camera { get; private set; }
        public GameHud Hud => _hud;
        public GameSessionController Session { get; private set; }

        public void Initialize()
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

            Scenario = new FuldaGapScenario();
            Scenario.LoadOOB("res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json", "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
            Scenario.PrintSummary();

            TurnManager = new TurnManager();
            foreach (var u in Scenario.BlueBattalions) TurnManager.RegisterBattalion(u.Item1);
            foreach (var u in Scenario.RedBattalions) TurnManager.RegisterBattalion(u.Item1);

            _bootstrapper.Initialize();
            Renderer = _bootstrapper.Renderer;
            Camera = _bootstrapper.Camera;
            Session = new GameSessionController(_owner, Scenario, TurnManager, Renderer, _hud);
            _hud.SetStatusText(GetStatusText());
            GD.Print("3D scene ready.");
        }

        public string GetStatusText() => Session?.GetStatusText() ?? "Turn 1";

        public void HandleUnitClicked(int faction, ColdWarWargame.Models.Battalion bat, Vector2I pos) => Session?.OnUnitClicked(faction, bat, pos);
        public void HandleTileClicked(Vector2I pos) => Session?.OnTileClicked(pos);
        public void HandleRightClick() => Session?.OnRightClick();
        public void HandleHoverChanged(Vector2I? pos) => Session?.OnHoverChanged(pos);
        public void HandleMouseMoved(Vector2 position) => Session?.OnMouseMoved(position);
        public void HandleKeyboard(InputEventKey key) => Session?.HandleKeyboard(key);
    }
}
