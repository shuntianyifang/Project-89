import os
p = r"D:\project-89\Scripts\Scenarios\FuldaGapScenario.cs"
f = open(p, "r")
c = f.read()
f.close()
c = c.replace(chr(92)+chr(120)+chr(50)+chr(50), chr(34))
f = open(p, "w")
f.write(c)
f.close()
print("Fixed backslash-x22 to real double-quotes")
