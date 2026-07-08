import sys

with open('GameManager.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find the combat block
start_idx = -1
for i, line in enumerate(lines):
    if '        else if (_sel != null)' in line:
        start_idx = i
        break

if start_idx < 0:
    print('ERROR: Could not find anchor')
    sys.exit(1)

# Find the end of the block - "        }" 8 spaces, at least 2 lines after start
end_idx = -1
for i in range(start_idx + 2, min(start_idx + 15, len(lines))):
    if lines[i].rstrip() == '        }':
        end_idx = i
        break

if end_idx < 0:
    print('ERROR: Could not find end of block')
    sys.exit(1)

print(f'Found block: lines {start_idx+1} to {end_idx+1}')

# New code to insert. Each entry is a single line string (with \\n)
new_lines = [
    '        else if (_sel != null)\\n',
    '        {\\n',
    '            if (_inCombat) return;\n',
    '            _inCombat = true;\n',
    '\n',
    '            var friendlyUnits = (_turnMgr.CurrentFaction == 1 ? _scenario.BlueBattalions : _scenario.RedBattalions)\n.\n                .Where(u => u.Item1 != _sel.Unit)\n',
    '                .ToList();\n',
    '            var eligible = EngagementResolver.GetEligibleUnits(pos, friendlyUnits, 2);\n',
    '            eligible.Insert(0, (_sel.Unit, _sel.Pos));\n',
    '\n',
    '            float terrainBonus = _scenario.Map.GetTile(pos).TerrainType switch { 1 => 0.1f, 2 => 0.3f, 3 => 0.4f, _ => 0f };\n',
    '            string[] terrainNames = { "Plains", "Forest", "Semi-Urban", "Urban" };\n',
    '            int terrainType = _scenario.Map.GetTile(pos).TerrainType;\n',
    '            string tName = terrainType >= 0 && terrainType < terrainNames.Length ? terrainNames[terrainType] : "??";\n',
    '            int tBonus = (int)(terrainBonus * 10);\n',
    '\n',
    '            _combatPanel = new CombatDeploymentPanel();\n',
    '            AddChild(_combatPanel);\n',
    '\n',
    '            Battalion defBat = bat;\n',
    '            Vector2I defPos = pos;\n',
    '\n',
    '            _combatPanel.OnAttackerConfirmed = (CombatForce attackerForce) =>\n',
    '            {\n',
    '                var defEligible = (_turnMgr.CurrentFaction == 1 ? _scenario.RedBattalions : _scenario.BlueBattalions)\n',
    '                    .Where(u => u.Item1 != defBat)\n',
    '                    .ToList();\n',
    '                var ctx = new CombatContext\n',
    '                {\n',
    '                    DefenderTerrainBonus = terrainBonus,\n',
    '                    AttackerOOSTurns = _sel.Unit.TurnsOOS,\n',
    '                    DefenderOOSTurns = defBat.TurnsOOS\n',
    '                };\n',
    '                var defenderForce = CombatAutoDeployer.AutoFillForce(defEligible, defBat);\n',
    '                var result = _resolver.ResolveCombat(\n',
    '                    attackerForce.GetAllBattalions(),\n',
    '                    defenderForce.GetAllBattalions(),\n',
    '                    ctx);\n',
    '                _combatPanel.RemoveContent();\n',
    '                _combatPanel.ShowResult(result);\n',
    '                _sel = null; _renderer.ClearSel(); _currentReachable.Clear();\n',
    '            };\n',
    '\n',
    '            _combatPanel.OnResultDismissed = () =>\n',
    '            {\n',
    '                if (_combatPanel != null) { _combatPanel.Dismiss(); _combatPanel = null; }\n',
    '                _inCombat = false;\n',
    '                _renderer.SetBlueUnits(_scenario.BlueBattalions);\n',
    '                _renderer.SetRedUnits(_scenario.RedBattalions);\n',
    '                _infoLabel.Text = "Click to select";\n',
    '            };\n',
    '\n',
    '            _combatPanel.OnCancel = () =>\n',
    '            {\n',
    '                if (_combatPanel != null) { _combatPanel.Dismiss(); _combatPanel = null; }\n',
    '                _sel = null; _renderer.ClearSel(); _currentReachable.Clear();\n',
    '                _inCombat = false;\n',
    '                _infoLabel.Text = "Combat cancelled";\n',
    '            };\n',
    '\n',
    '            _combatPanel.ShowAttackerPhase(_sel.Unit, defBat, eligible, tBonus, tName);\n',
    '        }\n',
]

new_content = lines[:start_idx] + new_lines + lines[end_idx+1:]

with open('GameManager.cs', 'w', encoding='utf-8') as f:
    f.writelines(new_content)
print('GameManager.cs patched')