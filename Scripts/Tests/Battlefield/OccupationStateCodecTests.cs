using Godot;
using ColdWarWargame.Scenarios;

namespace ColdWarWargame.Tests.Battlefield
{
    public static class OccupationStateCodecTests
    {
        static int _fails = 0;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[OCC FAIL] " + msg); }
            else GD.Print("[OCC PASS] " + msg); }

        static void Test_DefaultHalfHalf()
        {
            var map = OccupationStateCodec.CreateDefaultHalfHalf(4, 4);
            Assert(map[0, 0] == 2, "Top half should be PACT by default");
            Assert(map[0, 1] == 2, "Second row should be PACT by default");
            Assert(map[0, 2] == 1, "Third row should be NATO by default");
            Assert(map[3, 3] == 1, "Bottom half should be NATO by default");
        }

        static void Test_RoundTripSaveLoad()
        {
            string path = "user://occupation_state_codec_roundtrip_test.json";
            var source = OccupationStateCodec.CreateDefaultHalfHalf(6, 4);
            source[0, 0] = 1;
            source[5, 3] = 2;

            OccupationStateCodec.Save(path, source);
            bool loaded = OccupationStateCodec.TryLoad(path, 6, 4, out var restored);

            Assert(loaded, "Occupation state file should load back");
            Assert(restored[0, 0] == 1, "Round-trip should preserve NATO cell");
            Assert(restored[5, 3] == 2, "Round-trip should preserve PACT cell");
        }

        public static void RunAll()
        {
            _fails = 0;

            Test_DefaultHalfHalf();
            Test_RoundTripSaveLoad();

            if (_fails == 0)
                GD.Print("All OccupationStateCodecTests passed");
            else
                GD.PrintErr(_fails + " OccupationStateCodecTests FAILED");
        }
    }
}