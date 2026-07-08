using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Models;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Factories;

namespace ColdWarWargame.Tests.Battlefield
{
    public static class VisionTests
    {
        static int _fails = 0;
        static bool _dbReady = false;
        static void Assert(bool c, string m) { if (!c) { _fails++; GD.PrintErr("[VISION FAIL] " + m); } else GD.Print("[VISION PASS] " + m); }

        static void EnsureDb()
        {
            if (_dbReady) return;
            UnitDatabase.Initialize("res://Scripts/Data/Units");
            TemplateDatabase.Initialize("res://Scripts/Data/Templates");
            _dbReady = true;
        }

        static Battalion MakeManualBattalion(params SubUnitInstance[] units)
        {
            var b = new Battalion { Name = "VisionTest", Faction = 1 };
            var c = new Company { CompanyId = "C1", Name = "C1" };
            var p = new Platoon { PlatoonId = "P1", Type = "standard" };
            foreach (var u in units) p.Units.Add(u);
            c.Platoons.Add(p);
            b.Companies.Add(c);
            return b;
        }

        static void Test_BattalionVisionTiers()
        {
            EnsureDb();

            var noRecon = MakeManualBattalion(new SubUnitInstance("us_m1a1_abrams"));
            Assert(noRecon.CalculateVisionRange() == 6, "Base vision: no Recon alive => 6");
            var noReconInfo = noRecon.GetVisionRuleInfo();
            Assert(noReconInfo.reason == "瞎子基线", "Base vision reason => 瞎子基线");

            var hasRecon = MakeManualBattalion(new SubUnitInstance("us_m1a1_acav"));
            Assert(hasRecon.CalculateVisionRange() == 8, "Standard vision: has alive Recon => 8");
            var hasReconInfo = hasRecon.GetVisionRuleInfo();
            Assert(hasReconInfo.reason == "建制内侦察激活", "Standard vision reason => 建制内侦察激活");

            var mixed = MakeManualBattalion(new SubUnitInstance("us_m1a1_abrams"), new SubUnitInstance("us_m1a1_acav"));
            var reconUnit = mixed.GetAllSubUnits().First(u => u.HasCapability("Recon"));
            reconUnit.CurrentHp = 0;
            Assert(mixed.CalculateVisionRange() == 6, "Recon dead fallback: non-recon alive => 6");

            var advanced = BattalionFactory.CreateFullBattalion("r1", "sov_recon_battalion", 2);
            Assert(advanced.CalculateVisionRange() == 12, "Advanced vision: recon battalion => 12");
            var advancedInfo = advanced.GetVisionRuleInfo();
            Assert(advancedInfo.reason == "专业侦察营", "Advanced vision reason => 专业侦察营");
        }

        public static void RunAll()
        {
            _fails = 0; GD.Print("--- Vision 测试 ---");
            var pos = new Vector2I(5, 5);
            var grids = GridHelpers.GetGridsInChebyshevDistance(pos, 2);
            Assert(grids.Count == 25, "Range 2 from (5,5) -> 25 tiles (5x5)");
            var pos2 = new Vector2I(0, 0);
            var grids2 = GridHelpers.GetGridsInChebyshevDistance(pos2, 1);
            Assert(grids2.Count == 9, "Range 1 from (0,0) -> 9 tiles (3x3)");
            Test_BattalionVisionTiers();
            if (_fails == 0) GD.Print("All VisionTests passed");
            else GD.PrintErr(_fails + " VisionTests FAILED");
        }
    }
}
