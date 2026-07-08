with open(r"D:\project-89\Scripts\Scenarios\FuldaGapScenario.cs", "r", encoding="utf-8") as f:
    lines = f.readlines()
ts = next(i for i, l in enumerate(lines) if "TerrainRows" in l and "string[]" in l)
s = lines[ts + 2].strip().strip(",").strip('"')
print("Terrain row 0:", s)
print("Length:", len(s), "(expect 50)")
print("Valid chars:", all(c in "0123" for c in s))

si = next(i for i, l in enumerate(lines) if "InfraRows" in l and "string[]" in l)
infra = lines[si + 2].strip().strip(",").strip('"')
print("Infra row 0:", infra[:50])
print("Infra length:", len(infra))

ss = next(i for i, l in enumerate(lines) if "SupplySpecial" in l and "string[]" in l)
sup = lines[ss + 2].strip().strip(",").strip('"')
print("Supply row 0:", sup[:50])
print("Supply length:", len(sup))
