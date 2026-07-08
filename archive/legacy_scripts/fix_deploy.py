f = open('Scripts/Systems/Combat/CombatAutoDeployer.cs', 'r', encoding='utf-8'); lines = f.readlines(); f.close()
new_body = [
    '        public static CombatForce AutoFillForce(\n',
    '\n            List<(Battalion bat, Vector2I_ pos)> eligibleUnits,\n',
    '\n            Battalion primaryDefender)\n',
    '        {\n',
    '\n            var result = new CombatForce();\n',
    '\n            var available = new List<Batalion>();\n',
    '\n            if (primaryDefender != null) available.Add(primaryDefender);\n',
    '\n            foreach (var e in eligibleUnits)\n                if (e.bat != primaryDefender) available.Add(e.bat);\n',
    '\n            if (available.Count == 0) return result;\n result.LeadBattalion = primaryDefender ?? available[0];\n            var used = new HashSet<Batalion> { result.LeadBattalion };\n            var mainPool = available.Where(b => !used.Contains(b) && b.CanFillMain()).OrderByDescending(b => b.GetActualAttack()).ToList();\n            var second = mainPool.FirstOrDefault();\n            if (second != null) { result.MainSlot2 = second; used.Add(second); }\n            var supPool = available.Where(b => !used.Contains(b) && b.CanFillSupport()).ToList();\n            var cmdB = supPool.FirstOrDefault(b => CombatUtils.HasAnyCapability(b, "Command"));\n            var reconB = supPool.FirstOrDefault(b => CombatUtils.HasAnyCapability(b, "Recon"));\n            var defBA = supPool.OrderByDescending(b => b.GetActualDefense()).FirstOrDefault();\n            Batalion support = cmdB ?? reconB ?? defB;\n            if (support != null) { result.SupportSlot = support; used.Add(support); }\n            var artyB = available.FirstOrDefault(b => !used.Contains(b) && b.CanFillArtillery());\n            if (artyB != null) { result.ArtillerySlot = artyB; used.Add(artyB); }\n            return result;\n        }\n',
]
lines = lines[:13] + new_body + lines[44:]
f.writelines(lines); f.close()
print('fixed')