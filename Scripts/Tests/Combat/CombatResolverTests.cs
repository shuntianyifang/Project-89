using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Combat;
using Godot;

namespace ColdWarWargame.Tests.Combat
{
    public static class CombatResolverTests
    {
        static int fails = 0;
        static void Assert(bool cond, string msg)
        {
            if (!cond) { fails++; GD.PrintErr("[TEST FAIL] " + msg); }
            else { GD.Print("[TEST PASS] " + msg); }
        }

        static void AssertFloat(float actual, float expected, string msg, float eps = 0.01f)
        {
            bool ok = Math.Abs(actual - expected) < eps;
            if (!ok) { fails++; GD.PrintErr("[TEST FAIL] " + msg + ": expected " + expected + ", got " + actual); }
            else { GD.Print("[TEST PASS] " + msg + ": " + actual); }
        }

        static Battalion MakeBatWithUnitIds(params string[] unitIds)
        {
            var bat = new Battalion { Name = "TestBat", Faction = 1 };
            var comp = new Company { Name = "C1" };
            var pl = new Platoon { PlatoonId = "P1" };
            foreach (var id in unitIds)
                pl.Units.Add(new SubUnitInstance(id));
            comp.Platoons.Add(pl);
            bat.Companies.Add(comp);
            return bat;
        }

        static Battalion MakeEngineerBat(params string[] unitIds)
        {
            var bat = MakeBatWithUnitIds(unitIds);
            bat.BattalionTags.Add("Engineer");
            return bat;
        }

        static void Test_TerrainPlain()
        {
            var resolver = new CombatResolver();
            var atk = MakeBatWithUnitIds("us_mech_rifles");
            var def = MakeBatWithUnitIds("us_mech_rifles");
            var ctx = new CombatContext { DefenderTerrainBonus = 0f, AttackerOOSTurns = 0, DefenderOOSTurns = 0 };
            var result = resolver.ComputeAdvantage(atk, def, ctx);
            bool hasTerrainMod = result.Modifiers.Exists(m => m.Source == "TerrainDefenderBonus");
            Assert(!hasTerrainMod, "Plain terrain: no TerrainDefenderBonus modifier (bonus = 0)");
        }

        static void Test_TerrainUrban()
        {
            var resolver = new CombatResolver();
            var atk = MakeBatWithUnitIds("us_mech_rifles");
            var def = MakeBatWithUnitIds("us_mech_rifles");
            var ctxPlain = new CombatContext { DefenderTerrainBonus = 0f, AttackerOOSTurns = 0, DefenderOOSTurns = 0 };
            var ctxUrban = new CombatContext { DefenderTerrainBonus = 0.4f, AttackerOOSTurns = 0, DefenderOOSTurns = 0 };

            var resultPlain = resolver.ComputeAdvantage(atk, def, ctxPlain);
            var resultUrban = resolver.ComputeAdvantage(atk, def, ctxUrban);

            var mod = resultUrban.Modifiers.Find(m => m.Source == "TerrainDefenderBonus");
            Assert(mod != null, "Urban terrain: TerrainDefenderBonus modifier exists");
            if (mod != null)
                AssertFloat(mod.Value, -0.4f, "Urban terrain: modifier value = -0.4");

            float vDiff = resultUrban.Value - resultPlain.Value;
            AssertFloat(vDiff, -0.4f, "Urban terrain: V is ~0.4 lower than plain");
        }

        static void Test_TerrainForest()
        {
            var resolver = new CombatResolver();
            var atk = MakeBatWithUnitIds("us_mech_rifles");
            var def = MakeBatWithUnitIds("us_mech_rifles");
            var ctx = new CombatContext { DefenderTerrainBonus = 0.1f, AttackerOOSTurns = 0, DefenderOOSTurns = 0 };
            var result = resolver.ComputeAdvantage(atk, def, ctx);

            var mod = result.Modifiers.Find(m => m.Source == "TerrainDefenderBonus");
            Assert(mod != null, "Forest terrain: TerrainDefenderBonus modifier exists");
            if (mod != null)
                AssertFloat(mod.Value, -0.1f, "Forest terrain: modifier value = -0.1");
        }

        static void Test_EngineerHalvesTerrainBonus_SingleCombat()
        {
            var resolver = new CombatResolver();
            var atk = MakeEngineerBat("us_mech_rifles");
            var def = MakeBatWithUnitIds("us_mech_rifles");
            var ctx = new CombatContext { DefenderTerrainBonus = 0.4f, AttackerOOSTurns = 0, DefenderOOSTurns = 0 };

            var result = resolver.ComputeAdvantage(atk, def, ctx);
            var mod = result.Modifiers.Find(m => m.Source == "TerrainDefenderBonus");
            Assert(mod != null, "Engineer single combat: TerrainDefenderBonus modifier exists");
            if (mod != null)
                AssertFloat(mod.Value, -0.2f, "Engineer single combat: terrain bonus halved to -0.2");
        }

        static void Test_EngineerHalvesTerrainBonus_ForceCombat()
        {
            var resolver = new CombatResolver();
            var atkLead = MakeEngineerBat("us_mech_rifles");
            var atkSupport = MakeBatWithUnitIds("us_mech_rifles");
            var defLead = MakeBatWithUnitIds("sov_motostrelkovy");

            var attackers = new List<Battalion> { atkLead, atkSupport };
            var defenders = new List<Battalion> { defLead };
            var ctx = new CombatContext { DefenderTerrainBonus = 0.4f };

            var preview = resolver.PreviewCombat(attackers, defenders, ctx);
            var mod = preview.Advantage.Modifiers.Find(m => m.Source == "TerrainDefenderBonus");
            Assert(mod != null, "Engineer force combat: TerrainDefenderBonus modifier exists");
            if (mod != null)
                AssertFloat(mod.Value, -0.2f, "Engineer force combat: terrain bonus halved to -0.2");
        }

        static Battalion MakeFatigueBat(float ap, int fatigue) => new Battalion { Name = "FatigueTest", Faction = 1, CurrentAP = ap, Fatigue = fatigue };

        static Battalion MakeSimpleInfantryBat(string name)
        {
            var bat = MakeBatWithUnitIds("us_mech_rifles");
            bat.Name = name;
            return bat;
        }

        static void Test_FatigueMultiplier()
        {
            var fresh = MakeFatigueBat(12f, 0);
            var tired = MakeFatigueBat(12f, 6);
            var exhausted = MakeFatigueBat(12f, 8);
            AssertFloat(fresh.GetFatigueCombatMultiplier(), 1.0f, "Fatigue 0: mult 1.0");
            AssertFloat(tired.GetFatigueCombatMultiplier(), 0.9f, "Fatigue 6: mult 0.9");
            AssertFloat(exhausted.GetFatigueCombatMultiplier(), 0.5f, "Fatigue 8: mult 0.5");
        }

        static void Test_ForceCombat_OosAppliedPerBattalionPower()
        {
            var resolver = new CombatResolver();

            var atkLead = MakeSimpleInfantryBat("AtkLead");
            var atkSupport = MakeSimpleInfantryBat("AtkSupport");
            var defLead = MakeSimpleInfantryBat("DefLead");
            var defSupport = MakeSimpleInfantryBat("DefSupport");

            var attackers = new List<Battalion> { atkLead, atkSupport };
            var defenders = new List<Battalion> { defLead, defSupport };

            var ctxPlain = new CombatContext
            {
                DefenderTerrainBonus = 0f,
                AttackerBattalionOOSTurns = new List<int> { 0, 0 },
                DefenderBattalionOOSTurns = new List<int> { 0, 0 }
            };

            var ctxSupportOos = new CombatContext
            {
                DefenderTerrainBonus = 0f,
                AttackerBattalionOOSTurns = new List<int> { 0, 2 },
                DefenderBattalionOOSTurns = new List<int> { 0, 0 }
            };

            var plain = resolver.ResolveCombat(attackers, defenders, ctxPlain, 123ul);
            var supportOos = resolver.ResolveCombat(attackers, defenders, ctxSupportOos, 123ul);

            Assert(supportOos.Advantage.Value < plain.Advantage.Value,
                "Force combat: non-lead battalion OOS lowers attacker advantage through power scaling");

            Assert(!supportOos.Advantage.Modifiers.Exists(m => m.Source.StartsWith("OOS_")),
                "Force combat: OOS no longer appears as advantage modifier entries");
        }

        public static void RunAll()
        {
            fails = 0;
            var resolver = new CombatResolver();

            // ---- Terrain tests ----
            Test_TerrainPlain();
            Test_TerrainUrban();
            Test_TerrainForest();
            Test_EngineerHalvesTerrainBonus_SingleCombat();
            Test_EngineerHalvesTerrainBonus_ForceCombat();
            Test_ForceCombat_OosAppliedPerBattalionPower();

            // ---- Existing tests ----
            var defHeavy = MakeBatWithUnitIds("us_m1a1_abrams");
            var atkNoAnti = MakeBatWithUnitIds("us_mech_rifles");
            var ctx = new CombatContext { AttackerOOSTurns = 0, DefenderOOSTurns = 0 };
            var r1 = resolver.ComputeAdvantage(atkNoAnti, defHeavy, ctx);
            Assert(r1.Modifiers.Exists(m => m.Source == "HeavyArmorOverride" && m.Target == "attacker"), "HeavyArmorOverride applied to attacker when defender has HeavyArmor");

            var atkAntiOnly = MakeBatWithUnitIds("us_tow2");
            var r2 = resolver.ComputeAdvantage(atkAntiOnly, defHeavy, ctx);
            Assert(r2.Modifiers.Count >= 1, "AntiTank/HeavyArmor related modifiers present");

            var atkHeli = MakeBatWithUnitIds("us_ah64a_apache");
            var defNoAA = MakeBatWithUnitIds("us_mech_rifles");
            var r3 = resolver.ComputeAdvantage(atkHeli, defNoAA, ctx);
            Assert(r3.Modifiers.Exists(m => m.Source == "NoAAAgainstHeli"), "NoAAAgainstHeli applied to defender");

            var atkArt = MakeBatWithUnitIds("us_m109a2");
            var defNoArt = MakeBatWithUnitIds("us_mech_rifles");
            var r4 = resolver.ComputeAdvantage(atkArt, defNoArt, ctx);
            Assert(r4.Modifiers.Exists(m => m.Source == "NoArtilleryAgainstArtillery" || m.Source == "NoAAAgainstHeli"), "Artillery difference produces modifier");

            var combatResult = resolver.ResolveCombat(atkHeli, defNoAA, ctx, 12345);
            Assert(combatResult.DefenderHpLost > 0, "Defender takes casualties in Heli vs NoAA scenario");
            Assert(combatResult.AttackerHpLost >= 0, "Attacker casualties computed");
            Assert(combatResult.AttackerCasualties.Sum(c => c.HpLost) == combatResult.AttackerHpLost, "Attacker casualty totals match");
            Assert(combatResult.DefenderCasualties.Sum(c => c.HpLost) == combatResult.DefenderHpLost, "Defender casualty totals match");

            var aNoRecon = MakeBatWithUnitIds("us_m1a1_abrams");
            var bNoRecon = MakeBatWithUnitIds("us_m109a2");
            var r5 = resolver.ComputeAdvantage(aNoRecon, bNoRecon, ctx);
            GD.Print("Symmetry test modifiers count: " + r5.Modifiers.Count);


            // Report
            if (fails == 0) GD.Print("All CombatResolverTests passed");
            else GD.PrintErr(fails + " CombatResolverTests failed");
        }
    }
}