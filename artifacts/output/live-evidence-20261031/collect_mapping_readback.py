"""Read actual CS1 mapping and a bounded prefix of program 20; never assign or run."""
import datetime as dt
import json
import re
import socket
import struct
import sys
import time
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
ROOT = Path(__file__).resolve().parent
DATA = ROOT/'data'
TZ = dt.timezone(dt.timedelta(hours=8))
ALLOWED = {'&1??', '&1#1->', '&1#2->', 'I42', 'I5190', 'I124', 'I224',
           'LIST PROG 20,0,32', 'LIST PROG 20,32,32',
           'LIST PROG 20,64,32', 'LIST PROG 20,96,32'}


def main():
    events = []
    destination = DATA/'mapping-and-program-readback-02.jsonl'
    if destination.exists():
        raise FileExistsError('Do not overwrite an earlier capture')
    with destination.open('x', encoding='utf-8') as out:
        with socket.create_connection(('192.6.94.5', 1025), timeout=3) as sock:
            sock.settimeout(3)
            def query(command):
                if command not in ALLOWED:
                    raise ValueError('Read-only allowlist rejected '+command)
                payload = command.encode('ascii')
                packet = struct.pack('!BBHHH',0x40,0xbf,0,0,len(payload))+payload
                sent_at = dt.datetime.now(TZ).isoformat(timespec='milliseconds')
                start = time.perf_counter()
                sock.sendall(packet)
                raw = b''
                error = None
                try:
                    while b'\x06' not in raw and b'\x07' not in raw and not raw.endswith(b'\x00'):
                        chunk = sock.recv(4096-len(raw))
                        if not chunk:
                            raise ConnectionError('Controller closed socket')
                        raw += chunk
                        if len(raw)>=4096:
                            raise ValueError('Response exceeded bounded query size')
                except (TimeoutError, ConnectionError, ValueError) as exc:
                    error = str(exc)
                response = raw.decode('ascii',errors='backslashreplace').strip('\x00\x06\r\n ')
                event = dict(sent_at=sent_at, received_at=dt.datetime.now(TZ).isoformat(timespec='milliseconds'),
                             command=command,response=response,request_hex=packet.hex(),raw_hex=raw.hex(),
                             query_ms=round((time.perf_counter()-start)*1000,3),
                             result='transport_error' if error else 'controller_error' if b'\x07' in raw or response.startswith('ERR') else 'ok',
                             error=error)
                events.append(event)
                out.write(json.dumps(event,ensure_ascii=False)+'\n')
                out.flush()
                return event
            status = query('&1??')['response']
            for command in ('&1#1->','&1#2->','I42','I5190','I124','I224'):
                query(command)
            if re.fullmatch('[0-9A-Fa-f]{18}',status) and not int(status[:6],16)&1:
                for offset in (0,32,64,96):
                    if query(f'LIST PROG 20,{offset},32')['result'] != 'ok':
                        break
            else:
                print('CS1 running/unknown; skipped program prefix listing')
    (DATA/'program20-prefix-readback.txt').write_text(
        'SOURCE: live controller LIST readback attempt, 2026-10-31; requested prefix words 0..127.\n'
        'Not a complete program; not tied to the historical DXF run.\n\n'+
        '\n\n'.join(e['command']+' ['+e['result']+']\n'+e['response'] for e in events if e['command'].startswith('LIST'))+'\n',encoding='utf-8')
    print(json.dumps(events,ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
