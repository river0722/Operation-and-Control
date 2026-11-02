5.3 上位机绘图与CAD文件读取 复核说明
取证日期：2026-10-22

一、材料与结果
正文：5.3_上位机绘图与CAD文件读取.docx，共6页。
samples 目录含11个实际DXF文件。综合样例cad-six-types-mm.dxf覆盖6类实体、7个独立图形。
综合样例结果：7笔画、102点；逐边以0.5 mm上限重采样后149点。
4个正常样例通过，7个异常样例按预期拒绝。
data/parser-results.tsv 是现有解析器输出，data/verification.json 是独立几何核验结果。
data/entity-comparison.csv 按实体记录点数、端点及密集参考曲线到实际导入折线的最大距离。
英寸样例的终点(1,2)变为(25.4,50.8) mm；无单位样例按10 mm/单位变为(10,20) mm。

二、测试边界
截图来自当前源码构建的独立离线WinForms实例，模拟模式已选择但未连接，鼠标跟随关闭。
四类典型图形触发实际添加按钮；自由笔画向MotionCanvas的鼠标事件处理函数输入屏幕坐标。
CAD加载调用DxfReader.Read(file,0.01,scale)，再用PathView.LoadDrawing显示，跳过原生文件选择框。
截图使用实际控件DrawToBitmap渲染，不是设计稿，不是人工现场操作记录。
cad-before-empty-ui.png为空白输入状态；cad-imported-ui.png为加载后状态。
cad-geometry-comparison.png左图为根据DXF几何参数独立重建的参考图，并非AutoCAD截图。
本次未连接PMAC，未下发运动，也未测试AutoCAD实际打开/导出过程和机械精度。
0.01 mm为本次弦差设置；曲线最大偏差为密集采样核验，不能推广为全部样条的全局误差保证。

三、仅重算已导出数据
需要Python 3与Pillow。在解压目录执行：
python reproduce/build_section53.py --base . --analyze
该步骤不启动上位机、不连接控制器；会重算verification.json、entity-comparison.csv、reference-geometry.csv和对比图。

四、重新调用当前源码取证
需要本项目源码、.NET 3.5、可构建该项目的MSBuild以及Windows图形会话。
先将 online/Operations Control/Operations Control.csproj 的Release输出到一个独立目录，不覆盖正在运行的程序。
示例编译命令（按实际目录替换路径）：
csc.exe /nologo /platform:x86 /target:exe /out:<独立输出目录>/section53_evidence.exe /reference:"<独立输出目录>/Operations Control.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll reproduce/section53_evidence.cs
然后执行 section53_evidence.exe "<本材料目录绝对路径>"。
运行器会读取samples中的每个DXF，将实际解析点输出至data，并生成界面原图。
运行器要求独立实例初始处于模拟模式，否则中止；没有任何连接或运行按钮调用。
正式文件选择器的人工检查需另外执行：打开现有程序，点击“导入DXF”，选择同一综合样例，核对笔画数与点数。

五、实现依据
Files/DxfReader.cs：ASCII DXF结构、单位、六类实体及拒绝条件。
Controls/CanvasProjection.cs：屏幕与毫米坐标转换，保持等比例且Y向上。
Controls/MotionCanvas.cs：自由笔画事件及结束条件。
Motion/PathGeometry.cs：弦差离散、平移缩放、校验与逐边重采样。
Views/PathView.cs：导入按钮、0.01 mm弦差设置、图形加载及旧规划失效。
Models/PathModels.cs：DrawingPath与MotionPoint；Models/CanvasModels.cs：设备范围与计数换算。
reproduce/source-fingerprints.json 记录本次关键源码与实际测试程序集的SHA256。
SHA256SUMS.txt 记录打包时的文件哈希。重算后生成文件的哈希可能变化。
