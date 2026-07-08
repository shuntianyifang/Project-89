using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using ColdWarWargame.Models;
using ColdWarWargame.Data.TOE;

namespace ColdWarWargame.Factories
{
    public static class BattalionFactory
    {
        public static Battalion CreateFullBattalion(string instanceId, string templateId, int faction)
        {
            var template = TemplateDatabase.GetTemplate(templateId);
            var battalion = new Battalion
            {
                InstanceId = instanceId,
                Name = template.Name,
                Faction = faction,
                CurrentAP = 12.0f,
                Fatigue = 0,
                TemplateRole = template.Role ?? "main"
            };
            foreach (var compKvp in template.Companies)
            {
                var company = new Company { CompanyId = compKvp.Key, Name = compKvp.Value.Name };
                foreach (var platKvp in compKvp.Value.Platoons)
                {
                    var platoon = new Platoon { PlatoonId = platKvp.Key, Type = platKvp.Value.Type };
                    if (platoon.Type == "standard" && platKvp.Value.Units != null)
                        InstantiateAndAddUnits(platoon, platKvp.Value.Units, "units");
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

        static void InstantiateAndAddUnits(Platoon platoon, Dictionary<string, SubUnitDef> unitDefs, string category)
        {
            foreach (var def in unitDefs)
            {
                var instance = new SubUnitInstance(def.Value.UnitId)
                {
                    NodeId = def.Key, Category = category
                };
                instance.CurrentHp = def.Value.MaxHp;
                platoon.Units.Add(instance);
            }
        }

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
                                u.Category == categoryProp.Name && u.NodeId == unitProp.Name);
                            if (targetUnit != null && unitProp.Value.TryGetProperty("current_hp", out JsonElement hpElement))
                                targetUnit.CurrentHp = hpElement.GetInt32();
                        }
                    }
                }
            }
        }
    }
}
