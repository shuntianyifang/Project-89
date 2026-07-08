using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;

namespace ColdWarWargame.UI
{
    /// <summary>
    /// 战斗部署面板——热座模式插槽式部署界面
    /// 流程: 攻击方部署 → 防御方自动部署 → 结算 → 展示结果
    /// </summary>
    public partial class CombatDeploymentPanel : Control
    {
        public CombatDeploymentPanel() { }
        // ===== 槽位定义 =====
        private static readonly (string label, string desc, Color color)[] SlotDefs = new[]
        {
            ("MAIN 1", "主力营（必需）", new Color(0.9f, 0.4f, 0.2f)),  // 主战插槽1
            ("MAIN 2", "主力营（可选）", new Color(0.9f, 0.6f, 0.2f)),  // 主战插槽2
            ("SUPPORT", "辅助营（可选）", new Color(0.3f, 0.7f, 0.3f)), // 辅助插槽
            ("ARTILLERY", "炮兵营（可选）", new Color(0.4f, 0.4f, 0.8f)), // 炮兵插槽
        };

        // ===== 回调 =====
        public Action<CombatForce> OnAttackerConfirmed;           // 玩家确认部署
        public Action OnCancel;                                   // 玩家取消
        public Action OnResultDismissed;                         // 关闭结果面板

        // ===== 状态 =====
        private Battalion _leadAttacker;
        private Battalion _leadDefender;
        private int _terrainBonus;
        private string _terrainName;
        private CombatForce _attackerForce = new();
        private CombatForce _defenderForce = new();
        private List<(Battalion bat, Vector2I pos)> _eligibleUnits;
        private Battalion _selectedUnit;
        private Button[] _slotButtons = new Button[4];
        private Label[] _slotLabels = new Label[4];
        private Button _confirmBtn;
        private Button _cancelBtn;
        private Button _nextBtn;
        private Control _contentRoot;
        private Control _resultRoot;


        // ===== 展示攻击阶段 =====
        public void ShowAttackerPhase(
            Battalion leadAttacker,
            Battalion leadDefender,
            List<(Battalion bat, Vector2I pos)> eligibleUnits,
            int terrainBonus,
            string terrainName)
        {
            _leadAttacker = leadAttacker;
            _leadDefender = leadDefender;
            _eligibleUnits = eligibleUnits;
            _terrainBonus = terrainBonus;
            _terrainName = terrainName;
            _attackerForce = new CombatForce();
            // _attackerForce.LeadBattalion = _leadAttacker;  // 改为手动分配
            _selectedUnit = null;

            BuildUI(isResult: false);
            UpdateSlots();
            EnableConfirm();
        }

        // ===== 构建主面板 =====
        void BuildUI(bool isResult)
        {
            // 清除旧的
            if (_contentRoot != null) { RemoveChild(_contentRoot); _contentRoot.QueueFree(); }
            if (_resultRoot != null) { RemoveChild(_resultRoot); _resultRoot.QueueFree(); }

            // 半透明背景
            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.75f);
            bg.MouseFilter = Control.MouseFilterEnum.Ignore;
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);

            var outer = new Panel();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.08f, 0.12f);
            style.CornerRadiusTopLeft = 8; style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8; style.CornerRadiusBottomRight = 8;
            outer.AddThemeStyleboxOverride("panel", style);
            outer.Size = new Vector2(920, 680);
            var vpSize = GetViewport().GetVisibleRect().Size;
            outer.Position = new Vector2((vpSize.X - 920) / 2, (vpSize.Y - 680) / 2);

            AddChild(outer);
            _contentRoot = outer;
            var vbox = new VBoxContainer();
            vbox.Size = new Vector2(880, 640);
            vbox.Position = new Vector2(20, 20);
            vbox.AddThemeConstantOverride("separation", 10);
            outer.AddChild(vbox);

            // ---- Header ----
            var header = new Label();
            header.Text = "BATTLE ENGAGEMENT  —  Terrain: " + _terrainName + " (+" + _terrainBonus + " def)";
            header.AddThemeFontSizeOverride("font_size", 20);
            header.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.4f));
            header.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(header);

            // ---- 攻击方vs防御方 ----
            var vsBox = new HBoxContainer();
            vsBox.AddThemeConstantOverride("separation", 16);
                        vbox.AddChild(vsBox);

            string atkName = _leadAttacker != null ? _leadAttacker.Name : "???";
            string defName = _leadDefender != null ? _leadDefender.Name : "???";
            string atkFaction = _leadAttacker?.Faction == 1 ? "NATO" : "WP";
            string defFaction = _leadDefender?.Faction == 1 ? "NATO" : "WP";

            var atkLbl = new Label();
            atkLbl.Text = "[" + atkFaction + "]  " + atkName;
            atkLbl.AddThemeFontSizeOverride("font_size", 16);
            atkLbl.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1.0f));
            vsBox.AddChild(atkLbl);

            var vsLbl = new Label();
            vsLbl.Text = "  vs  ";
            vsLbl.AddThemeFontSizeOverride("font_size", 16);
            vsLbl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
            vsBox.AddChild(vsLbl);

            var defLbl = new Label();
            defLbl.Text = "[" + defFaction + "]  " + defName;
            defLbl.AddThemeFontSizeOverride("font_size", 16);
            defLbl.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
            vsBox.AddChild(defLbl);

            // ---- 可用单位列表 ----
            var availLabel = new Label();
            availLabel.Text = "Available Forces  —  click a battalion, then click a slot to assign";
            availLabel.AddThemeFontSizeOverride("font_size", 13);
            availLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            vbox.AddChild(availLabel);

            var availScroll = new VBoxContainer();
            availScroll.Size = new Vector2(880, 0);
            availScroll.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(availScroll);
            var availFlow = availScroll;

            foreach (var (bat, pos) in _eligibleUnits)
            {
                var card = MakeUnitCard(bat, pos, isAvailable: true, isSelected: false, isAttackerSide: true);
                if (card != null) availFlow.AddChild(card);
            }

            // ---- 插槽行 ----
            var slotLabel = new Label();
            slotLabel.Text = "Your Deployment  —  click a filled slot to remove unit";
            slotLabel.AddThemeFontSizeOverride("font_size", 13);
            slotLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            vbox.AddChild(slotLabel);

            var slotRow = new HBoxContainer();
            slotRow.AddThemeConstantOverride("separation", 10);
                        vbox.AddChild(slotRow);

            for (int i = 0; i < 4; i++)
            {
                var (label, desc, color) = SlotDefs[i];
                var (slotBtn, slotLbl) = MakeSlotButton(i, label, desc, color);
                _slotButtons[i] = slotBtn;
                _slotLabels[i] = slotLbl;
                slotRow.AddChild(slotBtn);
            }

            // ---- 预测和按钮 ----
            vbox.AddChild(new Control { Size = new Vector2(0, 8) }); // spacer

            var btnRow = new HBoxContainer();
                        btnRow.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(btnRow);

            _cancelBtn = MakeActionButton("Cancel", new Color(0.5f, 0.2f, 0.2f));
            _cancelBtn.Pressed += () => { _selectedUnit = null; OnCancel?.Invoke(); };
            btnRow.AddChild(_cancelBtn);

            _confirmBtn = MakeActionButton("Confirm Attack", new Color(0.2f, 0.6f, 0.3f));
            _confirmBtn.Pressed += OnConfirmPressed;
            btnRow.AddChild(_confirmBtn);

            _nextBtn = null;
        }

        Button MakeUnitCard(Battalion bat, Vector2I pos, bool isAvailable, bool isSelected, bool isAttackerSide)
        {
            if (IsUnitAssigned(bat)) return null;
            if (bat == _leadDefender && !isAttackerSide) return null; // lead defender handled separately

            var btn = new Button();
            btn.Size = new Vector2(200, 70);
            string atkStr = bat.GetActualAttack().ToString("0.0");
            string defStr = bat.GetActualDefense().ToString("0.0");
            string hpStr = bat.GetTotalCurrentHp() + "/" + bat.GetTotalMaxHp();
            btn.Text = bat.Name + "\nATK " + atkStr + "  DEF " + defStr + "  HP " + hpStr;
            btn.AddThemeFontSizeOverride("font_size", 11);

            var flatStyle = new StyleBoxFlat();
            flatStyle.BgColor = isSelected ? new Color(0.3f, 0.5f, 0.7f, 0.5f) : new Color(0.15f, 0.15f, 0.2f, 0.6f);
            flatStyle.BorderWidthLeft = 1; flatStyle.BorderWidthRight = 1;
            flatStyle.BorderWidthTop = 1; flatStyle.BorderWidthBottom = 1;
            flatStyle.BorderColor = isSelected ? new Color(0.5f, 0.8f, 1.0f) : new Color(0.3f, 0.3f, 0.4f);
            flatStyle.CornerRadiusTopLeft = 4; flatStyle.CornerRadiusTopRight = 4;
            flatStyle.CornerRadiusBottomLeft = 4; flatStyle.CornerRadiusBottomRight = 4;
            btn.AddThemeStyleboxOverride("normal", flatStyle);

            btn.AddThemeColorOverride("font_color", Colors.White);

            btn.Pressed += () =>
            {
                _selectedUnit = bat;
            };

            return btn;
        }

        (Button, Label) MakeSlotButton(int slotIndex, string label, string desc, Color color)
        {
            var btn = new Button();
            btn.Size = new Vector2(205, 90);
            btn.AddThemeFontSizeOverride("font_size", 12);

            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.12f, 0.12f, 0.18f, 0.9f);
            normalStyle.BorderWidthLeft = 2; normalStyle.BorderWidthRight = 2;
            normalStyle.BorderWidthTop = 2; normalStyle.BorderWidthBottom = 2;
            normalStyle.BorderColor = color * 0.5f;
            normalStyle.CornerRadiusTopLeft = 4; normalStyle.CornerRadiusTopRight = 4;
            normalStyle.CornerRadiusBottomLeft = 4; normalStyle.CornerRadiusBottomRight = 4;
            btn.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
            hoverStyle.BorderColor = color;
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
            btn.Text = label + "\n" + desc + "\n(empty)";

            var lbl = new Label(); // hidden label to store current unit name
            lbl.Text = "";
            lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
            btn.AddChild(lbl);

            int idx = slotIndex;
            btn.Pressed += () => OnSlotClicked(idx);

            return (btn, lbl);
        }

        void OnSlotClicked(int slotIndex)
        {
            var slotForce = GetSlotBattalion(slotIndex);

            if (slotForce != null && _selectedUnit == null)
            {
                // 空点已填充的插槽 → 移除
                SetSlotBattalion(slotIndex, null);
                RebuildAvailable();
                return;
            }

            if (_selectedUnit != null && slotForce == null)
            {
                // 有选中单位，点空插槽 → 分配
                // 检查这个单位是否已被其他插槽占用
                if (IsUnitAssigned(_selectedUnit))
                {
                    _selectedUnit = null;
                    RebuildAvailable();
                    return;
                }
                bool ok = slotIndex switch { 0 => _selectedUnit.CanFillMain(), 1 => _selectedUnit.CanFillMain(), 2 => _selectedUnit.CanFillSupport(), 3 => _selectedUnit.CanFillArtillery(), _ => false };
                if (!ok) return;
                SetSlotBattalion(slotIndex, _selectedUnit);
                _selectedUnit = null;
                UpdateSlots();
                EnableConfirm();
                RebuildAvailable();
                return;
            }

            if (_selectedUnit != null && slotForce != null)
            {
                // 有选中单位，点已填充插槽 → 替换
                if (!IsUnitAssigned(_selectedUnit))
                {
                    bool ok = slotIndex switch { 0 => _selectedUnit.CanFillMain(), 1 => _selectedUnit.CanFillMain(), 2 => _selectedUnit.CanFillSupport(), 3 => _selectedUnit.CanFillArtillery(), _ => false };
                    if (!ok) return;
                    SetSlotBattalion(slotIndex, _selectedUnit);
                    _selectedUnit = null;
                    UpdateSlots();
                    EnableConfirm();
                    RebuildAvailable();
                }
            }
        }

        Battalion GetSlotBattalion(int idx)
        {
            return idx switch
            {
                0 => _attackerForce.LeadBattalion,
                1 => _attackerForce.MainSlot2,
                2 => _attackerForce.SupportSlot,
                3 => _attackerForce.ArtillerySlot,
                _ => null
            };
        }

        void SetSlotBattalion(int idx, Battalion bat)
        {
            switch (idx)
            {
                case 0: _attackerForce.LeadBattalion = bat; break;
                case 1: _attackerForce.MainSlot2 = bat; break;
                case 2: _attackerForce.SupportSlot = bat; break;
                case 3: _attackerForce.ArtillerySlot = bat; break;
            }
        }

        bool IsUnitAssigned(Battalion bat)
        {
            return _attackerForce.LeadBattalion == bat ||
                   _attackerForce.MainSlot2 == bat ||
                   _attackerForce.SupportSlot == bat ||
                   _attackerForce.ArtillerySlot == bat;
        }

        void UpdateSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                var bat = GetSlotBattalion(i);
                var (label, desc, color) = SlotDefs[i];

                if (bat != null)
                {
                    string atkStr = bat.GetActualAttack().ToString("0.0");
                    string defStr = bat.GetActualDefense().ToString("0.0");
                    _slotButtons[i].Text = label + "\n" + bat.Name + "\nATK " + atkStr + "  DEF " + defStr;
                    _slotButtons[i].AddThemeColorOverride("font_color", Colors.White);
                }
                else
                {
                    _slotButtons[i].Text = label + "\n" + desc + "\n(empty)";
                    _slotButtons[i].AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
                }
            }
        }

        void EnableConfirm()
        {
            _confirmBtn.Disabled = _attackerForce.LeadBattalion == null && _attackerForce.MainSlot2 == null;
        }

        void RebuildAvailable()
        {
            if (_contentRoot == null) return;
            BuildUI(isResult: false);
            EnableConfirm();
            UpdateSlots();
        }

        void OnConfirmPressed()
        {
            if (_attackerForce.LeadBattalion == null && _attackerForce.MainSlot2 == null) return;
            OnAttackerConfirmed?.Invoke(_attackerForce);
        }

        // ===== 防御方展示 =====
        public void ShowDefenderPreview(CombatForce defenderForce)
        {
            _defenderForce = defenderForce;
            RemoveContent();

            var outer = new Panel();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.08f, 0.12f);
            style.CornerRadiusTopLeft = 8; style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8; style.CornerRadiusBottomRight = 8;
            outer.AddThemeStyleboxOverride("panel", style);
            outer.Size = new Vector2(600, 400);
            var vpSize = GetViewport().GetVisibleRect().Size;
            outer.Position = new Vector2((vpSize.X - 600) / 2, (vpSize.Y - 400) / 2);

            AddChild(outer);
            var vbox = new VBoxContainer();
            vbox.Position = new Vector2(20, 20);
            vbox.AddThemeConstantOverride("separation", 10);
            outer.AddChild(vbox);

            var header = new Label();
            header.Text = "Defender Deployment (Auto-Filled)";
            header.AddThemeFontSizeOverride("font_size", 18);
            header.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.4f));
            header.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(header);

            string defFaction = _leadDefender?.Faction == 1 ? "NATO" : "WP";
            var defLabel = new Label();
            defLabel.Text = "[" + defFaction + "] " + (_leadDefender?.Name ?? "???");
            defLabel.AddThemeFontSizeOverride("font_size", 15);
            defLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
            defLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(defLabel);

            // Show defender slots
            string[] defSlotNames = { "MAIN 1", "MAIN 2", "SUPPORT", "ARTILLERY" };
            Battalion[] defSlots = { defenderForce.LeadBattalion, defenderForce.MainSlot2,
                                     defenderForce.SupportSlot, defenderForce.ArtillerySlot };

            for (int i = 0; i < 4; i++)
            {
                var slot = defSlots[i];
                if (slot == null) continue;
                string atkS = slot.GetActualAttack().ToString("0.0");
                string defS = slot.GetActualDefense().ToString("0.0");
                var sl = new Label();
                sl.Text = "  " + defSlotNames[i] + ": " + slot.Name + "  (ATK " + atkS + "  DEF " + defS + ")";
                sl.AddThemeFontSizeOverride("font_size", 13);
                sl.AddThemeColorOverride("font_color", Colors.White);
                vbox.AddChild(sl);
            }

            vbox.AddChild(new Control { Size = new Vector2(0, 10) });

            var resolveBtn = MakeActionButton("Resolve Combat", new Color(0.8f, 0.3f, 0.2f));
            resolveBtn.Pressed += () => OnResolvePressed?.Invoke();
            vbox.AddChild(resolveBtn);
            _nextBtn = resolveBtn;
        }

        public Action OnResolvePressed;

        // ===== 结算结果展示 =====
        public void ShowResult(CombatResolutionResult result)
        {
            RemoveContent();

            var outer = new Panel();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.08f, 0.12f);
            style.CornerRadiusTopLeft = 8; style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8; style.CornerRadiusBottomRight = 8;
            outer.AddThemeStyleboxOverride("panel", style);
            outer.Size = new Vector2(700, 520);
            var vpSize = GetViewport().GetVisibleRect().Size;
            outer.Position = new Vector2((vpSize.X - 700) / 2, (vpSize.Y - 520) / 2);
            AddChild(outer);
            _resultRoot = outer;
            var vbox = new VBoxContainer();
            vbox.Size = new Vector2(660, 480);
            vbox.Position = new Vector2(20, 20);
            vbox.AddThemeConstantOverride("separation", 6);
            outer.AddChild(vbox);

            // Result header
            string vicLevel = GetVictoryLevelName(result.Advantage.Value);
            var hl = new Label();
            hl.Text = "COMBAT RESULT  —  V = " + result.Advantage.Value.ToString("+0.00;-0.00") + "  (" + vicLevel + ")";
            hl.AddThemeFontSizeOverride("font_size", 20);
            hl.AddThemeColorOverride("font_color", result.Advantage.Value >= 0 ? new Color(0.3f, 1.0f, 0.3f) : new Color(1.0f, 0.3f, 0.3f));
            hl.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(hl);

            // Casualties
            var casHeader = new Label();
            casHeader.Text = "Casualties";
            casHeader.AddThemeFontSizeOverride("font_size", 14);
            casHeader.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            vbox.AddChild(casHeader);

            string atkFaction = _leadAttacker?.Faction == 1 ? "NATO" : "WP";
            string defFaction = _leadDefender?.Faction == 1 ? "NATO" : "WP";

            var atkCas = new Label();
            atkCas.Text = atkFaction + " loses " + result.AttackerHpLost + " HP (fatigue +" + result.AttackerFatigueGained + ")";
            atkCas.AddThemeFontSizeOverride("font_size", 13);
            atkCas.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.4f));
            vbox.AddChild(atkCas);

            int atkDestroyed = result.AttackerCasualties.Count(c => c.IsDestroyed);
            var atkDet = new Label();
            atkDet.Text = "  " + atkDestroyed + " sub-units destroyed, " +
                (result.AttackerCasualties.Count - atkDestroyed) + " damaged";
            atkDet.AddThemeFontSizeOverride("font_size", 11);
            atkDet.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.5f));
            vbox.AddChild(atkDet);

            var defCas = new Label();
            defCas.Text = defFaction + " loses " + result.DefenderHpLost + " HP (fatigue +" + result.DefenderFatigueGained + ")";
            defCas.AddThemeFontSizeOverride("font_size", 13);
            defCas.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 1.0f));
            vbox.AddChild(defCas);

            int defDestroyed = result.DefenderCasualties.Count(c => c.IsDestroyed);
            var defDet = new Label();
            defDet.Text = "  " + defDestroyed + " sub-units destroyed, " +
                (result.DefenderCasualties.Count - defDestroyed) + " damaged";
            defDet.AddThemeFontSizeOverride("font_size", 11);
            defDet.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.5f));
            vbox.AddChild(defDet);

            // Modifiers
            var modHeader = new Label();
            modHeader.Text = "Modifiers Applied";
            modHeader.AddThemeFontSizeOverride("font_size", 14);
            modHeader.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            vbox.AddChild(modHeader);

            int modCount = 0;
            foreach (var m in result.Advantage.Modifiers)
            {
                if (Math.Abs(m.Value) < 0.01f) continue;
                var ml = new Label();
                string sign = m.Value > 0 ? "+" : "";
                ml.Text = "  " + m.Source + ": " + sign + m.Value.ToString("0.00") + "  (" + m.Reason + ")";
                ml.AddThemeFontSizeOverride("font_size", 11);
                ml.AddThemeColorOverride("font_color", m.Value >= 0 ? new Color(0.5f, 1.0f, 0.5f) : new Color(1.0f, 0.5f, 0.5f));
                vbox.AddChild(ml);
                modCount++;
            }
            if (modCount == 0)
            {
                var nl = new Label();
                nl.Text = "  (no significant modifiers)";
                nl.AddThemeFontSizeOverride("font_size", 11);
                nl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
                vbox.AddChild(nl);
            }

            vbox.AddChild(new Control { Size = new Vector2(0, 8) });

            var dismissBtn = MakeActionButton("Dismiss — Press Esc", new Color(0.15f, 0.6f, 0.3f));
            dismissBtn.Pressed += () => OnResultDismissed?.Invoke();
            vbox.AddChild(dismissBtn);
        }

        string GetVictoryLevelName(float v)
        {
            if (v >= 1.5f) return "Decisive Victory";
            if (v >= 1.0f) return "Major Victory";
            if (v >= 0.5f) return "Marginal Victory";
            if (v >= 0.0f) return "Stalemate";
            if (v >= -0.5f) return "Marginal Defeat";
            if (v >= -1.0f) return "Major Defeat";
            return "Crushing Defeat";
        }

        Button MakeActionButton(string text, Color color)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Size = new Vector2(240, 40);
            btn.AddThemeFontSizeOverride("font_size", 14);

            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = color;
            normalStyle.CornerRadiusTopLeft = 4; normalStyle.CornerRadiusTopRight = 4;
            normalStyle.CornerRadiusBottomLeft = 4; normalStyle.CornerRadiusBottomRight = 4;
            btn.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
            hoverStyle.BgColor = color.Lerp(new Color(1, 1, 1), 0.2f);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.AddThemeColorOverride("font_color", Colors.White);
            return btn;
        }

        public void RemoveContent()
        {
            if (_contentRoot != null) { if (_contentRoot.GetParent() == this) { RemoveChild(_contentRoot); _contentRoot.QueueFree(); } _contentRoot = null; }
            if (_resultRoot != null) { if (_resultRoot.GetParent() == this) { RemoveChild(_resultRoot); _resultRoot.QueueFree(); } _resultRoot = null; }
        }

       public void Dismiss()
        {
            RemoveContent();
            QueueFree();
        }
        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                if (_resultRoot == null) return;
                OnResultDismissed?.Invoke();
            }
        }

    }
}
