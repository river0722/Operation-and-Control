
import io
from docx import Document
from docx.shared import Pt
paths = {
 "A": r"D:\pmac资料\output\上位机需求分析与功能设计.docx",
 "B": r"D:\pmac资料\output\5.2_功能模块规划与工艺流程\5.2_功能模块规划与工艺流程.docx",
 "C": r"D:\pmac资料\output\5.3_上位机绘图与CAD文件读取\5.3_上位机绘图与CAD文件读取.docx",
}
buf=[]
for k,p in paths.items():
    d=Document(p)
    buf.append("="*60); buf.append(k)
    st=d.styles
    names=[s.name for s in st]
    buf.append("styles: "+", ".join(names))
    for nm in ["Normal","Heading 1","Heading 2","Title","List Number","Table Grid"]:
        try:
            s=st[nm]
        except KeyError:
            continue
        f=s.font
        buf.append(f"  style {nm}: font={f.name} size={f.size} bold={f.bold} eastasia?")
        pf=s.paragraph_format
        buf.append(f"     align={pf.alignment} first_line={pf.first_line_indent} space_before={pf.space_before} space_after={pf.space_after} line={pf.line_spacing} rule={pf.line_spacing_rule}")
    # first body paragraph run detail
    for i,para in enumerate(d.paragraphs[:8]):
        if not para.text.strip(): continue
        r=para.runs[0] if para.runs else None
        if r is None: continue
        rpr = r._element.rPr
        import re
        xml = rpr.xml if rpr is not None else ""
        xml = re.sub(r"\s+"," ",xml)
        buf.append(f"  P{i} style={para.style.name} runfont={r.font.name} size={r.font.size} bold={r.bold} rPr={xml[:400]}")
    # table style
    for ti,t in enumerate(d.tables[:2]):
        buf.append(f"  table{ti} style={t.style.name if t.style else None} align={t.alignment} autofit={t.autofit}")
        cell=t.rows[0].cells[0]
        p0=cell.paragraphs[0]
        r0=p0.runs[0] if p0.runs else None
        if r0 is not None:
            buf.append(f"     hdr style={p0.style.name} font={r0.font.name} size={r0.font.size} bold={r0.bold}")
with io.open(r"D:\pmac资料\output\_rewrite_tmp\fmt.txt","w",encoding="utf-8") as f:
    f.write("\n".join(buf))
print("done")
