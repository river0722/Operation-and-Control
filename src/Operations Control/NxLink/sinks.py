"""
输出通道 —— 把真实样机的数据送进 NX 2212 MCD。

NX MCD 2212 的 ExternalSignalConfigurationBuilder 原生支持这些外部适配器
（从本机 NXOpen.xml 提取）：OPCUAClient / OPCDAClient / UdpClient / TcpClient /
ShmClient / ProfinetClient / PlcSimAdvClient / MatlabClient / FMUClient。

本模块实现其中两种零成本的：
  * UdpSink   —— 直接往 MCD 的 UdpClient 端口发，延迟最低
  * OpcUaSink —— 本机起一个 OPC UA 服务器，MCD 用 OpcUaClient 连过来（推荐，
                 信号名/数据类型在 MCD 里可直接浏览选择）
"""

from __future__ import annotations

import asyncio
import json
import socket
import threading
import time
from typing import Dict


class UdpSink:
    """UDP 发送端。format: json / csv / kv"""

    def __init__(self, host: str = "127.0.0.1", port: int = 55001,
                 fmt: str = "json", names: list[str] | None = None):
        self.host = host
        self.port = port
        self.fmt = fmt
        self.names = names or []
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def start(self):
        return self

    def close(self):
        try:
            self._sock.close()
        except Exception:
            pass

    def encode(self, values: Dict[str, float]) -> bytes:
        if self.fmt == "csv":
            ordered = [values[n] for n in self.names] if self.names else list(values.values())
            return ",".join(f"{v:.6f}" for v in ordered).encode("ascii")
        if self.fmt == "kv":
            return ";".join(f"{k}={v:.6f}" for k, v in values.items()).encode("ascii")
        # 默认 json，附带时间戳便于 NX 侧排查
        payload = dict(values)
        payload["t"] = round(time.time(), 4)
        return json.dumps(payload, ensure_ascii=False).encode("utf-8")

    def send(self, values: Dict[str, float]) -> None:
        self._sock.sendto(self.encode(values), (self.host, self.port))


class OpcUaSink:
    """在后台线程里跑一个 asyncua OPC UA 服务器。

    MCD 侧把变量映射为输入信号后，客户端每次订阅周期即可拿到最新值。
    """

    def __init__(self, endpoint: str = "opc.tcp://0.0.0.0:4840/pmac/",
                 namespace: str = "PMAC", object_name: str = "XYPlatform",
                 names: list[str] | None = None):
        self.endpoint = endpoint
        self.namespace = namespace
        self.object_name = object_name
        self.names = names or []
        self._loop = None
        self._thread = None
        self._ready = threading.Event()
        self._nodes: Dict[str, object] = {}
        self._latest: Dict[str, float] = {}
        self._error: BaseException | None = None
        self._stop_event = None    # asyncio.Event，用于让 _serve 干净退出
        self.write_count = 0
        self.write_errors = 0
        self.ticks = 0            # 服务器线程循环次数，用于诊断是否卡死
        self.write_interval = 0.01   # 服务器线程里的节点刷新周期(秒)
        self.write_timeout = 0.05    # 单次写入超时(秒)

    # ---------------- 线程与事件循环 ----------------
    def _diagnose(self) -> str:
        """把底层异常翻译成能直接动手的提示。

        最常见的是 Errno 10048：上一次跑的 3_simulate / 4_real 窗口没关，
        端口还被占着。原始 traceback 只会说 "每个套接字地址只允许使用一次"，
        看不出是谁占的，所以这里直接给排查动作。
        """
        e = self._error
        text = f"{type(e).__name__}: {e}" if e is not None else "未知错误"
        low = text.lower()
        if "10048" in low or "address already in use" in low:
            return (f"{text}\n"
                    "  → 端口已被占用：多半是另一个桥接程序还在跑"
                    "（3_simulate / 4_real / check_opcua 三者只能开一个）。\n"
                    "    先把那个黑窗口关掉（Ctrl+C 或点 ×），再启动本程序。\n"
                    "    查是谁占着：netstat -ano | findstr :4843")
        if "10013" in low or "10049" in low or ("access" in low and "denied" in low):
            return (f"{text}\n"
                    "  → 该端口被系统或安全策略禁止监听。换一个高位端口再试，\n"
                    "    例如把 config.json 的 endpoint 改成 opc.tcp://127.0.0.1:4943/pmac/")
        return (f"{text}\n"
                "  → 若是端口问题，可把 config.json 的 endpoint 改成 "
                "opc.tcp://127.0.0.1:4943/pmac/ 再试")

    @staticmethod
    def _endpoint_hostport(endpoint: str):
        from urllib.parse import urlparse
        u = urlparse(endpoint.replace("opc.tcp://", "http://", 1))
        return (u.hostname or "127.0.0.1"), (u.port or 4840)

    @staticmethod
    def _port_bindable(host: str, port: int) -> bool:
        """启动前先试绑一次，把'端口被占'这种最常见的问题挡在 asyncua 之前。

        直接让 asyncua 去绑，它会先打一整屏 Traceback 再抛异常，
        用户看到的重点全被淹掉了。
        """
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        try:
            s.bind((host, port))
            return True
        except OSError:
            return False
        finally:
            s.close()

    def start(self, timeout: float = 10.0):
        host, port = self._endpoint_hostport(self.endpoint)
        if not self._port_bindable(host, port):
            raise RuntimeError(
                f"OPC UA 服务器启动失败：端口 {port} 已被占用\n"
                "  → 多半是另一个桥接程序还在跑"
                "（3_simulate / 4_real / check_opcua 三者只能开一个）。\n"
                "    先把那个黑窗口关掉（Ctrl+C 或点 ×），再启动本程序。\n"
                f"    查是谁占着：netstat -ano | findstr :{port}")

        self._thread = threading.Thread(target=self._run_loop, name="opcua-server",
                                        daemon=True)
        self._thread.start()
        if not self._ready.wait(timeout):
            if self._error is not None:
                raise RuntimeError(
                    "OPC UA 服务器启动失败：\n  " + self._diagnose()) from self._error
            raise RuntimeError(f"OPC UA 服务器启动超时（{self.endpoint}）")
        if self._error is not None:
            raise RuntimeError(
                "OPC UA 服务器启动失败：\n  " + self._diagnose()) from self._error
        return self

    def _run_loop(self):
        import asyncio
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        try:
            self._loop.run_until_complete(self._serve())
        except BaseException as exc:  # noqa: BLE001
            self._error = exc
            self._ready.set()      # 唤醒 start() 以便立刻报错

    async def _serve(self):
        from asyncua import Server

        server = Server()
        await server.init()
        server.set_endpoint(self.endpoint)
        server.set_server_name("PMAC-MCD Bridge")

        stop = asyncio.Event()
        self._stop_event = stop

        idx = await server.register_namespace(self.namespace)
        obj = await server.nodes.objects.add_object(idx, self.object_name)

        for name in self.names:
            var = await obj.add_variable(idx, name, 0.0)
            await var.set_writable()   # 让 MCD 侧识别为 Input/Output
            self._nodes[name] = var

        async with server:
            self._ready.set()
            # 由服务器线程周期性地把最新值刷进节点。
            # 注意：不要用 run_coroutine_threadsafe + future.result() 从主线程写入，
            # 实测 asyncua 服务器运行约 1 秒后该跨线程等待会阻塞到超时。
            #
            # 退出用 Event 而不是 loop.stop()：直接 stop 会让 `async with server`
            # 的 __aexit__ 在已停止的 loop 上执行，抛 "Event loop is closed" 噪音。
            # 循环条件每轮检查一次，sleep 最多 10ms，退出延迟可以忽略。
            #
            # 定时必须用 sleep，不能用 asyncio.wait_for(stop.wait(), timeout=...)
            # —— 实测 Python 3.11.9 (Windows) 上 wait_for 会立刻超时返回，
            # 100 次循环只花 0.005s，会把服务器线程变成占满一个核的空转。
            while not stop.is_set():
                self.ticks += 1
                try:
                    vals = dict(self._latest)
                    for name, val in vals.items():
                        node = self._nodes.get(name)
                        if node is not None:
                            # wait_for 是必要的保护：个别环境下 write_value 可能挂起，
                            # 一旦挂起又无超时，整个服务器线程会被拖死。
                            await asyncio.wait_for(node.write_value(float(val)),
                                                   timeout=self.write_timeout)
                            self.write_count += 1
                except asyncio.TimeoutError:
                    self.write_errors += 1
                except Exception:
                    self.write_errors += 1
                await asyncio.sleep(self.write_interval)

    # ---------------- 写入 ----------------
    def send(self, values: Dict[str, float]) -> None:
        """只更新缓存，立即返回，绝不阻塞采集主循环。"""
        self._latest = dict(values)

    def stats(self) -> str:
        return f"OPC UA 写入 {self.write_count} 次，失败 {self.write_errors} 次"

    def close(self):
        """通知服务器线程退出并等它把 `async with server` 走完。

        以前直接 loop.stop()，会让 __aexit__ 在已关闭的 loop 上执行，
        表现为退出时刷一屏 "Event loop is closed" / "Task was destroyed"，
        容易被误当成故障。
        """
        loop, evt = self._loop, self._stop_event
        if loop is not None and evt is not None:
            try:
                loop.call_soon_threadsafe(evt.set)
            except Exception:
                pass
        t = self._thread
        if t is not None and t.is_alive():
            t.join(timeout=2.0)


def build_sink(cfg: dict, names: list[str], log):
    kind = (cfg.get("type") or "udp").lower()
    if kind == "udp":
        u = cfg.get("udp", {})
        log.info("输出通道: UDP -> %s:%s (format=%s)",
                 u.get("host", "127.0.0.1"), u.get("port", 55001), u.get("format", "json"))
        return UdpSink(u.get("host", "127.0.0.1"), int(u.get("port", 55001)),
                       u.get("format", "json"), names).start()
    if kind == "opcua":
        o = cfg.get("opcua", {})
        log.info("输出通道: OPC UA 服务器 %s", o.get("endpoint"))
        return OpcUaSink(o.get("endpoint", "opc.tcp://0.0.0.0:4840/pmac/"),
                         o.get("namespace", "PMAC"),
                         o.get("object", "XYPlatform"), names).start()
    raise ValueError(f"未知的 sink.type: {kind}")
