# Operation and Control

Turbo PMAC C# WinForms 上位机，面向 .NET Framework 3.5 / x86，支持模拟运行与 PcommServer 实机通信。

程序提供 DXF 导入与绘图、直线 / 圆弧 / PVT / SPLINE1 轨迹规划、手动控制、程序编辑与下载、运行反馈、轨迹分析、设备诊断和日志导出。

完整 PVT 路径下发几何、参数和通用算法，由控制器计算速度与段时间；上位机提供同算法预览。实现、变量预留和验收边界见 [PVT 下位机参数化规划](docs/software/PVT下位机参数化规划.md)。

## 编译与运行

需要 Windows、.NET Framework 3.5，以及 Visual Studio 的 .NET 桌面开发工具或 MSBuild。模拟模式无需安装 PMAC 驱动。

```powershell
# Release 编译
.\scripts\build.ps1

# 编译并运行模拟检查
.\scripts\build.ps1 -Configuration Release -Test

# 启动程序
& '.\src\Operations Control\bin\Release\Operations Control.exe'
```

也可在 Visual Studio 中打开 `src/Operations Control/Operations Control.csproj`。测试样例位于 [examples/course-shapes.dxf](examples/course-shapes.dxf)。

## 项目内容

- `src/Operations Control/`：上位机源码和 Visual Studio 工程。
- `tests/`：模拟、工作流、模块与电机调试测试。
- `scripts/build.ps1`：编译和测试入口。
- `docs/software/`：上位机操作、验收和实现说明。

运行环境、实机配置和安全限制见 [上位机使用与验收说明](docs/software/工艺工作台使用与验收.md)。

实机连接后可自动复制位置反馈给 Python 并转发到 NX，首次运行需安装桥接依赖，见 [NX 脚本数据转发](docs/software/NX脚本数据转发.md)。

项目采用 [MIT License](LICENSE)。
