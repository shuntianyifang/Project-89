using Godot;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Factories;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Systems.Supply;
using ColdWarWargame.Systems.Victory;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Tests.Combat;
using ColdWarWargame.Tests.Battlefield;
using ColdWarWargame.Tests.Supply;
using ColdWarWargame.Tests.Turns;
using ColdWarWargame.Tests.Victory;

public partial class GameManager : Node
{
    public override void _Ready()
    {
        UnitDatabase.Initialize("res://Scripts/Data/Units");
        TemplateDatabase.Initialize("res://Scripts/Data/Templates");
        GD.Print("系统引擎初始化完毕。");

        CombatResolverTests.RunAll();
        GridTests.RunAll();
        TurnManagerTests.RunAll();
        SupplyManagerTests.RunAll();
        VictoryTrackerTests.RunAll();

        GD.Print("");
        GD.Print("========== 加载 Fulda Gap 场景 ==========");
        var scenario = new FuldaGapScenario();
        scenario.LoadOOB(
            "res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json",
            "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json"
        );
        scenario.PrintSummary();

        GD.Print("");
        GD.Print("========== 移动演示 ==========");
        scenario.PrintReachableFor("美军机步营", scenario.BlueBattalions[0].pos, 12f);
        scenario.PrintReachableFor("苏军摩步营", scenario.RedBattalions[0].pos, 12f);

        GD.Print("");
        GD.Print("========== 综合演示 ==========");
        var tm = new ColdWarWargame.Systems.Turns.TurnManager();
        foreach (var bat in scenario.BlueBattalions) tm.RegisterBattalion(bat.Item1);
        foreach (var bat in scenario.RedBattalions) tm.RegisterBattalion(bat.Item1);

        GD.Print("  [回合1-蓝军] " + tm.PhaseName());
        var resolver = new ColdWarWargame.Systems.Combat.CombatResolver();
        var ctx = new ColdWarWargame.Systems.Combat.CombatContext
        {
            DefenderTerrainBonus = 0.1f, AttackerOOSTurns = 0, DefenderOOSTurns = 0
        };
        tm.InitiateCombat(scenario.BlueBattalions[1].Item1, scenario.RedBattalions[0].Item1, ctx);
        tm.FinishAttackerDeployment();
        var combatResult = tm.FinishDefenderDeployment(resolver);
        GD.Print("  " + scenario.BlueBattalions[1].Item1.Name + " -> " + scenario.RedBattalions[0].Item1.Name + " V=" + combatResult.Advantage.Value.ToString("0.00"));

        var supplyMgr = new SupplyManager();
        var allUnits = new System.Collections.Generic.List<(ColdWarWargame.Models.Battalion, Godot.Vector2I)>();
        foreach (var bat in scenario.BlueBattalions) allUnits.Add((bat.Item1, bat.Item2));
        foreach (var bat in scenario.RedBattalions) allUnits.Add((bat.Item1, bat.Item2));
        supplyMgr.UpdateFactionEndTurn(1, scenario.Map, allUnits, new System.Collections.Generic.HashSet<Godot.Vector2I>(), new System.Collections.Generic.HashSet<Godot.Vector2I>());

        var vt = new ColdWarWargame.Systems.Victory.VictoryTracker();
        vt.RecordCombatResult(combatResult, 1);
        var bluePos = new System.Collections.Generic.HashSet<Godot.Vector2I>();
        var redPos = new System.Collections.Generic.HashSet<Godot.Vector2I>();
        foreach (var bat in scenario.BlueBattalions) bluePos.Add(bat.Item2);
        foreach (var bat in scenario.RedBattalions) redPos.Add(bat.Item2);
        vt.UpdateControl(scenario.Map, bluePos, redPos, scenario.ZOC);
        vt.ScoreControlVP();
        var assess = vt.Evaluate(1);
        GD.Print("  [战局] 蓝:" + assess.BlueVP + "VP / 红:" + assess.RedVP + "VP");
        GD.Print("  [结果] R=" + assess.Ratio.ToString("0.00") + " -> " + assess.BlueLevel.DisplayName());

        tm.EndStrategicTurn();
        GD.Print("  [回合1-红军] " + tm.PhaseName());
        GD.Print("场景初始化完成。");
    }
}
