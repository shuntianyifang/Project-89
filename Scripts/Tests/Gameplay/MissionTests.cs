using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using ColdWarWargame.Factories;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Gameplay;
using ColdWarWargame.Systems.Turns;
using ColdWarWargame.Systems.Victory;
using GridMap=ColdWarWargame.Systems.Battlefield.GridMap;
using TileData=ColdWarWargame.Models.TileData;

namespace ColdWarWargame.Tests.Gameplay
{
    public static class MissionTests
    {
        private static int _fails;
        private static void Check(bool ok,string text)
        { if(ok) GD.Print("[MISSION PASS] "+text); else { _fails++; GD.PrintErr("[MISSION FAIL] "+text); } }
        private static void Core()
        {
            var config=MissionConfiguration.Load(); var tracker=new MissionTracker(config);
            var map=new GridMap(50,30); var control=new int[50,30];
            for(int x=0;x<50;x++) { map.SetTile(new(x,8),new TileData(0,2)); control[x,8]=2; }
            map.PrimarySupplySources[2]=new(){new(49,8)};
            var red=BattalionFactory.CreateFullBattalion("red","fg_tank_bn",2);
            Check(tracker.TryExit(red,new(0,8),1,map,control,0)!=null,"Unsupplied unit cannot breakthrough");
            control[20,8]=1;
            Check(tracker.TryExit(red,new(0,8),1,map,control,10)!=null,"Captured exit alone does not count without controlled road corridor");
            control[20,8]=2;
            var support=BattalionFactory.CreateFullBattalion("support","fg_regimental_recon",2);
            Check(tracker.TryExit(support,new(0,8),1,map,control,10)!=null,"Recon support cannot impersonate main breakthrough force");
            red.Fatigue=9;
            Check(tracker.TryExit(red,new(0,8),1,map,control,10)!=null,"Disorganized main force cannot exit effectively"); red.Fatigue=0;
            var depleted=BattalionFactory.CreateFullBattalion("weak","fg_tank_bn",2);
            foreach(var u in depleted.GetAllSubUnits().Take(20)) u.CurrentHp=0;
            Check(tracker.TryExit(depleted,new(0,8),1,map,control,10)!=null,"CE below threshold rejects breakthrough");
            tracker.CompleteRound(1); tracker.CompleteRound(1);
            Check(tracker.State.DelayRounds==1,"Completed full round scores delay once");
            Check(tracker.TryExit(red,new(0,8),2,map,control,10)==null && tracker.State.FirstBreakthroughRound==2,"Connected supplied main battalion records effective breakthrough");
            float score=tracker.RedScore(control);
            Check(tracker.TryExit(red,new(0,8),2,map,control,10)!=null && tracker.RedScore(control)==score,"Breakthrough cannot score twice");
            tracker.CompleteRound(2);
            Check(tracker.State.DelayRounds==1,"First breakthrough freezes subsequent delay income");
            var second=BattalionFactory.CreateFullBattalion("second","fg_tank_bn",2);
            tracker.TryExit(second,new(0,8),3,map,control,10);
            Check(Math.Abs(tracker.RedScore(control)-85)<.001,"Two full-effectiveness battalions give 70 breakthrough points plus 15 for the controlled node");
            var blue=BattalionFactory.CreateFullBattalion("11ACR_1Sqdn","fg_cav_squadron",1);
            control[14,29]=1;
            Check(tracker.TryExit(blue,new(14,29),3,map,control,10)==null,"Living blue cavalry can withdraw through own supplied exit");
            float before=tracker.BlueScore;
            foreach(var u in blue.GetAllSubUnits()) u.CurrentHp=0;
            Check(tracker.BlueScore==before && tracker.State.Exits.Last().Unit.Restore().HasSurvivingSubUnits,"Withdrawn effectiveness is frozen and not an eliminated battalion");
            var uninterrupted=new MissionTracker(config); for(int i=1;i<=20;i++) uninterrupted.CompleteRound(i);
            Check(uninterrupted.State.DelayRounds==12 && uninterrupted.BlueScore==70,"24-hour assessment window caps delay without ending campaign");
            var battlefield=new int[50,30]; battlefield[13,8]=2;
            Check(uninterrupted.RedScore(battlefield)==15,"Only configured key nodes give geographic mission points");
            var losses=new VictoryTracker(); losses.RestoreStatistics(new[]{0,999999,0,0,0,0,0});
            var result=new CampaignResult(losses,21,"test",uninterrupted,new int[50,30]);
            Check(result.Assessment.BlueLevel==VictoryLevel.DecisiveVictory,"Task outcome ignores raw battle-loss VP");
        }
        private static void SaveAndSession()
        {
            var tree=(SceneTree)Engine.GetMainLoop(); var owner=tree.Root.GetNode<global::GameManager>("GameManager");
            var node=new Node(); owner.AddChild(node); var host=new GameSessionHost(node,owner);
            try
            {
                host.Start();
                var bat=host.Scenario.BlueBattalions[0].bat;
                host.Scenario.BlueBattalions[0]=(bat,new(0,8));
                host.Session.OnUnitClicked(1,bat,new(0,8)); host.Session.OnExitSelected();
                Check(host.Scenario.Missions.State.Exits.Count==1 && !host.Scenario.BlueBattalions.Any(u=>u.bat==bat),"Real session removes withdrawn unit from map and records it");
                host.Session.OnExitSelected(); Check(host.Scenario.Missions.State.Exits.Count==1,"Repeated UI exit action cannot duplicate withdrawal");
                host.Session.OnEndTurn(); host.Session.OnEndTurn();
                Check(host.Scenario.Missions.State.RoundHistory.Count==1,"Real session records task situation once per complete round");
                var save=CampaignSave.Capture(host.Scenario,host.TurnManager,new VictoryTracker());
                var path="user://mission-test-"+Guid.NewGuid()+".json";
                try
                {
                    save.Write(path); var read=CampaignSave.Read(path);
                    host.Shutdown(); host.Start(); read.Apply(host.Scenario,host.TurnManager,new VictoryTracker());
                    Check(host.Scenario.Missions.State.Exits.Count==1 && host.Scenario.BlueBattalions.Count==3,"File save/load preserves exited roster and active units separately");
                    Check(host.Scenario.Missions.State.RoundHistory.Count==1 && host.Scenario.Missions.Journal().Contains("有组织撤离"),
                        "File save/load preserves round situation and withdrawal journal");
                    var corrupt=CampaignSave.Capture(host.Scenario,host.TurnManager,new VictoryTracker());
                    corrupt.Missions.RoundHistory[0].ObjectiveOwners[0]=3;
                    bool badHistory=false;
                    try { corrupt.Apply(host.Scenario,host.TurnManager,new VictoryTracker()); } catch(InvalidOperationException) { badHistory=true; }
                    Check(badHistory && host.Scenario.Missions.State.RoundHistory[0].ObjectiveOwners[0]!=3,
                        "Invalid history rejected without mutating the current campaign");
                    read.Missions.Exits.Add(read.Missions.Exits[0]); bool rejected=false;
                    try { read.Apply(host.Scenario,host.TurnManager,new VictoryTracker()); } catch(InvalidOperationException) { rejected=true; }
                    Check(rejected && host.Scenario.Missions.State.Exits.Count==1,"Duplicate exit save rejected before mutating live mission state");
                }
                finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path)); }
                host.Scenario.BlueBattalions.Clear(); host.Session.OnEndTurn();
                Check(host.Session.Result==null,"Living withdrawn faction is not treated as annihilated");
                host.Session.OnEndCampaign(); Check(host.Session.Result?.MissionSummary!=null,"Manual ending settles actual task outcome");
                host.Shutdown(); host.Start();
                Check(host.Scenario.Missions.State.Exits.Count==0 && host.Scenario.Missions.State.CompletedRounds==0 &&
                    host.Scenario.Missions.State.RoundHistory.Count==0,"Restart clears mission ledger, history and earned points");
                var control=new int[50,30];
                for(int x=0;x<50;x++) for(int y=0;y<30;y++) control[x,y]=2;
                host.Scenario.ApplyOccupationState(control);
                for(int i=0;i<host.Scenario.BlueBattalions.Count;i++)
                    host.Scenario.BlueBattalions[i]=(host.Scenario.BlueBattalions[i].bat,new(i+1,25));
                var red=host.Scenario.RedBattalions[0].bat;
                host.Scenario.RedBattalions[0]=(red,new(0,8));
                host.TurnManager.RestoreStrategicState(2,1);
                host.Session.OnUnitClicked(2,red,new(0,8)); host.Session.OnExitSelected();
                Check(host.Scenario.Missions.State.FirstBreakthroughRound==1 &&
                    !host.Scenario.RedBattalions.Any(u=>u.bat==red),"Real session checks actual historical-map supply and road corridor for breakthrough");
            }
            finally { host.Shutdown(); owner.RemoveChild(node); node.QueueFree(); }
        }
        private static void RoundHistory()
        {
            var tracker=new MissionTracker(MissionConfiguration.Load());
            var map=new GridMap(50,30); var control=new int[50,30];
            for(int x=0;x<50;x++) { map.SetTile(new(x,8),new TileData(0,2)); control[x,8]=2; }
            map.PrimarySupplySources[2]=new(){new(49,8)};
            tracker.CompleteRound(1,map,control); tracker.CompleteRound(1,map,control);
            Check(tracker.State.RoundHistory.Count==1 && tracker.State.RoundHistory[0].RedCorridorOpen && tracker.State.DelayRounds==1,
                "Open road without effective breakthrough still earns approved delay score, once");
            control[20,8]=1; control[13,28]=1;
            tracker.CompleteRound(2,map,control);
            Check(!tracker.State.RoundHistory[1].RedCorridorOpen && tracker.State.RoundHistory[1].ObjectiveOwners[1]==1,
                "Round history records blocked corridor and key node ownership");
            control[20,8]=2;
            map.BlockCrossing(new(20,8),new(21,8));
            Check(!MissionTracker.HasControlledRoad(map,new(0,8),control,2),"Closed river crossing blocks recorded road corridor");
            control[0,8]=1;
            Check(!MissionTracker.HasControlledRoad(map,new(0,8),control,2),"Enemy-controlled exit cannot be an open red corridor");
            var red=BattalionFactory.CreateFullBattalion("history-red","fg_tank_bn",2);
            var openMap=new GridMap(50,30);
            for(int x=0;x<50;x++) openMap.SetTile(new(x,8),new TileData(0,2));
            openMap.PrimarySupplySources[2]=new(){new(49,8)}; control[0,8]=2;
            tracker.TryExit(red,new(0,8),3,openMap,control,10);
            for(int i=3;i<=13;i++) tracker.CompleteRound(i,openMap,control);
            Check(tracker.State.DelayRounds==2 && !tracker.State.RoundHistory[2].DelayAwarded &&
                tracker.Journal().Contains("有效突破") && tracker.Journal().Contains("24小时考核窗口结束"),
                "Journal records breakthrough, frozen income and assessment window without ending play");
            var saved=tracker.Capture(); var restored=new MissionTracker(tracker.Configuration);
            restored.Validate(saved,Array.Empty<Battalion>(),14); restored.Restore(saved); restored.CompleteRound(13,openMap,control);
            Check(restored.Journal()==tracker.Journal() && restored.BlueScore==tracker.BlueScore && restored.State.RoundHistory.Count==13,
                "History round-trip and repeated settlement cannot duplicate events or points");
            saved.RoundHistory[2].DelayAwarded=true;
            bool rejected=false;
            try { restored.Validate(saved,Array.Empty<Battalion>(),14); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected,"History cannot award delay in the first breakthrough round");
            var legacy=tracker.Capture(); legacy.RoundHistory.Clear();
            restored.Validate(legacy,Array.Empty<Battalion>(),14); restored.Restore(legacy); restored.CompleteRound(14,openMap,control);
            Check(restored.State.RoundHistory.Count==1 && restored.State.RoundHistory[0].Round==14 &&
                restored.Journal().Contains("旧存档"),"Early version-2 save resumes history without inventing old situations or scores");
            Check(restored.Summary(control).Contains("保存") && restored.Summary(control).Contains("节点"),
                "Task panel and final summary show all four score components");
        }
        public static int RunAll()
        { _fails=0; Core(); RoundHistory(); SaveAndSession(); GD.Print(_fails==0?"All MissionTests passed":_fails+" MissionTests FAILED"); return _fails; }
    }
}
