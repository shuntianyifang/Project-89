from docx import Document
doc = Document(r"D:\project-89\功能规格说明_冷戰兵棋推演系统.docx")
for i,p in enumerate(doc.paragraphs):
    t = p.text.strip()
    s = p.style.name
    if "xxx" in t.lower() or (s.startswith("Heading") and t):
        print(f"[{i}] [{s}] '{t[:100]}'")
