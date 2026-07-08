using Godot;
using System;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameHud
    {
        private readonly CanvasLayer _canvasLayer;
        private readonly Action _onEndTurnPressed;

        private Label _infoLabel;
        private Label _statusLabel;
        private Button _endTurnButton;
        private Panel _tooltipPanel;
        private Label _tooltipLabel;

        public GameHud(CanvasLayer canvasLayer, Action onEndTurnPressed)
        {
            _canvasLayer = canvasLayer;
            _onEndTurnPressed = onEndTurnPressed;
        }

        public CanvasLayer Canvas => _canvasLayer;
        public Label InfoLabel => _infoLabel;
        public Label StatusLabel => _statusLabel;
        public Button EndTurnButton => _endTurnButton;
        public Panel TooltipPanel => _tooltipPanel;
        public Label TooltipLabel => _tooltipLabel;

        public void Initialize()
        {
            _infoLabel = new Label();
            _infoLabel.Position = new Vector2(10, 10);
            _infoLabel.AddThemeFontSizeOverride("font_size", 16);
            _infoLabel.Text = "Fulda Gap 1985 - Click to select, reachable tile to move";
            _canvasLayer.AddChild(_infoLabel);

            _statusLabel = new Label();
            _statusLabel.Position = new Vector2(10, 34);
            _statusLabel.AddThemeFontSizeOverride("font_size", 14);
            _canvasLayer.AddChild(_statusLabel);

            _endTurnButton = new Button();
            _endTurnButton.Position = new Vector2(10, 60);
            _endTurnButton.Text = "End Turn [Space]";
            _endTurnButton.Pressed += () => _onEndTurnPressed?.Invoke();
            _endTurnButton.FocusMode = Control.FocusModeEnum.None;
            _canvasLayer.AddChild(_endTurnButton);

            _tooltipPanel = new Panel();
            var tipStyle = new StyleBoxFlat();
            tipStyle.BgColor = new Color(0, 0, 0, 0.85f);
            tipStyle.CornerRadiusTopLeft = 4;
            tipStyle.CornerRadiusTopRight = 4;
            tipStyle.CornerRadiusBottomLeft = 4;
            tipStyle.CornerRadiusBottomRight = 4;
            _tooltipPanel.AddThemeStyleboxOverride("panel", tipStyle);
            _tooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _tooltipPanel.Visible = false;
            _tooltipPanel.Size = new Vector2(520, 32);
            _canvasLayer.AddChild(_tooltipPanel);

            _tooltipLabel = new Label();
            _tooltipLabel.AddThemeFontSizeOverride("font_size", 14);
            _tooltipLabel.AddThemeColorOverride("font_color", Colors.White);
            _tooltipLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _tooltipLabel.Position = new Vector2(8, 4);
            _tooltipLabel.Size = new Vector2(504, 24);
            _tooltipPanel.AddChild(_tooltipLabel);
        }

        public void SetInfoText(string text) => _infoLabel.Text = text;

        public void SetStatusText(string text) => _statusLabel.Text = text;

        public void SetTooltipVisible(bool visible) => _tooltipPanel.Visible = visible;

        public void SetTooltipText(string text, Vector2 mouseScreenPos, Vector2 viewportSize)
        {
            _tooltipLabel.Text = text;
            _tooltipLabel.Size = new Vector2(504, 24);
            _tooltipPanel.Size = new Vector2(520, 32);

            Vector2 tipPos = mouseScreenPos + new Vector2(20, 20);
            tipPos.X = Mathf.Clamp(tipPos.X, 0, viewportSize.X - _tooltipPanel.Size.X);
            tipPos.Y = Mathf.Clamp(tipPos.Y, 0, viewportSize.Y - _tooltipPanel.Size.Y);
            _tooltipPanel.Position = tipPos;
            _tooltipPanel.Visible = true;
        }
    }
}
