"""Read-only structural audit; counts data entries, not historical vehicles."""
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def audit():
    templates, units = {}, {}
    issues = []
    for folder, target in [("Templates", templates), ("Units", units)]:
        for path in sorted((ROOT / "Scripts/Data" / folder).glob("*.json")):
            for key, value in read(path).items():
                if key in target:
                    issues.append(f"Duplicate {folder} ID: {key}")
                target[key] = value
    def entries(value):
        if isinstance(value, dict):
            if "unit_id" in value:
                yield value
            else:
                for child in value.values():
                    yield from entries(child)
        elif isinstance(value, list):
            for child in value:
                yield from entries(child)
    composition = {}
    for key, template in templates.items():
        counts = Counter()
        for entry in entries(template):
            uid = entry["unit_id"]
            counts[uid] += 1
            if uid not in units:
                issues.append(f"{key}: missing unit {uid}")
            elif entry.get("max_hp") != units[uid]["combat_stats"]["max_hp"]:
                issues.append(f"{key}/{uid}: template HP {entry.get('max_hp')} vs unit HP {units[uid]['combat_stats']['max_hp']}")
        composition[key] = dict(sorted(counts.items()))
    factions = {}
    identifiers = set()
    for side in ["blue", "red"]:
        rows = read(ROOT / f"Scripts/Data/Scenarios/Fulda_Gap/oob_{side}.json")[f"faction_{side}"]
        for row in rows:
            uid = row["instance_id"]
            if uid in identifiers:
                issues.append(f"Duplicate instance ID: {uid}")
            identifiers.add(uid)
            if row["template_id"] not in templates:
                issues.append(f"{uid}: missing template")
            if not (0 <= row["x"] < 50 and 0 <= row["y"] < 30):
                issues.append(f"{uid}: out of bounds")
        factions[side] = {"instances": len(rows), "templates": dict(Counter(r["template_id"] for r in rows))}
    source = (ROOT / "Scripts/Scenarios/FuldaGapScenario.cs").read_text(encoding="utf-8-sig")
    layers = {}
    for name in ["TerrainRows", "InfraRows"]:
        block = re.search(rf"{name}\s*=\s*\{{(.*?)\}};", source, re.S).group(1)
        rows = re.findall(r'"([0-9]+)"', block)
        layers[name] = {"rows": len(rows), "row_lengths": dict(Counter(map(len, rows)))}
        if len(rows) != 30 or any(len(row) != 50 for row in rows):
            issues.append(f"{name}: expected 30 rows of 50 cells")
    return {"schema_version": 1, "note": "Structural entries are not a historical equipment census; unresolved findings do not mean tests failed.", "factions": factions, "map_layers": layers, "template_composition": composition, "issues": sorted(set(issues))}

if __name__ == "__main__":
    print(json.dumps(audit(), ensure_ascii=False, indent=2))
