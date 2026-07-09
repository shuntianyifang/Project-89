import json

with open(r"D:\project-89\tools\spec_content_2.json", "r", encoding="utf-8") as f:
    data = json.load(f)

for k in data:
    print(f"Key: {k}")
    print(f"  Codepoints: {[hex(ord(c)) for c in k]}")

# Now test matching against known heading text
from docx import Document
doc = Document(r"D:\课设\功能规格说明模板.docx")
h71 = doc.paragraphs[71].text.strip()
print(f"\nHeading: {h71}")
print(f"  Codepoints: {[hex(ord(c)) for c in h71]}")

# Test each key
for k, v in data.items():
    if isinstance(v, str):
        print(f"  '{k}' in heading: {k in h71}")
