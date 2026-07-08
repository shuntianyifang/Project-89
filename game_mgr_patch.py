import json, sys

with open('GameManager.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Add using statements
old_using = 'using ColdWarWargame.Tests.Victory;'
new_using = old_using + '\nusing ColdWarWargame.UI;\n' + '\nusing ColdWarWargame.Systems.Battlefield;'
if old_using in content:
    content = content.replace(old_using, new_using)
    print('Added using statements')
else:
    print('WARN: Could not find using anchor')

# 2. Add _combatPanel field
old_field = 'private CombatResolver _resolver = new();'
new_field = old_field + '\n    private CombatDeploymentPanel _combatPanel;'
if old_field in content:
    content = content.replace(old_field, new_field)
    print('Added _combatPanel field')

# 3. Replace the combat resolve in OnUnitClicked
old_combat = '_infLabel.Text = "Combat: " + _sel.Unit.Name + " -> " + bat.Name + " V=" + result.Advantage.Value.ToString("0.00");'
new_combat = '_infoLabel.Text = "Click to select";'
if old_combat in content:
    content = content.replace(old_combat, new_combat)
    print('Replaced old combat result display')

with open('GameManager.cs', 'w', encoding='utf-8') as f:
    f.write(content)
print('GameManager.cs updated')