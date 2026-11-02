
import os, re
from docx import Document
from docx.shared import Pt, Cm, RGBColor, Emu
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.style import WD_STYLE_TYPE
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

HEI = "\u9ed1\u4f53"
SONG = "\u5b8b\u4f53"
LATIN = "Times New Roman"

def set_fonts(owner, latin, ea, size=None, bold=None, color=None):
    f = owner.font
    if size is not None:
        f.size = Pt(size)
    if bold is not None:
        f.bold = bold
    if color is not None:
        f.color.rgb = color
    f.name = latin
    rPr = owner.element.get_or_add_rPr()
    rFonts = rPr.find(qn('w:rFonts'))
    if rFonts is None:
        rFonts = OxmlElement('w:rFonts')
        rPr.append(rFonts)
    rFonts.set(qn('w:ascii'), latin)
    rFonts.set(qn('w:hAnsi'), latin)
    rFonts.set(qn('w:eastAsia'), ea)

def setup_doc():
    doc = Document()
    sec = doc.sections[0]
    sec.page_width = Emu(7560310)
    sec.page_height = Emu(10692130)
    sec.left_margin = Emu(791845)
    sec.right_margin = Emu(791845)
    sec.top_margin = Emu(756285)
    sec.bottom_margin = Emu(720090)
    sec.header_distance = Cm(1.2)
    sec.footer_distance = Cm(1.2)

    normal = doc.styles['Normal']
    set_fonts(normal, LATIN, SONG, 10.5)
    pf = normal.paragraph_format
    pf.line_spacing = 1.25
    pf.space_before = Pt(0)
    pf.space_after = Pt(2.5)
    pf.first_line_indent = Pt(21)
    pf.widow_control = True

    specs = {
        'Heading 1': (HEI, 16, 0, 10),
        'Heading 2': (HEI, 13, 12, 5),
        'Heading 3': (HEI, 11, 8, 3),
    }
    for name, (ea, size, before, after) in specs.items():
        st = doc.styles[name]
        set_fonts(st, LATIN, ea, size, True, RGBColor(0, 0, 0))
        st.font.italic = False
        p = st.paragraph_format
        p.space_before = Pt(before)
        p.space_after = Pt(after)
        p.first_line_indent = Pt(0)
        p.line_spacing = 1.15
        p.keep_with_next = True

    cap = doc.styles.add_style('FigCaption', WD_STYLE_TYPE.PARAGRAPH)
    cap.base_style = normal
    set_fonts(cap, LATIN, SONG, 9.5)
    cap.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap.paragraph_format.first_line_indent = Pt(0)
    cap.paragraph_format.space_before = Pt(3)
    cap.paragraph_format.space_after = Pt(8)
    cap.paragraph_format.line_spacing = 1.0

    tcap = doc.styles.add_style('TableCaption', WD_STYLE_TYPE.PARAGRAPH)
    tcap.base_style = normal
    set_fonts(tcap, LATIN, SONG, 9.5, True)
    tcap.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    tcap.paragraph_format.first_line_indent = Pt(0)
    tcap.paragraph_format.space_before = Pt(8)
    tcap.paragraph_format.space_after = Pt(3)
    tcap.paragraph_format.line_spacing = 1.0
    tcap.paragraph_format.keep_with_next = True

    tcell = doc.styles.add_style('TableText', WD_STYLE_TYPE.PARAGRAPH)
    tcell.base_style = normal
    set_fonts(tcell, LATIN, SONG, 9)
    tcell.paragraph_format.first_line_indent = Pt(0)
    tcell.paragraph_format.space_before = Pt(0.5)
    tcell.paragraph_format.space_after = Pt(0.5)
    tcell.paragraph_format.line_spacing = 1.1

    fml = doc.styles.add_style('Formula', WD_STYLE_TYPE.PARAGRAPH)
    fml.base_style = normal
    set_fonts(fml, LATIN, SONG, 10.5)
    fml.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    fml.paragraph_format.first_line_indent = Pt(0)
    fml.paragraph_format.space_before = Pt(3)
    fml.paragraph_format.space_after = Pt(3)
    fml.paragraph_format.line_spacing = 1.2

    cp = doc.core_properties
    cp.author = ""
    cp.last_modified_by = ""
    cp.comments = ""
    cp.category = ""
    cp.subject = ""
    add_page_footer(doc)
    return doc

def add_page_footer(doc):
    footer = doc.sections[0].footer
    p = footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Pt(0)
    r = p.add_run("\u2014 ")
    set_fonts(r, LATIN, SONG, 9)
    fld = OxmlElement('w:fldSimple')
    fld.set(qn('w:instr'), ' PAGE ')
    rr = OxmlElement('w:r')
    rpr = OxmlElement('w:rPr')
    rf = OxmlElement('w:rFonts')
    rf.set(qn('w:ascii'), LATIN); rf.set(qn('w:hAnsi'), LATIN); rf.set(qn('w:eastAsia'), SONG)
    sz = OxmlElement('w:sz'); sz.set(qn('w:val'), '18')
    rpr.append(rf); rpr.append(sz)
    t = OxmlElement('w:t'); t.text = "1"
    rr.append(rpr); rr.append(t); fld.append(rr)
    p._p.append(fld)
    r2 = p.add_run(" \u2014")
    set_fonts(r2, LATIN, SONG, 9)

def add_rich(par, text):
    parts = re.split(r'(\*\*.+?\*\*)', text)
    for part in parts:
        if not part:
            continue
        if part.startswith('**') and part.endswith('**'):
            r = par.add_run(part[2:-2])
            r.bold = True
        else:
            par.add_run(part)

def para(doc, text, indent=True, space_after=None, align=None):
    p = doc.add_paragraph()
    if not indent:
        p.paragraph_format.first_line_indent = Pt(0)
    if align is not None:
        p.alignment = align
    if space_after is not None:
        p.paragraph_format.space_after = Pt(space_after)
    add_rich(p, text)
    return p

def h(doc, level, text):
    return doc.add_heading(text, level=level)

def formula(doc, text):
    p = doc.add_paragraph(style='Formula')
    p.add_run(text)
    return p

def caption(doc, text, kind='fig'):
    p = doc.add_paragraph(style='FigCaption' if kind == 'fig' else 'TableCaption')
    p.add_run(text)
    return p

def figure(doc, path, width_in, max_h_in=7.35):
    from PIL import Image
    im = Image.open(path)
    ratio = im.height / float(im.width)
    w = width_in
    if w * ratio > max_h_in:
        w = max_h_in / ratio
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Pt(0)
    p.paragraph_format.space_before = Pt(6)
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.keep_with_next = True
    p.add_run().add_picture(path, width=Emu(int(w * 914400)))
    return p

def shade(cell, hexcolor):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), hexcolor)
    tcPr.append(shd)

def set_cell_margins(table, left=0.14, right=0.14, top=0.04, bottom=0.04):
    tblPr = table._tbl.tblPr
    mar = OxmlElement('w:tblCellMar')
    for tag, val in (('w:top', top), ('w:left', left), ('w:bottom', bottom), ('w:right', right)):
        e = OxmlElement(tag)
        e.set(qn('w:w'), str(int(val * 567)))
        e.set(qn('w:type'), 'dxa')
        mar.append(e)
    tblPr.append(mar)

def add_table(doc, cap, headers, rows, widths, center_cols=(0,), keep_together=None, keep_head_rows=3):
    caption(doc, cap, 'table')
    t = doc.add_table(rows=1, cols=len(headers))
    t.style = 'Table Grid'
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    tblPr = t._tbl.tblPr
    layout = OxmlElement('w:tblLayout'); layout.set(qn('w:type'), 'fixed')
    tblPr.append(layout)
    set_cell_margins(t)
    for i, wcm in enumerate(widths):
        t.columns[i].width = Cm(wcm)
    hdr = t.rows[0]
    trPr = hdr._tr.get_or_add_trPr()
    th = OxmlElement('w:tblHeader'); trPr.append(th)
    cs = OxmlElement('w:cantSplit'); trPr.append(cs)
    for i, text in enumerate(headers):
        c = hdr.cells[i]
        c.width = Cm(widths[i])
        p = c.paragraphs[0]
        p.style = doc.styles['TableText']
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(text); r.bold = True
        c.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        shade(c, 'EDEDED')
    for row in rows:
        tr = t.add_row()
        trPr = tr._tr.get_or_add_trPr()
        cs = OxmlElement('w:cantSplit'); trPr.append(cs)
        for i, text in enumerate(row):
            c = tr.cells[i]
            c.width = Cm(widths[i])
            p = c.paragraphs[0]
            p.style = doc.styles['TableText']
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER if i in center_cols else WD_ALIGN_PARAGRAPH.LEFT
            add_rich(p, text)
            c.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    all_rows = t.rows
    if keep_together is None:
        keep_together = len(rows) <= 9
    if keep_together:
        for row in all_rows[:-1]:
            for c in row.cells:
                for p in c.paragraphs:
                    p.paragraph_format.keep_with_next = True
    elif keep_head_rows:
        for row in all_rows[:keep_head_rows]:
            for c in row.cells:
                for p in c.paragraphs:
                    p.paragraph_format.keep_with_next = True
    sp = doc.add_paragraph()
    sp.paragraph_format.first_line_indent = Pt(0)
    sp.paragraph_format.space_before = Pt(0)
    sp.paragraph_format.space_after = Pt(0)
    r = sp.add_run("")
    r.font.size = Pt(4)
    return t