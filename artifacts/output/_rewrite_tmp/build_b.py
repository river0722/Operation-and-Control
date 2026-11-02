
# -*- coding: utf-8 -*-
import sys
sys.path.insert(0, r"D:\pmac资料\output\_rewrite_tmp")
from docxkit import *

MED = r"D:\pmac资料\output\_rewrite_tmp\media"

doc = setup_doc()
h(doc, 1, "5.2 功能模块规划与工艺流程")

para(doc, "本节说明图形怎样变成可执行轨迹、执行结果又怎样被确认。上位机管解析、规划、程序生成、任务管理和结果分析；PMAC 管程序解释、双轴插补、伺服运算和输出。")

h(doc, 2, "5.2.1 功能模块划分及关系")
para(doc, "系统用三类数据串联模块：DrawingPath 存输入图形，MotionPlan 存规划和程序，TrajectoryRun 存一次运行及反馈。手动调试、设备配置和统一反馈通过共享操作状态参与运行管理。图5-1 上半部分是上位机的七个功能域，下半部分是 Turbo PMAC；图底注明三条边界：通信应答不等于动作完成，软件轮询不等于伺服周期，硬件急停由独立安全回路承担。")
figure(doc, MED + r"\B_image1.png", 5.83)
caption(doc, "图5-1  上位机功能模块及与控制器的职责边界")

h(doc, 2, "5.2.2 模块输入、输出与协作")
para(doc, "模块之间只通过数据对象和业务接口交换信息：界面提请求，业务服务检查条件并组织执行，通信会话访问设备；算法模块不直接操作界面或控制器。")
add_table(doc, "表5-4  功能模块的输入、输出与实现对应",
  ["模块", "输入", "输出及下游关系", "当前实现"],
  [
   ["图形输入与交互", "鼠标点、图形参数、文件路径", "生成笔画或提交解析请求，交给轨迹处理", "PathView、MotionCanvas"],
   ["文件解析", "ASCII DXF 文本、单位或比例", "DrawingPath；非法实体返回具体错误", "DxfReader"],
   ["轨迹处理", "笔画、缩放/平移、点间距、行程", "校验、变换、重采样后的路径，送入规划器", "PathGeometry、CanvasProjection"],
   ["速度与时间规划", "路径、当前位置、插补模式、速度与加速度上限", "MotionPlan：分段位置、末速度、时间、转移标记", "TrajectoryPlanner、MotionPlanningService"],
   ["程序生成与预览", "MotionPlan、轴映射、坐标系和程序号", "PMAC 程序文本，供预览、保存和下载", "PmacProgramWriter、ProgramFileStore"],
   ["运动流程与互锁", "运行/停止请求、规划、有效反馈、配置确认", "启动或停止操作；维护归属、完成与异常状态", "MotionWorkflow、OperationState、MotionGuard"],
   ["控制会话与传输", "设备操作、程序文件", "命令应答、下载结果、原始位置与状态；统一串行调度", "ControllerSession、StaCommandDispatcher、PcommTransport"],
   ["反馈监测与诊断", "位置、轴与坐标系状态、查询时刻", "统一反馈、轨迹点和报警，返回界面与流程管理", "FeedbackService、AlarmService、DeviceStatus"],
   ["分析、导出与持久化", "MotionPlan、TrajectoryRun、配置和操作事件", "误差与滞后统计、CSV/TXT/PMC、XML 配置与日志", "TrajectoryAnalysis、RunDataExporter、DeviceConfigurationStore"],
   ["手动调试与模拟", "点动、定位、使能、回零或模拟参数", "受保护的轴操作；模拟反馈用于流程验证", "ManualMotionService、CanvasFollower、SimulatorTransport"],
  ],
  [2.9, 4.3, 5.4, 4.0], center_cols=())
para(doc, "界面线程只做交互和显示，解析、规划、分析放后台，PcommServer 的 COM 调用走独立 STA 队列串行执行。各页面共用 FeedbackService，不重复查询。断开、换轴映射或取消任务后，旧请求结果作废，不能再触发原任务。")

h(doc, 2, "5.2.3 主要工艺流程")
para(doc, "图5-2 是从输入到反馈的主流程，分三段：上位机准备下发、PMAC 执行、上位机监测留存。解析或规划失败返回修改，启动条件不足不下发，运行后分正常完成与异常/停止两路。")
figure(doc, MED + r"\B_image2.png", 5.75)
caption(doc, "图5-2  图形输入、运动执行与状态反馈工艺流程")

h(doc, 2, "5.2.4 七个工艺步骤与关键判断")
para(doc, "**（1）设备准备与输入建立**：选模拟或实机模式并连接，核对 X/Y 轴号、ct/mm 比例、行程、坐标系和程序号，再用绘图、典型图形或 DXF 建立路径。绘图只改数据，不动电机；鼠标跟随单独启用。")
para(doc, "**（2）文件解析与轨迹处理**：解析器按实体取出线段、圆弧、圆、多段线和受支持的样条，按文件单位或指定比例换成毫米；几何模块做平移、缩放、重采样和行程检查，形成带笔画的 DrawingPath。不支持的实体、异常数据或越界路径显示原因并停止。")
para(doc, "**（3）速度与时间规划**：按路径、当前位置、点间距和速度、加速度上限生成 PVT 或 SPLINE1，笔画间插入定位转移。PVT 在急转角降到停速；SPLINE1 做圆滑，不保证过所有输入点。规划同时检查曲线极值和行程，超限就延长时间。")
para(doc, "**（4）程序生成与启动复核**：规划写成轴映射、绝对坐标、插补模式、位置、末速度和时间的程序文本供预览；图形、参数或起点变了必须重新规划。启动前检查连接、反馈新鲜度、配置确认、轴使能与闭环、故障和到位，执行前再确认当前位置与规划起点相距不超过 0.01 mm、预留坐标系空闲。")
para(doc, "**（5）下发与控制器执行**：实机把程序写入临时文件，经 PcommServer 下载，成功且任务未取消才发送坐标系启动命令。PMAC 按自己的周期解释程序，完成插补和伺服运算。模拟模式跑数学规划模型，只验证流程。")
para(doc, "**（6）反馈监测、完成与停止**：定时查询位置、轴状态和坐标系状态，记采样时刻并更新实际轨迹。正常完成要满足：规划时间到、轴与坐标系到位、坐标系程序已停、终点偏差不超过 0.05 mm、无阻止运行的故障。故障、越界、超时或停止请求进入中止流程，发出停止后等连续新反馈确认；确认不了就保持锁定并提示重试，通信恢复后不自动续跑。")
para(doc, "**（7）分析与数据留存**：结束后用 MotionPlan 和 TrajectoryRun 算时间配对误差、轮廓偏差、滞后和查询统计，导出原始点、规划段、实际反馈、分析报告和程序，中止的运行标为“未完整运行”。两轴顺序读取，时间不完全一致，结果用于过程诊断，机械精度另测。")

h(doc, 2, "5.2.5 接口与数据流")
para(doc, "主数据链：DrawingPath → MotionPlan → PMAC 程序 → ControllerFeedback → TrajectoryRun → AnalysisResult。下载成功只说明传输成功，运行和完成还要靠状态反馈证明。")
add_table(doc, "表5-5  主要接口与数据流",
  ["数据流 / 接口", "发送方 → 接收方", "内容与处理规则"],
  [
   ["图形数据", "界面 / DxfReader → PathGeometry", "DrawingPath：毫米坐标、笔画和点列；DXF 按 $INSUNITS 或指定比例换算。"],
   ["规划请求与结果", "PathView → MotionPlanningService → TrajectoryPlanner", "输入 PlanningOptions 与当前位置；输出 MotionPlan，含段时间、位置、末速度、模式和转移标记。"],
   ["程序生成", "MotionPlan → PmacProgramWriter", "生成轴映射、ABS、PVT 或 SPLINE1 等程序文本；图形或参数改变使旧规划失效。"],
   ["设备业务接口", "MotionWorkflow / ManualMotionService → IControllerSession", "StartPlan、StopPlan、Jog、Move、Home、ReadFeedback 等经 Execute 进入同一 STA 队列。"],
   ["实机传输", "ControllerSession → PcommTransport → PcommServer → PMAC", "ASCII 命令请求与应答、程序下载；以太网设备由厂家工具配置，当前主流程不用自写 Socket。"],
   ["执行反馈", "PMAC → ControllerSession → FeedbackService → 流程与界面", "位置计数、轴状态、坐标系状态、查询时间；转换为毫米位置、运行判定、报警和显示数据。"],
   ["运行记录与文件", "MotionWorkflow → TrajectoryRun → 分析与导出", "全量反馈独立于画布历史保存；导出 drawing.csv、planned.csv、actual.csv、analysis.txt、program.pmc。"],
  ],
  [3.0, 5.0, 8.6], center_cols=())
para(doc, "**单位**：轨迹用 mm、mm/s、mm/s²，规划时间用 ms；控制器位置是 ct，按各轴 ct/mm 换算，手动界面的计数参数不能与毫米参数混用。")
para(doc, "**命令**：#nP 读位置，#n? 读电机状态，&c?? 读坐标系状态；&cBpR 启动坐标系 c 的程序 p，&cA 请求中止。n、c、p 为电机号、坐标系号和程序号。")
para(doc, "**节奏**：常规 50 ms 请求一次，手动确认可加密到 25 ms；上一轮未返回不发新的；超过 1 s 未更新标为过期并限制新运动。软件停止仍依赖这条通道。")

h(doc, 2, "5.2.6 上下位机职责边界")
para(doc, "四类主体的分工可概括为：上位机管“想做什么、怎么规划”，PMAC 管“按周期做出来”，驱动与机械决定“准不准”，安全回路独立于前三者。")
add_table(doc, "表5-6  上位机、控制器及现场设备职责",
  ["对象", "承担职责", "边界与验收依据"],
  [
   ["上位机", "用户交互、文件解析、轨迹与时间规划、程序生成与下载、启动与停止请求、轮询监测和数据分析", "不以界面定时器承担伺服周期；通信应答与设备到位分别判断；坐标映射和设备配置须先核对"],
   ["Turbo PMAC", "解释已下载的运动程序，执行 PVT/SPLINE1 双轴同步插补、伺服运算和硬件输出，更新位置与运行状态", "按现场配置处理限位、跟随误差和回零；参数、固件兼容性和保护动作需实机验证"],
   ["驱动器与机械", "接收 PMAC 输出并驱动电机，形成实际位移；按接线提供反馈、限位和原点信号", "内部脉冲计数不是独立机械测量；精度、方向、行程和重复性需量具或测量系统验证"],
   ["独立安全回路", "硬件急停、安全门和必要的抱闸等安全动作，按设备设计实现", "不依赖 Windows 轮询或上位机通信队列；按现场安全接线和设备要求验收"],
  ],
  [2.8, 7.0, 6.8], center_cols=())

h(doc, 2, "5.2.7 实现状态与提交证据")
para(doc, "**已实现**：自由绘图与规定范围的 ASCII DXF 解析、坐标换算与行程检查、PVT/SPLINE1 规划与程序生成、模拟运行、手动保护、统一反馈、停止与完成判定、误差与滞后分析、导出、配置和日志。实机传输、下发、回零和 PLC 启停已有代码，也留有只读诊断记录。")
para(doc, "**待实机验收**：完整双轴图形执行、轴向与比例标定、机械回零、现场 PLC 协作、驱动器与失联保护、停止距离、真实采样性能和轨迹精度。接口存在或只读通信成功，不算完成。")
para(doc, "**计划功能**：原生 DWG 解析、独立 G54 工件坐标系、与笔画联动的抬笔/刀具开关、控制器同步锁存采集。目前用 DXF 转换、路径平移和顺序查询替代。")
para(doc, "本节提交图5-1、图5-2、表5-4、表5-5 及两张图的 draw.io 源文件；验收时沿“输入—规划—下发—反馈—结果”追溯一次任务。")

out_b = r"D:\pmac资料\output\_rewrite_tmp\B_new.docx"
doc.save(out_b)
print("saved", out_b)
