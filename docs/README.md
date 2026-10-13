# Operation and Control

PMAC 运动控制学习资料与 C# WinForms 上位机项目。上位机使用 .NET Framework 3.5、x86，支持模拟模式与 PcommServer 实机通信，提供电机位置反馈、点动、定位、程序下载和双轴绘图跟随。

![上位机双画布界面](online/screenshots/dual-canvases.png)

## 目录

| 路径 | 内容 |
| --- | --- |
| [online](online/) | 上位机源码、编译脚本、界面截图与设计说明 |
| [pmac资料](pmac资料/) | PMAC 用户手册、编程手册、通信教程、培训资料及伺服驱动资料 |
| [PMAC零基础最快学习路线.md](PMAC零基础最快学习路线.md) | PMAC 入门学习路线 |
| [PMAC上位机学习路线.md](PMAC上位机学习路线.md) | C# 上位机学习路线 |
| [tmp](tmp/) | 学习过程中提取的参考文本、图片和参考代码 |

## 编译与运行

在 Windows 上安装 .NET Framework 3.5，并准备 Visual Studio 的 .NET 桌面开发工具。在仓库根目录执行：

```powershell
cd online
.\build.ps1
```

生成的程序位于 `online/Operations Control/bin/Release/Operations Control.exe`。默认使用模拟模式，点击“连接设备”即可体验。实机通信需要安装并配置 32 位 PcommServer / PMAC 驱动。

也可在 Visual Studio 中打开 `online/Operations Control/Operations Control.csproj`。操作方法、坐标配置、模拟检查及模块说明见 [上位机项目说明](online/README.md)，代码结构与后续视觉检测设计见 [设计说明](online/docs/代码结构与视觉检测多线程设计说明.md)。

## 参考与许可

上位机的界面参考 [lin-tea/PMAC_CSharp](https://github.com/lin-tea/PMAC_CSharp)，具体参考版本见上位机项目说明。

项目保留仓库的 [MIT LICENSE](LICENSE)。收录的第三方手册、安装包及参考素材，其权利归各自原作者所有。
