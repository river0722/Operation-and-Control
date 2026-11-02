from pathlib import Path
import csv, json, math, shutil, hashlib, sys
BASE=Path(r'D:\pmac资料\output\5.3_上位机绘图与CAD文件读取')
ROOT=Path(r'D:\pmac资料')
def pairs(items):
    return ''.join(str(c)+'\n'+str(v)+'\n' for c,v in items)
def entity(kind, fields): return [(0,kind)]+fields
def dxf(name, entities, units=4):
    text=pairs([(0,'SECTION'),(2,'HEADER'),(9,'$ACADVER'),(1,'AC1015'),(9,'$INSUNITS'),(70,units),(0,'ENDSEC'),(0,'SECTION'),(2,'ENTITIES')]+entities+[(0,'ENDSEC'),(0,'EOF')])
    (BASE/'samples'/name).write_text(text,encoding='ascii')
def line(z=False): return entity('LINE',[(8,'0'),(10,0),(20,0),(30,1 if z else 0),(11,1),(21,2),(31,0)])
def spline(flags=0):
    return entity('SPLINE',[(8,'0'),(70,flags),(71,2),(72,6),(73,3),(74,0)]+[(40,k) for k in [0,0,0,1,1,1]]+[(10,0),(20,-6),(30,0),(10,2),(20,-2),(30,0),(10,4),(20,-6),(30,0)])
def prepare():
    for folder in ['samples','data','figures','reproduce']: (BASE/folder).mkdir(parents=True,exist_ok=True)
    main=entity('LINE',[(8,'0'),(10,-7),(20,-7),(11,-3),(21,-7)])
    main+=entity('ARC',[(8,'0'),(10,-5),(20,-3),(40,2),(50,0),(51,180)])
    main+=entity('CIRCLE',[(8,'0'),(10,4),(20,4),(40,2)])
    main+=entity('LWPOLYLINE',[(8,'0'),(90,4),(70,1),(10,-6),(20,2),(10,-2),(20,2),(10,-2),(20,6),(10,-6),(20,6)])
    main+=spline()
    main+=entity('POLYLINE',[(8,'0'),(66,1),(70,0),(10,0),(20,0),(30,0)])
    for x,y in [(1,-1),(3,1),(5,-1)]: main+=entity('VERTEX',[(8,'0'),(70,0),(10,x),(20,y),(30,0)])
    main+=entity('SEQEND',[(8,'0')])
    main+=entity('LWPOLYLINE',[(8,'0'),(90,2),(70,0),(10,-1),(20,2),(42,1),(10,1),(20,2)])
    dxf('cad-six-types-mm.dxf',main)
    dxf('inch-line.dxf',line(),1); dxf('unitless-line.dxf',line(),0)
    dxf('reject-text.dxf',entity('TEXT',[(8,'0'),(10,0),(20,0),(40,1),(1,'DEMO')]))
    dxf('reject-insert.dxf',entity('INSERT',[(8,'0'),(2,'BLOCK_A'),(10,0),(20,0)]))
    dxf('reject-nonzero-z.dxf',line(True))
    dxf('reject-periodic-spline.dxf',spline(2))
    dxf('reject-fit-only-spline.dxf',entity('SPLINE',[(70,0),(71,3),(72,0),(73,0),(74,2),(11,0),(21,0),(11,2),(21,2)]))
    dxf('reject-unit-code.dxf',line(),3)
    (BASE/'samples'/'reject-incomplete.dxf').write_text('0\nSECTION\n2\nENTITIES\n0\nLINE\n10\n0\n20\n0\n11\n1\n21\n1\n0\nENDSEC\n',encoding='ascii')
    shutil.copy2(ROOT/'online/examples/course-shapes.dxf',BASE/'samples/course-shapes.dxf')
    shutil.copy2(ROOT/'tmp/section53_evidence.cs',BASE/'reproduce/section53_evidence.cs')
    print(BASE)
def read_points(name):
    groups={}
    with (BASE/'data'/name).open(encoding='utf-8-sig',newline='') as f:
        for r in csv.DictReader(f): groups.setdefault(int(r['stroke']),[]).append((float(r['x_mm']),float(r['y_mm'])))
    return list(groups.values())
def distseg(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]; den=dx*dx+dy*dy
    t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/den)) if den else 0
    return math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dy)
def analyze():
    imported=read_points('cad-six-types-mm-import.csv')
    n=2000
    arc=lambda cx,cy,r,start,sweep:[(cx+r*math.cos(start+sweep*i/n),cy+r*math.sin(start+sweep*i/n)) for i in range(n+1)]
    reference=[ [(-7,-7),(-3,-7)],arc(-5,-3,2,0,math.pi),arc(4,4,2,0,2*math.pi),[(-6,2),(-2,2),(-2,6),(-6,6),(-6,2)],[(4*i/n,-6+8*(i/n)*(1-i/n)) for i in range(n+1)],[(1,-1),(3,1),(5,-1)],arc(0,2,1,math.pi,math.pi)]
    types=['LINE','ARC','CIRCLE','LWPOLYLINE closed','SPLINE degree2','POLYLINE','LWPOLYLINE bulge1']
    checks=[]
    with (BASE/'data/reference-geometry.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.writer(f);w.writerow(['stroke','point','x_mm','y_mm'])
        for i,pts in enumerate(reference,1):
            for j,(x,y) in enumerate(pts,1): w.writerow([i,j,x,y])
    assert len(imported)==7
    for i,(src,dst) in enumerate(zip(reference,imported)):
        deviation=max(min(distseg(p,a,b) for a,b in zip(dst,dst[1:])) for p in src)
        checks.append({'stroke':i+1,'entity':types[i],'points':len(dst),'start':dst[0],'end':dst[-1],'reference_to_polyline_max_mm':deviation,'endpoint_max_mm':max(math.dist(src[0],dst[0]),math.dist(src[-1],dst[-1]))})
        assert deviation<=.01000001 and checks[-1]['endpoint_max_mm']<1e-10
    transform=read_points('cad-transformed.csv'); resampled=read_points('cad-resampled.csv')
    transform_error=max(math.dist((x*.5+1,y*.5-1),q) for p,t in zip(imported,transform) for (x,y),q in zip(p,t))
    maxstep=max(math.dist(a,b) for p in resampled for a,b in zip(p,p[1:]))
    assert transform_error<1e-12 and maxstep<=.500000001
    assert read_points('inch-line-import.csv')[0][-1]==(25.4,50.8)
    assert read_points('unitless-line-import.csv')[0][-1]==(10,20)
    allpoints=[p for s in imported for p in s]
    results=list(csv.DictReader((BASE/'data/parser-results.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
    for r in results: assert r['result']==('REJECT' if r['case'].startswith('reject-') else 'ACCEPT'),r
    summary={'test_date':'2026-10-22','chord_tolerance_mm':.01,'reference_dense_intervals':n,'strokes':len(imported),'points':len(allpoints),'bounds_mm':[min(x for x,y in allpoints),min(y for x,y in allpoints),max(x for x,y in allpoints),max(y for x,y in allpoints)],'entity_checks':checks,'transform_max_error_mm':transform_error,'resampled_points':sum(map(len,resampled)),'resampled_max_edge_mm':maxstep,'accepted_cases':sum(r['result']=='ACCEPT' for r in results),'rejected_cases':sum(r['result']=='REJECT' for r in results),'parser_results':results,'drawing_demo_points':sum(map(len,read_points('drawing-demo.csv')))}
    (BASE/'data/verification.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    with (BASE/'data/entity-comparison.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.writer(f);w.writerow(['stroke','entity','points','start_x_mm','start_y_mm','end_x_mm','end_y_mm','max_dense_reference_to_segments_mm'])
        for r in checks:w.writerow([r['stroke'],r['entity'],r['points'],*r['start'],*r['end'],r['reference_to_polyline_max_mm']])
    plot(reference,imported)
    print(json.dumps(summary,ensure_ascii=False,indent=2))
def plot(reference,imported):
    from PIL import Image,ImageDraw,ImageFont
    image=Image.new('RGB',(1800,1040),'white');d=ImageDraw.Draw(image)
    fontpath=r'C:\Windows\Fonts\msyh.ttc'
    font=ImageFont.truetype(fontpath,25);small=ImageFont.truetype(fontpath,21);title=ImageFont.truetype(fontpath,30)
    colors=['#225F99','#C37526','#248B78','#745E9A','#C45062','#3C7981','#8A7348']
    for panel,(pts,lab) in enumerate([(reference,'导入前参考几何'),(imported,'导入后解析点列')]):
        ox=80+panel*900;oy=115;size=720
        def p(x,y):return (ox+(x+9)*size/18,oy+(9-y)*size/18)
        d.text((ox,30),lab,font=title,fill='black')
        for a in range(-8,10,2):
            d.line([p(a,-9),p(a,9)],fill='#DFE5EA',width=1);d.line([p(-9,a),p(9,a)],fill='#DFE5EA',width=1)
            d.text((p(a,-9)[0]-12,oy+size+8),str(a),font=small,fill='#46515B')
            d.text((ox-42,p(-9,a)[1]-12),str(a),font=small,fill='#46515B')
        d.rectangle([ox,oy,ox+size,oy+size],outline='#788490',width=2)
        d.line([p(0,-9),p(0,9)],fill='#9AA5AF',width=2);d.line([p(-9,0),p(9,0)],fill='#9AA5AF',width=2)
        for i,stroke in enumerate(pts):
            screen=[p(*v) for v in stroke];d.line(screen,fill=colors[i],width=4)
            if panel:
                for x,y in screen: d.ellipse((x-3,y-3,x+3,y+3),fill=colors[i])
            x,y=screen[0];d.text((x+8,y-31),str(i+1),font=font,fill=colors[i])
        d.text((ox+size-105,oy+size+43),'X / mm',font=small,fill='black');d.text((ox-32,oy-38),'Y / mm',font=small,fill='black')
    d.text((80,930),'左图按 DXF 参数独立重建，并非 AutoCAD 截图；右图读取程序实际导出的 CSV。',font=font,fill='#364451')
    d.text((80,976),'编号 1—7 对应实体对比表；两图采用相同坐标范围和等比例显示。',font=font,fill='#364451')
    image.save(BASE/'figures/cad-geometry-comparison.png')

def make_doc():
    from docx import Document
    from docx.shared import Pt,Cm,RGBColor
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.enum.table import WD_TABLE_ALIGNMENT,WD_CELL_VERTICAL_ALIGNMENT
    from docx.oxml import OxmlElement
    from docx.oxml.ns import qn
    s=json.loads((BASE/'data/verification.json').read_text(encoding='utf-8'))
    doc=Document(); sec=doc.sections[0]
    sec.page_width=Cm(21);sec.page_height=Cm(29.7)
    sec.top_margin=Cm(2.1);sec.bottom_margin=Cm(2);sec.left_margin=Cm(2.2);sec.right_margin=Cm(2.2)
    normal=doc.styles['Normal'];normal.font.name='Times New Roman';normal.font.size=Pt(10.5)
    normal._element.get_or_add_rPr().rFonts.set(qn('w:eastAsia'),'宋体')
    normal.paragraph_format.line_spacing=1.25;normal.paragraph_format.space_after=Pt(5)
    for name,size in [('Title',18),('Heading 1',14),('Heading 2',12),('Heading 3',10.5)]:
        st=doc.styles[name];st.font.name='Times New Roman';st.font.size=Pt(size);st.font.bold=True;st.font.color.rgb=RGBColor(0,0,0)
        st._element.get_or_add_rPr().rFonts.set(qn('w:eastAsia'),'黑体')
        st.paragraph_format.space_before=Pt(8);st.paragraph_format.space_after=Pt(6)
        for border in list(st.element.xpath('./w:pPr/w:pBdr')):border.getparent().remove(border)
    def p(text):
        a=doc.add_paragraph(text);a.paragraph_format.first_line_indent=Cm(.74);return a
    def h(text,page=False):
        a=doc.add_heading(text,2);a.paragraph_format.page_break_before=page;return a
    def cap(text,keep=False):
        a=doc.add_paragraph(text);a.alignment=WD_ALIGN_PARAGRAPH.CENTER;a.paragraph_format.space_after=Pt(5);a.paragraph_format.keep_with_next=keep
        for r in a.runs:r.font.size=Pt(9.5)
    def pic(name,width=16.6):
        doc.add_picture(str(BASE/'figures'/name),width=Cm(width));a=doc.paragraphs[-1];a.alignment=WD_ALIGN_PARAGRAPH.CENTER;a.paragraph_format.keep_with_next=True;a.paragraph_format.space_after=Pt(3)
    def equation(text):
        a=doc.add_paragraph(text);a.alignment=WD_ALIGN_PARAGRAPH.CENTER;a.paragraph_format.space_after=Pt(6)
    def table(headers,rows,widths,size=9.5):
        t=doc.add_table(rows=1,cols=len(headers));t.autofit=False;t.alignment=WD_TABLE_ALIGNMENT.CENTER
        for col,w in zip(t.columns,widths):col.width=Cm(w)
        borders=OxmlElement('w:tblBorders')
        for name in ['top','left','bottom','right','insideH','insideV']:
            e=OxmlElement('w:'+name);e.set(qn('w:val'),'single');e.set(qn('w:sz'),'5');e.set(qn('w:color'),'D9D9D9');borders.append(e)
        t._tbl.tblPr.append(borders)
        rep=OxmlElement('w:tblHeader');t.rows[0]._tr.get_or_add_trPr().append(rep)
        for rid,row in enumerate([headers]+rows):
            cells=t.rows[0].cells if rid==0 else t.add_row().cells
            cells[0]._tc.getparent().get_or_add_trPr().append(OxmlElement('w:cantSplit'))
            for i,(cell,text) in enumerate(zip(cells,row)):
                cell.width=Cm(widths[i]);cell.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
                a=cell.paragraphs[0];a.paragraph_format.space_after=Pt(1);a.paragraph_format.space_before=Pt(1);a.paragraph_format.line_spacing=1.12
                if i==0 and len(text)<12:a.alignment=WD_ALIGN_PARAGRAPH.CENTER
                r=a.add_run(text);r.font.size=Pt(size);r.bold=rid==0
                props=cell._tc.get_or_add_tcPr();margin=OxmlElement('w:tcMar')
                for n,v in [('top',75),('bottom',75),('left',90),('right',90)]:
                    e=OxmlElement('w:'+n);e.set(qn('w:w'),str(v));e.set(qn('w:type'),'dxa');margin.append(e)
                props.append(margin)
                if rid==0:
                    shade=OxmlElement('w:shd');shade.set(qn('w:fill'),'D9E5EF');props.append(shade)
        a=doc.add_paragraph();a.paragraph_format.line_spacing=Pt(3);a.paragraph_format.space_after=Pt(0);a.add_run().font.size=Pt(1)
    # Page 1
    doc.add_paragraph('5.3 上位机绘图与CAD文件读取','Title')
    p('本项目面向 Turbo PMAC 双轴运动控制，将图形输入与运动执行分开处理：用户可在画布中自由绘制或添加典型图形，也可读取规定范围内的二维 ASCII DXF 文件；两种输入最终均形成以毫米为单位、按笔画组织的点列，交给后续轨迹处理模块。当前不直接读取 DWG 文件。')
    h('5.3.1 典型图形与自由绘制')
    p('界面提供直线、圆、圆弧和五角星的参数化添加功能。直线由起点和终点确定，圆由圆心和半径确定，圆弧还需起始角与扫角。自由绘制时，左键按下开始记录一笔，拖动时将屏幕坐标换算为毫米坐标；松开按键、移出绘图区或失去焦点时结束该笔。撤销删除最后一笔，清空绘图删除已有输入。')
    pic('drawing-typical-and-freehand-ui.png')
    cap('图5-3 当前上位机的典型图形与自由笔画界面')
    p('图5-3 中同时保留直线、圆、半圆弧、五角星及一条波形自由笔画，共 5 笔画、115 点。右侧实际轨迹为空，是因为本次仅检查图形输入，程序未连接控制器，鼠标跟随保持关闭。绘图本身不代表电机已运动。')
    p('取证方式：在当前源码构建的独立离线实例中，依次触发四类图形的添加按钮，再向画布鼠标事件处理函数输入拖动坐标，最后用 WinForms DrawToBitmap 保存实际界面。该记录验证绘图数据链和界面渲染；未声称完成了现场人工操作或机械精度测试。')
    # Page 2
    h('5.3.2 CAD格式与实体支持范围',True)
    p('当前文件选择器只提供 *.dxf，DxfReader 进一步检查扩展名、文本结构及实体内容。可读取的是 ASCII DXF；DWG 应先在 CAD 软件中另存为二维 ASCII DXF，再删除无关文字、标注并分解块。文件中的几何定义是否受支持，比文件版本名称更能决定能否导入，不能据此宣称兼容所有 AutoCAD 文件。')
    cap('表5-7 当前支持的实体及解析条件',True)
    table(['实体类型','读取内容与转换方式','支持条件'],[
        ['LINE','读取起点与终点，保留为一条两点笔画。','XY 平面直线。'],
        ['ARC','读取圆心、半径、起止角，按逆时针扫角离散。','正半径；角度与坐标有效。'],
        ['CIRCLE','读取圆心与半径，离散完整圆周。','正半径；末点返回起点附近。'],
        ['LWPOLYLINE','按顶点顺序读取；闭合标志补回首点；bulge 转换为圆弧段。','普通二维多段线，支持直线与圆弧混合。'],
        ['POLYLINE','读取 VERTEX 序列及 SEQEND，按多段线规则处理。','普通二维形式；不支持三维或曲线拟合多段线。'],
        ['SPLINE','读取次数、控制点、节点矢量及可选权重，用有理 de Boor 算法求值后细分。','次数 1—10；非周期；控制点及节点完整；权重为正。'],
    ],[3.0,7.0,6.6])
    p('解析器拒绝 ENTITIES 中的未支持实体，不静默跳过。当前不支持 INSERT 块引用、TEXT/MTEXT 文字、DIMENSION 标注、HATCH 填充、ELLIPSE 椭圆、三维实体，以及周期样条和只有拟合点的样条。非零 Z 或高程、倾斜或翻转的 OCS 法向同样被拒绝；这些对象须在 CAD 端转换或删除。')
    h('文件与数值限制')
    p('文件大小上限为 16 MB，路径点数上限为 20000；组码和值须成对出现，文件须包含 ENTITIES 与 EOF。解析器检查非有限数值，几何校验还要求变换后每个坐标的绝对值不超过 1000000 mm。此数值上限只是输入有效性限制，实际设备行程在轨迹规划时另行检查。')
    p('单位来自 HEADER 中的 $INSUNITS：当前支持无单位、英寸、英尺、毫米、厘米、米、mil 和微米。无单位文件使用用户给定的“毫米/文件单位”比例；未支持的单位编号会提示转换为毫米。二进制 DXF 和 DWG 原生解析尚未实现，不计入已完成功能。')
    # Page 3
    h('5.3.3 CAD样例与导入前后对比',True)
    p('本节构造 cad-six-types-mm.dxf 作为综合输入，文件头写入 $INSUNITS=4，以毫米表示几何参数。样例包含 7 个独立实体，覆盖前述六类实体，其中 LWPOLYLINE 分别用于闭合方框与 bulge 圆弧测试。样例文件随文提交，可直接检查组码和实体参数。')
    cap('表5-8 综合样例的原始几何参数',True)
    table(['编号','实体','导入前的定义 单位mm'],[
        ['1','LINE','起点 (−7,−7)，终点 (−3,−7)。'],
        ['2','ARC','圆心 (−5,−3)，半径 2，起始角 0°，终止角 180°。'],
        ['3','CIRCLE','圆心 (4,4)，半径 2。'],
        ['4','LWPOLYLINE','闭合方框：(−6,2)、(−2,2)、(−2,6)、(−6,6)。'],
        ['5','SPLINE','二次；控制点 (0,−6)、(2,−2)、(4,−6)；节点 0,0,0,1,1,1。'],
        ['6','POLYLINE','开放折线：(1,−1)、(3,1)、(5,−1)。'],
        ['7','LWPOLYLINE','两端点 (−1,2)、(1,2)，首顶点 bulge=1，形成下半圆。'],
    ],[1.1,3.4,12.1])
    pic('cad-geometry-comparison.png')
    cap('图5-4 DXF原始几何参数与上位机解析点列对比')
    p('左图按表5-8 的几何参数独立计算，作为导入前参考图；右图读取 DxfReader 实际导出的 CSV，叠加离散点并保持相同坐标比例。左图并非 AutoCAD 截图，本次样例为构造的测试文件，未宣称已在 AutoCAD 中完成导出兼容性试验。')
    # Page 4
    h('5.3.4 实际导入界面与数值核验',True)
    pic('cad-imported-ui.png')
    cap('图5-5 综合DXF样例加载后的当前上位机界面')
    p('本次调用现有 DxfReader.Read(file, 0.01, 1) 读取样例，再通过 PathView.LoadDrawing 显示；跳过了原生文件选择对话框。该方法验证解析、图形数据及显示链，生产导入按钮使用同一解析器。界面底部显示“7 笔画 / 102 点”，与导出文件记录一致，包围盒为 X∈[−7,6] mm、Y∈[−7,6] mm。')
    cap('表5-9 原始几何与实际导入结果的数值对比',True)
    table(['编号','导入点数','端点最大偏差 mm','参考曲线至点列折线最大偏差 mm'],[
        [str(r['stroke']),str(r['points']),f"{r['endpoint_max_mm']:.6f}",f"{r['reference_to_polyline_max_mm']:.6f}"] for r in s['entity_checks']
    ],[1.2,2.4,4.5,8.5],9.3)
    p('曲线核验在每条解析参考曲线的参数区间等分 2000 段，计算 2001 个参考点到导入折线的最近距离；直线和折线同时核对顶点。样例中最大值约 0.007752 mm，小于本次设置的 0.01 mm。该值是本样例的密集采样结果，不是所有样条的全局误差证明，也不表示机床机械精度。')
    # Page 5
    h('5.3.5 实体解析与坐标转换',True)
    p('读取过程先将 DXF 文本转换为“组码—值”序列，再识别 HEADER 单位和 ENTITIES 实体。以 LINE 为例，组码 10/20 为起点 X/Y，11/21 为终点 X/Y；ARC/CIRCLE 使用 10/20 表示圆心、40 表示半径，ARC 的 50/51 为起止角；LWPOLYLINE 的 70 为标志、90 为顶点数、42 为 bulge。不同实体据此转换为同一类二维点列。')
    p('圆和圆弧按弦差设置离散，当前导入界面传入 0.01 mm，单步圆心角同时不超过 10°。多段线保留顶点与顺序，遇到 bulge=b 时按扫角 θ=4 arctan(b) 还原圆弧段。样条由控制点、节点及权重求值，在非零节点区间预分段后按中点偏差自适应细分。上述处理将不同 CAD 表达统一为折线表示。')
    h('单位换算与几何变换')
    p('设文件单位到毫米的比例为 u，文件坐标为 (x_d,y_d)，先计算 (x_mm,y_mm)=u(x_d,y_d)。需要平移或缩放时，PathGeometry.Transform 再对毫米点列执行下式，其中 s>0，Δx、Δy 均以毫米计。')
    equation('x′ = s x_mm + Δx      y′ = s y_mm + Δy')
    cap('表5-10 单位换算与几何变换的实际结果',True)
    table(['检查项','输入与参数','程序输出'],[
        ['英寸换算','inch-line.dxf；终点 (1,2)，u=25.4。','终点 (25.4,50.8) mm。'],
        ['无单位换算','unitless-line.dxf；终点 (1,2)，指定 u=10。','终点 (10,20) mm。'],
        ['平移与缩放','综合样例；s=0.5，Δx=1，Δy=−1。','(−7,−7) → (−2.5,−4.5) mm；全点核验差为 0。'],
        ['后续重采样','综合样例；每条原始边按 0.5 mm 间距上限细分。','102 点变为 149 点；最大相邻点距为 0.5 mm。'],
    ],[2.5,7.1,7.0])
    h('画布坐标与轨迹数据')
    p('画布保持 X/Y 相同的物理比例，并将屏幕向下的 Y 轴转换为毫米坐标向上的 Y 轴。设绘图区左、下边界为 L、B，宽高为 W、H，则显示公式如下；自由绘制使用其逆变换。因此屏幕像素点不是电机计数，自由笔画还受像素分辨率影响。')
    equation('x_screen = L + (x_mm − X_min) W / (X_max − X_min)')
    equation('y_screen = B − (y_mm − Y_min) H / (Y_max − Y_min)')
    p('统一数据结构为 DrawingPath，记录来源 Source 和 Strokes；每一笔画是有序的 MotionPoint(X,Y) 列表，单位为 mm。绘图数据不含时间、速度或伺服指令，后续规划模块才按设备行程校验、逐边重采样并形成运动轨迹。设备计数按各轴 ct/mm 比例换算；该换算不等于独立的 G54 工件坐标系。')
    # Page 6
    h('5.3.6 异常样例与提交证据',True)
    p('除综合样例外，本次还读取项目原有 course-shapes.dxf，得到 5 笔画、80 点；英寸与无单位样例各得到 1 笔画、2 点。共 4 个正常样例通过解析。为检查输入边界，另构造 7 个异常文件，实际结果如下；完整异常消息保存在 parser-results.tsv 中。')
    cap('表5-11 异常文件与实际拒绝原因',True)
    table(['样例文件','实际结果与提示要点'],[
        ['reject-text.dxf','拒绝：不支持实体 TEXT；提示删除文字、标注并分解块。'],
        ['reject-insert.dxf','拒绝：不支持实体 INSERT；提示在 CAD 中分解块。'],
        ['reject-nonzero-z.dxf','拒绝：只支持 XY 平面二维图形，提示投影到 Z=0。'],
        ['reject-periodic-spline.dxf','拒绝：周期样条须转换为非周期样条或多段线。'],
        ['reject-fit-only-spline.dxf','拒绝：缺少完整控制点、次数与节点矢量。'],
        ['reject-unit-code.dxf','拒绝：单位编号 3 未支持，提示转换为毫米。'],
        ['reject-incomplete.dxf','拒绝：缺少 EOF，提示文件可能不完整。'],
    ],[6.3,10.3],9.4)
    p('这些结果直接证明本组异常输入被识别。DWG、二进制 DXF、其他未支持实体、文件大小与点数限制的说明来自当前源码审查，本次未对每一种边界另做独立文件试验，不将其写成本次全部实测通过。')
    h('证据文件与复核方法')
    cap('表5-12 提交材料与可复核内容',True)
    table(['材料位置','用途'],[
        ['samples/','11 个实际 DXF 文件，包含综合样例、项目原有样例、单位及异常样例。'],
        ['figures/','典型绘图、空白输入、CAD 加载界面原图，以及几何参数与解析点列对比图。'],
        ['data/','原始解析点 CSV、参考几何、变换与重采样坐标、实体对比、解析结果和核验 JSON。'],
        ['reproduce/','C# 离线取证源码、Python 几何核验脚本及复现说明。'],
        ['SHA256SUMS.txt','材料文件哈希，用于核对提交文件是否发生变化。'],
    ],[3.6,13.0],9.4)
    p('复核时先打开综合 DXF 对照表5-8，再读取对应导入 CSV，检查笔画编号、点数、端点和方向。曲线误差与单位换算可运行随附脚本复算。需要人工检查文件选择器时，可在当前程序中点击“导入 DXF”选择同一文件，并对照图5-5；该人工步骤不冒充本次自动化取证的已执行记录。')
    p('本节完成范围为图形建立、规定范围的 ASCII DXF 读取、毫米坐标转换和轨迹输入数据形成。原生 DWG、自动分解块、文字轮廓转换、三维投影、自动拼接或优化实体顺序尚未实现；导入成功后仍须完成规划与启动条件检查，不能据此直接认定设备可执行。')
    foot=sec.footer.paragraphs[0];foot.alignment=WD_ALIGN_PARAGRAPH.CENTER
    field=OxmlElement('w:fldSimple');field.set(qn('w:instr'),'PAGE');foot._p.append(field)
    doc.core_properties.title='5.3 上位机绘图与CAD文件读取';doc.core_properties.subject='Turbo PMAC 双轴上位机绘图与二维DXF读取';doc.core_properties.author=''
    out=BASE/'5.3_上位机绘图与CAD文件读取.docx';doc.save(out);print(out)

if __name__=='__main__':
    if '--base' in sys.argv: BASE=Path(sys.argv[sys.argv.index('--base')+1]).resolve()
    if '--prepare' in sys.argv:prepare()
    if '--analyze' in sys.argv:analyze()
    if '--doc' in sys.argv:make_doc()
