using ColdWarWargame.Data;

namespace ColdWarWargame.Core
{
    public class SubUnitInstance
    {
        // 持有对静态数据的引用，极大地节省内存
        public UnitTemplate Template { get; private set; }
        
        // 运行时动态可变状态
        public int CurrentHp { get; set; }
        
        // 状态函数 Si：存活返回 1，阵亡返回 0
        public int SurvivalState => CurrentHp > 0 ? 1 : 0; 
        
        // 构造函数：注入模板，初始化满血
        public SubUnitInstance(string unitId)
        {
            Template = UnitDatabase.GetTemplate(unitId);
            CurrentHp = Template.CombatStats.MaxHp;
        }

        // 暴露常用属性，方便外部（如战损抛骰子算法）调用
        public int Cost => Template.SystemVars.Cost;
        public int BaseWeight => Template.SystemVars.BaseWeight;
        public bool HasCapability(string cap) => Template.TacticalTags.Capabilities.Contains(cap);

        public string NodeId { get; set; }   
        public string Category { get; set; } 

    }
}