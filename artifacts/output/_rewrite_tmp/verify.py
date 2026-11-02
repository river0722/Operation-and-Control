
import zipfile
from docx import Document
files = [
 r"D:\pmac资料\output\上位机需求分析与功能设计.docx",
 r"D:\pmac资料\output\5.2_功能模块规划与工艺流程\5.2_功能模块规划与工艺流程_修订版.docx",
 r"D:\pmac资料\output\5.3_上位机绘图与CAD文件读取\5.3_上位机绘图与CAD文件读取_修订版.docx",
]
for f in files:
    d=Document(f)
    z=zipfile.ZipFile(f)
    bad=z.testzip()
    txt="".join(p.text for p in d.paragraphs)
    print(f.split("\\")[-1], "| paras", len(d.paragraphs), "tables", len(d.tables), "imgs", len(d.inline_shapes),
          "chars", len(txt), "zip_ok", bad is None, "media", len([n for n in z.namelist() if n.startswith("word/media/")]))
