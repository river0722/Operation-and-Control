
import sys, json
from docx import Document
paths = [
 r"D:\pmac资料\output\上位机需求分析与功能设计.docx",
 r"D:\pmac资料\output\5.2_功能模块规划与工艺流程\5.2_功能模块规划与工艺流程.docx",
 r"D:\pmac资料\output\5.3_上位机绘图与CAD文件读取\5.3_上位机绘图与CAD文件读取.docx",
]
for p in paths:
    d = Document(p)
    paras = d.paragraphs
    body = d.element.body
    total = sum(len(x.text) for x in paras)
    print("="*80)
    print(p)
    print("paragraphs:", len(paras), "chars:", total, "tables:", len(d.tables), "images:", len(d.inline_shapes))
    # section info
    s = d.sections[0]
    print("page w/h:", s.page_width, s.page_height, "margins:", s.left_margin, s.right_margin, s.top_margin, s.bottom_margin)
    print("styles used:", sorted(set(x.style.name for x in paras)))
