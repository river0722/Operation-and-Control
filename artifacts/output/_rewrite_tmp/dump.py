
import json, io
from docx import Document
paths = {
 "A": r"D:\pmac资料\output\上位机需求分析与功能设计.docx",
 "B": r"D:\pmac资料\output\5.2_功能模块规划与工艺流程\5.2_功能模块规划与工艺流程.docx",
 "C": r"D:\pmac资料\output\5.3_上位机绘图与CAD文件读取\5.3_上位机绘图与CAD文件读取.docx",
}
out=[]
for k,p in paths.items():
    d = Document(p)
    out.append("="*70)
    out.append(f"### {k} {p}")
    body = d.element.body
    from docx.table import Table
    from docx.text.paragraph import Paragraph
    idx_p = 0; idx_t = 0
    for child in body.iterchildren():
        tag = child.tag.split('}')[-1]
        if tag == 'p':
            para = Paragraph(child, d)
            txt = para.text
            if txt.strip():
                out.append(f"[P{idx_p}|{para.style.name}] {txt}")
            idx_p += 1
        elif tag == 'tbl':
            t = Table(child, d)
            out.append(f"[TABLE{idx_t}] rows={len(t.rows)} cols={len(t.columns)}")
            for row in t.rows:
                out.append("  | " + " | ".join(c.text.replace("\n"," / ").strip() for c in row.cells))
            idx_t += 1
with io.open(r"D:\pmac资料\output\_rewrite_tmp\dump.txt","w",encoding="utf-8") as f:
    f.write("\n".join(out))
print("ok", len(out))
