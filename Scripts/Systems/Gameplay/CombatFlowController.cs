using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Rendering;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Turns;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class CombatFlowController
    {
        private readonly CanvasLayer _canvasLayer;
        private readonly GameHud _hud;
        private readonly Grid3DRenderer _renderer;
        private readonly FuldaGapScenario _scenario;
        private readonly TurnManager _turnMgr;
        private readonly CombatResolver _resolver;
        private readonly CombatDeploymentPresenter _presenter;

        public CombatFlowController(
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
            _presenter = new CombatDeploymentPresenter(canvasLayer, hud, renderer, scenario, turnMgr, resolver);
        }

        public bool IsActive => _presenter.IsActive;

        public void StartCombat(
            Battalion attacker,
            Battalion defender,
            Vector2I defenderPos,
            Action<CombatForce, CombatForce, CombatResolutionResult> onResolved,
            Action onCancelled,
            Action onClosed)
        {
            var friendlyUnits = (_turnMgr.CurrentFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions)
                .Where(u => u.Item1 != attacker)
                .ToList();
            var eligible = EngagementResolver.GetEligibleUnits(defenderPos, friendlyUnits, 2);
            eligible = eligible.Where(u => u.bat.CurrentAP >= 4).ToList();
            eligible.Insert(0, (attacker, new Vector2I(defenderPos.X, defenderPos.Y)));

            var artySupports = friendlyUnits
                .Where(u => u.bat.GetArtilleryRange() > 0
                    && (u.Item2.X - defenderPos.X) * (u.Item2.X - defenderPos.X) + (u.Item2.Y - defenderPos.Y) * (u.Item2.Y - defenderPos.Y) <= u.bat.GetArtilleryRange() * u.bat.GetArtilleryRange()
                    && u.bat.CurrentAP >= 4
                    && !eligible.Any(e => e.bat == u.bat))
                .ToList();
            eligible.InsertRange(0, artySupports);

            float terrainBonus = _scenario.Map.GetTile(defenderPos).TerrainType switch { 1 => 0.1f, 2 => 0.3f, 3 => 0.4f, _ => 0f };
            string[] terrainNames = { "Plains", "Forest", "Semi-Urban", "Urban" };
            int terrainType = _scenario.Map.GetTile(defenderPos).TerrainType;
            string terrainName = terrainType >= 0 && terrainType < terrainNames.Length ? terrainNames[terrainType] : "??";

            _turnMgr.InitiateCombat(attacker, defender, new CombatContext
            {
                DefenderTerrainBonus = terrainBonus,
                AttackerFaction = attacker.Faction,
                DefenderFaction = defender.Faction,
                AttackerOOSTurns = attacker.TurnsOOS,
                DefenderOOSTurns = defender.TurnsOOS
            });

            _presenter.StartCombatFlow(
                attacker,
                defender,
                defenderPos,
                eligible,
                terrainBonus,
                terrainName,
                onResolved,
                onCancelled,
                onClosed);
        }
    }
}
