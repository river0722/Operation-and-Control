
import io
from docx import Document
from docx.table import Table
from docx.text.paragraph import Paragraph
paths = {
 "A": r"D:\pmac资料\output\上位机需求分析与功能设计.docx",
 "B": r"D:\pmac资料\output\_rewrite_tmp\B_new.docx",
 "C": r"D:\pmac资料\output\_rewrite_tmp\C_new.docx",
}
out=[]
for k,p in paths.items():
    d=Document(p)
    out.append("#"*20+" "+k)
    for child in d.element.body.iterchildren():
        tag=child.tag.split('}')[-1]
        if tag=='p':
            para=Paragraph(child,d)
            t=para.text.strip()
            if t:
                out.append(f"[{para.style.name}] {t}")
        elif tag=='tbl':
            t=Table(child,d)
            out.append(f"<TABLE {len(t.rows)}x{len(t.columns)}>")
            for r in t.rows[:2]:
                out.append("   | "+" | ".join(c.text.strip() for c in r.cells))
io.open(r"D:\pmac资料\output\_rewrite_tmp\proof.txt","w",encoding="utf-8").write("\n".join(out))
print("ok")
