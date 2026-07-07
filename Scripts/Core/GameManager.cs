using Godot;
using ColdWarWargame.Data;
using ColdWarWargame.Data.TOE;
using ColdWarWargame.Core.Factories;

public partial class GameManager : Node
{
    public override void _Ready()
    {
        // 1. 启动数据库引擎
        UnitDatabase.Initialize("res://Scripts/Data/Units");
        TemplateDatabase.Initialize("res://Scripts/Data/Templates");

        GD.Print("系统引擎初始化完毕。");

        ColdWarWargame.Core.Combat.CombatResolverTests.RunAll();
        
        // 2. 试运行兵工厂：下线一个美军标准机步营
        var testBat = BattalionFactory.CreateFullBattalion("1st_bat_7th_cav", "us_mech_battalion_standard", 1);
        
        // 3. 打印核心验算数据
        GD.Print($"=== 兵工厂测试报告 ===");
        GD.Print($"营名称: {testBat.Name} (阵营: {testBat.Faction})");
        GD.Print($"下辖连队数: {testBat.Companies.Count}");
        GD.Print($"全营总子单位数: {System.Linq.Enumerable.Count(testBat.GetAllSubUnits())}");
        GD.Print($"初始战斗效能比 (CE): {testBat.CalculateCE() * 100}%");
        GD.Print($"初始面板攻击力: {testBat.GetActualAttack()}");
        GD.Print($"侦察视野范围: {testBat.CalculateVisionRange()} 格");
        GD.Print($"======================");
    }
}