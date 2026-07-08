using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.UI;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class CombatDeploymentPresenter
    {
        private readonly CanvasLayer _canvasLayer;
        private readonly GameHud _hud;
        private readonly Grid3DRenderer _renderer;
        private readonly FuldaGapScenario _scenario;
        private readonly TurnManager _turnMgr;
        private readonly CombatResolver _resolver;

        private CombatDeploymentPanel _panel;
        private CombatForce _attackerStored;
        private CombatForce _defenderStored;
        private bool _isActive;

        public CombatDeploymentPresenter(
            CanvasLayer canvasLayer,
            GameHud hud,
            Grid3DRenderer renderer,
            FuldaGapScenario scenario,
            TurnManager turnMgr,
            CombatResolver resolver)
        {
            _canvasLayer = canvasLayer;
            _hud = hud;
            _renderer = renderer;
            _scenario = scenario;
            _turnMgr = turnMgr;
            _resolver = resolver;
        }

        public bool IsActive => _isActive;

        public void StartCombatFlow(
            Battalion attacker,
            Battalion defender,
            Vector2I defenderPos,
            List<(Battalion bat, Vector2I pos)> eligibleUnits,
            float terrainBonus,
            string terrainName,
            Action<CombatForce, CombatForce, CombatResolutionResult> onResolved,
            Action onCancelled,
            Action onClosed)
        {
            _isActive = true;
            _panel = new CombatDeploymentPanel();
            _canvasLayer.AddChild(_panel);
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            var defenderAutoEligible = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)
                .Where(u => u.Item1 != defender)
                .Where(u => Math.Max(Math.Abs(u.Item2.X - defenderPos.X), Math.Abs(u.Item2.Y - defenderPos.Y)) <= 2 && u.Item1.CurrentAP >= 4)
                .ToList();
            var defenderArtySupportsForAuto = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)
                .Where(u => u.Item1 != defender && u.Item1.GetArtilleryRange() > 0
                    && (u.Item2.X - defenderPos.X) * (u.Item2.X - defenderPos.X) + (u.Item2.Y - defenderPos.Y) * (u.Item2.Y - defenderPos.Y) <= u.Item1.GetArtilleryRange() * u.Item1.GetArtilleryRange()
                    && u.Item1.CurrentAP >= 4
                    && !defenderAutoEligible.Any(e => e.Item1 == u.Item1))
                .ToList();
            defenderAutoEligible.AddRange(defenderArtySupportsForAuto);

            _panel.OnAttackerConfirmed = (CombatForce attackerForce) =>
            {
                _attackerStored = CloneForce(attackerForce);

                _turnMgr.FinishAttackerDeployment();
                _panel.ShowDefenderPhase(
                    _attackerStored,
                    attacker,
                    defender,
                    defenderAutoEligible,
                    (int)(terrainBonus * 10),
                    terrainName);
            };

            _panel.OnDefenderConfirmed = (CombatForce defenderForce) =>
            {
                _defenderStored = CloneForce(defenderForce);
                _turnMgr.CompleteCombatResolution();

                var ctx = new CombatContext
                {
                    DefenderTerrainBonus = terrainBonus,
                    AttackerOOSTurns = attacker.TurnsOOS,
                    DefenderOOSTurns = defender.TurnsOOS,
                    AttackerBattalionOOSTurns = _attackerStored.GetAllBattalions().Select(b => b.TurnsOOS).ToList(),
                    DefenderBattalionOOSTurns = _defenderStored.GetAllBattalions().Select(b => b.TurnsOOS).ToList()
                };

                var result = _resolver.ResolveCombat(
                    _attackerStored.GetAllBattalions(),
                    _defenderStored.GetAllBattalions(),
                    ctx);

                foreach (var b in _attackerStored.GetAllBattalions())
                {
                    b.Fatigue = Math.Min(Battalion.FatigueOverflowCap, b.Fatigue + result.AttackerFatigueGained);
                    b.CurrentAP = Math.Max(0, b.CurrentAP - 4);
                }

                foreach (var b in _defenderStored.GetAllBattalions())
                {
                    b.Fatigue = Math.Min(Battalion.FatigueOverflowCap, b.Fatigue + result.DefenderFatigueGained);
                    b.CurrentAP = Math.Max(0, b.CurrentAP - 4);
                }

                _panel.RemoveContent();
                _panel.ShowResult(result);
                onResolved?.Invoke(_attackerStored, _defenderStored, result);
            };

            _panel.OnPreviewChanged = (atkForce, defForce, isDefenderPhase) =>
            {
                if (atkForce == null || atkForce.GetAllBattalions().Count == 0)
                    return;

                CombatForce effectiveDefForce = defForce;
                if (!isDefenderPhase)
                {
                    effectiveDefForce = CombatAutoDeployer.AutoFillForce(defenderAutoEligible, defender);
                    _panel.ShowOpponentPreview("对方自动填充（预估）：\n" + string.Join("\n", FormatForceLines(effectiveDefForce)));
                }
                else
                {
                    _panel.ShowOpponentPreview("对方锁定部署（真实）：\n" + string.Join("\n", FormatForceLines(_attackerStored)));
                }

                if (effectiveDefForce == null || effectiveDefForce.GetAllBattalions().Count == 0)
                    return;

                var ctx = new CombatContext
                {
                    DefenderTerrainBonus = terrainBonus,
                    AttackerOOSTurns = attacker.TurnsOOS,
                    DefenderOOSTurns = defender.TurnsOOS,
                    AttackerBattalionOOSTurns = atkForce.GetAllBattalions().Select(b => b.TurnsOOS).ToList(),
                    DefenderBattalionOOSTurns = effectiveDefForce.GetAllBattalions().Select(b => b.TurnsOOS).ToList()
                };

                var preview = _resolver.PreviewCombat(
                    atkForce.GetAllBattalions(),
                    effectiveDefForce.GetAllBattalions(),
                    ctx);
                _panel.ShowDeploymentPreview(preview, isDefenderPhase);
            };

            _panel.OnResultDismissed = () =>
            {
                Dismiss();
                onClosed?.Invoke();
            };

            _panel.OnCancel = () =>
            {
                _turnMgr.CancelCombat();
                Dismiss();
                onCancelled?.Invoke();
            };

            _panel.ShowAttackerPhase(attacker, defender, eligibleUnits, (int)(terrainBonus * 10), terrainName);
        }

        private static CombatForce CloneForce(CombatForce src)
        {
            return new CombatForce
            {
                LeadBattalion = src?.LeadBattalion,
                MainSlot2 = src?.MainSlot2,
                SupportSlot = src?.SupportSlot,
                ArtillerySlot = src?.ArtillerySlot
            };
        }

        private static IEnumerable<string> FormatForceLines(CombatForce force)
        {
            yield return "MAIN 1: " + (force?.LeadBattalion?.Name ?? "(empty)");
            yield return "MAIN 2: " + (force?.MainSlot2?.Name ?? "(empty)");
            yield return "SUPPORT: " + (force?.SupportSlot?.Name ?? "(empty)");
            yield return "ARTILLERY: " + (force?.ArtillerySlot?.Name ?? "(empty)");
        }

        public void Dismiss()
        {
            if (_panel != null)
            {
                _panel.Dismiss();
                _panel = null;
            }
            _isActive = false;
            _renderer.SetBlueUnits(_scenario.BlueBattalions);
            _renderer.SetRedUnits(_scenario.RedBattalions);
        }
    }
}
