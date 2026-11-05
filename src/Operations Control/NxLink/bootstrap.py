"""Repair a copied venv using this computer's Python before running the bridge."""
from pathlib import Path
import runpy
import subprocess
import sys
import venv

HERE = Path(__file__).resolve().parent


def usable(executable, dependencies=False):
    code = "import sys; assert sys.version_info[:2] == (3,13)"
    if dependencies:
        code += "; import asyncua"
    try:
        return subprocess.run([str(executable), "-I", "-c", code],
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              timeout=10, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)).returncode == 0
    except (OSError, subprocess.TimeoutExpired):
        return False


def prepare():
    if sys.version_info[:2] != (3, 13):
        raise RuntimeError("Python 3.13 is required")
    # A working selected interpreter with dependencies needs no writes or downloads.
    if usable(sys.executable, dependencies=True):
        return Path(sys.executable)
    target = HERE / ".venv"
    python = target / "Scripts" / "python.exe"
    if not usable(python):
        print("NX：正在使用本机 Python 3.13 修复桥接虚拟环境…", flush=True)
        # Upgrade refreshes pyvenv.cfg and executables; never delete the copied folder.
        venv.EnvBuilder(with_pip=True, upgrade=target.exists()).create(target)
    if not usable(python, dependencies=True):
        bits = subprocess.check_output([str(python), "-I", "-c", "import struct; print(struct.calcsize('P')*8)"],
                                       text=True, timeout=10,
                                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)).strip()
        wheels = HERE / "wheels" / ("win_amd64" if bits == "64" else "win32")
        offline = wheels.is_dir() and any(wheels.glob("*.whl"))
        print("NX：正在从随包依赖离线安装 asyncua…" if offline else
              "NX：正在安装 asyncua 依赖，首次安装需要网络…", flush=True)
        install = [str(python), "-m", "pip", "install", "--disable-pip-version-check", "--no-input"]
        install += ["--no-index", "--find-links", str(wheels)] if offline else ["--retries", "1", "--timeout", "15"]
        subprocess.run(install + ["-r", str(HERE / "requirements.txt")],
                       check=True, timeout=150,
                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if not usable(python, dependencies=True):
        raise RuntimeError("桥接环境校验失败，请运行 NxLink/setup.bat 查看安装日志")
    return python


def main():
    try:
        python = prepare()
        print(f"NX：桥接解释器 {python}", flush=True)
        if "--setup-only" in sys.argv:
            return 0
        if Path(sys.executable).resolve() == python.resolve():
            sys.argv = [str(HERE / "nxlink.py"), "--host-input"]
            runpy.run_path(str(HERE / "nxlink.py"), run_name="__main__")
            return 0
        # Keep stdin intact: it carries live feedback from the C# host.
        # exec on Windows does not preserve the Process handle, so wait in this parent.
        child = subprocess.Popen([str(python), "-u", str(HERE / "nxlink.py"), "--host-input"],
                                 stdin=sys.stdin, stdout=sys.stdout, stderr=sys.stderr,
                                 creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        return child.wait()
    except Exception as exc:
        print(f"NX 环境准备失败：{exc}", file=sys.stderr, flush=True)
        return 3


if __name__ == "__main__":
    raise SystemExit(main())
