using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Gameplay;
using ColdWarWargame.Systems.Supply;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Systems.Victory;

namespace ColdWarWargame.Tests.Gameplay
{
    public static class CampaignTests
    {
        private static int _fails;
        private static void Assert(bool ok, string message)
        {
            if (ok) GD.Print("[CAMPAIGN PASS] " + message);
            else { _fails++; GD.PrintErr("[CAMPAIGN FAIL] " + message); }
        }
        private static Battalion Unit(int faction, string id = "test") => new() {
            InstanceId = id, Faction = faction, CurrentAP = 12,
            Companies = new() { new Company { Platoons = new() { new Platoon {
                Units = new() { new SubUnitInstance("us_mech_rifles") }
            } } } }
        };

        private static void TestMovementCombatSettlement()
        {
            var map = new ColdWarWargame.Systems.Battlefield.GridMap(10, 10);
            var zoc = new ZOCManager(map);
            var movement = new MovementResolver(map);
            var blue = Unit(1, "blue"); var red = Unit(2, "red");
            var target = new Vector2I(5, 4);
            var units = new List<(Battalion bat, Vector2I pos)> { (blue, new Vector2I(2, 4)), (red, target) };
            red.Fatigue = 8;
            Assert(zoc.GetFactionZOC(units.Where(u => u.bat.Faction == 2)).Contains(new Vector2I(4, 4)), "Fatigue 8 still projects ZOC");
            red.Fatigue = 9; red.CurrentAP = 0;
            var enemyZoc = zoc.GetFactionZOC(units.Where(u => u.bat.Faction == 2));
            var reachable = movement.GetReachableTiles(units[0].pos, blue.CurrentAP, enemyZoc.Contains, p => p == target, blue);
            Assert(reachable.ContainsKey(new Vector2I(4, 4)) && !reachable.ContainsKey(target), "Disorganized enemy loses ZOC but still blocks its own tile");
            blue.CurrentAP -= reachable[new Vector2I(4, 4)]; units[0] = (blue, new Vector2I(4, 4));
            var turns = new TurnManager(); turns.RegisterBattalion(blue); turns.RegisterBattalion(red);
            var ctx = new CombatContext();
            turns.InitiateCombat(blue, red, ctx); turns.FinishAttackerDeployment();
            var result = new CombatResolver().ResolveCombat(blue, red, ctx, 42);
            turns.CompleteCombatResolution();
            blue.CurrentAP = Math.Max(0, blue.CurrentAP - 4);
            blue.Fatigue += result.AttackerFatigueGained;
            var victory = new VictoryTracker(); victory.RecordCombatResult(result, 1);
            new SupplyManager().UpdateFactionEndTurn(1, map, units, new() { target }, enemyZoc);
            turns.EndStrategicTurn();
            Assert(turns.CurrentFaction == 2 && turns.TurnNumber == 1, "Move, combat and settlement return control to Red");
            turns.EndStrategicTurn();
            Assert(turns.CurrentFaction == 1 && turns.TurnNumber == 2 && blue.CurrentAP == blue.GetMaxAP(), "Both factions finish a round and Blue AP refreshes");
            Assert(victory.CombatCount == 1, "Combat recorded exactly once");
            var control = new int[10,10];
            var activeBlue = zoc.GetFactionZOC(new[] { units[0] });
            var occupation = victory.UpdateOccupationFromEntryAndZOC(map, control,
                new() { units[0].pos }, new() { target }, zoc, blueActiveZoc: activeBlue, redActiveZoc: enemyZoc);
            Assert(occupation[6,4] == 0 && occupation[5,4] == 2, "Disorganized enemy occupies own tile without capturing neighboring tiles");
        }

        private static void TestThresholdAndTermination()
        {
            var b = Unit(1); var u = b.GetAllSubUnits().Single();
            u.CurrentHp = 1;
            Assert(u.SurvivalState == 1, "Sub-unit lives below 30% until HP reaches zero");
            Assert(!CampaignResult.HasLivingBattalions(new[] { b }), "Whole battalion below 30% is eliminated");
            var dead = new CombatResolver().ResolveCombat(new List<Battalion> { b }, new List<Battalion> { Unit(2) }, new(), 7);
            Assert(u.CurrentHp == 0 && dead.AttackerCasualties.Any(c => c.IsDestroyed), "Multi-battalion resolution records elimination even with zero rounded damage");
            var rules = new GameSessionRules();
            rules.DeclareDraw();
            foreach (GameAction action in Enum.GetValues<GameAction>())
                Assert(!rules.IsActionAllowed(GameFlowController.GameState.Selecting, action), "Ended campaign blocks " + action);
            var result = new CampaignResult(new VictoryTracker(), 3, "手动结束");
            Assert(result.EndEvent == GameplayEventType.MatchDrawn && result.Assessment.TurnNumber == 3, "Zero VP manual ending produces stalemate");
        }

        private static void TestSaveRoundTrip()
        {
            var scenario = new FuldaGapScenario();
            scenario.LoadOOB("res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json", "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
            var turns = new TurnManager(); var victory = new VictoryTracker();
            var original = scenario.BlueBattalions[0].bat;
            original.CurrentAP = 5.5f; original.Fatigue = 6; original.TurnsOOS = 1; original.WasOOSLastTurn = true;
            var firstUnit = original.GetAllSubUnits().First(); firstUnit.CurrentHp = 1;
            turns.RestoreStrategicState(2, 4);
            victory.RestoreStatistics(new[] { 123, 456, 10, 20, 30, 40, 5 });
            var save = CampaignSave.Capture(scenario, turns, victory);
            var path = "user://campaign-test-" + Guid.NewGuid() + ".json";
            try
            {
                save.Write(path);
                original.CurrentAP = 0; original.Companies.Clear(); scenario.RedBattalions.Clear();
                CampaignSave.Read(path).Apply(scenario, turns, victory);
                var restored = scenario.BlueBattalions[0].bat;
                Assert(restored.CurrentAP == 5.5f && restored.Fatigue == 6 && restored.WasOOSLastTurn && restored.TurnsOOS == 1,
                    "Save restores AP, fatigue and supply state");
                Assert(restored.GetAllSubUnits().First().CurrentHp == 1 && scenario.RedBattalions.Count > 0,
                    "Save restores sub-unit HP, structure and both factions");
                Assert(turns.CurrentFaction == 2 && turns.TurnNumber == 4 && victory.CaptureStatistics().SequenceEqual(save.Statistics),
                    "Save restores turn, faction, VP and casualty statistics");
                Assert(scenario.GetOccupationMap().Cast<int>().SequenceEqual(save.Control), "Save restores exact occupation map");
                var bad = CampaignSave.Read(path); bad.Units[0].X = -1;
                bool rejected = false;
                try { bad.Apply(scenario, turns, victory); } catch (InvalidOperationException) { rejected = true; }
                Assert(rejected && scenario.BlueBattalions[0].bat == restored, "Invalid save rejected without changing live game");
            }
            finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path)); }
        }

        private static void TestSessionLifecycle()
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var owner = tree.Root.GetNode<global::GameManager>("GameManager");
            var root = new Node(); owner.AddChild(root);
            var host = new GameSessionHost(root, owner);
            try
            {
                host.Start();
                var firstCount = host.Scenario.BlueBattalions.Count;
                host.Session.OnEndTurn(); host.Session.OnEndTurn();
                Assert(host.TurnManager.TurnNumber == 2, "Real session supports consecutive hotseat turns");
                host.Session.OnEndCampaign();
                var result = host.Session.Result;
                host.Session.OnEndTurn(); host.Session.OnEndCampaign();
                Assert(result != null && host.Session.Result == result && host.TurnManager.TurnNumber == 2,
                    "Real session ending freezes turns and repeated settlement");
                host.Shutdown(); host.Start();
                Assert(host.Session.Result == null && host.TurnManager.TurnNumber == 1 &&
                    host.Scenario.BlueBattalions.Count == firstCount, "Restart restores initial deployment and clears result");
                host.Scenario.RedBattalions.Clear();
                host.Session.OnEndTurn();
                Assert(host.Session.Result != null && host.Session.Result.Reason.Contains("华约") &&
                    host.TurnManager.CurrentFaction == 1, "Eliminated faction automatically ends real session before turn switches");
            }
            finally { host.Shutdown(); owner.RemoveChild(root); root.QueueFree(); }
        }

        public static int RunAll()
        {
            _fails = 0;
            TestMovementCombatSettlement(); TestThresholdAndTermination(); TestSaveRoundTrip(); TestSessionLifecycle();
            if (_fails == 0) GD.Print("All CampaignTests passed");
            else GD.PrintErr(_fails + " CampaignTests FAILED");
            return _fails;
        }
    }
}
