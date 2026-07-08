using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameApplication
    {
        private readonly Node _root;
        private readonly GameManager _owner;
        private GameSessionHost _sessionHost;

        public GameApplication(Node root, GameManager owner)
        {
            _root = root;
            _owner = owner;
        }

        public string Title { get; } = "Fulda Gap 1985";
        public bool IsInitialized { get; private set; }
        public GameSessionHost SessionHost => _sessionHost;
        public Grid3DRenderer Renderer => _sessionHost?.Renderer;
        public GameCamera Camera => _sessionHost?.Camera;
        public GameHud Hud => _sessionHost?.Hud;
        public GameSessionController Session => _sessionHost?.Session;

        public void Initialize()
        {
            if (IsInitialized)
                return;

            StartNewGame();
        }

        public void StartNewGame()
        {
            _sessionHost = new GameSessionHost(_root, _owner);
            _sessionHost.Initialize();
            IsInitialized = true;
        }

        public void RestartCurrentGame() => StartNewGame();

        public string GetStatusText() => _sessionHost?.GetStatusText() ?? "Turn 1";

        public void HandleUnitClicked(int faction, Battalion bat, Vector2I pos) => _sessionHost?.HandleUnitClicked(faction, bat, pos);
        public void HandleTileClicked(Vector2I pos) => _sessionHost?.HandleTileClicked(pos);
        public void HandleRightClick() => _sessionHost?.HandleRightClick();
        public void HandleHoverChanged(Vector2I? pos) => _sessionHost?.HandleHoverChanged(pos);
        public void HandleMouseMoved(Vector2 position) => _sessionHost?.HandleMouseMoved(position);
        public void HandleKeyboard(InputEventKey key) => _sessionHost?.HandleKeyboard(key);
    }
}
