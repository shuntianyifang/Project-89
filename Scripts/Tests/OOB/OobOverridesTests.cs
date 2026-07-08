using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Factories;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;

namespace ColdWarWargame.Tests.OOB
{
    public static class OobOverridesTests
    {
        static int _fails = 0;

        static void Assert(bool cond, string msg)
        {
            if (!cond) { _fails++; GD.PrintErr("[OOB FAIL] " + msg); }
            else GD.Print("[OOB PASS] " + msg);
        }

        static Battalion MakeBaseBattalion() =>
            BattalionFactory.CreateFullBattalion("test_bat", "us_mech_battalion_standard", 1);

        static JsonElement ParseObj(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        static void Test_RemoveAndAddNode()
        {
            var bat = MakeBaseBattalion();
            var structure = ParseObj(@"{
                ""remove_nodes"": [""companies/Company_A/platoons/Platoon_A3""],
                ""add_nodes"": {
                    ""companies/Company_HQ/platoons/Sniper_Team"": {
                        ""type"": ""standard"",
                        ""units"": {
                            ""sniper_1"": { ""unit_id"": ""us_mech_rifles"", ""current_hp"": 4, ""max_hp"": 9 }
                        }
                    }
                }
            }");

            BattalionFactory.ApplyStructureOverrides(bat, structure);

            var companyA = bat.Companies.FirstOrDefault(c => c.CompanyId == "Company_A");
            Assert(companyA != null, "Company_A should still exist after removing one platoon");
            Assert(companyA != null && companyA.Platoons.All(p => p.PlatoonId != "Platoon_A3"), "Platoon_A3 should be removed");

            var companyHq = bat.Companies.FirstOrDefault(c => c.CompanyId == "Company_HQ");
            Assert(companyHq != null, "Company_HQ should be auto-created by add_nodes");
            var sniperTeam = companyHq?.Platoons.FirstOrDefault(p => p.PlatoonId == "Sniper_Team");
            Assert(sniperTeam != null, "Sniper_Team should be added");
            var sniper = sniperTeam?.Units.FirstOrDefault(u => u.Category == "units" && u.NodeId == "sniper_1");
            Assert(sniper != null, "sniper_1 should be present in Sniper_Team");
            Assert(sniper != null && sniper.CurrentHp == 4, "sniper_1 current_hp should be set from payload");
        }

        static void Test_StateOverrides()
        {
            var bat = MakeBaseBattalion();
            var state = ParseObj(@"{
                ""Company_A"": {
                    ""Platoon_A1"": {
                        ""vehicle"": {
                            ""veh_1"": { ""current_hp"": 0 }
                        }
                    }
                }
            }");

            BattalionFactory.ApplyStateOverrides(bat, state);

            var companyA = bat.Companies.First(c => c.CompanyId == "Company_A");
            var platoonA1 = companyA.Platoons.First(p => p.PlatoonId == "Platoon_A1");
            var veh1 = platoonA1.Units.First(u => u.Category == "vehicle" && u.NodeId == "veh_1");
            Assert(veh1.CurrentHp == 0, "state_overrides should update current_hp for existing unit");
        }

          static void Test_BattalionTags_LoadedFromTemplate()
          {
            var usAviation = BattalionFactory.CreateFullBattalion("tag_us_avi", "us_aviation_battalion", 1);
            Assert(usAviation.HasBattalionTag("Heli_Battalion"), "Template battalion_tags should load: us_aviation_battalion has Heli_Battalion");

            var sovAviation = BattalionFactory.CreateFullBattalion("tag_sov_avi", "sov_aviation_battalion", 2);
            Assert(sovAviation.HasBattalionTag("Heli_Battalion"), "Template battalion_tags should load: sov_aviation_battalion has Heli_Battalion");
          }

        static void Test_ScenarioLoadWithOverrides_EndToEnd()
        {
            string bluePath = "user://oob_blue_override_test.json";
            string redPath = "user://oob_red_override_test.json";

            string blueJson = @"{
              ""faction_blue"": [
                {
                  ""instance_id"": ""blue_test_1"",
                  ""template_id"": ""us_mech_battalion_standard"",
                  ""x"": 8,
                  ""y"": 16,
                  ""structure_overrides"": {
                    ""remove_nodes"": [
                      ""companies/Company_A/platoons/Platoon_A3""
                    ],
                    ""add_nodes"": {
                      ""companies/Company_HQ/platoons/Sniper_Team"": {
                        ""type"": ""standard"",
                        ""units"": {
                          ""sniper_1"": { ""unit_id"": ""us_mech_rifles"", ""current_hp"": 4, ""max_hp"": 9 }
                        }
                      }
                    }
                  },
                  ""state_overrides"": {
                    ""Company_A"": {
                      ""Platoon_A1"": {
                        ""vehicle"": {
                          ""veh_1"": { ""current_hp"": 0 }
                        }
                      }
                    }
                  }
                }
              ]
            }";

            string redJson = @"{
              ""faction_red"": [
                {
                  ""instance_id"": ""red_test_1"",
                  ""template_id"": ""sov_motorifle_battalion"",
                  ""x"": 10,
                  ""y"": 2
                }
              ]
            }";

            using (var file = FileAccess.Open(bluePath, FileAccess.ModeFlags.Write))
                file.StoreString(blueJson);
            using (var file = FileAccess.Open(redPath, FileAccess.ModeFlags.Write))
                file.StoreString(redJson);

            var scenario = new FuldaGapScenario();
            scenario.LoadOOB(bluePath, redPath);

            Assert(scenario.BlueBattalions.Count == 1, "Scenario should load 1 blue battalion from custom OOB");
            Assert(scenario.RedBattalions.Count == 1, "Scenario should load 1 red battalion from custom OOB");

            var bat = scenario.BlueBattalions[0].bat;
            var companyA = bat.Companies.FirstOrDefault(c => c.CompanyId == "Company_A");
            Assert(companyA != null && companyA.Platoons.All(p => p.PlatoonId != "Platoon_A3"), "E2E: remove_nodes should be applied during scenario load");

            var companyHq = bat.Companies.FirstOrDefault(c => c.CompanyId == "Company_HQ");
            var sniper = companyHq?.Platoons.FirstOrDefault(p => p.PlatoonId == "Sniper_Team")?.Units
                .FirstOrDefault(u => u.Category == "units" && u.NodeId == "sniper_1");
            Assert(sniper != null && sniper.CurrentHp == 4, "E2E: add_nodes should be applied during scenario load");

            var veh1 = companyA?.Platoons.FirstOrDefault(p => p.PlatoonId == "Platoon_A1")?.Units
                .FirstOrDefault(u => u.Category == "vehicle" && u.NodeId == "veh_1");
            Assert(veh1 != null && veh1.CurrentHp == 0, "E2E: state_overrides should be applied during scenario load");
        }

        public static int RunAll()
        {
            _fails = 0;
            GD.Print("--- OOB Override 测试 ---");

            UnitDatabase.Initialize("res://Scripts/Data/Units");
            TemplateDatabase.Initialize("res://Scripts/Data/Templates");

            Test_RemoveAndAddNode();
            Test_StateOverrides();
            Test_BattalionTags_LoadedFromTemplate();
            Test_ScenarioLoadWithOverrides_EndToEnd();

            if (_fails == 0) GD.Print("All OobOverridesTests passed");
            else GD.PrintErr(_fails + " OobOverridesTests FAILED");

            return _fails;
        }
    }
}
