"""Receive copied host feedback over stdin. This mode never loads PcommServer."""
from __future__ import annotations

import json
import math
import queue
import sys
import threading
import time

import sinks


class HostFrames:
    def __init__(self, cfg, deriver_factory):
        self.signals = {s["name"]: s for s in cfg["signals"]}
        self.derivers = {}
        for name in ("X_speed", "Y_speed"):
            s = self.signals[name]
            self.derivers[name] = deriver_factory(
                s["from"], name, window=s.get("window", 3),
                lowpass_hz=s.get("lowpass_hz", 12),
                deadband=s.get("deadband", 0.1),
                max_speed=s.get("max_speed", 500),
                unit_scale=s.get("unit_scale", 1))
        self.last_seq = -1
        self.times = {}
        self.motors = None
        self.latest = None

    def accept(self, frame):
        seq = frame["seq"]
        if type(seq) is not int or seq < 0:
            raise ValueError("seq must be a nonnegative integer")
        if seq <= self.last_seq:
            return None
        keys = ("t", "x_counts", "y_counts", "x_time_s", "y_time_s")
        if any(type(frame[k]) not in (int, float) or not math.isfinite(frame[k]) for k in keys):
            raise ValueError("non-finite feedback")
        if abs(time.time() - frame["t"]) > 1:
            raise ValueError("expired feedback")
        motors = (frame["x_motor"], frame["y_motor"])
        if any(type(m) is not int or not 1 <= m <= 8 for m in motors) or motors[0] == motors[1]:
            raise ValueError("invalid motor mapping")
        values = {"seq": seq, "t": frame["t"]}
        # Validate the whole pair before changing either axis's derivative history.
        for axis in ("X", "Y"):
            pos = axis + "_Pos"
            sig = self.signals[pos]
            values[pos] = frame[axis.lower() + "_counts"] * sig.get("scale", 1) + sig.get("offset", 0)
            if not math.isfinite(values[pos]):
                raise ValueError("non-finite engineering position")
            previous = self.times.get(axis) if motors == self.motors else None
            if previous is not None and frame[axis.lower() + "_time_s"] < previous:
                raise ValueError("axis timestamp moved backwards")
        if motors != self.motors:
            self.reset()
            self.motors = motors
        for axis in ("X", "Y"):
            pos, speed = axis + "_Pos", axis + "_speed"
            stamp = frame[axis.lower() + "_time_s"]
            previous = self.times.get(axis)
            deriver = self.derivers[speed]
            if previous is None or stamp > previous:
                if previous is not None and stamp - previous > 0.5:
                    deriver.reset()
                values[speed] = deriver.update(values, stamp)
                self.times[axis] = stamp
            elif stamp == previous:
                values[speed] = self.latest[speed] if self.latest else 0.0
        # Focused feedback can update only one axis. Stop the stale axis separately.
        newest = max(self.times.values())
        for axis in ("X", "Y"):
            if newest - self.times[axis] > 0.5:
                values[axis + "_speed"] = 0.0
                self.derivers[axis + "_speed"].reset()
        self.last_seq = seq
        self.latest = values
        return values

    def reset(self):
        for deriver in self.derivers.values():
            deriver.reset()
        self.times.clear()

    def stopped(self):
        self.reset()
        if self.latest is None:
            return None
        self.latest = dict(self.latest, X_speed=0.0, Y_speed=0.0)
        return self.latest


def run(config_path, sink_override, log, deriver_factory):
    sink = None
    frames = None
    try:
        cfg = json.loads(config_path.read_text(encoding="utf-8"))
        if sink_override:
            cfg["sink"]["type"] = sink_override
        frames = HostFrames(cfg, deriver_factory)
        sink = sinks.build_sink(dict(cfg["sink"]), [s["name"] for s in cfg["signals"]], log)
        inbox = queue.Queue(maxsize=1)

        def receive():
            for line in sys.stdin:
                # Keep only the newest frame if the downstream server falls behind.
                while True:
                    try:
                        inbox.put_nowait(line)
                        break
                    except queue.Full:
                        try:
                            inbox.get_nowait()
                        except queue.Empty:
                            pass
            inbox.put(None)

        threading.Thread(target=receive, daemon=True, name="host-input").start()
        print("NXLINK_READY", flush=True)
        log.info("上位机输入模式已启动；不连接 PMAC；OPC UA/输出配置沿用 config.json")
        last_received = last_stats = time.monotonic()
        stale = False
        count = 0
        while True:
            try:
                line = inbox.get(timeout=0.1)
            except queue.Empty:
                if not stale and time.monotonic() - last_received >= 0.5:
                    stopped = frames.stopped()
                    if stopped is not None:
                        sink.send(stopped)
                    log.warning("上位机反馈中断，NX 速度已归零，等待新反馈")
                    stale = True
                continue
            if line is None:
                break
            try:
                values = frames.accept(json.loads(line))
            except (ValueError, KeyError, TypeError) as exc:
                log.warning("忽略无效上位机帧: %s", exc)
                continue
            if values is None:
                continue
            sink.send(values)
            last_received = time.monotonic()
            stale = False
            count += 1
            if last_received - last_stats >= 1:
                log.info("上位机转发 %.1f Hz | seq=%s | X_Pos=%.4f Y_Pos=%.4f X_speed=%.4f Y_speed=%.4f",
                         count / (last_received - last_stats), values["seq"],
                         values["X_Pos"], values["Y_Pos"], values["X_speed"], values["Y_speed"])
                count = 0
                last_stats = last_received
        return 0
    except Exception:
        log.exception("上位机数据桥启动或运行失败；OPC UA 模式需安装 asyncua")
        return 3
    finally:
        if sink is not None:
            if frames is not None:
                stopped = frames.stopped()
                if stopped is not None:
                    sink.send(stopped)
                    time.sleep(0.05)
            sink.close()
