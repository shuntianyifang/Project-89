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

            _panel.OnAttackerConfirmed = (CombatForce attackerForce) =>
            {
                _attackerStored = attackerForce;
                var defEligible = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)
                    .Where(u => u.Item1 != defender)
                    .ToList();
                defEligible = defEligible
                    .Where(u => Math.Max(Math.Abs(u.Item2.X - defenderPos.X), Math.Abs(u.Item2.Y - defenderPos.Y)) <= 2 && u.Item1.CurrentAP >= 4)
                    .ToList();

                var defArtySupports = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)
                    .Where(u => u.Item1 != defender && u.Item1.GetArtilleryRange() > 0
                        && (u.Item2.X - defenderPos.X) * (u.Item2.X - defenderPos.X) + (u.Item2.Y - defenderPos.Y) * (u.Item2.Y - defenderPos.Y) <= u.Item1.GetArtilleryRange() * u.Item1.GetArtilleryRange()
                        && u.Item1.CurrentAP >= 4
                        && !defEligible.Any(e => e.Item1 == u.Item1))
                    .ToList();
                defEligible.AddRange(defArtySupports);

                _defenderStored = CombatAutoDeployer.AutoFillForce(defEligible, defender);
                _panel.RemoveContent();
                _panel.ShowDefenderPreview(_defenderStored);
            };

            _panel.OnResolvePressed = () =>
            {
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

            _panel.OnResultDismissed = () =>
            {
                Dismiss();
                onClosed?.Invoke();
            };

            _panel.OnCancel = () =>
            {
                Dismiss();
                onCancelled?.Invoke();
            };

            _panel.ShowAttackerPhase(attacker, defender, eligibleUnits, (int)(terrainBonus * 10), terrainName);
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
