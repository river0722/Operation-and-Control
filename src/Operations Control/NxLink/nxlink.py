r"""
nxlink —— PMAC <-> NX MCD 纯通讯程序（不含点动、不含回零）

来源
----
以 4_real.py 为骨架改写：保留它"提权 → 清残留 → 依赖预检 → 起采集"的启动流程，
把 bridge.py 里真正做通讯的部分抽出来，删掉全部控制类功能。

4_real.py / 4_real.bat / bridge.py 一个字都没有改，本文件是独立的新增程序。

删掉的东西（对照 bridge.py）
---------------------------
  * 键盘点动：msvcrt 读键线程、d/a/w/s 键位、1/2/3/4 步长切换、h 帮助
  * 回零：o 键、两次确认、confirm_timeout、min_gap、config.json 里的 homing 段
  * --jog 参数（本程序不认识它；传了会报错，避免误以为点动有效）
  * 任何硬编码的 PMAC 命令

也就是说：**本程序启动后不发出一条运动命令**，只做
      读位置 -> 差分算速度 -> 推 OPC UA -> 循环
平台怎么动，由你的上位机决定。

保留的通讯能力
-------------
  1) PcommServer 采集：#1P / #2P 逐条读，批量读取不可用时自动退回
  2) 位置差分速度：3 帧窗口 + 12Hz 低通 + 0.1mm/s 死区 + 500mm/s 跳变保护
  3) OPC UA 推送：4 个节点（X_Pos / Y_Pos / X_speed / Y_speed），NX 侧配置不用改
  4) 断线重连、每秒统计日志、seq 帧号 + 统一时间戳
  5) send_command()：给上位机预留的下发入口（默认一条都不发，不调用就完全不工作）

三条硬约束（原项目踩过的坑，改了就通不了）
-----------------------------------------
  1) 必须【管理员】运行。PcommServer 是进程外 COM 服务器（ActiveX EXE），
     64 位解释器也能 Dispatch（2026-11-05 实测 3/3 成功）；真正卡住的是
     Open(device)——非管理员下 PComm32W 的以太网 Open 直接返回 False。
     所以位数不限，权限才是门槛。本程序默认自己请求提权。
  2) PMAC2-Eth-Lite 只接受一个 TCP 1025 连接，本程序必须是唯一持有者。
     不要再同时开 4_real、7_link_only、PEWin32，否则互相抢断（表现为连续采集失败）。
  3) COM 只能在采集线程里调。send_command() 只负责入队，由采集线程在帧间隙发出。

启动方式
--------
    python nxlink.py                  # 提权 → 清残留 → 检查依赖 → 开始通讯
    python nxlink.py --simulate       # 不接硬件，用正弦轨迹验证 NX 侧
    python nxlink.py --once           # 只采一帧打印，核对数值/零点
    python nxlink.py --no-elevate     # 已经是管理员时跳过提权
    python nxlink.py --no-kill        # 不清理残留的 PcommServer.exe
    python nxlink.py --sink udp       # 临时改用 UDP 输出（默认 OPC UA）

退出：Ctrl+C（或在窗口里按 Ctrl+Break）。

当作模块用（合进上位机）
-----------------------
    import sys
    sys.path.insert(0, r"D:\pmac_mcd_bridge")
    import nxlink

    link = nxlink.NxLink(on_frame=on_frame)   # on_frame 可选
    link.start()                              # 立即返回，后台线程在采
    pos = link.get("X_Pos")
    snap = link.latest()                      # {'X_Pos':…, 'seq':…, 't':…}
    link.send_command("#1J^1638", "X+1mm")    # 要动平台时由上位机按钮调用
    link.stop()
"""

from __future__ import annotations

import argparse
import ctypes
import importlib.util
import json
import logging
import math
import queue
import subprocess
import sys
import threading
import time
from collections import deque
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import sinks        # noqa: E402  同目录的输出通道（本程序不改动它）

SW_SHOWNORMAL = 1
CREATE_NO_WINDOW = 0x08000000
TITLE = "nxlink —— PMAC -> NX MCD 纯通讯"


# ---------------------------------------------------------------------------
# 启动外壳（沿用 4_real.py 的做法）
# ---------------------------------------------------------------------------
def is_admin() -> bool:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


def elevate(argv: list[str]) -> bool:
    """用管理员身份重新启动自己。返回 True 表示本进程该退出了。"""
    params = subprocess.list2cmdline([str(Path(__file__).resolve())] + list(argv))
    try:
        rc = ctypes.windll.shell32.ShellExecuteW(
            None, "runas", sys.executable, params, str(HERE), SW_SHOWNORMAL)
    except Exception as exc:
        print(f"  [!!] 请求提权失败: {exc}")
        return False

    # >32 成功；<=32 是错误码，5 通常就是用户点了"否"
    if rc <= 32:
        if rc == 5:
            print("  [!!] 提权被拒绝（UAC 那一下点了“否”）。")
        else:
            print(f"  [!!] 提权失败，ShellExecuteW 返回 {rc}。")
        print("       可以改成：右键 python.exe -> 以管理员身份运行，再执行本脚本。")
        return False
    print("  已用管理员身份重新启动，本窗口退出。")
    return True


def kill_leftover_server() -> None:
    """清掉残留的 PcommServer.exe。

    它是进程外 COM 服务器，会一直握着 PMAC 的唯一 TCP 连接；
    只要它还活着，任何 Open 都返回 False（连 ping 都可能不通）。
    """
    try:
        r = subprocess.run(["taskkill", "/F", "/IM", "PcommServer.exe"],
                           capture_output=True, text=True,
                           creationflags=CREATE_NO_WINDOW)
        if r.returncode == 0:
            print("  [OK]  清掉了一个残留的 PcommServer.exe（它占着 PMAC 连接）")
        else:
            print("       没有残留的 PcommServer.exe，继续。")
    except Exception as exc:
        print(f"  [??]  清理 PcommServer 失败（不影响启动）: {exc}")


def preflight(force: bool) -> bool:
    """依赖预检。返回 False 表示不该继续。"""
    missing = [m for m in ("win32com", "pythoncom")
               if importlib.util.find_spec(m) is None]
    if missing:
        print(f"  [!!] 缺少 pywin32（{', '.join(missing)}），无法调用 PcommServer。")
        print(f"       安装：{sys.executable} -m pip install pywin32")
        return False

    if importlib.util.find_spec("asyncua") is None:
        if force:
            print("  [!!] 没装 asyncua，但指定了 --force，继续（只可能走 UDP 通道）。")
            return True
        print("  [!!] 没装 asyncua —— 默认输出通道是 OPC UA，跑到一半会失败。")
        print(f"       安装：{sys.executable} -m pip install asyncua")
        print("       想先用 UDP 通道试：加 --force")
        return False
    return True


# ---------------------------------------------------------------------------
# 位置差分速度
# ---------------------------------------------------------------------------
class VelocityDeriver:
    """把位置差分成速度（mm/s），供 NX 的速度控制执行器使用。

    这段逻辑是通讯链路的一部分，不是显示层的修饰，不能省：

    为什么不用 PMAC 的 #nV：
        Turbo PMAC 的 #nV 返回的是指令速度，单位随固件可能是 counts/ms，
        也可能是用户单位，需要再实测一个系数。位置 #nP 的 mm 换算已经实测
        闭环（1637 counts ≈ 0.99915 mm），用它差分出来的速度单位天然是 mm/s。

    三处处理缺一不可：
        1) 多帧窗口差分 —— 单帧 20ms 差分时 1 count(0.61µm)=0.03mm/s 的量化
           噪声会让静止时的速度来回跳，模型跟着抖；
        2) 一阶低通 —— 压残余噪声；
        3) 死区 —— 低于阈值直接吐 0，模型才能真正停住。

    防位置跳变：重连、PMAC 复位、改 offset 时位置会突跳，差分出来是几百 mm/s
    的假速度会把模型甩飞。max_speed 超限就当异常，清空历史重新起步，且输出 0
    而不是限幅值（限幅值同样会把模型推走）。
    """

    def __init__(self, source_name: str, target_name: str,
                 window: int = 3, lowpass_hz: float = 12.0,
                 deadband: float = 0.02, max_speed: float = 500.0,
                 unit_scale: float = 1.0):
        self.source_name = source_name
        self.target_name = target_name
        self.window = max(1, int(window))
        self.lowpass_hz = float(lowpass_hz)
        self.deadband = abs(float(deadband))
        self.max_speed = abs(float(max_speed))
        self.unit_scale = float(unit_scale)

        self._buf: deque = deque(maxlen=self.window + 1)
        self._v = 0.0
        self.jumps = 0

    def reset(self) -> None:
        """重连后调用：丢掉历史位置，避免拿旧帧算出一个假速度。"""
        self._buf.clear()
        self._v = 0.0

    def update(self, values: dict, t: float) -> float:
        p = values.get(self.source_name)
        if p is None:
            return self._v

        self._buf.append((t, float(p)))
        if len(self._buf) < 2:
            self._v = 0.0
            return 0.0

        (t0, p0), (t1, p1) = self._buf[0], self._buf[-1]
        dt = t1 - t0
        if dt <= 1e-6:
            return self._v

        # 物理量统一按 mm/s 算：deadband / max_speed 都以 mm/s 表达，
        # unit_scale 只在最后一步换成下游单位（NX 旋转副是 °/s）。
        # 若提前乘进来，deadband=0.1 会变成 0.1°/s（比噪声还小），
        # max_speed=500 会变成 500°/s≈13.9mm/s（正常点动被误判成跳变）。
        raw = (p1 - p0) / dt

        if abs(raw) > self.max_speed:
            self.jumps += 1
            self._buf.clear()
            self._buf.append((t1, p1))
            self._v = 0.0
            return 0.0

        if self.lowpass_hz > 0:
            tau = 1.0 / (2.0 * math.pi * self.lowpass_hz)
            alpha = dt / (tau + dt)
            self._v += alpha * (raw - self._v)
        else:
            self._v = raw

        if abs(self._v) < self.deadband:
            self._v = 0.0
        return self._v * self.unit_scale


# ---------------------------------------------------------------------------
# 通讯主类
# ---------------------------------------------------------------------------
class NxLink:
    """PMAC 采集 + 速度派生 + OPC UA 推送，一个对象管完。

    线程模型：
        start()  → 起一个采集线程（daemon），start() 本身立即返回
        采集线程 → 读 PMAC、算速度、写 sink、填 latest()、调 on_frame 回调
        stop()   → 通知采集线程退出并 join

    外部（上位机 UI 线程）只碰公开方法，不要访问内部的 source / sink。
    """

    def __init__(self, config: str | Path | None = None,
                 simulate: bool = False,
                 period_ms: int | None = None,
                 on_frame=None,
                 sink_override: str | None = None,
                 logger: logging.Logger | None = None):
        """
        :param config:        config.json 路径，默认同目录
        :param simulate:      True 时不接硬件，用模拟轨迹跑通 NX 侧链路
        :param period_ms:     覆盖配置里的采样周期（ms）
        :param on_frame:      每帧回调 on_frame(frame: dict)，在采集线程里执行，
                              必须很快返回，除了取数/画界面不要做重活
        :param sink_override: 覆盖配置里的 sink.type（"opcua" / "udp"）
        :param logger:        外部传入的 logger；不传则用 "nxlink" 这个
        """
        global pmac_source
        import pmac_source
        self.cfg = self._load_config(Path(config) if config else HERE / "config.json")
        self.simulate = bool(simulate)
        self.on_frame = on_frame
        self.log = logger or logging.getLogger("nxlink")
        if not self.log.handlers and not logging.getLogger().handlers:
            self.log.addHandler(logging.NullHandler())

        self.signals = self.cfg["signals"]
        self.names = [s["name"] for s in self.signals]
        # source="derived" 的信号（速度）不向 PMAC 发命令，由本地差分算出
        self.read_signals = [s for s in self.signals if s.get("source") != "derived"]
        self.read_names = [s["name"] for s in self.read_signals]

        loop = self.cfg.get("loop", {})
        self.period = (period_ms if period_ms
                       else loop.get("period_ms", 20)) / 1000.0
        self.max_err = int(loop.get("max_consecutive_errors", 10))
        self.reconnect_delay = float(loop.get("reconnect_delay_s", 2.0))
        self.stats_interval = float(loop.get("stats_interval_s", 1.0))
        self.batch = bool(self.cfg["pmac"].get("batch_commands", False))
        self._batch_ok: bool | None = None

        if sink_override:
            self.cfg["sink"]["type"] = sink_override

        self.source = None
        self.sink = None

        # ---- 命令出口（默认空队列，一条都不会发）----
        # 保留它是因为 PcommServer 只允许一条连接：上位机若自己再 Dispatch
        # 一次，就会把本程序的连接抢断（就是 PEWin 干过的事）。上位机要动
        # 平台，只能从这里入队，由采集线程在帧间隙发出。
        self._cmd_q: "queue.Queue[tuple[str, str]]" = queue.Queue(maxsize=64)
        self._cmd_sent = 0
        self._cmd_failed = 0

        # ---- 帧缓存 ----
        # _frame 是"当前这一帧"的唯一副本：上位机、on_frame、OPC UA 三个消费者
        # 拿到的是同一份数据、同一个 seq，不会出现两边错帧对不上。
        self._lock = threading.Lock()
        self._frame: dict | None = None
        self._seq = 0

        # ---- 生命周期与统计 ----
        self._thread: threading.Thread | None = None
        self._stop = threading.Event()
        self._started = False
        self._t0 = 0.0
        self._frames = 0
        self._err_total = 0
        self._cb_errors = 0
        self._rate = 0.0
        self.last_error: str | None = None

        self.derivers = self._build_derivers()

    # ------------------------------------------------------------ 配置
    @staticmethod
    def _load_config(path: Path) -> dict:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)

    def _build_derivers(self) -> list:
        out = []
        for sig in self.signals:
            if sig.get("source") != "derived":
                continue
            src = sig.get("from")
            if not src:
                self.log.warning("信号 %s 标了 derived 但缺少 from 字段，已跳过",
                                 sig.get("name"))
                continue
            if src not in self.names:
                self.log.warning("信号 %s 的来源 %s 不在信号表里，已跳过",
                                 sig["name"], src)
                continue
            out.append(VelocityDeriver(
                source_name=src,
                target_name=sig["name"],
                window=sig.get("window", 3),
                lowpass_hz=sig.get("lowpass_hz", 12.0),
                deadband=sig.get("deadband", 0.02),
                max_speed=sig.get("max_speed", 500.0),
                unit_scale=sig.get("unit_scale", 1.0),
            ))
        return out

    # ------------------------------------------------------------ 生命周期
    def start(self) -> "NxLink":
        """连 PMAC、起 OPC UA 服务器、启动采集线程。失败抛异常，不做静默降级。"""
        if self._started:
            return self
        self._stop.clear()
        self._open_source()
        self._open_sink()

        self._started = True
        self._t0 = time.perf_counter()
        self._thread = threading.Thread(target=self._loop, name="nxlink", daemon=True)
        self._thread.start()

        self.log.info("通讯已启动 | 周期=%.1fms | 读取=%s | 输出=%s",
                      self.period * 1000, self.read_names,
                      self.cfg["sink"].get("type", "udp"))
        if self.derivers:
            self.log.info("速度通道 | %s",
                          "  ".join(f"{d.target_name}={d.source_name}差分"
                                   f"(窗口{d.window}帧/低通{d.lowpass_hz:.0f}Hz/"
                                   f"死区{d.deadband:.3f}×{d.unit_scale:g})"
                                   for d in self.derivers))
        self.log.info("本程序不含点动/回零，不会主动发出任何运动命令")
        return self

    def stop(self, timeout: float = 3.0) -> None:
        """停采集线程并关闭连接。可以重复调用。"""
        self._stop.set()
        t = self._thread
        if t is not None and t.is_alive():
            t.join(timeout=timeout)
        self._close_all()
        self._started = False
        self.log.info("通讯已停止 | 累计 %d 帧 | 下发 %d 条命令",
                      self._frames, self._cmd_sent)

    def _close_all(self) -> None:
        for closer in (self.sink, self.source):
            try:
                if closer is not None:
                    closer.close()
            except Exception:
                pass

    def __enter__(self):
        return self.start()

    def __exit__(self, *exc):
        self.stop()
        return False

    # ------------------------------------------------------------ 连接
    def _open_source(self):
        p = self.cfg["pmac"]
        if self.simulate:
            self.source = pmac_source.SimulatedSource(self.read_signals)
        else:
            self.source = pmac_source.PmacSource(
                prog_id=p.get("prog_id", "PcommServer.PmacDevice.1"),
                device_number=int(p.get("device_number", 0)),
                auto_select_device=bool(p.get("auto_select_device", False)),
            )
        self.source.connect()
        self._log_identity()

    def _log_identity(self) -> None:
        if self.simulate or not hasattr(self.source, "device_info"):
            return
        try:
            info = self.source.device_info()
            self.log.info("控制器 | 型号=%s | 通讯=%s | 固件=%s",
                          info.get("pmac_type_name"), info.get("location_name"),
                          info.get("rom_version"))
            if info.get("pmac_type") == 7:
                self.log.info("已确认为 PMAC2 Turbo，与 PMAC2-Eth-Lite (Clipper) 匹配")
        except Exception as exc:
            self.log.debug("读取控制器身份失败: %s", exc)

    def _open_sink(self) -> None:
        self.sink = sinks.build_sink(dict(self.cfg["sink"]), self.names, self.log)

    # ------------------------------------------------------------ 采集
    def _read_frame(self) -> dict:
        """读一帧，返回 {信号名: 工程单位数值}，只含要向控制器读取的信号。"""
        if self.simulate:
            return self.source.read_values()

        values: dict = {}
        if self.batch and self._batch_ok is not False:
            cmds = [s["command"] for s in self.read_signals]
            try:
                lines = self.source.read_batch(cmds)
                parsed = [pmac_source.parse_number(ln) for ln in lines]
                self._batch_ok = True
                for sig, val in zip(self.read_signals, parsed):
                    values[sig["name"]] = self._to_engineering(sig, val)
                return values
            except Exception as exc:
                if self._batch_ok is None:
                    self.log.warning("批量读取不可用，改为逐条读取: %s", exc)
                    self._batch_ok = False

        for sig in self.read_signals:
            raw = self.source.read_response(sig["command"])
            values[sig["name"]] = self._to_engineering(sig, pmac_source.parse_number(raw))
        return values

    def _derive(self, values: dict, t: float) -> dict:
        for d in self.derivers:
            values[d.target_name] = d.update(values, t)
        return values

    @staticmethod
    def _to_engineering(sig: dict, raw: float) -> float:
        return raw * sig.get("scale", 1.0) + sig.get("offset", 0.0)

    # ------------------------------------------------------------ 命令下发
    def send_command(self, cmd: str, desc: str = "") -> bool:
        """把一条 PMAC 在线命令排进队列，由采集线程在下一帧间隙发出。

        这是上位机控制下位机的唯一入口。本程序自己从不调用它。
        返回值只表示"是否成功入队"，不表示命令执行成功——
        PMAC 的执行结果看每秒日志里 X_Pos/Y_Pos 是否在变。

        :param cmd:  完整命令文本，例如 "#1J^1638"、"#1J=1638.4"
        :param desc: 日志里显示的可读描述，例如 "X+1mm"
        """
        cmd = (cmd or "").strip()
        if not cmd:
            return False
        if self.simulate:
            self.log.warning("模拟模式：命令 %s 未发送", cmd)
            return False
        try:
            self._cmd_q.put_nowait((cmd, desc or cmd))
            return True
        except queue.Full:
            self.log.warning("命令队列已满，丢弃 %s（PMAC 响应跟不上？）", cmd)
            self._cmd_failed += 1
            return False

    def _drain_cmds(self, max_cmds: int = 2) -> None:
        """在采集线程里发命令。每帧最多 2 条，避免命令突发把采集饿死。"""
        if self.simulate or self.source is None:
            return
        for _ in range(max_cmds):
            try:
                cmd, desc = self._cmd_q.get_nowait()
            except queue.Empty:
                return
            try:
                resp = self.source.read_response(cmd)
                resp = (resp or "").strip().replace("\r", " ")
                self._cmd_sent += 1
                self.log.info("下发 %-14s | %s -> %s", desc, cmd, resp or "(空响应)")
            except Exception as exc:
                self._cmd_failed += 1
                self.last_error = f"命令 {cmd} 发送失败: {exc}"
                self.log.warning("命令发送失败 %s (%s): %s", cmd, desc, exc)

    # ------------------------------------------------------------ 读取接口
    def latest(self) -> dict | None:
        """取当前帧的副本（含 seq / t）。还没采到就返回 None。

        返回副本而不是内部对象，避免宿主改坏共享帧；
        seq 和值仍然是同一份，不影响"同一帧"的语义。
        """
        with self._lock:
            return dict(self._frame) if self._frame is not None else None

    def get(self, name: str, default: float = 0.0) -> float:
        """便捷取值：link.get("X_Pos")。"""
        with self._lock:
            if self._frame is None:
                return default
            return float(self._frame.get(name, default))

    def is_running(self) -> bool:
        t = self._thread
        return bool(self._started and t is not None and t.is_alive())

    def stats(self) -> dict:
        """链路状态，给上位机做健康指示用。"""
        return {
            "running": self.is_running(),
            "uptime_s": round(time.perf_counter() - self._t0, 1) if self._t0 else 0.0,
            "frames": self._frames,
            "rate_hz": round(self._rate, 1),
            "seq": self._seq,
            "errors": self._err_total,
            "cb_errors": self._cb_errors,
            "jumps": sum(d.jumps for d in self.derivers),
            "cmds_queued": self._cmd_q.qsize(),
            "cmds_sent": self._cmd_sent,
            "cmds_failed": self._cmd_failed,
            "last_error": self.last_error,
        }

    # ------------------------------------------------------------ 主循环
    def _loop(self) -> None:
        t_next = time.perf_counter()
        t_stats = t_next
        frames = 0
        errors = 0

        while not self._stop.is_set():
            t_next += self.period          # 固定节拍：用累加而不是 now+period，
            try:                           # 否则每帧的耗时会被不断叠加进周期
                self._drain_cmds()
                values = self._read_frame()
                values = self._derive(values, time.perf_counter())

                with self._lock:
                    seq = self._seq
                    self._seq += 1
                    values["seq"] = seq
                    values["t"] = round(time.time(), 4)
                    self._frame = values   # 原子替换引用，读者永远拿到完整一帧

                if self.sink is not None:
                    self.sink.send(values)

                self._frames += 1
                frames += 1            # 本统计窗口内的帧数，别跟 self._frames 混了
                errors = 0

                cb = self.on_frame
                if cb is not None:
                    try:
                        cb(values)     # 传的是同一份对象，热路径不复制
                    except Exception as exc:
                        self._cb_errors += 1
                        if self._cb_errors <= 5:
                            self.log.warning("on_frame 回调异常: %s", exc)

            except Exception as exc:
                errors += 1
                self._err_total += 1
                self.last_error = str(exc)
                self.log.warning("采集失败(%d/%d): %s", errors, self.max_err, exc)
                if errors >= self.max_err:
                    self.log.error("连续失败次数超限，尝试重连 PMAC …")
                    if not self._reconnect():
                        break
                    errors = 0

            now = time.perf_counter()
            if now - t_stats >= self.stats_interval:
                self._rate = frames / (now - t_stats)
                with self._lock:
                    snap = dict(self._frame) if self._frame is not None else {}
                self.log.info("实时 %6.1f Hz | seq=%s | %s",
                              self._rate, snap.get("seq", "-"),
                              "  ".join(f"{k}={v:.3f}" for k, v in snap.items()
                                        if k not in ("seq", "t")))
                frames = 0
                t_stats = now

            sleep = t_next - time.perf_counter()
            if sleep > 0:
                time.sleep(sleep)
            else:
                t_next = time.perf_counter()   # 跟不上就放弃补偿，避免雪崩

    def _reconnect(self) -> bool:
        """断线重连。返回 False 表示收到了停止请求，应该退出主循环。"""
        while not self._stop.is_set():
            try:
                self._close_all()
                self._open_source()
                self._open_sink()
                for d in self.derivers:
                    d.reset()      # 历史位置已经没意义了，重来
                self.log.info("PMAC 重连成功")
                return True
            except Exception as exc:
                self.log.error("重连失败: %s —— %.0fs 后重试", exc, self.reconnect_delay)
                if self._stop.wait(self.reconnect_delay):
                    return False
        return False


# ---------------------------------------------------------------------------
# 日志与环境
# ---------------------------------------------------------------------------
def configure_logging(verbose: bool, log_file: Path) -> logging.Logger:
    log = logging.getLogger("nxlink")
    log.setLevel(logging.DEBUG if verbose else logging.INFO)
    log.handlers.clear()
    log.propagate = False

    fmt = logging.Formatter("%(asctime)s.%(msecs)03d  %(levelname)-7s %(message)s",
                            "%H:%M:%S")
    sh = logging.StreamHandler(sys.stdout)
    sh.setFormatter(fmt)
    log.addHandler(sh)

    try:
        fh = logging.FileHandler(log_file, encoding="utf-8")
        fh.setFormatter(logging.Formatter("%(asctime)s %(levelname)-7s %(message)s"))
        log.addHandler(fh)
    except Exception:
        pass
    return log


def _print_banner() -> None:
    print("=" * 64)
    print(f"  {TITLE}")
    print("  只读位置、只推数据；点动与回零已移除")
    print("=" * 64)
    print(f"  python : {sys.executable}")
    print(f"  bitness: {'64' if sys.maxsize > 2 ** 32 else '32'} bit")
    print(f"  cwd    : {HERE}")
    print(f"  admin  : {is_admin()}")
    print()


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="PMAC -> NX MCD 纯通讯程序（无点动、无回零）")
    ap.add_argument("--config", default=str(HERE / "config.json"))
    ap.add_argument("--simulate", action="store_true", help="用模拟数据代替真实 PMAC")
    ap.add_argument("--host-input", action="store_true",
                    help="从上位机标准输入接收 JSON 帧，不连接 PMAC、不提权、不清理进程")
    ap.add_argument("--period", type=int, help="采样周期(ms)，覆盖配置")
    ap.add_argument("--once", action="store_true", help="只采一帧并打印，核对数值")
    ap.add_argument("--sink", choices=("opcua", "udp"), help="覆盖配置里的输出通道")
    ap.add_argument("--no-elevate", action="store_true", help="不请求提权")
    ap.add_argument("--no-kill", action="store_true", help="不清理残留的 PcommServer.exe")
    ap.add_argument("--force", action="store_true", help="缺 asyncua 也继续（只可能走 UDP）")
    ap.add_argument("-v", "--verbose", action="store_true")
    args = ap.parse_args(argv)

    if args.host_input:
        if args.simulate or args.once:
            ap.error("--host-input 不能与 --simulate / --once 同用")
        from host_input import run
        log = configure_logging(args.verbose, HERE / "nxlink.log")
        return run(Path(args.config), args.sink, log, VelocityDeriver)

    _print_banner()

    if not args.no_elevate and not is_admin():
        print("  当前不是管理员，正在请求提权（UAC 弹窗点“是”）...")
        # 提权后要跳过提权本身，否则新进程会再次判断
        passthrough = [a for a in (argv if argv is not None else sys.argv[1:])
                       if a != "--no-elevate"]
        if elevate(passthrough + ["--no-elevate"]):
            return 0
        return 1

    if not args.no_kill:
        kill_leftover_server()

    if not preflight(args.force):
        return 2

    log = configure_logging(args.verbose, HERE / "nxlink.log")
    global pmac_source
    import pmac_source
    pmac_source.setup_logging(log)

    link = NxLink(config=args.config, simulate=args.simulate,
                  period_ms=args.period, on_frame=None,
                  sink_override=args.sink, logger=log)
    try:
        if args.once:
            link._open_source()
            try:
                values = link._derive(link._read_frame(), time.perf_counter())
                print(json.dumps(values, ensure_ascii=False, indent=2))
                log.info("提示：--once 只采一帧，没有历史位置可比，速度必然为 0，属正常")
            finally:
                if link.source is not None:
                    link.source.close()
            return 0

        link.start()
        log.info("按 Ctrl+C 退出（本程序不会主动让平台运动）")
        while link.is_running():
            time.sleep(0.5)
    except KeyboardInterrupt:
        log.info("用户中断")
    except pmac_source.PmacError as exc:
        log.error("%s", exc)
        return 2
    except Exception as exc:
        log.exception("未预期的错误: %s", exc)
        return 3
    finally:
        link.stop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
