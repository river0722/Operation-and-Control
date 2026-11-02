"""Preserve existing real GUI sessions and inspect CAD headers without moving hardware."""
import collections
import hashlib
import json
import os
import re
import shutil
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
DATA = ROOT/'data'
SOURCE = Path(os.environ['LOCALAPPDATA'])/'PMAC Operations Control'
out = []
for name in ('20261030.log', '20261031.log'):
    path = SOURCE/'logs'/name
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    sessions, current = [], None
    for n, line in enumerate(lines, 1):
        # A new startup or any new connection ends the preceding mode interval.
        if '请选择连接模式' in line or '已连接 ' in line:
            if current:
                sessions.append(current)
                current = None
        if '已连接 PMAC' in line:
            current = {'source': str(path), 'source_start_line': n, 'lines': []}
        if current:
            current['lines'].append((n, line))
            if '设备连接已关闭' in line:
                sessions.append(current)
                current = None
    if current:
        sessions.append(current)
    for i, s in enumerate(sessions, 1):
        filename = name.replace('.log', f'-real-session-{i:02d}.log')
        (DATA/filename).write_text('\n'.join(line for _, line in s['lines'])+'\n', encoding='utf-8')
        selected = [(n, line) for n, line in s['lines'] if any(word in line for word in
            ('命令 ', '指令', '到位', '停稳', '启动', '运行', '报警', '恢复', '参数', 'DXF', '导入'))]
        out.append(dict(source=s['source'], source_start_line=s['source_start_line'],
                        source_end_line=s['lines'][-1][0], extracted_file=filename,
                        first_line=s['lines'][0][1], last_line=s['lines'][-1][1],
                        selected_events=[dict(source_line=n, text=line) for n, line in selected]))
(DATA/'existing-real-sessions.json').write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding='utf-8')

cad = []
for path in (REPO/'examples'/'Drawing1.dwg', REPO/'examples'/'Drawing1.dxf', REPO/'nailong_machine_fit.dxf'):
    item = dict(path=str(path), size=path.stat().st_size, sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                provenance='Existing workspace file; authorship/export settings not verified')
    if path.suffix.lower() == '.dxf':
        lines = path.read_text(encoding='utf-8', errors='replace').splitlines()
        pairs = [(lines[i].strip(), lines[i+1].strip()) for i in range(0, len(lines)-1, 2)]
        section, entities = '', collections.Counter()
        for i, (code, value) in enumerate(pairs):
            if (code, value) == ('0', 'SECTION'):
                section = pairs[i+1][1]
            elif (code, value) == ('0', 'ENDSEC'):
                section = ''
            elif section == 'ENTITIES' and code == '0':
                entities[value] += 1
            if code == '9' and value in ('$ACADVER', '$INSUNITS', '$LASTSAVEDBY'):
                item[value] = pairs[i+1][1]
        item['entity_counts'] = dict(entities)
    cad.append(item)
(DATA/'existing-cad-inventory.json').write_text(json.dumps(cad, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'real_sessions': [{k:v for k,v in s.items() if k!='selected_events'} for s in out],
                  'selected_real_events': [e for s in out for e in s['selected_events']], 'cad':cad}, ensure_ascii=False, indent=2))
