using Godot;
using System;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Victory;

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
        private Panel _vpPanel;
        private Label _vpNatoLabel;
        private Label _vpSepLabel;
        private Label _vpPactLabel;
        private Label _vpTurnLabel;
        private Panel _orgPanel;
        private Label _orgLabel;

        public GameHud(CanvasLayer canvasLayer, Action onEndTurnPressed, Action onCasualtyStatsPressed)
        {
            _canvasLayer = canvasLayer;
            _onEndTurnPressed = onEndTurnPressed;
            _onCasualtyStatsPressed = onCasualtyStatsPressed;
        }

        public CanvasLayer Canvas => _canvasLayer;
        private Button _endCampaignButton;
        private AcceptDialog _resultDialog;
        private RichTextLabel _resultText;
        private AcceptDialog _missionDialog;
        private RichTextLabel _missionText;
        private Button _exitButton;
        private bool _restartPending;

        private void RequestRestart(Action restart)
        {
            if (_restartPending) return;
            _restartPending = true;
            // A dialog is also a Viewport. Keep it in the tree until input dispatch returns.
            Callable.From(() =>
            {
                _restartPending = false;
                if (GodotObject.IsInstanceValid(_canvasLayer) && _canvasLayer.IsInsideTree()
                    && !_canvasLayer.IsQueuedForDeletion())
                    restart();
            }).CallDeferred();
        }
        public void ConfigureMissions(Action exit,Action show)
        {
            _exitButton=new Button { Text="突破／撤离所选单位",Position=new Vector2(10,358) };
            _exitButton.Pressed+=()=>exit(); _canvasLayer.AddChild(_exitButton);
            var button=new Button { Text="任务与区域",Position=new Vector2(10,390) };
            button.Pressed+=()=>show(); _canvasLayer.AddChild(button);
            _missionDialog=new AcceptDialog { Title="战役任务与进度" };
            _missionText=new RichTextLabel { FitContent=false };
            _missionText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _missionText.OffsetLeft=16; _missionText.OffsetTop=32; _missionText.OffsetRight=-16; _missionText.OffsetBottom=-56;
            _missionDialog.AddChild(_missionText); _canvasLayer.AddChild(_missionDialog);
        }
        public void ShowMissions(string text)
        { _missionText.Text=text; _missionDialog.PopupCentered(new Vector2I(680,520)); }
        public void UpdateMissionPanel(MissionTracker missions,int[,] control,int turn)
        {
            float width=_canvasLayer.GetViewport().GetVisibleRect().Size.X;
            _vpPanel.Position=new Vector2(Math.Max(0,(width-_vpPanel.Size.X)/2),4);
            _vpNatoLabel.Text=$"北约任务 {missions.BlueScore:0.0} 分";
            _vpPactLabel.Text=$"华约任务 {missions.RedScore(control):0.0} 分";
            _vpTurnLabel.Text=$"迟滞 {missions.State.DelayRounds}/{missions.Configuration.WindowRounds} · 突破 {missions.BreakthroughEquivalent:0.0}/{missions.Configuration.BreakthroughTarget:0.#}";
            if(_missionText!=null) _missionText.Text=missions.Summary(control);
        }

        public void ConfigureCampaignActions(Action endCampaign, Action restart, Action save, Action load)
        {
            _endCampaignButton = new Button { Text = "结束战役并结算", Position = new Vector2(10, 124) };
            _endCampaignButton.Pressed += () => endCampaign();
            _canvasLayer.AddChild(_endCampaignButton);
            var restartButton = new Button { Text = "重新开局", Position = new Vector2(10, 156) };
            var confirmation = new ConfirmationDialog { Title = "重新开局", DialogText = "放弃当前对局并恢复初始部署？" };
            confirmation.Confirmed += () => RequestRestart(restart);
            _canvasLayer.AddChild(confirmation);
            restartButton.Pressed += () => confirmation.PopupCentered();
            _canvasLayer.AddChild(restartButton);
            var saveButton = new Button { Text = "保存对局", Position = new Vector2(10, 188) };
            saveButton.Pressed += () => save();
            _canvasLayer.AddChild(saveButton);
            var loadButton = new Button { Text = "读取对局", Position = new Vector2(10, 220) };
            var loadConfirmation = new ConfirmationDialog { Title = "读取对局", DialogText = "用存档替换当前对局？" };
            loadConfirmation.Confirmed += () => load();
            _canvasLayer.AddChild(loadConfirmation);
            loadButton.Pressed += () => loadConfirmation.PopupCentered();
            _canvasLayer.AddChild(loadButton);
            _resultDialog = new AcceptDialog { Title = "Fulda Gap 战役结算", Exclusive = true };
            _resultText = new RichTextLabel();
            _resultText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _resultText.OffsetLeft=16; _resultText.OffsetTop=32; _resultText.OffsetRight=-16; _resultText.OffsetBottom=-56;
            _resultDialog.AddChild(_resultText);
            _resultDialog.GetOkButton().Text = "查看战场";
            _resultDialog.AddButton("重新开局", true, "restart");
            _resultDialog.CustomAction += action => { if (action == "restart") RequestRestart(restart); };
            _canvasLayer.AddChild(_resultDialog);
        }

        public void ShowCampaignResult(CampaignResult result)
        {
            if(_exitButton!=null) _exitButton.Disabled=true;
            _endTurnButton.Disabled = true;
            _endCampaignButton.Disabled = true;
            _resultText.Text = result.Summary;
            _resultDialog.PopupCentered(new Vector2I(600, 440));
            SetStatusText("战役已结束");
            SetInfoText("对局已结算，可查看战场或重新开局");
        }

        public void ResetCampaignResult()
        {
            if(_exitButton!=null) _exitButton.Disabled=false;
            _endTurnButton.Disabled = false;
            _endCampaignButton.Disabled = false;
            _resultDialog.Hide();
        }

        public void Initialize()
        {
            _infoLabel = new Label();
            _infoLabel.Position = new Vector2(10, 264);
            _infoLabel.Size = new Vector2(720, 90);
            _infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _infoLabel.AddThemeFontSizeOverride("font_size", 16);
            _infoLabel.Text = "点击己方营，再点击可达格移动或敌方营交战。F6：补给覆盖；F7：控制区域。";
            _canvasLayer.AddChild(_infoLabel);

            _statusLabel = new Label();
            _statusLabel.Position = new Vector2(10, 40);
            _statusLabel.AddThemeFontSizeOverride("font_size", 14);
            _canvasLayer.AddChild(_statusLabel);

            _endTurnButton = new Button();
            _endTurnButton.Position = new Vector2(10, 60);
            _endTurnButton.Text = "结束回合 [空格]";
            _endTurnButton.Pressed += () => _onEndTurnPressed?.Invoke();
            _endTurnButton.FocusMode = Control.FocusModeEnum.None;
            _canvasLayer.AddChild(_endTurnButton);

            _casualtyStatsButton = new Button();
            _casualtyStatsButton.Position = new Vector2(10, 92);
            _casualtyStatsButton.Size = new Vector2(160, 24);
            _casualtyStatsButton.Text = "战役战损";
            _casualtyStatsButton.Pressed += () => _onCasualtyStatsPressed?.Invoke();
            _casualtyStatsButton.FocusMode = Control.FocusModeEnum.None;
            _canvasLayer.AddChild(_casualtyStatsButton);

            // VP panel
            _vpPanel = new Panel();
            var vpStyle = new StyleBoxFlat();
            vpStyle.BgColor = new Color(0.04f, 0.06f, 0.10f, 0.90f);
            vpStyle.SetCornerRadiusAll(6);
            _vpPanel.AddThemeStyleboxOverride("panel", vpStyle);
            _vpPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _vpPanel.Size = new Vector2(780, 38);
            _vpPanel.Position = new Vector2(400, 4);
            _canvasLayer.AddChild(_vpPanel);

            _vpNatoLabel = MakeVPLabel("北约 0 VP（0 格）", new Color(0.36f, 0.61f, 0.84f), new Vector2(12, 8));
            _vpPanel.AddChild(_vpNatoLabel);
            _vpSepLabel = MakeVPLabel("|", new Color(0.5f, 0.5f, 0.5f), new Vector2(230, 8));
            _vpPanel.AddChild(_vpSepLabel);
            _vpPactLabel = MakeVPLabel("华约 0 VP（0 格）", new Color(0.88f, 0.44f, 0.38f), new Vector2(246, 8));
            _vpPanel.AddChild(_vpPactLabel);
            _vpTurnLabel = MakeVPLabel("第1回合 僵持（R=1.00）", new Color(0.7f, 0.7f, 0.7f), new Vector2(495, 8));
            _vpPanel.AddChild(_vpTurnLabel);

            // Casualty panel
            _casualtyStatsPanel = new Panel();
            var statsStyle = new StyleBoxFlat();
            statsStyle.BgColor = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            statsStyle.SetCornerRadiusAll(6);
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
            _casualtyStatsCloseButton.Text = "关闭";
            _casualtyStatsCloseButton.Pressed += () => _casualtyStatsPanel.Visible = false;
            _casualtyStatsCloseButton.FocusMode = Control.FocusModeEnum.None;
            _casualtyStatsPanel.AddChild(_casualtyStatsCloseButton);

            // Org panel
            _orgPanel = new Panel();
            var orgStyle = new StyleBoxFlat();
            orgStyle.BgColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
            orgStyle.SetCornerRadiusAll(6);
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

            // Tooltip
            _tooltipPanel = new Panel();
            var tipStyle = new StyleBoxFlat();
            tipStyle.BgColor = new Color(0, 0, 0, 0.85f);
            tipStyle.SetCornerRadiusAll(4);
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

        private static Label MakeVPLabel(string text, Color color, Vector2 pos)
        {
            var l = new Label();
            l.Text = text;
            l.AddThemeFontSizeOverride("font_size", 14);
            l.AddThemeColorOverride("font_color", color);
            l.MouseFilter = Control.MouseFilterEnum.Ignore;
            l.Position = pos;
            return l;
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

        public void UpdateVPPanel(VictoryTracker vt, float viewportWidth, int turn)
        {
            if (vt == null) return;
            var a = vt.Evaluate(turn);
            _vpNatoLabel.Text = "北约 " + a.BlueVP + " VP（" + vt.BlueControlledCount + " 格）";
            _vpPactLabel.Text = "华约 " + a.RedVP + " VP（" + vt.RedControlledCount + " 格）";
            _vpTurnLabel.Text = "第" + turn + "回合 " + a.BlueLevel.DisplayName() + "（R=" + a.Ratio.ToString("F2") + "）";
            float panelW = 780f;
            float w = viewportWidth > 100f ? viewportWidth : (float)DisplayServer.WindowGetSize().X;
            if (w < 100f) w = 1920f;
            _vpPanel.Position = new Vector2((w - panelW) / 2f, 4);
        }

        public void ShowOrgPanel(Battalion bat, float viewportHeight)
        {
            if (bat == null) { _orgPanel.Visible = false; return; }
            string text = BattalionOrgReporter.BuildOrganizationSummary(bat);
            if (string.IsNullOrEmpty(text)) { _orgPanel.Visible = false; return; }
            _orgLabel.Text = text;
            var lines = text.Split('\n');
            int maxW = 0;
            foreach (var line in lines) { int w = line.Length; if (w > maxW) maxW = w; }
            float panelW = Math.Max(maxW * 8 + 20, 100);
            float panelH = Math.Max(lines.Length * 18 + 16, 60);
            _orgPanel.Size = new Vector2(panelW, panelH);
            _orgPanel.Position = new Vector2(10, viewportHeight - panelH - 10);
            _orgPanel.Visible = true;
        }

        public void HideOrgPanel() => _orgPanel.Visible = false;
    }
}
