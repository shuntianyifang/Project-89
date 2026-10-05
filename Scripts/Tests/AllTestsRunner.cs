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

            int fails = GridTests.RunAll();
            fails += OccupationStateCodecTests.RunAll();
            fails += FrontlineTests.RunAll();
            fails += VisionTests.RunAll();
            fails += EngagementTests.RunAll();
            fails += CombatResolverTests.RunAll();
            fails += SupplyManagerTests.RunAll();
            fails += TurnManagerTests.RunAll();
            fails += VictoryTrackerTests.RunAll();

            fails += OobOverridesTests.RunAll();
            fails += Gameplay.CampaignTests.RunAll();

            GD.Print("========== TEST RUN FINISHED ==========");
            return fails;
        }
    }
}
