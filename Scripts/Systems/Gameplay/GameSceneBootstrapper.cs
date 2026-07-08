using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameSceneBootstrapper
    {
        private readonly Node _root;
        private readonly FuldaGapScenario _scenario;
        private readonly CanvasLayer _canvasLayer;
        private readonly GameHud _hud;
        private readonly System.Action<int, Battalion, Vector2I> _onUnitClicked;
        private readonly System.Action<Vector2I> _onTileClicked;
        private readonly System.Action _onRightClick;
        private readonly System.Action<Vector2I?> _onHoverChanged;

        public GameSceneBootstrapper(
            Node root,
            FuldaGapScenario scenario,
            CanvasLayer canvasLayer,
            GameHud hud,
            System.Action<int, Battalion, Vector2I> onUnitClicked,
            System.Action<Vector2I> onTileClicked,
            System.Action onRightClick,
            System.Action<Vector2I?> onHoverChanged)
        {
            _root = root;
            _scenario = scenario;
            _canvasLayer = canvasLayer;
            _hud = hud;
            _onUnitClicked = onUnitClicked;
            _onTileClicked = onTileClicked;
            _onRightClick = onRightClick;
            _onHoverChanged = onHoverChanged;
        }

        public Grid3DRenderer Renderer { get; private set; }
        public GameCamera Camera { get; private set; }

        public void Initialize()
        {
            float gw = 50f, gh = 30f;
            Camera = new GameCamera();
            Camera.Target = new Vector3(gw / 2, 0, gh / 2);
            _root.AddChild(Camera);

            Renderer = new Grid3DRenderer();
            Renderer.CellSize = 1.0f;
            Renderer.SetGrid(_scenario.Map);
            Renderer.SetBlueUnits(_scenario.BlueBattalions);
            Renderer.SetRedUnits(_scenario.RedBattalions);
            Renderer.OnUnitClicked = _onUnitClicked;
            Renderer.OnTileClicked = _onTileClicked;
            Renderer.OnRightClick = _onRightClick;
            Renderer.OnHoverChanged = _onHoverChanged;
            Renderer.SetCameraRef(Camera.Cam);
            _root.AddChild(Renderer);

            _hud.Initialize();
        }

        public void Cleanup()
        {
            if (Renderer != null && GodotObject.IsInstanceValid(Renderer))
            {
                if (Renderer.GetParent() != null)
                    _root.RemoveChild(Renderer);
                Renderer.QueueFree();
            }

            if (Camera != null && GodotObject.IsInstanceValid(Camera))
            {
                if (Camera.GetParent() != null)
                    _root.RemoveChild(Camera);
                Camera.QueueFree();
            }

            Renderer = null;
            Camera = null;
        }
    }
}
