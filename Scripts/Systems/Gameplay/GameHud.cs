using Godot;
using System;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameHud
    {
        private readonly CanvasLayer _canvasLayer;
        private readonly Action _onEndTurnPressed;
        private readonly Action _onCasualtyStatsPressed;

        private Label _infoLabel;
        private Label _statusLabel;
        private Button _endTurnButton;
        private Button _casualtyStatsButton;
        private Panel _tooltipPanel;
        private Label _tooltipLabel;
        private Panel _casualtyStatsPanel;
        private Label _casualtyStatsLabel;
        private Button _casualtyStatsCloseButton;

        // battalion organization panel (lower-left)
        private Panel _orgPanel;
        private Label _orgLabel;

        public GameHud(CanvasLayer canvasLayer, Action onEndTurnPressed, Action onCasualtyStatsPressed)
        {
            _canvasLayer = canvasLayer;
            _onEndTurnPressed = onEndTurnPressed;
            _onCasualtyStatsPressed = onCasualtyStatsPressed;
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
            _infoLabel.Text = "Fulda Gap 1985 - Click to select, reachable tile to move | Supply Overlay [F6]";
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

            _casualtyStatsButton = new Button();
            _casualtyStatsButton.Position = new Vector2(10, 92);
            _casualtyStatsButton.Size = new Vector2(160, 24);
            _casualtyStatsButton.Text = "Campaign Losses";
            _casualtyStatsButton.Pressed += () => _onCasualtyStatsPressed?.Invoke();
            _casualtyStatsButton.FocusMode = Control.FocusModeEnum.None;
            _canvasLayer.AddChild(_casualtyStatsButton);

            _casualtyStatsPanel = new Panel();
            var statsStyle = new StyleBoxFlat();
            statsStyle.BgColor = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            statsStyle.CornerRadiusTopLeft = 6;
            statsStyle.CornerRadiusTopRight = 6;
            statsStyle.CornerRadiusBottomLeft = 6;
            statsStyle.CornerRadiusBottomRight = 6;
            _casualtyStatsPanel.AddThemeStyleboxOverride("panel", statsStyle);
            _casualtyStatsPanel.Position = new Vector2(200, 10);
            _casualtyStatsPanel.Size = new Vector2(360, 180);
            _casualtyStatsPanel.Visible = false;
            _canvasLayer.AddChild(_casualtyStatsPanel);

            _casualtyStatsLabel = new Label();
            _casualtyStatsLabel.Position = new Vector2(12, 12);
            _casualtyStatsLabel.Size = new Vector2(336, 120);
            _casualtyStatsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _casualtyStatsLabel.AddThemeFontSizeOverride("font_size", 14);
            _casualtyStatsLabel.AddThemeColorOverride("font_color", Colors.White);
            _casualtyStatsPanel.AddChild(_casualtyStatsLabel);

            _casualtyStatsCloseButton = new Button();
            _casualtyStatsCloseButton.Position = new Vector2(12, 138);
            _casualtyStatsCloseButton.Size = new Vector2(120, 24);
            _casualtyStatsCloseButton.Text = "Close";
            _casualtyStatsCloseButton.Pressed += () => _casualtyStatsPanel.Visible = false;
            _casualtyStatsCloseButton.FocusMode = Control.FocusModeEnum.None;
            _casualtyStatsPanel.AddChild(_casualtyStatsCloseButton);

            // --- org panel in lower-left ---
            _orgPanel = new Panel();
            var orgStyle = new StyleBoxFlat();
            orgStyle.BgColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
            orgStyle.CornerRadiusTopLeft = 6;
            orgStyle.CornerRadiusTopRight = 6;
            orgStyle.CornerRadiusBottomLeft = 6;
            orgStyle.CornerRadiusBottomRight = 6;
            _orgPanel.AddThemeStyleboxOverride("panel", orgStyle);
            _orgPanel.Visible = false;
            _orgPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _canvasLayer.AddChild(_orgPanel);

            _orgLabel = new Label();
            _orgLabel.Position = new Vector2(10, 8);
            _orgLabel.AddThemeFontSizeOverride("font_size", 13);
            _orgLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f, 1f));
            _orgLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _orgPanel.AddChild(_orgLabel);

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
        public void SetCampaignCasualtyText(string text) => _casualtyStatsLabel.Text = text;
        public void SetCampaignCasualtyPanelVisible(bool visible) => _casualtyStatsPanel.Visible = visible;
        public bool IsCampaignCasualtyPanelVisible => _casualtyStatsPanel?.Visible ?? false;
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

        /// <summary>Show battalion organization panel at lower-left</summary>
        public void ShowOrgPanel(Battalion bat, float viewportHeight)
        {
            if (bat == null) { _orgPanel.Visible = false; return; }
            string text = BattalionOrgReporter.BuildOrganizationSummary(bat);
            if (string.IsNullOrEmpty(text)) { _orgPanel.Visible = false; return; }

            _orgLabel.Text = text;
            // measure: count lines and max column width
            var lines = text.Split('\n');
            int maxW = 0;
            foreach (var line in lines)
            {
                int w = line.Length; // approximate
                if (w > maxW) maxW = w;
            }
            float panelW = Math.Max(maxW * 8 + 20, 100);
            float panelH = Math.Max(lines.Length * 18 + 16, 60);
            _orgPanel.Size = new Vector2(panelW, panelH);
            _orgPanel.Position = new Vector2(10, viewportHeight - panelH - 10);
            _orgPanel.Visible = true;
        }

        public void HideOrgPanel() => _orgPanel.Visible = false;
    }
}