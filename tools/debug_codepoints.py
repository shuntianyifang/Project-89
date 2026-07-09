import json, unicodedata
from docx import Document

doc = Document(r"D:\课设\功能规格说明模板.docx")

# Get prev heading text and JSON key
h71 = doc.paragraphs[71].text.strip()
h71_nfc = unicodedata.normalize("NFC", h71)

# Load JSON key
with open(r"D:\project-89\tools\spec_content_2.json", "r", encoding="utf-8") as f:
    data = json.load(f)
key = list(data.keys())[1]  # "3.2.1模块概述"
key_nfc = unicodedata.normalize("NFC", key)

print(f"Heading:      {h71_nfc}")
print(f"  Codepoints: {[hex(ord(c)) for c in h71_nfc]}")
print(f"Key:          {key_nfc}")
print(f"  Codepoints: {[hex(ord(c)) for c in key_nfc]}")
print(f"Match: {key_nfc in h71_nfc}")
print(f"Equal: {key_nfc == h71_nfc}")
