# 4.1—4.3 证据与材料清单

这份清单用于准备 4.1、4.2、4.3 的最终填写稿。它不等同于正文。

状态说明：

- `[已收集]`：目录中已有文件或可直接从源码/测试结果整理出的材料。
- `[软件/模拟]`：已有，但只能证明上位机、解析器或模拟器行为，不能替代实机验收。
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
| [待补] | 实机 GUI 连接截图 | 截图中应能看到实机模式、设备名/设备号、连接成功状态和时间；不要只放模拟模式截图 |
| [待补] | 一次完整实机命令—反馈日志 | 至少记录：连接、状态读取、参数设置、运动启动、位置/状态反馈、停止或复位；保留原始命令、应答、时间戳和结果 |
|        |                           |                                                              |
|        |                           |                                                              |
|        |                           |                                                              |
|        |                           |                                                              |
|        |                           |                                                              |

## 4.2 上下位机坐标空间映射

### 已收集或可直接整理

| 状态 | 材料 | 现有内容 / 用途 | 文件或来源 |
|---|---|---|---|
| [已收集] | 上位机坐标定义 | 绘图区使用 mm；X 向右为正、Y 向上为正；屏幕像素原点在左上角、屏幕 Y 向下 | `src/Operations Control/Controls/CanvasProjection.cs`；`docs/software/代码功能说明.md` |
| [已收集] | 像素 ↔ mm 转换公式 | `x = Xmin + (px-L)/W·(Xmax-Xmin)`；`y = Ymin + (T+H-py)/H·(Ymax-Ymin)`；反向显示公式也已整理 | `artifacts/output/4.1-4.3上下位机交互与图形复现.docx`；源码同上 |
| [已收集] | mm ↔ ct 换算接口 | `C_X = x·K_X`、`C_Y = y·K_Y` 的软件接口；每轴可使用独立 ct/mm 比例；越界点拒绝规划 | `src/Operations Control/Models/CanvasModels.cs`；`CanvasProjection.cs`；`tests/SmokeTests.cs` |
| [软件/模拟] | 默认演示参数 | X/Y 电机 1/2；`100 ct/mm`；各轴范围 `-10～+10 mm`；绘图区保持等比例 | `README.md`；`docs/software/工艺工作台使用与验收.md` |
| [已收集] | 越界处理规则 | 规划前逐点检查范围，超出时提示并拒绝生成/启动，不自动裁剪；可通过统一缩放/平移后重新规划 | `CanvasModels.cs`；`TrajectoryPlanner.cs`；已有 4.1—4.3 文档 |
| [软件/模拟] | 已知点示例表 | `(-10,-10)`、`(0,0)`、`(10,10)` 与绘图区边界/中心的对应关系；可作为公式示例 | `artifacts/output/4.1-4.3上下位机交互与图形复现.docx` |
| [软件/模拟] | 映射前后界面截图 | 双画布、输入图形与反馈轨迹可展示软件映射效果 | `assets/screenshots/dual-canvases.png`、`assets/screenshots/workflow-path.png`、`assets/screenshots/workflow-analysis.png` |

### 待补或不能由现有材料证明

| 状态 | 需要补的证据 | 具体要求 |
|---|---|---|
| [待补] | 工作平面坐标系实物示意图 | 在平台照片或示意图上标出机械原点、X/Y 正方向、工件零点、有效行程和上位机绘图区对应位置 |
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
| [待补] | 本组实际 AutoCAD 原文件 | 提供本组实际绘制并导出的 `.dwg` 或 ASCII `.dxf`，以及 AutoCAD 导出设置/版本截图；当前综合样例是软件测试文件，`examples/Drawing1.dwg` 也没有导入实测链路 |
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
