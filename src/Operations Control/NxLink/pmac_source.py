"""
PMAC 数据源 —— 通过 Delta Tau PcommServer (COM) 读取真实样机的实时数据。

接口依据《PcommServer.pdf》手册（本机：桌面/YXYKZ/pmac资料/调试文档/PMAC实用手册）：

    Open(int dwDevice, out bool pbSuccess)
    Close(int dwDevice)
    GetResponseEx(int dwDevice, string question, bool bAddLF, out string pAnswer, out int pStatus)
    SelectDevice(int hWindow, out int pDeviceNumber, out bool pbSuccess)

要点：
  * PcommServer.exe 是进程外 COM 服务器(ActiveX EXE)，64 位 Python 可直接调用，
    不需要 PComm32W.dll —— 该 DLL 只有 32 位版本，64 位 Python 用 ctypes 加载不了。
  * 手册明确 PcommServer 是线程安全的，LockPmac/ReleasePmac 已不再需要。
"""

from __future__ import annotations

import os
import re
import site
import sys
import time
from typing import Optional

LOG = None  # 由 setup_logging 注入


def setup_logging(logger):
    global LOG
    LOG = logger


# --------------------------------------------------------------------------
# pywin32 的 DLL 搜索路径补丁
# pythoncom3xx.dll 位于 site-packages/pywin32_system32，未做 post-install 时
# import pythoncom 会以 AttributeError 失败。必须在 import 之前补上搜索路径。
# --------------------------------------------------------------------------
def _fix_pywin32_dll_path() -> None:
    if sys.platform != "win32":
        return
    try:
        if hasattr(os, "add_dll_directory"):
            for sp in site.getsitepackages():
                cand = os.path.join(sp, "pywin32_system32")
                if os.path.isdir(cand):
                    os.add_dll_directory(cand)
    except Exception:
        pass


_fix_pywin32_dll_path()

import pythoncom  # noqa: E402
import win32com.client  # noqa: E402
import pywintypes  # noqa: E402


# --------------------------------------------------------------------------
# PcommServer 状态码（手册 "Error Handling - ASCII Communication"）
#   COMM_EOT        0x80000000  收到 ACK，正常结束
#   COMM_TIMEOUT    0xC0000000  超时
#   COMM_BADCKSUM   0xD0000000  校验错误
#   COMM_ERROR      0xE0000000  无法通信
#   COMM_FAIL       0xF0000000  严重失败
#   COMM_UNSOLICITED 0x10000000 非请求响应（PLC/SEND 语句造成）
# --------------------------------------------------------------------------
_STATUS_OK_HIGH = (0x00000000, 0x80000000, 0x10000000)

_NUMBER_RE = re.compile(r"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?")


class PmacError(RuntimeError):
    pass


# --------------------------------------------------------------------------
# DEVPMACTYPE / DEVLOCATIONTYPE 枚举（PcommServer.pdf 手册 Page 44~45）
#   Clipper = Turbo PMAC2-Eth-Lite，应返回 DEV_PT_PMAC2T(7) + DEV_LT_ETH(3)
# --------------------------------------------------------------------------
DEVPMACTYPE = {
    1: "PMAC1 (Non-Turbo)", 2: "PMAC2 (Non-Turbo)", 3: "PMAC Ultralight (Non-Turbo)",
    4: "Geo PMAC (Non-Turbo)", 5: "PMAC (Non-Turbo/Turbo 分界)",
    6: "PMAC1 Turbo", 7: "PMAC2 Turbo", 8: "PMAC Turbo Ultralite",
    9: "UMAC Turbo", 10: "QMAC Turbo", 11: "PMAC1 Turbo Small Memory",
    12: "PMAC2 Turbo Small Memory",
}

DEVLOCATIONTYPE = {
    0: "未知", 1: "ISA 总线", 2: "串口", 3: "以太网", 4: "PCI 总线", 5: "USB", 6: "其他",
}

# 出厂默认 IP（Delta Tau 官方《PMAC 32-Bit Communication Driver User Manual》）
DEFAULT_PMAC_IP = "192.6.94.5"


def parse_number(text: str) -> float:
    """从 PMAC 的 ASCII 响应中抠出数值。

    PMAC 的响应常带 \r、ACK、多余前缀，取第一个合法数字即可。
    十六进制（如 $A00000）不做处理，调用方应避免用这类命令读位置。
    """
    if text is None:
        raise PmacError("空响应")
    cleaned = text.replace(",", "").strip()
    m = _NUMBER_RE.search(cleaned)
    if not m:
        raise PmacError(f"响应中找不到数值: {text!r}")
    return float(m.group())


def status_ok(status: int) -> bool:
    return (status & 0xF0000000) in _STATUS_OK_HIGH


class PmacSource:
    """PcommServer COM 封装。"""

    def __init__(self, prog_id: str = "PcommServer.PmacDevice.1",
                 device_number: int = 0,
                 auto_select_device: bool = False):
        self.prog_id = prog_id
        self.device_number = device_number
        self.auto_select_device = auto_select_device
        self._obj = None
        self._opened = False
        self._coinitialized = False

    # ---------------- 生命周期 ----------------
    def connect(self) -> None:
        pythoncom.CoInitialize()
        self._coinitialized = True

        try:
            self._obj = win32com.client.Dispatch(self.prog_id)
        except pywintypes.com_error as exc:
            raise PmacError(
                f"无法创建 COM 对象 {self.prog_id}（{exc}）。\n"
                f"  常见原因：\n"
                f"   1) PcommServer 未注册 —— 以管理员运行: "
                f"\"C:\\Windows\\System32\\PcommServer.exe\" /regserver\n"
                f"   2) 运行在无桌面会话的服务/沙箱中，进程外 COM 服务器无法启动\n"
                f"   3) 权限不足 —— 请用普通桌面用户或管理员身份启动本程序"
            ) from exc

        if self.auto_select_device:
            # 弹出 PEWin 的设备选择对话框
            res = self._obj.SelectDevice(0)
            dev, ok = self._unpack2(res)
            if not ok:
                raise PmacError("SelectDevice 被取消或失败")
            self.device_number = int(dev)
            if LOG:
                LOG.info("SelectDevice 选定设备号 = %d", self.device_number)

        res = self._obj.Open(self.device_number)
        ok = self._unpack1(res)
        if not ok:
            raise PmacError(
                f"Open(device={self.device_number}) 返回 False。\n"
                f"  COM 对象已创建成功，说明位数/注册没问题，卡的是【打开设备】这一层。\n"
                f"  本机已确认：Device{self.device_number} 注册表 "
                f"HKLM\\System\\CurrentControlSet\\Services\\Pmac\\Device{self.device_number} 存在。\n"
                f"  按顺序排查：\n"
                f"   1) PEWin32 是不是还开着？关掉再试（以太网链路可能被独占）\n"
                f"   2) ping 192.6.94.5 是否通、TCP 1025 端口是否可连\n"
                f"   3) PMAC 是否上电、网线灯是否亮\n"
                f"   4) 换设备号试：python probe.py --scan --apply\n"
                f"   5) 用 PEWin 终端重新确认 #1P 还能返回数字"
            )
        self._opened = True
        if LOG:
            LOG.info("PMAC 设备 %d 已打开", self.device_number)

    def close(self) -> None:
        try:
            if self._opened and self._obj is not None:
                self._obj.Close(self.device_number)
        except Exception:
            pass
        finally:
            self._opened = False
            self._obj = None
            if self._coinitialized:
                try:
                    pythoncom.CoUninitialize()
                except Exception:
                    pass
                self._coinitialized = False

    # ---------------- 读取 ----------------
    @staticmethod
    def _unpack1(res):
        if isinstance(res, (tuple, list)):
            return bool(res[0])
        return bool(res)

    @staticmethod
    def _unpack2(res):
        if isinstance(res, (tuple, list)) and len(res) >= 2:
            return res[0], bool(res[1])
        return res, True

    # ---------------- 控制器身份识别 ----------------
    def device_info(self) -> dict:
        """读取控制器身份：型号 / 固件 / 是否 Turbo / 通讯方式。

        用于确认"程序确实连上了 Clipper"，而不是连到了别的 PMAC 或空设备号。
        任何一项取不到都不抛异常，返回 None，由调用方决定是否告警。
        """
        info = {"device_number": self.device_number}
        o = self._obj

        for key, method in (("pmac_type", "GetPmacType"),
                            ("location", "GetPmacLocation")):
            try:
                res = getattr(o, method)(self.device_number)
                info[key] = int(self._unpack1_raw(res))
            except Exception:
                info[key] = None

        info["pmac_type_name"] = DEVPMACTYPE.get(info.get("pmac_type"), "未知型号")
        info["location_name"] = DEVLOCATIONTYPE.get(info.get("location"), "未知")

        for key, method in (("rom_version", "GetRomVersion"),
                            ("rom_date", "GetRomDate"),
                            ("firmware_type", "GetFirmwareType")):
            try:
                res = getattr(o, method)(self.device_number)
                v = self._unpack1_raw(res)
                info[key] = "" if v is None else str(v).strip()
            except Exception:
                info[key] = None

        try:
            info["turbo"] = bool(o.IsTurbo)
        except Exception:
            try:
                info["turbo"] = bool(o.IsTurbo(self.device_number))
            except Exception:
                info["turbo"] = None

        info["is_turbo_pmac2"] = (info.get("turbo") is True) or \
                                 (info.get("pmac_type") in (7, 8, 9, 10, 11, 12))
        return info

    @staticmethod
    def _unpack1_raw(res):
        if isinstance(res, (tuple, list)):
            return res[0]
        return res

    def read_response(self, command: str, add_lf: bool = True) -> str:
        """发送一条在线命令，返回 PMAC 的 ASCII 响应字符串。"""
        if not self._opened:
            raise PmacError("未连接")
        res = self._obj.GetResponseEx(self.device_number, command, add_lf)
        answer, status = self._unpack2(res)
        answer = "" if answer is None else str(answer)
        if not status_ok(int(status)):
            raise PmacError(
                f"命令 {command!r} 通信异常，status=0x{int(status) & 0xFFFFFFFF:08X}，响应={answer!r}"
            )
        return answer.strip()

    def read_batch(self, commands: list[str]) -> list[str]:
        """把多条命令一次性发出（PMAC 用回车分隔），返回按行拆开的响应。

        注意：某些固件/通信方式下批量响应行数可能与命令数不一致，
        因此做长度对齐处理；若不一致，调用方会退化为逐条读取。
        """
        joined = "\r".join(commands)
        raw = self.read_response(joined, add_lf=True)
        lines = [ln.strip() for ln in re.split(r"[\r\n]+", raw) if ln.strip()]
        if len(lines) < len(commands):
            lines += [""] * (len(commands) - len(lines))
        return lines[: len(commands)]

    def read_value(self, command: str) -> float:
        return parse_number(self.read_response(command))

    # 上下文管理
    def __enter__(self):
        self.connect()
        return self

    def __exit__(self, *exc):
        self.close()
        return False


# --------------------------------------------------------------------------
# 模拟数据源：无硬件时用于打通 NX 侧链路
# --------------------------------------------------------------------------
class SimulatedSource:
    def __init__(self, signals: list[dict]):
        self.device_number = -1
        self.signals = signals
        self._t0 = time.perf_counter()

    def connect(self):
        if LOG:
            LOG.warning("使用【模拟数据源】—— 未连接真实 PMAC")

    def close(self):
        pass

    def read_values(self) -> dict:
        import math
        t = time.perf_counter() - self._t0
        out = {}
        for i, sig in enumerate(self.signals):
            amp = 50.0 * (i + 1)
            out[sig["name"]] = amp * math.sin(2 * math.pi * 0.2 * t + i)
        return out
