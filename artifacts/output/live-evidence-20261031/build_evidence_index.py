"""Build a reviewable checklist revision without overwriting an unsaved editor buffer."""
import csv
import datetime as dt
import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
DATA = ROOT/'data'
tz = dt.timezone(dt.timedelta(hours=8))
captured = dt.datetime.now(tz).isoformat(timespec='milliseconds')
sources = []
source_paths = [
    (REPO/'docs'/'4.1-4.3证据清单.md', 'checklist-original-on-disk.md'),
    (REPO/'src'/'Operations Control'/'Controls'/'CanvasProjection.cs', 'source-CanvasProjection.cs'),
    (REPO/'src'/'Operations Control'/'Models'/'CanvasModels.cs', 'source-CanvasModels.cs'),
    (REPO/'src'/'Operations Control'/'Models'/'DeviceStatus.cs', 'source-DeviceStatus.cs'),
    (REPO/'src'/'Operations Control'/'bin'/'Debug'/'Operations Control.exe', None),
    (Path(os.environ['LOCALAPPDATA'])/'PMAC Operations Control'/'logs'/'20261030.log', None),
    (Path(os.environ['LOCALAPPDATA'])/'PMAC Operations Control'/'logs'/'20261031.log', 'gui-20261031-after.log'),
]
for path, snapshot in source_paths:
    content = path.read_bytes()
    if snapshot:
        (DATA/snapshot).write_bytes(content)
    sources.append(dict(path=str(path), snapshot=snapshot, size=len(content),
                        sha256=hashlib.sha256(content).hexdigest(),
                        modified_at=dt.datetime.fromtimestamp(path.stat().st_mtime, tz).isoformat(),
                        captured_at=captured))
(DATA/'sources.json').write_text(json.dumps(sources, ensure_ascii=False, indent=2), encoding='utf-8')

templates = {
    'field-two-point-calibration.csv': (
        ['status','point','target_x_mm','target_y_mm','expected_x_ct','expected_y_ct','controller_x_ct','controller_y_ct','measured_x_mm','measured_y_mm','error_x_mm','error_y_mm','instrument','speed_mm_s','timestamp','operator','evidence'],
        [['TODO','1']+['']*15, ['TODO','2']+['']*15]),
    'field-reproduction.csv': (
        ['status','cad_filename','cad_sha256','autocad_export_version','target_size_mm','strokes','points','mode','spacing_mm','speed_mm_s','start_x_mm','start_y_mm','end_x_mm','end_y_mm','measured_endpoint_x_mm','measured_endpoint_y_mm','contour_rms_mm','contour_max_mm','instrument','date','operator','photo_video','run_export_folder'],
        [['TODO']+['']*22]),
    'field-hardware-and-safety.csv': (
        ['status','item','observed_result','evidence_filename','date','operator'],
        [['TODO',name,'','','',''] for name in ['铭牌完整型号','机械原点','工件零点','X物理正方向','Y物理正方向','实际X行程','实际Y行程','硬件急停回路','正负限位回路','驱动器报警联锁','回零与起点条件','真实报警恢复']]),
}
for name, (header, rows) in templates.items():
    with (DATA/name).open('w', encoding='utf-8-sig', newline='') as out:
        writer = csv.writer(out)
        writer.writerow(header)
        writer.writerows(rows)

original = (DATA/'checklist-original-on-disk.md').read_text(encoding='utf-8-sig')
revised = original.replace('# 4.1—4.3 证据与材料清单', '# 4.1—4.3 证据与材料清单（实机补充版，2026-10-31）', 1)
intro = '''
> 本版以原清单磁盘版本为基础补充，原文件在 Typora 中存在未保存编辑，故另存。完整证据、原始日志、截图、配置快照与限制说明见 [本次实机证据包](../artifacts/output/live-evidence-20261031/README.md)。
>
> 新增：实机连接截图；197 条原始命令应答；20 轮双轴状态/位置/误差/速度；当前硬件配置；昨日 DXF/PVT 启动、停止和运行完成的实机会话。机械照片、量具校验和同次运动原始轨迹仍待补。
>
> **坐标更正**：当前代码为 X 向右、Y 向下为正，原清单中的 Y 向上及反向公式已在本版修正；机械方向仍待现场核对。

'''
revised = revised.replace('\n这份清单用于', '\n'+intro+'这份清单用于', 1)
revised = revised.replace('- `[待补]`：', '- `[部分已收集]`：已有真实材料，但尚不能满足整项验收条件。\n- `[待补]`：', 1)

rows = {
    '实机 GUI 连接截图': '[已收集] | 实机 GUI 连接截图 | 已保存“PMAC 实机 / 已连接 / 设备 0”真实窗口，时间由配套采集元数据记录。见证据包 E01；截图本身没有时钟',
    '一次完整实机命令—反馈日志': '[部分已收集] | 一次完整实机命令—反馈日志 | 新采 197 条只读命令，含发送/接收时间、应答、十六进制与结果；昨日实机 GUI 会话含启动/停止/运行完成。仍缺同一次运动的原始参数写入、启动和停止命令。见 E02/E06/E07',
    '实机参数设置证据': '[部分已收集] | 实机参数设置证据 | TYPE=TURBO2, X4；VERSION=1.947；CID=602404；设备 0；地址 192.6.94.5:1025；GUI 电机 1/2、坐标系 1、程序 20；有配置快照与 I 参数读数。完整铭牌、运动参数写入及读回仍待补；P801 历史写入不能替代速度参数设置。见 E02/E04/E08',
    '实机运动启动和停止/复位证据': '[部分已收集] | 实机运动启动和停止/复位证据 | 2026-10-30 实机会话 03：18:10:47.915 运行中，18:10:54.911 用户停止被接受，18:10:55.078 轴/坐标系反馈停稳确认；没有连续截图或视频，复位仍待补。见 E06',
    '“全接管”连续演示': '[部分已收集] | “全接管”连续演示 | 已有实机会话覆盖连接→DXF→PVT→启动→停止→再次运行完成；缺参数写后读回、原始运动反馈、报警恢复和连续画面。见 E06/E07，不能标为完整演示验收',
    '工作平面坐标系实物示意图': '[待补] | 工作平面坐标系实物示意图 | 需平台照片标明机械原点、X/Y 正方向、工件零点和有效行程。当前软件 Y 正向为屏幕向下，尚不能据此推断机械方向',
    '实际单位、比例和偏移': '[部分已收集] | 实际单位、比例和偏移 | 硬件配置 X/Y=1000 ct/mm；GUI X=−120～280 mm、Y=−48～187 mm；坐标系 1；两轴控制器位置偏置寄存器均为 0。尚未量具标定、机械方向/工件零点确认；控制器软限位与 GUI 不一致。见 E02/E04',
    '真实映射前后对比截图': '[待补] | 真实映射前后对比截图 | 已有实机连接截图和静止位置原始读数，仍缺同一图形、控制器读数与平台物理位置的对应画面。Y 正方向按当前代码修正后再进行现场校验',
    '本组实际 AutoCAD 原文件': '[部分已收集] | 本组实际 AutoCAD 原文件 | 已清点 examples/Drawing1.dwg 与 Drawing1.dxf，记录哈希；DXF 为 AC1018、毫米、1 条 LINE。作者归属、AutoCAD 原图与导出设置尚未确认，不能把它视为历史 12 笔画运行文件。见 E09',
    '实机下发与运动日志': '[部分已收集] | 实机下发与运动日志 | 历史实机会话 03 有 12 笔画/177 点，PVT 1615/1617 段，运行中、停止反馈和运行完成。缺导入文件名/哈希、同次 PMAC 程序和硬件 actual/planned 原始轨迹。见 E06/E07',
    '运行前安全条件记录': '[部分已收集] | 运行前安全条件记录 | 2026-10-31 20 轮反馈中两轴使能、闭环、速度零、到位，未置正负限位/阻断故障位，Home Complete=0；历史日志有未使能/超速拦截。硬件急停、物理行程、回零与起点仍未确认；不能据此直接宣称满足运动条件。见 E03/E05/E08',
}
lines = revised.splitlines()
replacements = set()
for i, line in enumerate(lines):
    if not line.startswith('| [待补] | '):
        continue
    cells = line.split('|')
    key = cells[2].strip()
    if key in rows:
        lines[i] = '| '+rows[key]+' |'
        replacements.add(key)
if replacements != set(rows):
    raise ValueError('Checklist changed; inspect before applying: '+repr(set(rows)-replacements))
revised = '\n'.join(lines)+'\n'
revised = revised.replace('X 向右为正、Y 向上为正；屏幕像素原点在左上角、屏幕 Y 向下', '当前实现 X 向右、Y 向下为正；屏幕像素原点在左上角；机械方向待确认')
revised = revised.replace('y = Ymin + (T+H-py)/H·(Ymax-Ymin)', 'y = Ymin + (py-T)/H·(Ymax-Ymin)')
revised = revised.replace('反向显示公式也已整理', '原既有 Word 文档公式需同步复核；当前源码证据优先')
revised = revised.replace('- PMAC 型号：________________；固件版本：________________；通信方式：________________', '- PMAC 自报类型：`TURBO2, X4`（铭牌完整型号待补）；固件版本：`1.947`；通信方式：PcommServer COM / Ethernet；CID：`602404`')
revised = revised.replace('- PcommServer 设备号：________________；控制器地址/端口：________________', '- PcommServer 设备号：`0`；控制器地址/端口：`192.6.94.5:1025`')
revised = revised.replace('- X/Y 电机号：________________ / ________________；坐标系号：________________；程序号：________________', '- X/Y 电机号：`1 / 2`；坐标系号：`1`；程序号：`20`（后三者为 GUI 配置快照）')
revised = revised.replace('- 实机测试日期：________________；操作人：________________；测试文件/程序：________________', '- 本次只读补采日期：`2026-10-31 13:41:04—13:41:14 +08:00`；操作人：待现场填写；历史运动日期：`2026-10-30`；历史导入文件名：日志未记录')
revised = revised.replace('- 连接结果：________________；启动反馈：________________；停止/复位反馈：________________；报警/恢复结果：________________', '- 连接结果：实机设备 0 已连接；启动/停止：昨日实机会话日志有记录，连续画面待补；复位/真实报警恢复：待补')
revised = revised.replace('- X 行程：________________ mm；Y 行程：________________ mm；X ct/mm：________________；Y ct/mm：________________', '- GUI 配置 X 行程：`−120～280 mm`；Y 行程：`−48～187 mm`；X/Y 比例：`1000 / 1000 ct/mm`；以上待物理行程与量具标定')
revised = revised.replace('- X 偏移：________________；Y 偏移：________________；标定日期：________________；标定方法：________________', '- 控制器位置偏置原始寄存器：X/Y 均为 `000000000000`；机械/工件零偏：待补；标定日期/方法：待补，不能将寄存器为零视为完成标定')
revised += '''
## 2026-10-31 补采后提交状态

- [x] 有标识为实机的 GUI 连接截图，并附采集时间元数据。
- [x] 有型号/固件/配置只读命令原始应答，197 条记录、20 轮双轴反馈。
- [x] 有昨日实机会话的 DXF 导入、PVT 启动、停止反馈和运行完成摘要；已区分模拟会话。
- [ ] 同一次参数写入—读回—运动—停止的原始命令及连续画面仍待补。
- [ ] 机械原点/方向/行程、1000 ct/mm 标定及两个量具实测点仍待补。
- [ ] 同一 AutoCAD 文件→导入→程序→硬件轨迹→平台复现的完整对应关系仍待补。
- [ ] 真实报警恢复、硬件安全回路和机械精度仍待现场确认。

附件：[证据包总索引](../artifacts/output/live-evidence-20261031/README.md)、[两点校验空表](../artifacts/output/live-evidence-20261031/data/field-two-point-calibration.csv)、[图形复现空表](../artifacts/output/live-evidence-20261031/data/field-reproduction.csv)。
'''
destination = REPO/'docs'/'4.1-4.3证据清单_实机补充20261031.md'
destination.write_text(revised, encoding='utf-8')
(DATA/'checklist-revised.md').write_text(revised, encoding='utf-8')

manifest = []
for path in sorted(ROOT.rglob('*')):
    if path.is_file() and path.name != 'SHA256SUMS.txt':
        manifest.append(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.relative_to(ROOT).as_posix())
(ROOT/'SHA256SUMS.txt').write_text('\n'.join(manifest)+'\n', encoding='utf-8')
print(json.dumps({'checklist':str(destination), 'evidence_files':len(manifest), 'replaced_rows':len(replacements)}, ensure_ascii=False))
