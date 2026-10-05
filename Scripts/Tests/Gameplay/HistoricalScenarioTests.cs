using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using ColdWarWargame.Factories;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Combat;
using ColdWarWargame.Systems.Supply;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;
using ColdWarWargame.Systems.Battlefield;
using TileData = ColdWarWargame.Models.TileData;

namespace ColdWarWargame.Tests.Gameplay
{
    public static class HistoricalScenarioTests
    {
        private static int _fails;
        private static void Check(bool ok,string text)
        { if (ok) GD.Print("[HISTORICAL PASS] " + text); else { _fails++; GD.PrintErr("[HISTORICAL FAIL] " + text); } }
        public static int RunAll()
        {
            _fails=0;
            var scenario=new FuldaGapScenario();
            scenario.LoadOOB("res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json","res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json");
            var all=scenario.BlueBattalions.Concat(scenario.RedBattalions).ToList();
            Check(scenario.BlueBattalions.Count==4 && scenario.RedBattalions.Count==12,"Focused OOB contains two cavalry squadrons and two Soviet regiments");
            Check(all.Select(u=>u.pos).Distinct().Count()==all.Count && all.All(u=>scenario.Map.IsInBounds(u.pos)),"Unique valid initial deployment");
            var control=scenario.GetOccupationMap();
            Check(all.All(u=>control[u.pos.X,u.pos.Y]==u.bat.Faction),"Forward deployment begins on its own side of digitized border");
            var cav=scenario.BlueBattalions[0].bat;
            Check(cav.CanFillMain() && cav.CalculateVisionRange()==4,"Historical cavalry main role and 8 km information radius");
            Check(cav.GetAllSubUnits().Count(u=>u.UnitId.StartsWith("fg_m1a1"))==41 && cav.GetAllSubUnits().Count(u=>u.UnitId.StartsWith("fg_m3a1"))==38,"Cavalry TOE assumption: 41 tanks and 38 cavalry vehicles");
            Check(!cav.GetAllSubUnits().Any(u=>u.UnitId=="fg_m109a2") && scenario.BlueBattalions.Sum(u=>u.bat.GetAllSubUnits().Count(s=>s.UnitId=="fg_m109a2"))==12,"Detached artillery is not counted twice");
            var artillery=scenario.BlueBattalions.First(u=>u.bat.CanFillArtillery()).bat;
            Check(artillery.GetArtilleryRange()==9,"M109A2 normal ammunition uses 18 km support radius");
            var map=new GridMap(4,4);
            map.BlockCrossing(new(1,1),new(2,1));
            var move=new MovementResolver(map);
            Check(float.IsPositiveInfinity(move.GetMoveCost(new(1,1),new(2,1),_=>false)) &&
                float.IsPositiveInfinity(move.GetMoveCost(new(1,1),new(2,2),_=>false)),"Ground movement cannot cross unbridged river or bypass it diagonally");
            Check(float.IsFinite(move.GetMoveCost(new(1,0),new(2,0),_=>false)),"Open bridge crossing remains traversable");
            var corridor=new GridMap(4,1); corridor.PrimarySupplySources[1]=new(){new(0,0)};
            corridor.BlockCrossing(new(1,0),new(2,0));
            var sp=new SupplyNetwork().ComputeSupplySP(corridor,1,new(),new());
            Check(sp[0,0]==36 && sp[3,0]==0,"Configured west source supplies locally but river blocks propagation");
            Check(scenario.Map.PrimarySupplySources[1].Single().X==0 && scenario.Map.PrimarySupplySources[2].Single().X==49,"Historical scenario uses west/east logistics entrances");
            var calibration=new List<object>();
            var means=new List<(bool supported,int terrain,float attacker,float defender)>();
            foreach(var supported in new[]{false,true})
            foreach(var terrain in new[]{0,1,3})
            {
                var atkLoss=new List<float>(); var defLoss=new List<float>();
                var atkDestroyed=new List<int>(); var defDestroyed=new List<int>();
                for(ulong seed=1;seed<=64;seed++)
                {
                    var a=BattalionFactory.CreateFullBattalion("red","fg_tank_bn",2);
                    var d=BattalionFactory.CreateFullBattalion("blue","fg_cav_squadron",1);
                    int ah=a.GetTotalCurrentHp(),dh=d.GetTotalCurrentHp();
                    var attackers=new List<Battalion>{a};
                    if(supported)
                    {
                        attackers.Add(BattalionFactory.CreateFullBattalion("red_mrb","fg_mrb",2));
                        attackers.Add(BattalionFactory.CreateFullBattalion("red_recon","fg_regimental_recon",2));
                        attackers.Add(BattalionFactory.CreateFullBattalion("red_arty","fg_2s1_bn",2));
                    }
                    var defenders=new List<Battalion>{d};
                    if(supported) defenders.Add(BattalionFactory.CreateFullBattalion("blue_arty","fg_m109_battery",1));
                    ah=attackers.Sum(b=>b.GetTotalCurrentHp());
                    dh=defenders.Sum(b=>b.GetTotalCurrentHp());
                    var r=new CombatResolver().ResolveCombat(attackers,defenders,new CombatContext { DefenderTerrainBonus=terrain==3?.4f:terrain==1?.1f:0 },seed);
                    atkLoss.Add((float)r.AttackerHpLost/ah); defLoss.Add((float)r.DefenderHpLost/dh);
                    atkDestroyed.Add(r.AttackerCasualties.Count(c=>c.IsDestroyed));
                    defDestroyed.Add(r.DefenderCasualties.Count(c=>c.IsDestroyed));
                }
                means.Add((supported,terrain,atkLoss.Average(),defLoss.Average()));
                calibration.Add(new{supported,terrain,seeds=64,attacker_mean_loss=atkLoss.Average(),defender_mean_loss=defLoss.Average(),
                    attacker_min_destroyed=atkDestroyed.Min(),attacker_max_destroyed=atkDestroyed.Max(),
                    defender_min_destroyed=defDestroyed.Min(),defender_max_destroyed=defDestroyed.Max(),
                    attacker_min_loss=atkLoss.Min(),attacker_max_loss=atkLoss.Max(),defender_min_loss=defLoss.Min(),defender_max_loss=defLoss.Max()});
                Check(atkLoss.All(v=>float.IsFinite(v)&&v>=0&&v<=1) && defLoss.All(v=>float.IsFinite(v)&&v>=0&&v<=1),"Fixed-seed losses remain bounded, terrain="+terrain);
            }
            Check(means.Single(m=>m.supported&&m.terrain==0).attacker < means.Single(m=>!m.supported&&m.terrain==0).attacker,
                "Combined-arms support reduces attacking losses compared with isolated tank attack");
            Check(means.Single(m=>m.supported&&m.terrain==3).defender < means.Single(m=>m.supported&&m.terrain==0).defender,
                "Town cover reduces defending losses in the calibrated combined-arms case");
            using(var file=FileAccess.Open("user://fulda-calibration.json",FileAccess.ModeFlags.Write))
                file.StoreString(JsonSerializer.Serialize(calibration,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("Calibration report: "+ProjectSettings.GlobalizePath("user://fulda-calibration.json"));
            // Straight-line displacement budgets are model bounds, not top speeds.
            var budgets=new List<int>();
            foreach(var t in new[]{new TileData(1),new TileData(0),new TileData(0,1),new TileData(0,2)})
            {
                var road=new GridMap(60,1); for(int x=0;x<60;x++) road.SetTile(new(x,0),t);
                var reachable=new MovementResolver(road).GetReachableTiles(new(0,0),12,_=>false,_=>false);
                budgets.Add(reachable.Keys.Max(p=>p.X)*2);
            }
            Check(budgets.SequenceEqual(new[]{6,12,24,48}),"Two-hour model movement budgets: forest 6, cross-country 12, road 24, motorway 48 km");
            GD.Print(_fails==0?"All HistoricalScenarioTests passed":_fails+" HistoricalScenarioTests FAILED");
            return _fails;
        }
    }
}
