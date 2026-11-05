# 上位机复制实机反馈到 NX

实机连接成功后，上位机自动启动输出目录 `NxLink/nxlink.py --host-input`。上位机独占 PMAC 连接，通过子进程标准输入发送已采集的位置；Python 仅计算速度并提供 OPC UA 服务。模拟连接不会自动启动桥接。

## 首次部署

1. 目标电脑安装 Python 3.13。无需使用开发电脑的安装路径。
2. 上位机连接实机时自动探测可运行的 Python 3.13：检查可选环境变量 `PMAC_NXLINK_PYTHON`、本地 `.venv`、`py -3.13`、用户/系统注册表（含 32 位注册项）、PATH 和常见目录。每个候选必须实际启动且版本匹配，旧电脑复制来的失效虚拟环境会跳过。日志显示找到的本机完整路径。
3. 自动检查 `asyncua`。已安装时直接使用；缺少时创建或修复程序旁的 `.venv` 并安装依赖（首次安装需联网，程序目录需可写）。也可先运行 `NxLink/setup.bat` 完成环境准备。环境准备失败会写日志，保留实机控制连接。
4. 连接实机，在上位机日志中确认“NX 数据桥已就绪”和持续更新的 `seq`、位置与速度。
5. NX 连接 `opc.tcp://127.0.0.1:4843/pmac/`，选择 `XYPlatform` 下 `X_Pos`、`Y_Pos`、`X_speed`、`Y_speed`，填写标记表订阅项。

搬到另一台电脑时复制完整发布文件夹，保留 EXE 旁的 `NxLink`。不要打包开发电脑的 `.venv`；虚拟环境记录基础解释器绝对路径，并非可搬移的 Python。旧包若已经带上，新的检测逻辑会跳过失效环境；若系统解释器缺少依赖，将刷新旧 `.venv` 的配置和启动器，无需手改 `pyvenv.cfg`。

完整运行包额外包含 `NxLink/wheels/win_amd64` 和 `NxLink/wheels/win32`，按所选 Python 位数自动离线安装依赖，无需联网下载。目标电脑仍需装有 Python 3.13、.NET Framework 3.5 和对应的 PMAC 驱动。Python 与驱动使用目标电脑已安装的版本。

转换沿用输出目录 `NxLink/config.json`：位置 counts × `0.0006103515625` → mm，再加 offset；速度由位置差分，经窗口、低通、死区和跳变保护后 ×36 → °/s。NX 若改用直线速度执行器，改 `unit_scale` 为 1。X/Y 对应上位机当前映射的两个电机，不使用脚本中的 `#1P/#2P` 另行查询。

## 数据与生命周期

每个新反馈复制一份 JSON 行：`x_counts, y_counts, x_time_s, y_time_s, x_motor, y_motor, seq, t`。counts 是原始计数，只在 Python 换算一次；轴时间为上位机单调时钟秒数，`t` 为 Unix 采样时间。单轴快速刷新不会伪造另一个轴的新采样；差分分别使用两个轴的真实采样时间。重复快照不再次发送。

转发沿用现有上位机采样节拍，常规轮询 50ms、手动执行期间 25ms，实际频率取决于设备响应和命令队列。没有新增 PMAC 连接或额外位置查询；不会把低频反馈重复发送来冒充 50Hz。日志显示实际转发频率。

待发队列只保留最新帧，处理落后时允许跳号。脚本拒绝过期、非数值与倒序帧。500ms 未收到有效帧或某轴采样落后超过 500ms 时将相应 NX 速度归零；恢复后重置差分历史。断开设备或关闭上位机时关闭管道，脚本先输出零速度再停止 OPC UA。只清理本次启动的子进程，不结束系统中的其他 Python/PcommServer。

脚本缺依赖、端口冲突或意外退出会写入上位机“NX”日志，保留实机控制连接。处理后断开并重新连接实机即可重新启动桥接。异常退出不会自动重启。

## 脚本与原包

工程内 `src/Operations Control/NxLink/` 是随程序发布的副本，编译时复制到 EXE 旁。用户提供的原包也同步更新 `nxlink.py` 并增加 `host_input.py`，其 `config.json` 保持原值。

`--host-input` 在依赖预检、管理员提权和进程清理前分支，既不加载 COM 模块，也不连接/关闭 PMAC。原来的独立采集模式继续保留，需要其原有 pywin32 依赖。与上位机同时运行时必须使用 `--host-input`；不要启动原包默认的独立采集命令。

## 验证

`py -3 -m unittest discover -s tests -p test_nxlink.py -v` 检查单位、单轴采样、异常帧、断流与 EOF。`scripts/build.ps1 -Test` 检查上位机回归。实机现场仍需核对位置方向、比例、NX 零点、模型速度和实际帧率。

完整 C# → Python → OPC UA 集成测试（不访问硬件，使用配置中的本机 4843 端口）：先编译上位机并安装 Release 目录的脚本环境，再执行：

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v3.5\csc.exe" /nologo /target:exe /platform:x86 '/out:src\Operations Control\bin\Release\OperationsControl.NxLinkBridgeTests.exe' '/reference:src\Operations Control\bin\Release\Operations Control.exe' /reference:System.Core.dll tests\NxLinkBridgeTests.cs
& '.\src\Operations Control\bin\Release\NxLink\.venv\Scripts\python.exe' -m unittest discover -s tests -p test_nxlink.py -v
```

未安装 asyncua 的解释器会跳过 OPC UA 集成项，其余管道测试仍可运行。
