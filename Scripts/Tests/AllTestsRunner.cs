using Godot;
using ColdWarWargame.Tests.Battlefield;
using ColdWarWargame.Tests.Combat;
using ColdWarWargame.Tests.Supply;
using ColdWarWargame.Tests.Turns;
using ColdWarWargame.Tests.Victory;
using ColdWarWargame.Tests.OOB;

namespace ColdWarWargame.Tests
{
    public static class AllTestsRunner
    {
        public static int RunAll()
        {
            GD.Print("========== RUN ALL TESTS ==========");

            GridTests.RunAll();
            VisionTests.RunAll();
            EngagementTests.RunAll();
            CombatResolverTests.RunAll();
            SupplyManagerTests.RunAll();
            TurnManagerTests.RunAll();
            VictoryTrackerTests.RunAll();

            int oobFails = OobOverridesTests.RunAll();

            GD.Print("========== TEST RUN FINISHED ==========");
            return oobFails;
        }
    }
}
