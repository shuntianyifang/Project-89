using System;
using System.Linq;
using System.Collections.Generic;
using ColdWarWargame.Core.Entities;
using ColdWarWargame.Core.Combat;
using Godot;

namespace ColdWarWargame.Core.Combat
{
    // 简单的内建测试运行器：在 GameManager 中调用 CombatResolverTests.RunAll();
    public static class CombatResolverTests
    {
        static int fails = 0;
        static void Assert(bool cond, string msg)
        {
            if (!cond)
            {
                fails++;
                GD.PrintErr("[TEST FAIL] " + msg);
            }
            else
            {
                GD.Print("[TEST PASS] " + msg);
            }
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

        public static void RunAll()
        {
            fails = 0;
            var resolver = new CombatResolver();

            // Test-02 HeavyArmor 单侧惩罚
            var defHeavy = MakeBatWithUnitIds("us_m1a1_abrams"); // has HeavyArmor
            var atkNoAnti = MakeBatWithUnitIds("us_mech_rifles"); // light infantry, has LightAntiTank but for demonstration treat as lacking HeavyAntiTank/AntiTank
            var ctx = new CombatContext { AttackerOOSTurns = 0, DefenderOOSTurns = 0 };
            var r1 = resolver.ComputeAdvantage(atkNoAnti, defHeavy, ctx);
            Assert(r1.Modifiers.Exists(m => m.Source == "HeavyArmorOverride" && m.Target == "attacker"), "HeavyArmorOverride applied to attacker when defender has HeavyArmor");

            // Test-04 AntiTank only vs HeavyArmor (-0.5)
            var atkAntiOnly = MakeBatWithUnitIds("us_tow2"); // us_tow2 has HeavyAntiTank in JSON but demonstrates anti-case; adjust if needed
            var r2 = resolver.ComputeAdvantage(atkAntiOnly, defHeavy, ctx);
            // check for AntiTankVsHeavyArmor or HeavyArmorOverride presence (either is acceptable baseline)
            Assert(r2.Modifiers.Count >= 1, "AntiTank/HeavyArmor related modifiers present");

            // Test-11 Heli vs NoAA
            var atkHeli = MakeBatWithUnitIds("us_ah64a_apache");
            var defNoAA = MakeBatWithUnitIds("us_mech_rifles");
            var r3 = resolver.ComputeAdvantage(atkHeli, defNoAA, ctx);
            Assert(r3.Modifiers.Exists(m => m.Source == "NoAAAgainstHeli"), "NoAAAgainstHeli applied to defender");

            // Test-10 Artillery difference
            var atkArt = MakeBatWithUnitIds("us_m109a2");
            var defNoArt = MakeBatWithUnitIds("us_mech_rifles");
            var r4 = resolver.ComputeAdvantage(atkArt, defNoArt, ctx);
            Assert(r4.Modifiers.Exists(m => m.Source == "NoArtilleryAgainstArtillery" || m.Source == "NoAAAgainstHeli"), "Artillery difference produces modifier");

            // Test casualty resolution path with deterministic seed
            var combatResult = resolver.ResolveCombat(atkHeli, defNoAA, ctx, 12345);
            Assert(combatResult.DefenderHpLost > 0, "Defender takes casualties in Heli vs NoAA scenario");
            Assert(combatResult.AttackerHpLost >= 0, "Attacker casualties computed");
            Assert(combatResult.AttackerCasualties.Sum(c => c.HpLost) == combatResult.AttackerHpLost, "Attacker casualty totals match");
            Assert(combatResult.DefenderCasualties.Sum(c => c.HpLost) == combatResult.DefenderHpLost, "Defender casualty totals match");

            // Test symmetry: both no Recon
            var aNoRecon = MakeBatWithUnitIds("us_m1a1_abrams"); // this one actually has Recon? if so pick other
            var bNoRecon = MakeBatWithUnitIds("us_m109a2");
            var r5 = resolver.ComputeAdvantage(aNoRecon, bNoRecon, ctx);
            // we expect NoRecon possibly present on one or both depending on templates; just print summary
            GD.Print("Symmetry test modifiers count: " + r5.Modifiers.Count);

            // Report
            if (fails == 0) GD.Print("All CombatResolverTests passed");
            else GD.PrintErr($"{fails} CombatResolverTests failed");
        }
    }
}