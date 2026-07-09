import json

fname = r"D:\project-89\tools\spec_content_2.json"
with open(fname, "r", encoding="utf-8") as f:
    data = json.load(f)

# Fix keys to match actual template heading text
# Template uses: 模块介绍, 模块约束条件和输入输出, 接口说明
new_data = {}
renames = {
    "3.2.1模块概述": "3.2.1模块介绍",
    "3.2.2模块约束": "3.2.2模块约束条件和输入输出",
    "3.3.1模块概述": "3.3.1模块介绍",
    "3.3.2模块约束": "3.3.2模块约束条件和输入输出",
}
for k, v in data.items():
    if k in renames:
        new_data[renames[k]] = v
    else:
        new_data[k] = v

with open(fname, "w", encoding="utf-8") as f:
    json.dump(new_data, f, ensure_ascii=False, indent=2)

for old, new in renames.items():
    print(f"Renamed: {old} -> {new}")
print("Done")
