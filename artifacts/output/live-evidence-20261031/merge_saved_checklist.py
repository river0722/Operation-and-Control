"""Merge collected evidence into the user's saved checklist, preserving their edited scope."""
import datetime as dt
import hashlib
import json
import re
import shutil
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[2]
DATA=ROOT/'data'
TARGET=REPO/'docs'/'4.1-4.3证据清单.md'
content=TARGET.read_text(encoding='utf-8-sig')
marker='## 2026-10-31 实机补采索引'
if marker in content:
    raise ValueError('Already merged; edit the current document instead of reapplying')
stamp=dt.datetime.now(dt.timezone(dt.timedelta(hours=8))).strftime('%H%M%S')
backup=DATA/f'checklist-user-saved-before-merge-{stamp}.md'
backup.write_text(content,encoding='utf-8')
content=content.replace('- `[待补]`：','- `[部分已收集]`：已有实机材料，但尚不满足整项证据要求。\n- `[待补]`：',1)
content=content.replace('X 向右为正、Y 向上为正；屏幕像素原点在左上角、屏幕 Y 向下','当前代码 X 向右、Y 向下为正；屏幕像素原点在左上角；机械方向见现场填写，尚待照片校验')
content=content.replace('y = Ymin + (T+H-py)/H·(Ymax-Ymin)','y = Ymin + (py-T)/H·(Ymax-Ymin)')
content=content.replace('反向显示公式也已整理','当前代码与原 Word 中 Y 公式的差异已在补采记录注明')
lines=content.splitlines()
for i,line in enumerate(lines):
    if not line.startswith('| [待补] | '):
        continue
    item=line.split('|')[2].strip()
    if item=='实机 GUI 连接截图':
        lines[i]='| [已收集] | 实机 GUI 连接截图 | [实机连接截图](../artifacts/output/live-evidence-20261031/figures/01-real-connected.png)、[实机设置截图](../artifacts/output/live-evidence-20261031/figures/03-real-settings.png)；设备 0、已连接；采集时间见配套 JSON 元数据 |'
    elif item=='一次完整实机命令—反馈日志':
        lines[i]='| [部分已收集] | 一次完整实机命令—反馈日志 | 已有 197 条只读原始命令/应答、20 轮双轴反馈，以及昨日实机会话的启动、停止、运行完成摘要；仍缺同一次参数写入—回读—启动—运动反馈—停止的原始命令和连续画面。见下方实机补采索引 |'
    elif item=='工作平面坐标系实物示意图':
        lines[i]='| [部分已收集] | 工作平面坐标系实物示意图 | 已保留现场填写的机械原点和 X/Y 正方向原文；当前有实机设置截图与行程值。平台照片、工件零点和方向标注仍待补 |'
    elif item=='本组实际 AutoCAD 原文件':
        lines[i]='| [部分已收集] | 本组实际 AutoCAD 原文件 | 已清点 examples/Drawing1.dwg、Drawing1.dxf 并保存哈希；DXF 头为 AC1018、毫米，ENTITIES 只有 1 条 LINE；原图、归属和导出设置仍待确认，不能将它与历史 12 笔画运行视为同一文件 |'
content='\n'.join(lines)+'\n'
content+='''
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
'''
TARGET.write_text(content,encoding='utf-8')
# Keep the alternate review copy synchronized, retaining the user's latest saved additions.
(REPO/'docs'/'4.1-4.3证据清单_实机补充20261031.md').write_text(content,encoding='utf-8')
(DATA/'checklist-revised.md').write_text(content,encoding='utf-8')
(DATA/'merge-record.json').write_text(json.dumps(dict(backup=str(backup),target=str(TARGET),
    original_sha256=hashlib.sha256(backup.read_bytes()).hexdigest(),
    revised_sha256=hashlib.sha256(TARGET.read_bytes()).hexdigest(),
    saved_user_fields_preserved=True),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(target=str(TARGET),backup=str(backup)),ensure_ascii=False))
