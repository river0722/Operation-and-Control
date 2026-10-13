# PMAC 上位机控制程序

C# WinForms，保留原生按钮、分组框、字体和画布样式，目标框架为 .NET Framework 3.5 / x86。绘图、手动控制、程序编辑、分析、设置和日志都在同一个主窗口中。

## 运行与编译

双击 `Operations Control/bin/Release/Operations Control.exe`，选择模拟模式，点击“连接”。实机需要已配置的 32 位 PcommServer / PMAC 驱动。

在 Visual Studio 中打开 `Operations Control/Operations Control.slnx` 或 `.csproj`，也可在本目录运行：

```powershell
.\build.ps1
.\build.ps1 -Configuration Debug
.\build.ps1 -Test -ScreenshotDirectory "$env:TEMP\pmac-verification"
```

## 操作

| 页面 | 操作 |
| --- | --- |
| 绘图与运行 | 导入 DXF、添加直线/圆/圆弧/五角星、自由绘图，生成 PVT/Spline1 规划并运行；左侧实际轨迹，右侧绘图 |
| 手动控制 | 电机选择、点动、定位、速度、反馈和当前位置置零 HOMEZ |
| 程序 | 查看/保存规划程序，编辑、保存、下载自定义程序，发送单条命令 |
| 分析 | 轨迹偏差、滞后、采样统计与完整数据导出 |
| 设置 | 坐标比例和行程、程序参数、图形变换、使能/失能、机械回零 |
| 日志 | 查看、清空和导出操作记录 |

顶部始终显示连接状态、X/Y 位置和“停止”，Esc 同样可停止。运行期间锁定相关参数和手动命令；关闭程序会先停止运动。所有页面共用通信会话和画布。

模拟默认电机 1/2、100 ct/mm、行程 ±10 mm、滞后 80 ms。绘图不自动运动；勾选“鼠标跟随”后按住左键跟随，松开、失焦或移出画布即停。鼠标跟随使用独立轴目标更新；完整路径运行使用坐标系 PVT/SPLINE1 插补。

DXF 支持 ASCII 格式的 LINE、ARC、CIRCLE、LWPOLYLINE、二维 POLYLINE 和非周期控制点 SPLINE，DWG 需先转换。不支持的实体明确报错。多笔画间包含定位转移，当前无刀具开关控制。

自定义程序优先按 UTF-8 读取，无法解码时尝试 GBK；修改后需保存再下载，下载不会自动运行。模拟器不解释任意下载脚本或用户 PLC。完整反馈独立保存，可导出原始点、规划段、实际反馈及误差 CSV、报告和程序。

## 项目文件

`Form1` 协调主窗口；`Views/PathView` 处理绘图到分析流程；其余 Views 保留手动、坐标、程序和日志控件。`Services` 包含 DXF、几何、规划、分析及串行 STA 通信；`Models` 定义坐标、路径和 Turbo PMAC 状态。布局在 `.Designer.cs` 中，设计模式不会创建通信服务。

软件及模拟检查通过，实机通信、标定、机械回零与精度尚待验收。软件停止通过通信队列发送，不能替代硬件急停。

- [操作与验收](docs/工艺工作台使用与验收.md)
- [任务完成情况](docs/上位机任务完成情况.md)
- [DXF 样例](examples/course-shapes.dxf)
