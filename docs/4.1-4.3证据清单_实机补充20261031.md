# 4.1—4.3 证据与材料清单

这份清单用于准备 4.1、4.2、4.3 的最终填写稿。它不等同于正文。

状态说明：

- `[已收集]`：目录中已有文件或可直接从源码/测试结果整理出的材料。
- `[软件/模拟]`：已有，但只能证明上位机、解析器或模拟器行为，不能替代实机验收。
- `[部分已收集]`：已有实机材料，但尚不满足整项证据要求。
- `[待补]`：当前目录没有足够证据，需要现场操作、拍照、记录或由组员确认。

## 4.1 上下位机通信与 GUI 交互

### 已收集或可直接整理

| 状态 | 材料 | 现有内容 / 用途 | 文件或来源 |
|---|---|---|---|
| [已收集] | 系统架构图 | 上位机、PcommServer COM、以太网、Turbo PMAC、伺服机构及指令/反馈方向 | `references/pmac/上下位机标准通讯机制.png`、`.svg`、`.drawio` |
| [已收集] | 软件通信流程说明 | 界面操作 → 业务服务 → `ControllerSession` → `PcommTransport`/`PcommServer` → PMAC；反馈反向回到 GUI | `README.md`；`docs/software/代码功能说明.md` |
| [已收集] | GUI 主界面截图 | 连接状态、输入图形、实际轨迹、位置反馈、全局停止 | `assets/screenshots/dual-canvases.png`、`workflow-path.png` |
| [已收集] | 手动控制截图 | 轴选择、点动、定位、速度、使能/去使能、状态读数 | `assets/screenshots/manual-control.png`、`manual-deduplicated.png`、`motion-control.png` |
| [已收集] | 程序/命令界面截图 | PMAC 程序预览、程序编辑、单条命令及应答区域 | `assets/screenshots/program-editor.png`、`commands-and-programs.png`、`minimum-program.png` |
| [已收集] | 诊断与报警截图 | 每轴状态、跟随误差趋势、设备配置、当前/历史报警 | `assets/screenshots/diagnostics-trend.png`、`diagnostics-config.png`、`diagnostics-alarm.png` |
|          |                    |                                                              |                                                              |
| [已收集] | 实机只读通信诊断 | 2026-10-15；设备 `192.6.94.5:1025`；固件 `VERSION=1.947`；轴状态/位置/跟随误差/速度连续采样 55 轮 | `artifacts/output/pmac-live-diagnosis/diagnosis.md`、`query-summary.csv`、`pmac-ethernet-*.jsonl` |
| [已收集] | 实机状态与反馈数值 | 1 轴 `P=0 ct/F=100 ct/V=0`、2 轴 `P=101.46875 ct/F=0.53125 ct/V=0`，以及状态字、I 参数、内存读取 | 同上；只能证明只读反馈和故障诊断 |
| [已收集] | 安全边界说明 | GUI 停止不能替代硬件急停；限位、急停和独立联锁应由控制器/硬件安全回路承担 | `README.md`；`docs/software/工艺工作台使用与验收.md`；已有 4.1—4.3 文档 |

### 待补或不能由现有材料证明

| 状态 | 需要补的证据 | 具体要求 |
|---|---|---|
| [已收集] | 实机 GUI 连接截图 | [实机连接截图](../artifacts/output/live-evidence-20261031/figures/01-real-connected.png)、[实机设置截图](../artifacts/output/live-evidence-20261031/figures/03-real-settings.png)；设备 0、已连接；采集时间见配套 JSON 元数据 |
| [部分已收集] | 一次完整实机命令—反馈日志 | 已有 197 条只读原始命令/应答、20 轮双轴反馈，以及昨日实机会话的启动、停止、运行完成摘要；仍缺同一次参数写入—回读—启动—运动反馈—停止的原始命令和连续画面。见下方实机补采索引 |
|        |                           |                                                              |
|        |                           |                                                              |
|        |                           |                                                              |
|        |                           |                                                              |
|        |                           |                                                              |

## 4.2 上下位机坐标空间映射

### 已收集或可直接整理

| 状态 | 材料 | 现有内容 / 用途 | 文件或来源 |
|---|---|---|---|
| [已收集] | 上位机坐标定义 | 绘图区使用 mm；当前代码 X 向右、Y 向下为正；屏幕像素原点在左上角；机械方向见现场填写，尚待照片校验 | `src/Operations Control/Controls/CanvasProjection.cs`；`docs/software/代码功能说明.md` |
| [已收集] | 像素 ↔ mm 转换公式 | `x = Xmin + (px-L)/W·(Xmax-Xmin)`；`y = Ymin + (py-T)/H·(Ymax-Ymin)`；当前代码与原 Word 中 Y 公式的差异已在补采记录注明 | `artifacts/output/4.1-4.3上下位机交互与图形复现.docx`；源码同上 |
| [已收集] | mm ↔ ct 换算接口 | `C_X = x·K_X`、`C_Y = y·K_Y` 的软件接口；每轴可使用独立 ct/mm 比例；越界点拒绝规划 | `src/Operations Control/Models/CanvasModels.cs`；`CanvasProjection.cs`；`tests/SmokeTests.cs` |
| [软件/模拟] | 默认演示参数 | X/Y 电机 1/2；`100 ct/mm`；各轴范围 `-10～+10 mm`；绘图区保持等比例 | `README.md`；`docs/software/工艺工作台使用与验收.md` |
| [已收集] | 越界处理规则 | 规划前逐点检查范围，超出时提示并拒绝生成/启动，不自动裁剪；可通过统一缩放/平移后重新规划 | `CanvasModels.cs`；`TrajectoryPlanner.cs`；已有 4.1—4.3 文档 |
| [软件/模拟] | 已知点示例表 | `(-10,-10)`、`(0,0)`、`(10,10)` 与绘图区边界/中心的对应关系；可作为公式示例 | `artifacts/output/4.1-4.3上下位机交互与图形复现.docx` |
| [软件/模拟] | 映射前后界面截图 | 双画布、输入图形与反馈轨迹可展示软件映射效果 | `assets/screenshots/dual-canvases.png`、`assets/screenshots/workflow-path.png`、`assets/screenshots/workflow-analysis.png` |

### 待补或不能由现有材料证明

| 状态 | 需要补的证据 | 具体要求 |
|---|---|---|
| [部分已收集] | 工作平面坐标系实物示意图 | 已保留现场填写的机械原点和 X/Y 正方向原文；当前有实机设置截图与行程值。平台照片、工件零点和方向标注仍待补 |
|        |                          |                                                              |
| [待补] | 至少两个实测已知点校验 | 每个点记录上位机目标 `(x,y)`、换算计数、控制器读数/量具读数、误差、单位和测试条件；建议覆盖中心和非中心点 |
| [待补] | 真实映射前后对比截图 | 同一图形在上位机坐标、控制器读数和平台实际位置之间对应，需能看出 Y 方向是否翻转、比例是否正确 |
|        |                          |                                                              |

## 4.3 上位机图形与 AutoCAD 图形在下位机上的复现

### 已收集或可直接整理

| 状态 | 材料 | 现有内容 / 用途 | 文件或来源 |
|---|---|---|---|
| [已收集] | 上位机典型图形截图 | 直线、圆、圆弧、五角星、自由曲线/自由笔画的界面证据 | `artifacts/output/5.3_上位机绘图与CAD文件读取/figures/drawing-typical-and-freehand-ui.png` |
| [已收集] | CAD 导入界面截图 | 导入前后 UI、图形状态和笔画/点数显示 | `artifacts/output/5.3_上位机绘图与CAD文件读取/figures/cad-before-empty-ui.png`、`cad-imported-ui.png`、`cad-imported-canvas.png` |
| [已收集] | CAD 几何对比图 | 导入折线与独立重建参考几何的对比；不是 AutoCAD 原图截图 | `artifacts/output/5.3_上位机绘图与CAD文件读取/figures/cad-geometry-comparison.png` |
| [已收集] | DXF 样例文件 | 综合样例、课程样例、英寸单位、无单位及异常输入样例 | `artifacts/output/5.3_上位机绘图与CAD文件读取/samples/`；`examples/course-shapes.dxf`；根目录 `nailong_*.dxf` |
| [已收集] | 支持的 CAD 格式与实体 | 当前读取 ASCII DXF；支持 `LINE`、`ARC`、`CIRCLE`、`LWPOLYLINE`（含 bulge）、二维 `POLYLINE`、非周期控制点 `SPLINE`；DWG 需先转 ASCII DXF | `src/Operations Control/Files/DxfReader.cs`；`README.md` |
| [已收集] | 解析与几何核验数据 | 综合样例 7 笔画/102 点；0.01 mm 弦差；重采样后 149 点、最大边长 0.5 mm；4 个正常样例通过、7 个异常样例拒绝 | `artifacts/output/5.3_上位机绘图与CAD文件读取/data/verification.json`、`parser-results.tsv`、`entity-comparison.csv`、`cad-resampled.csv` |
| [已收集] | 单位换算数据 | 英寸样例按 25.4 mm/单位；无单位样例按指定比例；单位和边界错误有拒绝记录 | `artifacts/output/5.3_上位机绘图与CAD文件读取/data/inch-line-import.csv`、`unitless-line-import.csv`、`verification.json` |
| [已收集] | 轨迹/程序/反馈导出 | `drawing.csv`、`planned.csv`、`actual.csv`、`analysis.txt`、`program.pmc` 已有多组模拟运行记录 | `assets/screenshots/pvt-continuous/PMAC-20261029-184559-48254d/` 及同目录其他运行批次 |
| [软件/模拟] | 轨迹误差与滞后统计 | 示例运行：轮廓误差 RMS/最大值 `0.015839/0.136911 mm`，估计滞后 `80 ms`；来源明确为模拟反馈 | 对应运行目录 `analysis.txt` |
| [已收集] | 图形到下发流程 | 建立/导入 → 单位换算 → 点列离散 → 缩放/平移 → 行程检查 → PVT/SPLINE1 规划 → PMAC 程序预览 → 下载/启动 → 反馈采样/分析 | `docs/software/工艺工作台使用与验收.md`；`docs/software/代码功能说明.md` |

### 待补或不能由现有材料证明

| 状态 | 需要补的证据 | 具体要求 |
|---|---|---|
| [部分已收集] | 本组实际 AutoCAD 原文件 | 已清点 examples/Drawing1.dwg、Drawing1.dxf 并保存哈希；DXF 头为 AC1018、毫米，ENTITIES 只有 1 条 LINE；原图、归属和导出设置仍待确认，不能将它与历史 12 笔画运行视为同一文件 |
| [待补] | AutoCAD 原图与上位机导入图对照 | 同一文件在 AutoCAD、上位机导入后、轨迹数据中的名称/尺寸/坐标应能对应；现有 `cad-geometry-comparison.png` 是独立参考重建，不是 AutoCAD 截图 |
|        |                                |                                                              |
|        |                                |                                                              |
|        |                                |                                                              |
|        |                                |                                                              |
|        |                                |                                                              |

## 可直接回填的空白字段

这些字段对应上面标为“待补”的内容，现场记录后直接填入即可。

### 4.1 实机通信与 GUI



- X/Y 电机号：__1___ / _________2___；坐标系号：__________1______；程序号：___20_______

  

### 4.2 坐标映射

- 机械原点定义：__进到中间光电传感器第一个z相点位置 ；X 正方向：朝见电机运动方向为正_；Y 正方向：同x轴________________
- X 行程：______-120 ~ 280 mm；Y 行程：-48 ~ 187_ mm；X ct/mm：1000_；Y ct/mm：1000
- 校验点 1：上位机 `(x,y)=________________`；控制器/量具读数 `=________________`；误差 `=________________`
- 校验点 2：上位机 `(x,y)=________________`；控制器/量具读数 `=________________`；误差 `=________________`
- 越界测试图形/点：________________；GUI 提示：________________；是否阻止下发：________________

### 4.3 实机图形复现

- 图形类型：___dxf；

## 提交前总核对

- [ ] 三张核心图齐全：系统架构图、通信流程图、坐标系/映射图。
- [ ] GUI 截图标明“模拟/实机”，且至少有一组连续操作证据，不只放静态界面。
- [ ] 4.1 有实机命令—反馈日志，至少覆盖连接、读取、参数、启动、停止/复位。
- [ ] 4.2 有现场轴方向、原点、行程、ct/mm、偏移和两个实测校验点。
- [ ] 4.3 有本组 AutoCAD 原文件、导入数据、PMAC 程序/轨迹数据、实机复现照片和误差对比。
- [ ] 所有模拟或离线数据都明确标注来源，不能写成实机精度或机械复现结果。
- [ ] 安全功能单独说明硬件急停、限位和驱动器/控制器联锁，不把 GUI 停止按钮当作急停证明。

## 2026-10-31 实机补采索引

完整记录见 [实机证据包](../artifacts/output/live-evidence-20261031/README.md)。本节补充实际采集结果；保留上方现场填写内容及已删减的清单结构。

| 证据 | 结果 | 原始文件 |
|---|---|---|
| 实机连接与设置 | PMAC 实机、设备 0、已连接；实机配置确认框已勾选；两轴使能/闭环/到位、未寻零 | [连接](../artifacts/output/live-evidence-20261031/figures/01-real-connected.png)、[设置](../artifacts/output/live-evidence-20261031/figures/03-real-settings.png) |
| 类型/固件 | TYPE=TURBO2, X4；VERSION=1.947；CID=602404；192.6.94.5:1025。完整铭牌型号仍需照片 | [查询原始记录](../artifacts/output/live-evidence-20261031/data/controller-readonly.jsonl) |
| GUI 配置 | 电机 1/2、坐标系 1、程序号 20；X=−120～280 mm、Y=−48～187 mm；X/Y=1000 ct/mm；图形缩放 1、偏移 X/Y=0 | [硬件配置快照](../artifacts/output/live-evidence-20261031/data/hardware-profile.xml)、设置截图 |
| 连续反馈 | 20 轮两轴状态字均为 892000078001；位置 −0.5 / 0.46875 ct，跟随误差和速度均为 0 | [feedback.csv](../artifacts/output/live-evidence-20261031/data/feedback.csv) |
| 实际 CS1 轴定义 | 两次只读回报分别为 1638.4X / 1638.4Y；与 GUI 配置值不同，尚需核对含义和本次下发后读回；未执行新运动 | [轴定义/参数回读](../artifacts/output/live-evidence-20261031/data/mapping-and-program-readback-02.jsonl) |
| CS1 状态与时间基准 | &1??=A80020020000000000（未运行、到位）；I42=0，I5190=1；它们是当前值，不代表新规划已下发 | 同上 |
| 程序 20 查询 | LIST PROG 20,0,32 → BEL/ERR003；此次未取得程序内容。GUI 程序号不是下载成功证明 | 同上；[查询结果说明](../artifacts/output/live-evidence-20261031/data/program20-prefix-readback.txt) |
| 昨日实机启动/停止 | 2026-10-30 18:10:37 导入 12 笔画/177 点，PVT 1615 段；18:10:47.915 运行中；18:10:54.911 停止被接受；18:10:55.078 停稳确认 | [历史实机会话03](../artifacts/output/live-evidence-20261031/data/20261030-real-session-03.log) |
| 昨日实机运行完成 | 同会话 18:12:48 再次导入，PVT 1617 段/231.14 s，18:12:55.722 运行中，18:16:48.118 运行完成；缺同次文件名/程序/原始轨迹和平台画面 | 同上 |

现场原点、方向、行程与比例说明来源于用户保存的填写稿及设置截图；机械原点原文为“进到中间光电传感器第一个z相点位置”，正方向原文为“朝见电机运动方向为正，Y 同 X”。本次没有量具验证或把该原文解释成照片证明；“朝见”的含义仍待现场明确。

控制器 I7010/I7020=8，反馈来源为内部脉冲计数，因此上表的计数和到位不能替代独立机械位移/精度。控制器软限位与 GUI 行程存在差异，原始值见证据包参数表。当前软件 Y 正向为屏幕向下；物理方向仍以实际校验为准。

尚需现场补采：两个已知点的目标/控制器/量具对应值、同图形的 GUI 与平台照片、实机位置越界拦截，以及同次 DXF 文件→PMAC 程序→硬件轨迹→平台复现的对应材料。单次 LIST 的 ERR003 是命令查询错误，不能当成轴报警/恢复证据。
