
import io, re
from docx import Document
from docx.table import Table
from docx.text.paragraph import Paragraph
pairs = [
 ("A", r"D:\pmac资料\output\_rewrite_tmp\backup\A.docx", r"D:\pmac资料\output\上位机需求分析与功能设计.docx"),
 ("B", r"D:\pmac资料\output\_rewrite_tmp\backup\B.docx", r"D:\pmac资料\output\_rewrite_tmp\B_new.docx"),
 ("C", r"D:\pmac资料\output\_rewrite_tmp\backup\C.docx", r"D:\pmac资料\output\_rewrite_tmp\C_new.docx"),
]
def stats(p):
    d=Document(p)
    body_chars=sum(len(x.text) for x in d.paragraphs)
    tchars=0
    for t in d.tables:
        for r in t.rows:
            for c in r.cells:
                tchars+=len(c.text)
    return len(d.paragraphs), body_chars, len(d.tables), tchars, len(d.inline_shapes)
for k, old, new in pairs:
    o=stats(old); n=stats(new)
    print(k, "old paras/chars/tables/tblchars/imgs:", o)
    print(k, "new paras/chars/tables/tblchars/imgs:", n)
    print(k, "正文缩减 %.0f%%" % (100*(1-n[1]/o[1])), " 表格缩减 %.0f%%" % (100*(1-n[3]/o[3])))
