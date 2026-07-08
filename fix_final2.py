import os
p = r"D:\project-89\Scripts\Scenarios\FuldaGapScenario.cs"
with open(p, "rb") as f:
    c = f.read()
# Replace "x22 with just " (remove the x22 garbage)
before = len(c)
c = c.replace(b'"x22', b'"')
after = len(c)
print(f"Replaced {before - after} bytes across {(before - after) // 3} occurrences")
with open(p, "wb") as f:
    f.write(c)
print("Fixed FuldaGapScenario.cs: removed x22 from terrain strings")
