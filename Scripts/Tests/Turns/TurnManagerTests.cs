using System;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Turns;
using Godot;

namespace ColdWarWargame.Tests.Turns
{
    public static class TurnManagerTests
    {
        static int _fails = 0;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[TURN FAIL] " + msg); }
            else { GD.Print("[TURN PASS] " + msg); }
        }

        static void AssertFloat(float actual, float expected, string msg, float eps = 0.01f)
        {
            bool ok = Math.Abs(actual - expected) < eps;
            if (!ok) { _fails++; GD.PrintErr("[TURN FAIL] " + msg + ": expected " + expected + ", got " + actual); }
            else { GD.Print("[TURN PASS] " + msg + ": " + actual); }
        }

        static Battalion MakeBat(string name, int faction, float ap = 12f) =>
            new Battalion { Name = name, Faction = faction, CurrentAP = ap };

        static Battalion MakeFullBat(string name, int faction, params string[] unitIds)
        {
            var bat = new Battalion { Name = name, Faction = faction, CurrentAP = 12f };
            var comp = new Company { Name = "C1" };
            var pl = new Platoon { PlatoonId = "P1" };
            foreach (var id in unitIds) pl.Units.Add(new SubUnitInstance(id));
            comp.Platoons.Add(pl); bat.Companies.Add(comp);
            return bat;
        }

        static void Test_InitialState()
        {
            var tm = new TurnManager();
            Assert(tm.CurrentFaction == 1, "Initial: Blue faction (1)");
            Assert(tm.CurrentPhase == TurnManager.GamePhase.StrategicMovement, "Initial: StrategicMovement");
            Assert(tm.TurnNumber == 1, "Initial: Turn 1");
        }

        static void Test_EndStrategicTurn_SwitchesFaction()
        {
            var tm = new TurnManager();
            var blue = MakeBat("Blue", 1, 5f);
            var red = MakeBat("Red", 2, 3f);
            tm.RegisterBattalion(blue); tm.RegisterBattalion(red);
            tm.EndStrategicTurn();
            Assert(tm.CurrentFaction == 2, "Blue ends turn -> Red active");
            AssertFloat(red.CurrentAP, 12f, "Red AP reset to 12");
            AssertFloat(blue.CurrentAP, 5f, "Blue AP unchanged (5)");
            Assert(tm.TurnNumber == 1, "Turn stays 1 (Red hasn't gone yet)");
            tm.EndStrategicTurn();
            Assert(tm.CurrentFaction == 1, "Red ends turn -> Blue active");
            Assert(tm.TurnNumber == 2, "Both factions acted -> Turn 2");
        }

        static void Test_InvalidTransitionsThrow()
        {
            var tm = new TurnManager();
            bool threw = false;
            try { tm.FinishAttackerDeployment(); } catch (InvalidOperationException) { threw = true; }
            Assert(threw, "FinishAttackerDeployment throws in StrategicMovement");
            threw = false;
            try { tm.FinishDefenderDeployment(null); } catch (InvalidOperationException) { threw = true; }
            Assert(threw, "FinishDefenderDeployment throws in StrategicMovement");
            var blue1 = MakeBat("Blue1", 1); var blue2 = MakeBat("Blue2", 1);
            threw = false;
            try { tm.InitiateCombat(blue1, blue2, new CombatContext()); } catch (InvalidOperationException) { threw = true; }
            Assert(threw, "InitiateCombat with same faction throws");
        }

        static void Test_CombatFullFlow()
        {
            var tm = new TurnManager();
            var atk = MakeFullBat("BlueMech", 1, "us_mech_rifles");
            var def = MakeFullBat("RedInf", 2, "sov_motostrelkovy");
            var ctx = new CombatContext { DefenderTerrainBonus = 0.1f };
            tm.InitiateCombat(atk, def, ctx);
            Assert(tm.CurrentPhase == TurnManager.GamePhase.CombatDeployment_Attacker, "Initiate -> AttackerDeployment");
            tm.FinishAttackerDeployment();
            Assert(tm.CurrentPhase == TurnManager.GamePhase.CombatDeployment_Defender, "AttackerDone -> DefenderDeployment");
            Assert(tm.CurrentFaction == 2, "Switched to defender faction (2)");
            var resolver = new CombatResolver();
            var result = tm.FinishDefenderDeployment(resolver);
            Assert(tm.CurrentPhase == TurnManager.GamePhase.StrategicMovement, "Combat done -> StrategicMovement");
            Assert(tm.CurrentFaction == 1, "Back to initiating faction (1)");
            Assert(result != null, "Combat result exists");
        }

        static void Test_CombatManualCompleteFlow()
        {
            var tm = new TurnManager();
            var atk = MakeFullBat("BlueMech", 1, "us_mech_rifles");
            var def = MakeFullBat("RedInf", 2, "sov_motostrelkovy");
            var ctx = new CombatContext { DefenderTerrainBonus = 0.1f };

            tm.InitiateCombat(atk, def, ctx);
            tm.FinishAttackerDeployment();
            Assert(tm.CurrentPhase == TurnManager.GamePhase.CombatDeployment_Defender, "Manual flow: in defender deployment");
            Assert(tm.CurrentFaction == 2, "Manual flow: current faction switched to defender");

            tm.CompleteCombatResolution();
            Assert(tm.CurrentPhase == TurnManager.GamePhase.StrategicMovement, "Manual flow complete -> StrategicMovement");
            Assert(tm.CurrentFaction == 1, "Manual flow complete -> back to attacker faction");
        }

        static void Test_CombatCancelFlow()
        {
            var tm = new TurnManager();
            var atk = MakeFullBat("BlueMech", 1, "us_mech_rifles");
            var def = MakeFullBat("RedInf", 2, "sov_motostrelkovy");

            tm.InitiateCombat(atk, def, new CombatContext());
            tm.CancelCombat();

            Assert(tm.CurrentPhase == TurnManager.GamePhase.StrategicMovement, "Cancel flow -> StrategicMovement");
            Assert(tm.CurrentFaction == 1, "Cancel flow -> attacker faction restored");
        }

        static void Test_MultipleTurns()
        {
            var tm = new TurnManager();
            var blue = MakeBat("Blue", 1); var red = MakeBat("Red", 2);
            tm.RegisterBattalion(blue); tm.RegisterBattalion(red);
            for (int i = 0; i < 4; i++) tm.EndStrategicTurn();
            Assert(tm.TurnNumber == 3, "4 end-turn calls -> Turn 3");
            Assert(tm.CurrentFaction == 1, "After 4 end-turns -> Blue active");
        }

        public static void RunAll()
        {
            _fails = 0;
            GD.Print("--- TurnManager 测试 ---");
            Test_InitialState();
            Test_EndStrategicTurn_SwitchesFaction();
            Test_InvalidTransitionsThrow();
            Test_CombatFullFlow();
            Test_CombatManualCompleteFlow();
            Test_CombatCancelFlow();
            Test_MultipleTurns();
            if (_fails == 0) GD.Print("All TurnManagerTests passed");
            else GD.PrintErr(_fails + " TurnManagerTests FAILED");
        }
    }
}
