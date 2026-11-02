"""Capture timestamped Turbo PMAC evidence. No motion/assignment commands allowed."""
import csv
import datetime as dt
import json
import re
import socket
import struct
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
DATA = ROOT / 'data'
DATA.mkdir(exist_ok=True)
TZ = dt.timezone(dt.timedelta(hours=8))
ENDPOINT = ('192.6.94.5', 1025)
MEMORY = {'RHL:$0000CC', 'RHL:$00014C'}


def now():
    return dt.datetime.now(TZ).isoformat(timespec='milliseconds')


def flags(status):
    if not re.fullmatch(r'[0-9a-fA-F]{12}', status):
        return {'parse_error': status}
    a, b = int(status[:6], 16), int(status[6:], 16)
    return dict(active=bool(a & 0x800000), enabled=bool(a & 0x080000),
                open_loop=bool(a & 0x040000), negative_limit=bool(a & 0x400000),
                positive_limit=bool(a & 0x200000), velocity_zero=bool(a & 0x002000),
                in_position=bool(b & 1), home_complete=bool(b & 0x400),
                following_warning=bool(b & 2),
                blocking_fault=bool(b & 0x16c or a & 0x4000),
                position_limit_stopped=bool(b & 0x1800))


def main():
    session = {'started_at': now(), 'endpoint': list(ENDPOINT),
               'transport': 'TCP / VR_PMAC_GETRESPONSE 0xBF (independent read-only probe)',
               'gui_transport': 'PcommServer COM (existing GUI connection)',
               'motion_or_assignment_sent': False, 'samples_are_atomic': False}
    events, samples = [], []
    raw_path = DATA / 'controller-readonly.jsonl'
    if raw_path.exists():
        raise FileExistsError('Keep original capture intact; use another session directory')
    with raw_path.open('x', encoding='utf-8') as stream:
        with socket.create_connection(ENDPOINT, timeout=3) as sock:
            sock.settimeout(3)
            session['local_endpoint'] = list(sock.getsockname())
            session['connected_at'] = now()

            def query(command, phase, sample=None):
                if command not in MEMORY and not re.fullmatch(
                        r'(?:VERSION|CID|TYPE|I\d+|#[12][?PFV])', command):
                    raise ValueError('Read-only allowlist rejected ' + command)
                payload = command.encode('ascii')
                packet = struct.pack('!BBHHH', 0x40, 0xBF, 0, 0, len(payload)) + payload
                sent_at, start = now(), time.perf_counter()
                sock.sendall(packet)
                raw = b''
                while not (b'\x06' in raw or raw.endswith(b'\x00') or raw.endswith(b'\r\x07')):
                    part = sock.recv(4096 - len(raw))
                    if not part:
                        raise ConnectionError('Controller closed probe socket')
                    raw += part
                    if len(raw) >= 4096:
                        raise ValueError('Oversized controller response')
                response = raw.decode('ascii', errors='backslashreplace').strip('\x00\x06\r\n ')
                event = dict(sent_at=sent_at, received_at=now(), command=command,
                             response=response, raw_hex=raw.hex(), request_hex=packet.hex(),
                             query_ms=round((time.perf_counter()-start)*1000, 3),
                             phase=phase, sample=sample,
                             result='controller_error' if b'\x07' in raw or response.startswith('ERR') else 'ok')
                events.append(event)
                stream.write(json.dumps(event, ensure_ascii=False)+'\n')
                stream.flush()
                return response

            identity = {q: query(q, 'identity') for q in ('VERSION', 'CID', 'TYPE')}
            config = {}
            offsets = (0, 2, 3, 4, 8, 11, 12, 13, 14, 20, 21, 22, 28, 88)
            for axis in (1, 2):
                for offset in offsets:
                    q = 'I' + str(axis*100+offset)
                    config[q] = query(q, 'configuration')
            for q in ('I7010', 'I7016', 'I7020', 'I7026', *sorted(MEMORY)):
                config[q] = query(q, 'configuration')
            for sample in range(1, 21):
                row = {'sample': sample, 'started_at': now()}
                for axis in (1, 2):
                    values = {suffix: query(f'#{axis}{suffix}', 'feedback', sample)
                              for suffix in ('?', 'P', 'F', 'V')}
                    row[f'axis{axis}'] = dict(status=values['?'], position_ct=float(values['P']),
                                             following_error_ct=float(values['F']), velocity_raw=float(values['V']),
                                             **flags(values['?']))
                row['ended_at'] = now()
                samples.append(row)
                if sample != 20:
                    time.sleep(0.5)
        session['probe_socket_closed_at'] = now()
    session.update(ended_at=now(), query_count=len(events), sample_count=len(samples),
                   identity=identity, controller_configuration=config,
                   first_sample=samples[0], last_sample=samples[-1])
    (DATA/'session.json').write_text(json.dumps(session, ensure_ascii=False, indent=2), encoding='utf-8')
    (DATA/'feedback.json').write_text(json.dumps(samples, ensure_ascii=False, indent=2), encoding='utf-8')
    with (DATA/'command-response.csv').open('w', encoding='utf-8-sig', newline='') as out:
        writer = csv.DictWriter(out, fieldnames=list(events[0]))
        writer.writeheader()
        writer.writerows(events)
    with (DATA/'feedback.csv').open('w', encoding='utf-8-sig', newline='') as out:
        rows = [dict(sample=s['sample'], started_at=s['started_at'], ended_at=s['ended_at'], axis=a,
                     **s[f'axis{a}']) for s in samples for a in (1, 2)]
        writer = csv.DictWriter(out, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    print(json.dumps(session, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
