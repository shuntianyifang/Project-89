using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using ColdWarWargame.Core.Entities;
using ColdWarWargame.Data.TOE;
using Godot;

namespace ColdWarWargame.Core.Factories
{
    public static class BattalionFactory
    {
        /// <summary>
        /// 核心装配线：根据模板 ID 生成一个 100% 满编满血的营
        /// </summary>
        public static Battalion CreateFullBattalion(string instanceId, string templateId, int faction)
        {
            // 查阅模板字典（假设你已经建立好了 TemplateDatabase 静态类）
            var template = TemplateDatabase.GetTemplate(templateId); 
            
            var battalion = new Battalion
            {
                InstanceId = instanceId,
                Name = template.Name,
                Faction = faction,
                CurrentAP = 12.0f,
                Fatigue = 0
            };

            foreach (var compKvp in template.Companies)
            {
                var company = new Company 
                { 
                    CompanyId = compKvp.Key, 
                    Name = compKvp.Value.Name 
                };
                
                foreach (var platKvp in compKvp.Value.Platoons)
                {
                    var platoon = new Platoon 
                    { 
                        PlatoonId = platKvp.Key, 
                        Type = platKvp.Value.Type 
                    };
                    
                    // 解析排内单位并实例化
                    if (platoon.Type == "standard" && platKvp.Value.Units != null)
                    {
                        InstantiateAndAddUnits(platoon, platKvp.Value.Units, "units");
                    }
                    else if (platoon.Type == "composite")
                    {
                        if (platKvp.Value.Infantry != null)
                            InstantiateAndAddUnits(platoon, platKvp.Value.Infantry, "infantry");
                        if (platKvp.Value.Vehicle != null)
                            InstantiateAndAddUnits(platoon, platKvp.Value.Vehicle, "vehicle");
                    }
                    company.Platoons.Add(platoon);
                }
                battalion.Companies.Add(company);
            }
            return battalion;
        }

        private static void InstantiateAndAddUnits(Platoon platoon, Dictionary<string, SubUnitDef> unitDefs, string category)
        {
            foreach (var def in unitDefs)
            {
                var instance = new SubUnitInstance(def.Value.UnitId)
                {
                    NodeId = def.Key,       // 记录如 "tank_1" 等节点 ID
                    Category = category     // 标记为 infantry, vehicle 或 units
                };
                instance.CurrentHp = def.Value.MaxHp; // 初始满血
                platoon.Units.Add(instance);
            }
        }

        /// <summary>
        /// 战损覆写：将剧本中的特定残损状态应用到实体上
        /// </summary>
        public static void ApplyStateOverrides(Battalion battalion, JsonElement overridesElement)
        {
            if (overridesElement.ValueKind != JsonValueKind.Object) return;

            foreach (var compProp in overridesElement.EnumerateObject())
            {
                var company = battalion.Companies.FirstOrDefault(c => c.CompanyId == compProp.Name);
                if (company == null) continue;

                foreach (var platProp in compProp.Value.EnumerateObject())
                {
                    var platoon = company.Platoons.FirstOrDefault(p => p.PlatoonId == platProp.Name);
                    if (platoon == null) continue;

                    foreach (var categoryProp in platProp.Value.EnumerateObject())
                    {
                        foreach (var unitProp in categoryProp.Value.EnumerateObject())
                        {
                            var targetUnit = platoon.Units.FirstOrDefault(u => 
                                u.Category == categoryProp.Name && 
                                u.NodeId == unitProp.Name);

                            if (targetUnit != null && unitProp.Value.TryGetProperty("current_hp", out JsonElement hpElement))
                            {
                                targetUnit.CurrentHp = hpElement.GetInt32();
                            }
                        }
                    }
                }
            }
        }
    }
}