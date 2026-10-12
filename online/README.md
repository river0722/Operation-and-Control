# PMAC 上位机控制程序

基于原有空白工程实现的 C# WinForms 上位机，保留 .NET Framework 3.5 与 x86。主界面使用原生工具箱控件，按参考项目的形状分为四区：左上通讯、右上当前位置与速度、左下程序控制、右下点动与定位；下方并排增加实际轨迹与鼠标绘图画布。

参考目录 `lin-tea.github.io/_projects/(3) PMAC In C#.md` 指向 [PMAC_CSharp](https://github.com/lin-tea/PMAC_CSharp)。布局参考其 `finalGUI.png`，参考版本为 `e7378636ae9862aaed37eebdae3488c69c5ba36f`。

## 运行与编译

双击 `Operations Control/bin/Release/Operations Control.exe`。默认模拟模式，点击“连接设备”即可测试，不需要 PMAC 驱动。实机模式需要已安装、注册并配置的 32 位 PcommServer / PMAC 驱动。

在 Visual Studio 中打开 `Operations Control/Operations Control.slnx` 或 `.csproj`。运行需要启用 Windows 的 .NET Framework 3.5 功能；编译需要对应目标框架和 Windows 桌面开发工具。在 `online` 目录运行：

```powershell
.\build.ps1
.\build.ps1 -Configuration Debug
.\build.ps1 -Test
```

脚本自动寻找 Visual Studio 的 MSBuild。工程的 AnyCPU 配置实际指定 `PlatformTarget=x86`。界面截图保存在 `screenshots`。

## 画布操作

1. 默认 X 对应电机 1，Y 对应电机 2。连接后左画布按反馈位置记录实际轨迹，右画布按住鼠标左键绘图。未连接或未勾选“平台跟随鼠标”时，绘图只作预览。
2. 模拟模式默认 X/Y 范围为 −10 到 +10 mm，换算比例均为 **100 ct/mm，仅用于模拟演示**。正 Y 向上，两个方向保持相同的物理比例，画布以电机当前位置零点为坐标零点。
3. 勾选“平台跟随鼠标”，在右画布按住左键拖动，即可让两个模拟轴更新到鼠标目标。松开、移出绘图区、失去鼠标捕获、控件失焦、窗口失焦、按 Esc 或点击“停止运动”均结束本次跟随并发送双轴停止请求。
4. 跟随期间锁定电机选择、手动定位、命令发送和坐标参数。当前待执行目标由最新鼠标位置替换，最多一组跟随请求在执行，避免拖动越久排队越长。
5. 比例或行程修改后，跟随暂时禁用，必须点击“应用坐标”。应用坐标会清空两个画布；清除轨迹或绘图不会执行运动，清除绘图会结束正在进行的跟随。

**实机的 ct/mm 与允许行程尚未提供。** 切换到实机模式时，比例与范围输入置为 0，毫米反馈和绘图跟随暂不启用，避免沿用模拟标定。取得实际参数后分别填写 X/Y 行程上下限及 ct/mm，点击“应用坐标”。初次反馈到达且 X/Y 选择不同电机后才能启用跟随；开始跟随时还检查当前位置是否处于设置范围内、反馈是否过期。实机更换 X/Y 电机后必须重新确认并应用坐标参数。重新选择电机或执行 HOMEZ 会分断反馈轨迹，避免跨坐标跳变画出假连线。

目标范围检查只限制鼠标绘图目标，不能代替控制器硬件限位；手动点动和自由命令仍按计数操作。毫米换算为 `mm = 反馈计数 / ct每毫米`，未添加工件坐标偏移。实际比例应按电机编码器、传动比、丝杠导程和 PMAC 设置确定。

当前采用 **独立双轴目标跟随**：依次发送 `#XJ=目标计数` 和 `#YJ=目标计数`，并设置各轴点动速度。尚未配置 PMAC 坐标系统和运动程序，因此不保证两轴同步到达、直线插补或完整复现所有鼠标采样点。后续需要精确轮廓运动时，应接入已配置的坐标系统与缓冲插补程序。

轮询与跟随 Timer 均为 100 ms；实际频率取决于通信完成时间。左侧轨迹来自 `#nP` 反馈，不使用目标位置冒充实际位置，速度按反馈位置差估算。每个画布最多保留约 4000 个点，分批丢弃最早记录。

## 其他操作

- 点动默认“点动一步”。关闭后按住方向按钮连续点动，松开、失焦、失去捕获时停止；支持空格 / Enter 按放。
- 单轴“到位置”使用绝对反馈计数 ct。速度为 ct/ms，输入范围沿用参考工程的 0.001–15；实际机械速度范围应按轴配置调整。
- “当前位置置零 HOMEZ”修改当前位置零点，不执行机械寻零。
- 程序控制区可发送单条命令、选择文件、另存为和下载。程序编辑器通过“打开/编辑”或顶部菜单打开；日志通过“操作日志”菜单打开。两个辅助窗口关闭时隐藏，下次打开保留内容。
- 文件优先用 UTF-8 读取，无法解码时尝试 GBK；修改后的程序需保存再下载，替换文件或关闭主窗体前检查未保存内容。下载不会自动运行程序；模拟下载只验证文件，不执行内容。
- 日志可清空、导出 UTF-8 文本，最多保留 500 条，达到上限后开始新一批。

模拟器支持 `#nP`、`#nJ+`、`#nJ-`、`#nJ/`、`#nJ=值`、`#nJ^值`、`#nHOMEZ`、`In22` / `In22=值`、`VERSION` 和 `?`；不支持的命令明确报错。

## 用工具箱继续编辑

界面使用 `Button`、`Label`、`TextBox`、`ComboBox`、`NumericUpDown`、`CheckBox`、`GroupBox`、`MenuStrip`、`TableLayoutPanel`，以及 `Timer`、`OpenFileDialog`、`SaveFileDialog`。控件均在 `.Designer.cs` 的 `InitializeComponent` 中显式声明，可在设计器属性窗口修改文字、字体和布局。

先生成工程，右键 `Form1.cs` 选择“查看设计器”（Shift+F7）调整模块位置。编辑模块内部时，分别打开 `Views/ConnectionView.cs`、`ReadoutView.cs`、`ProgramView.cs`、`MotionView.cs`、`CanvasView.cs` 的设计器。程序编辑与日志辅助窗体也有独立设计器。每个界面采用 `.cs` 行为文件、`.Designer.cs` 控件布局、`.resx` 资源文件组织；设计模式不会创建设备通信服务。

仅画布绘制由可放入工具箱的自定义 `MotionCanvas` 控件负责，网格和曲线使用 GDI+ 绘制。其他操作均使用标准控件。正文 10 pt、辅助说明 9.5 pt，延续“放大一号”的设置；位置读数使用 11 pt Consolas，四分区标题 11 pt。最小窗体为 1040 × 850，以保留可读画布。

## 模块职责

| 文件 | 职责 |
| --- | --- |
| `Form1.cs` / `.Designer.cs` | 事件协调、四分区布局、Timer、文件流程、状态联动 |
| `Views/ConnectionView.cs` | 模式选择与连接状态 |
| `Views/ReadoutView.cs`、`Controls/AxisReadout.cs` | 电机反馈位置与估算速度 |
| `Views/MotionView.cs` | 双轴点动、单轴定位、停止与按放事件 |
| `Views/ProgramView.cs`、`ProgramEditorForm.cs` | 命令和文件操作、独立编辑窗口 |
| `Views/CanvasView.cs` | 毫米坐标参数、实机标定门槛、两画布状态 |
| `Controls/MotionCanvas.cs` | 双缓冲曲线绘制、鼠标采样、捕获与笔画事件 |
| `Models/CanvasModels.cs` | 毫米/计数/屏幕坐标换算、行程校验 |
| `Services/CanvasFollower.cs` | 最新目标合并、单请求调度、取消待执行目标 |
| `Views/LogView.cs`、`LogForm.cs` | 日志显示、导出与辅助窗口 |
| `Services/ControllerSession.cs` | STA 通信队列、PMAC 指令组合、反馈与速度计算 |
| `Services/PcommTransport.cs`、`SimulatorTransport.cs` | 实机 COM 传输与 8 轴模拟器 |
| `Tests/SmokeTests.cs` | 设计宿主、坐标换算、模拟传输及 WinForms 交互检查 |

COM 对象在同一 STA 线程创建、使用与释放，UI 不直接读写控制器。上一轮查询或跟随请求未完成时不会继续积压同类请求。取消使用代次标记跳过旧目标，并在同一队列中追加停止；双轴命令部分失败时尝试停止两个轴。

## 图标与验证

此前使用 `codex-image2` / `gpt-image-2` 生成的图标继续应用到 EXE、主窗口和辅助窗口。`Assets/pmac-console.ico` 包含 16–256 像素的传统 32 位位图帧，主窗体通过 `Form1.resx` 加载；部署无需额外复制图标。`Assets/pmac-console.png` 为透明背景资源，`Assets/ImageGeneration` 保留提示词、原图与转换脚本。

已通过 Debug / Release 编译和 126 项模拟检查，检查覆盖十个窗体/控件设计宿主、坐标换算、实际反馈轨迹、双轴目标、离线绘图、未知实机标定禁用、参数未应用禁用、停止与断开重连、取消排队目标。没有连接或操作实机，实机的驱动通信、机械参数和运动响应仍需现场验证。软件停止发送 `J/`，不能替代硬件急停；目前未加入伺服报警、机械限位状态或机械到位判定。


