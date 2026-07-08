using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System;
using Godot;
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
                TemplateRole = template.Role ?? "main",
                TemplateId = templateId,
                IsAdvancedReconBattalion = IsAdvancedReconTemplate(templateId, template.Name),
                BattalionTags = template.BattalionTags != null
                    ? new HashSet<string>(template.BattalionTags, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        static bool IsAdvancedReconTemplate(string templateId, string templateName)
        {
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                string id = templateId.ToLowerInvariant();
                if (id.Contains("recon") || id.Contains("cav_squadron"))
                    return true;
            }

            if (!string.IsNullOrWhiteSpace(templateName))
            {
                string name = templateName.ToLowerInvariant();
                if (name.Contains("recon") || name.Contains("侦察") || name.Contains("骑兵"))
                    return true;
            }

            return false;
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
                if (company == null)
                {
                    GD.PrintErr($"[OOB Override] state_overrides skipped: company '{compProp.Name}' not found in battalion '{battalion.InstanceId}'.");
                    continue;
                }

                foreach (var platProp in compProp.Value.EnumerateObject())
                {
                    var platoon = company.Platoons.FirstOrDefault(p => p.PlatoonId == platProp.Name);
                    if (platoon == null)
                    {
                        GD.PrintErr($"[OOB Override] state_overrides skipped: platoon '{platProp.Name}' not found in '{company.CompanyId}'.");
                        continue;
                    }

                    foreach (var categoryProp in platProp.Value.EnumerateObject())
                    {
                        foreach (var unitProp in categoryProp.Value.EnumerateObject())
                        {
                            var targetUnit = platoon.Units.FirstOrDefault(u =>
                                u.Category == categoryProp.Name && u.NodeId == unitProp.Name);
                            if (targetUnit == null)
                            {
                                GD.PrintErr($"[OOB Override] state_overrides skipped: unit '{unitProp.Name}' not found in '{company.CompanyId}/{platoon.PlatoonId}/{categoryProp.Name}'.");
                                continue;
                            }

                            if (unitProp.Value.TryGetProperty("current_hp", out JsonElement hpElement))
                            {
                                int maxHp = targetUnit.Template.CombatStats.MaxHp;
                                targetUnit.CurrentHp = Math.Clamp(hpElement.GetInt32(), 0, maxHp);
                            }
                        }
                    }
                }
            }
        }

        public static void ApplyStructureOverrides(Battalion battalion, JsonElement overridesElement)
        {
            if (overridesElement.ValueKind != JsonValueKind.Object) return;

            if (overridesElement.TryGetProperty("remove_nodes", out JsonElement removeNodes) &&
                removeNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var removeItem in removeNodes.EnumerateArray())
                {
                    if (removeItem.ValueKind != JsonValueKind.String) continue;
                    RemoveNodeByPath(battalion, removeItem.GetString());
                }
            }

            if (overridesElement.TryGetProperty("add_nodes", out JsonElement addNodes) &&
                addNodes.ValueKind == JsonValueKind.Object)
            {
                foreach (var addNode in addNodes.EnumerateObject())
                    AddNodeByPath(battalion, addNode.Name, addNode.Value);
            }
        }

        static void RemoveNodeByPath(Battalion battalion, string rawPath)
        {
            var path = NormalizePath(rawPath);
            if (path.Count < 2 || path[0] != "companies")
            {
                GD.PrintErr($"[OOB Override] remove_nodes skipped: invalid path '{rawPath}'.");
                return;
            }

            string companyId = path[1];
            var company = battalion.Companies.FirstOrDefault(c => c.CompanyId == companyId);
            if (company == null)
            {
                GD.PrintErr($"[OOB Override] remove_nodes skipped: company '{companyId}' not found.");
                return;
            }

            if (path.Count == 2)
            {
                battalion.Companies.Remove(company);
                return;
            }

            if (path.Count >= 4 && path[2] == "platoons")
            {
                string platoonId = path[3];
                var platoon = company.Platoons.FirstOrDefault(p => p.PlatoonId == platoonId);
                if (platoon == null)
                {
                    GD.PrintErr($"[OOB Override] remove_nodes skipped: platoon '{platoonId}' not found in '{companyId}'.");
                    return;
                }

                if (path.Count == 4)
                {
                    company.Platoons.Remove(platoon);
                    return;
                }

                if (path.Count == 6)
                {
                    string category = path[4];
                    string nodeId = path[5];
                    int removed = platoon.Units.RemoveAll(u => u.Category == category && u.NodeId == nodeId);
                    if (removed == 0)
                        GD.PrintErr($"[OOB Override] remove_nodes skipped: unit '{nodeId}' not found in '{companyId}/{platoonId}/{category}'.");
                    return;
                }
            }

            GD.PrintErr($"[OOB Override] remove_nodes skipped: unsupported path '{rawPath}'.");
        }

        static void AddNodeByPath(Battalion battalion, string rawPath, JsonElement payload)
        {
            var path = NormalizePath(rawPath);
            if (path.Count < 2 || path[0] != "companies")
            {
                GD.PrintErr($"[OOB Override] add_nodes skipped: invalid path '{rawPath}'.");
                return;
            }

            string companyId = path[1];
            if (path.Count == 2)
            {
                var company = new Company
                {
                    CompanyId = companyId,
                    Name = payload.TryGetProperty("name", out JsonElement nameEl)
                        ? nameEl.GetString()
                        : companyId
                };

                if (payload.TryGetProperty("platoons", out JsonElement platoonsEl) && platoonsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var platoonProp in platoonsEl.EnumerateObject())
                        company.Platoons.Add(BuildPlatoonFromJson(platoonProp.Name, platoonProp.Value));
                }

                battalion.Companies.RemoveAll(c => c.CompanyId == companyId);
                battalion.Companies.Add(company);
                return;
            }

            var targetCompany = battalion.Companies.FirstOrDefault(c => c.CompanyId == companyId);
            if (targetCompany == null)
            {
                targetCompany = new Company { CompanyId = companyId, Name = companyId };
                battalion.Companies.Add(targetCompany);
            }

            if (path.Count >= 4 && path[2] == "platoons")
            {
                string platoonId = path[3];

                if (path.Count == 4)
                {
                    var newPlatoon = BuildPlatoonFromJson(platoonId, payload);
                    targetCompany.Platoons.RemoveAll(p => p.PlatoonId == platoonId);
                    targetCompany.Platoons.Add(newPlatoon);
                    return;
                }

                if (path.Count == 6)
                {
                    string category = path[4];
                    string nodeId = path[5];

                    if (category != "units" && category != "infantry" && category != "vehicle")
                    {
                        GD.PrintErr($"[OOB Override] add_nodes skipped: invalid unit category '{category}' in '{rawPath}'.");
                        return;
                    }

                    var targetPlatoon = targetCompany.Platoons.FirstOrDefault(p => p.PlatoonId == platoonId);
                    if (targetPlatoon == null)
                    {
                        targetPlatoon = new Platoon
                        {
                            PlatoonId = platoonId,
                            Type = category == "units" ? "standard" : "composite"
                        };
                        targetCompany.Platoons.Add(targetPlatoon);
                    }

                    targetPlatoon.Units.RemoveAll(u => u.Category == category && u.NodeId == nodeId);
                    targetPlatoon.Units.Add(CreateUnitInstance(nodeId, category, payload));
                    return;
                }
            }

            GD.PrintErr($"[OOB Override] add_nodes skipped: unsupported path '{rawPath}'.");
        }

        static Platoon BuildPlatoonFromJson(string platoonId, JsonElement payload)
        {
            string type = payload.TryGetProperty("type", out JsonElement typeEl)
                ? typeEl.GetString()
                : "standard";

            var platoon = new Platoon { PlatoonId = platoonId, Type = type };

            if (payload.TryGetProperty("units", out JsonElement unitsEl) && unitsEl.ValueKind == JsonValueKind.Object)
                AddUnitsFromJsonObject(platoon, unitsEl, "units");

            if (payload.TryGetProperty("infantry", out JsonElement infEl) && infEl.ValueKind == JsonValueKind.Object)
                AddUnitsFromJsonObject(platoon, infEl, "infantry");

            if (payload.TryGetProperty("vehicle", out JsonElement vehEl) && vehEl.ValueKind == JsonValueKind.Object)
                AddUnitsFromJsonObject(platoon, vehEl, "vehicle");

            return platoon;
        }

        static void AddUnitsFromJsonObject(Platoon platoon, JsonElement unitsContainer, string category)
        {
            foreach (var unitProp in unitsContainer.EnumerateObject())
                platoon.Units.Add(CreateUnitInstance(unitProp.Name, category, unitProp.Value));
        }

        static SubUnitInstance CreateUnitInstance(string nodeId, string category, JsonElement unitPayload)
        {
            string unitId = unitPayload.GetProperty("unit_id").GetString();
            var instance = new SubUnitInstance(unitId)
            {
                NodeId = nodeId,
                Category = category
            };

            int maxHp = instance.Template.CombatStats.MaxHp;
            if (unitPayload.TryGetProperty("max_hp", out JsonElement maxHpEl))
                maxHp = maxHpEl.GetInt32();

            int currentHp = maxHp;
            if (unitPayload.TryGetProperty("current_hp", out JsonElement curHpEl))
                currentHp = curHpEl.GetInt32();

            instance.CurrentHp = Math.Clamp(currentHp, 0, instance.Template.CombatStats.MaxHp);
            return instance;
        }

        static List<string> NormalizePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return new List<string>();
            return rawPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
    }
}
