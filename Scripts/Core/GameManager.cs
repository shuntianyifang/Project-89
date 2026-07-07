using Godot;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Core.Factories;
using ColdWarWargame.Scenarios;

public partial class GameManager : Node
{
    public override void _Ready()
    {
        // 1. 启动数据库引擎
        UnitDatabase.Initialize("res://Scripts/Data/Units");
        TemplateDatabase.Initialize("res://Scripts/Data/Templates");

        GD.Print("系统引擎初始化完毕。");

        // 2. 运行已有测试
        ColdWarWargame.Core.Combat.CombatResolverTests.RunAll();
        ColdWarWargame.Battlefield.GridTests.RunAll();

        // 3. 加载 Fulda Gap 场景
        GD.Print("");
        GD.Print("========== 加载 Fulda Gap 场景 ==========");
        var scenario = new FuldaGapScenario();
        scenario.LoadOOB(
            "res://Scripts/Data/Scenarios/Fulda_Gap/oob_blue.json",
            "res://Scripts/Data/Scenarios/Fulda_Gap/oob_red.json"
        );
        scenario.PrintSummary();

        // 4. 演示移动范围
        GD.Print("");
        GD.Print("========== 移动演示 ==========");
        // 蓝军机步营（平原上，AP=12）
        scenario.PrintReachableFor("美军机步营", scenario.BlueBattalions[0].pos, 12f);
        // 红军摩步营（平原上，AP=12）
        scenario.PrintReachableFor("苏军摩步营", scenario.RedBattalions[0].pos, 12f);

        // 5. 演示战斗
        GD.Print("");
        GD.Print("========== 战斗演示 ==========");
        // 蓝军坦克营(西德) 进攻 红军摩步营(苏军)
        scenario.RunDemoCombat(
            scenario.BlueBattalions[1].bat, scenario.BlueBattalions[1].pos,
            scenario.RedBattalions[0].bat, scenario.RedBattalions[0].pos
        );

        GD.Print("场景初始化完成。");
    }
}
