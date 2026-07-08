f = open('Scripts/Systems/Combat/CombatDeploymentPanel.cs','r',encoding='utf-8'); lines = f.readlines(); f.close()

for i,l in enumerate(lines):
    if 'var availScroll = new ScrollContainer();' in l:
        print(f'ScrollContainer at line {i+1}')
        idx = i
        break

if idx >= 0:
    new_block = [
        '            var availScroll = new VBoxContainer();\n',
        '            availScroll.Size = new Vector2(880, 0);\n',
        '            availScroll.AddThemeConstantOverride("separation", 6);\n',
        '            vbox.AddChild(availScroll);\n',
        '            var availFlow = availScroll;\n',
    ]
    lines = lines[:idx] + new_block + lines[idx+9:]
    f = open('Scripts/Systems/Combat/CombatDeploymentPanel.cs','w',encoding='utf-8'); f.writelines(lines); f.close()
    print('Replaced')
else:
    print('Not found')