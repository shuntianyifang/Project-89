using System.Collections.Generic;
using System.Linq;

namespace ColdWarWargame.Models
{
    public class Platoon
    {
        public string PlatoonId { get; set; }
        public string Type { get; set; } // "standard" 或 "composite"

        // 所有存活与阵亡的子单位都塞在这里
        public List<SubUnitInstance> Units { get; set; } = new List<SubUnitInstance>();

        // 快捷方法：获取本排所有存活单位
        public IEnumerable<SubUnitInstance> GetAliveUnits() => 
            Units.Where(u => u.SurvivalState == 1);

        // 快捷方法：获取本排当前总部署费用 (用于计算 CE)
        public int GetAliveCost() => 
            GetAliveUnits().Sum(u => u.Cost);
            
        public int GetMaxCost() => 
            Units.Sum(u => u.Cost);
    }
}