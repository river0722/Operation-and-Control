
import io, zipfile, re
from docx import Document
from docx.shared import Emu
paths = {
 "A": r"D:\pmac资料\output\上位机需求分析与功能设计.docx",
 "B": r"D:\pmac资料\output\5.2_功能模块规划与工艺流程\5.2_功能模块规划与工艺流程.docx",
 "C": r"D:\pmac资料\output\5.3_上位机绘图与CAD文件读取\5.3_上位机绘图与CAD文件读取.docx",
}
buf=[]
for k,p in paths.items():
    d=Document(p)
    buf.append("="*50); buf.append(k)
    for si,s in enumerate(d.sections):
        buf.append(f" sec{si} page {s.page_width}x{s.page_height} margins L{s.left_margin} R{s.right_margin} T{s.top_margin} B{s.bottom_margin}")
        hdr = " | ".join(x.text for x in s.header.paragraphs if x.text.strip())
        ftr = " | ".join(x.text for x in s.footer.paragraphs if x.text.strip())
        buf.append(f"   header={hdr!r} footer={ftr!r} footer_linked={s.footer.is_linked_to_previous}")
        f=x=0
        for para in s.footer.paragraphs:
            f+=len(para._element.findall('.//{http://schemas.openxmlformats.org/wordprocessingml/2006/main}fldSimple'))
        buf.append(f"   footer fldSimple={f}")
    for i,sh in enumerate(d.inline_shapes):
        buf.append(f" img{i}: type={sh.type} w={sh.width} h={sh.height} inches={Emu(sh.width).inches:.2f}x{Emu(sh.height).inches:.2f}")
    with zipfile.ZipFile(p) as z:
        med=[n for n in z.namelist() if n.startswith('word/media/')]
        buf.append(" media: "+", ".join(f"{n}({z.getinfo(n).file_size})" for n in med))
    # normal style eastAsia
    st=d.styles['Normal']
    buf.append(" normal xml: "+re.sub(r"\s+"," ",st.element.xml)[:600])
with io.open(r"D:\pmac资料\output\_rewrite_tmp\fmt2.txt","w",encoding="utf-8") as f:
    f.write("\n".join(buf))
print("ok")
