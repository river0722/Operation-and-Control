# PMAC Course Completion Implementation Plan

**Goal:** 将现有手动控制程序扩展为可模拟验证的 DXF/绘图 → PVT/Spline 规划 → 执行 → 轨迹分析流程，并补齐轴状态、回零接口与课程说明。

**Architecture:** 保留 .NET Framework 3.5 / x86。独立图形模型、DXF 读取、规划及分析服务。新增工艺工作台窗体共享现有 STA 控制会话；模拟器执行同一份规划数据，实机输出并下载可审阅的 PMAC 程序。运行期间统一禁止手动干预，停止和关闭取消当前操作。

**Tech Stack:** C# 3.0、WinForms、PcommServer、ASCII DXF、自包含几何算法、现有 MSBuild/模拟检查。

用户已确认：采用 ASCII DXF；暂无实机参数，完成软件与模拟验证，实机保持待验收。

## 1. 几何与 CAD

- [x] 新增 Models/PathModels.cs：带笔画的路径、规划参数、计时段、执行采样。坐标为 mm，时间为 ms。
- [x] 新增 Services/PathGeometry.cs：直线、圆、圆弧、星形和曲线采样；平移/缩放；完整点列与范围检查。
- [x] 新增 Services/DxfReader.cs：LINE、ARC、CIRCLE、LWPOLYLINE（含 bulge）、POLYLINE、SPLINE（有理 B 样条）；读取单位并对不支持实体明确报错，禁止悄悄遗漏。
- [x] 测试已知圆弧端点、多段线 bulge、样条端点、单位缩放、畸形文件与不支持实体。

## 2. 规划、执行与反馈

- [x] 新增 Services/TrajectoryPlanner.cs：从完整路径生成 PVT 或 Spline 段；限制速度、加速度、行程和总点数；多笔画之间显式定位段。
- [x] 新增 Services/PmacProgramWriter.cs：绝对 XY 坐标、明确坐标系统/程序号/轴比例；生成后供查看和保存。
- [x] 扩展 ControllerSession 与 SimulatorTransport：计划执行/停止/状态、闭环使能与失能、机械 HOME 及可配置 PLC 启动，反馈采样不使用目标冒充实际。
- [x] 新增状态解析并以 Turbo PMAC 手册核对位定义；运行前检查状态，运行中失败/超时/停止退出。
- [x] 测试同步路径、结束状态、停止后不续跑、故障与超时、HOMEZ 与 HOME 的区别、命令生成。

## 3. 完整数据与分析

- [x] 新增 Services/TrajectoryAnalysis.cs：完整参考/实际点列 CSV，几何轮廓偏差、按时间配对误差、有限区间时延估计与通信耗时统计。
- [x] 每次采样记录 X/Y 时间差、请求耗时、计划时间；模拟结果显式标记来源。
- [x] 测试已知平移误差、已知延迟、空数据、未完成运行与长轨迹保留。

## 4. 工艺工作台

- [x] 新增 Views/PathWorkbenchForm.cs 与设计文件：路径编辑/导入、模式与参数、程序预览、运行/停止、状态与回零、分析/导出。
- [x] 通过主窗体菜单打开，串行共享通信；工作台运行期间锁定原有手动控制和连接切换；关闭工作台先停止。
- [x] 扩展 MotionCanvas 路径加载和点列读取，保留原有实时画布行为。
- [x] 更新 csproj/build.ps1，运行现有 126 项检查和新增功能检查；审查生成界面截图。

## 5. 资料与交付

- [x] 提供 DXF 样例和从图形到分析的模拟操作范例。
- [x] 补充需求/验收指标、工艺流程、坐标/通信/插补原理、精度与时延分析方法。
- [x] 更新 README、任务完成情况和标注表，明确实机参数、真实精度、现场运行及周报提交仍待人工验收。

验证命令：`online/build.ps1 -Configuration Release -Test`。所有自动运行均使用模拟器。未提供的机械参数不推断成真实设备参数。
