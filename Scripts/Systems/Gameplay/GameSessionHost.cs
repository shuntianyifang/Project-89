using Godot;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Turns;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSessionHost
    {
        private readonly Node _root;
        private readonly GameManager _owner;
        private CanvasLayer _canvasLayer;
        private GameHud _hud;
        private GameSceneBootstrapper _bootstrapper;

        public GameSessionHost(Node root, GameManager owner)
        {
            _root = root;
            _owner = owner;
        }

        public FuldaGapScenario Scenario { get; private set; }
        public TurnManager TurnManager { get; private set; }
        public Grid3DRenderer Renderer { get; private set; }
        public GameCamera Camera { get; private set; }
        public GameHud Hud => _hud;
        public GameSessionController Session { get; private set; }
        public bool IsStarted { get; private set; }

        public void Initialize() => Start();

        public void Start()
        {
            if (IsStarted)
                return;

            _canvasLayer = new CanvasLayer();
            _root.AddChild(_canvasLayer);
            _hud = new GameHud(_canvasLayer, () => Session?.OnEndTurn(), () => Session?.OnToggleCampaignCasualtyPanel());

            UnitDatabase.Initialize("res://Scripts/Data/Units");
            TemplateDatabase.Initialize("res://Scripts/Data/Templates");
            GD.Print("System initialized.");

            Scenario = new FuldaGapScenario();
            Scenario.LoadOOB("res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json", "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
            Scenario.LoadOccupationState();
            Scenario.PrintSummary();

            TurnManager = new TurnManager();
            foreach (var u in Scenario.BlueBattalions) TurnManager.RegisterBattalion(u.Item1);
            foreach (var u in Scenario.RedBattalions) TurnManager.RegisterBattalion(u.Item1);

            _bootstrapper = new GameSceneBootstrapper(_root, Scenario, _canvasLayer, _hud, HandleUnitClicked, HandleTileClicked, HandleRightClick, HandleHoverChanged);
            _bootstrapper.Initialize();
            Renderer = _bootstrapper.Renderer;
            Camera = _bootstrapper.Camera;
            Session = new GameSessionController(_owner, Scenario, TurnManager, Renderer, _hud);
            _hud.SetStatusText(GetStatusText());
            IsStarted = true;
            GD.Print("3D scene ready.");
        }

        public void Shutdown()
        {
            if (!IsStarted)
                return;

            Session = null;
            Renderer = null;
            Camera = null;
            TurnManager = null;
            Scenario = null;

            if (_bootstrapper != null)
            {
                _bootstrapper.Cleanup();
                _bootstrapper = null;
            }

            if (_hud != null)
            {
                _hud = null;
            }

            if (_canvasLayer != null && GodotObject.IsInstanceValid(_canvasLayer))
            {
                if (_canvasLayer.GetParent() != null)
                    _root.RemoveChild(_canvasLayer);
                _canvasLayer.QueueFree();
            }

            _canvasLayer = null;
            IsStarted = false;
        }

        public void Restart() => Shutdown();

        public string GetStatusText() => Session?.GetStatusText() ?? "Turn 1";

        public void HandleUnitClicked(int faction, ColdWarWargame.Models.Battalion bat, Vector2I pos) => Session?.OnUnitClicked(faction, bat, pos);
        public void HandleTileClicked(Vector2I pos) => Session?.OnTileClicked(pos);
        public void HandleRightClick() => Session?.OnRightClick();
        public void HandleHoverChanged(Vector2I? pos) => Session?.OnHoverChanged(pos);
        public void HandleMouseMoved(Vector2 position) => Session?.OnMouseMoved(position);
        public void HandleKeyboard(InputEventKey key) => Session?.HandleKeyboard(key);
    }
}
