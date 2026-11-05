import json
import importlib.util
import os
from pathlib import Path
import queue
import socket
import subprocess
import sys
import tempfile
import threading
import time
import unittest

PACKAGE = Path(__file__).resolve().parents[1] / "src" / "Operations Control" / "NxLink"
sys.path.insert(0, str(PACKAGE))
import nxlink
import bootstrap
from host_input import HostFrames


def frame(seq, x, y, tx=None, ty=None):
    stamp = 10 + seq * 0.05
    return dict(seq=seq, t=time.time(), x_counts=x, y_counts=y,
                x_time_s=stamp if tx is None else tx,
                y_time_s=stamp if ty is None else ty, x_motor=1, y_motor=2)


class FrameTests(unittest.TestCase):
    def setUp(self):
        self.cfg = json.loads((PACKAGE / "config.json").read_text(encoding="utf-8"))
        self.frames = HostFrames(self.cfg, nxlink.VelocityDeriver)

    def test_host_does_not_import_com_source(self):
        self.assertNotIn("pmac_source", sys.modules)

    def test_broken_copied_venv_is_repaired(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / ".venv"
            scripts = target / "Scripts"
            scripts.mkdir(parents=True)
            import venv
            venv.EnvBuilder(with_pip=False).create(target)
            (target / "pyvenv.cfg").write_text("home = Z:\\missing-python\nexecutable = Z:\\missing-python\\python.exe\n")
            python = scripts / "python.exe"
            self.assertFalse(bootstrap.usable(python))
            venv.EnvBuilder(with_pip=False, upgrade=True).create(target)
            self.assertTrue(bootstrap.usable(python))

    def test_units_and_velocity(self):
        first = self.frames.accept(frame(0, 1638.4, -3276.8))
        self.assertAlmostEqual(first["X_Pos"], 1)
        self.assertAlmostEqual(first["Y_Pos"], -2)
        self.assertEqual(first["X_speed"], 0)
        for i in range(1, 12):
            moving = self.frames.accept(frame(i, 1638.4 * (1 + i * 0.05), -3276.8))
        self.assertAlmostEqual(moving["X_speed"], 36, places=4)
        self.assertEqual(moving["Y_speed"], 0)

    def test_focus_does_not_make_duplicate_axis_samples(self):
        self.frames.accept(frame(0, 0, 0))
        self.frames.accept(frame(1, 163.84, 0, ty=10))
        self.assertEqual(len(self.frames.derivers["Y_speed"]._buf), 1)
        self.assertIsNone(self.frames.accept(frame(1, 999, 999)))

    def test_stale_axis_jump_and_resume_start_at_zero(self):
        self.frames.accept(frame(0, 0, 0))
        value = self.frames.accept(frame(1, 16384000, 0))
        self.assertEqual(value["X_speed"], 0)
        self.frames.stopped()
        resumed = self.frames.accept(frame(2, 32768000, 0))
        self.assertEqual(resumed["X_speed"], 0)
        remapped = frame(3, 0, 0)
        remapped["x_motor"] = 3
        self.assertEqual(self.frames.accept(remapped)["X_speed"], 0)

    def test_invalid_frames(self):
        for key, bad in (("x_counts", float("nan")), ("y_counts", float("inf")),
                         ("seq", -1), ("seq", 1.5), ("x_motor", 2),
                         ("t", time.time() - 5)):
            sample = frame(0, 0, 0)
            sample[key] = bad
            with self.assertRaises(ValueError):
                self.frames.accept(sample)

    def test_rejected_pair_does_not_change_axis_history(self):
        self.frames.accept(frame(0, 0, 0))
        with self.assertRaises(ValueError):
            self.frames.accept(frame(1, 100, 100, ty=9))
        self.assertEqual(len(self.frames.derivers["X_speed"]._buf), 1)
        self.assertEqual(self.frames.last_seq, 0)


class PipeTests(unittest.TestCase):
    @unittest.skipUnless(importlib.util.find_spec("asyncua"), "OPC UA integration requires asyncua")
    def test_csharp_bridge_to_opcua_and_restart(self):
        import asyncio
        from asyncua import Client
        executable = PACKAGE.parent / "bin" / "Release" / "OperationsControl.NxLinkBridgeTests.exe"
        self.assertTrue(executable.exists(), "Compile tests/NxLinkBridgeTests.cs into Release first")
        with tempfile.TemporaryFile(mode="w+", encoding="utf-8") as errors:
            env = dict(os.environ, PMAC_NXLINK_PYTHON=sys.executable)
            child = subprocess.Popen([str(executable)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                     stderr=errors, text=True, encoding="utf-8", env=env,
                                     creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            output = queue.Queue()

            def read_output():
                for line in child.stdout:
                    output.put(line.strip())
            reader = threading.Thread(target=read_output, daemon=True)
            reader.start()

            async def verify_nodes():
                async with Client("opc.tcp://127.0.0.1:4843/pmac/") as client:
                    idx = await client.get_namespace_index("PMAC")
                    obj = await client.nodes.objects.get_child([f"{idx}:XYPlatform"])
                    nodes = {name: await obj.get_child([f"{idx}:{name}"])
                             for name in ("X_Pos", "Y_Pos", "X_speed", "Y_speed")}
                    for _ in range(30):
                        if abs(await nodes["X_Pos"].read_value() - 1) < 1e-8:
                            break
                        await asyncio.sleep(0.05)
                    self.assertAlmostEqual(await nodes["X_Pos"].read_value(), 1)
                    self.assertAlmostEqual(await nodes["Y_Pos"].read_value(), -2)
                    await asyncio.sleep(0.6)
                    self.assertEqual(await nodes["X_speed"].read_value(), 0)
                    self.assertEqual(await nodes["Y_speed"].read_value(), 0)

            try:
                self.assertEqual(output.get(timeout=25), "BRIDGE_TEST_FRAME")
                asyncio.run(verify_nodes())
                child.stdin.write("stop\n")
                child.stdin.flush()
                child.stdin.close()
                result = child.wait(timeout=25)
                errors.seek(0)
                self.assertEqual(result, 0, errors.read())
            finally:
                if child.poll() is None:
                    child.kill()
                    child.wait(timeout=5)
                reader.join(2)
                child.stdout.close()

    def test_pipe_to_udp_timeout_recovery_and_eof(self):
        with tempfile.TemporaryDirectory() as temp, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
            udp.bind(("127.0.0.1", 0))
            udp.settimeout(5)
            cfg = json.loads((PACKAGE / "config.json").read_text(encoding="utf-8"))
            cfg["sink"]["udp"]["port"] = udp.getsockname()[1]
            config = Path(temp) / "config.json"
            config.write_text(json.dumps(cfg), encoding="utf-8")
            child = subprocess.Popen([sys.executable, "-u", str(PACKAGE / "nxlink.py"),
                                      "--host-input", "--sink", "udp", "--config", str(config)],
                                     stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                                     creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            output = queue.Queue()

            def read_output():
                for line in child.stdout:
                    output.put(line.strip())
            reader = threading.Thread(target=read_output, daemon=True)
            reader.start()

            def send(sample):
                child.stdin.write(json.dumps(sample) + "\n")
                child.stdin.flush()

            def receive():
                return json.loads(udp.recv(65535))

            try:
                deadline = time.monotonic() + 15
                while output.get(timeout=max(0.1, deadline - time.monotonic())) != "NXLINK_READY":
                    self.assertLess(time.monotonic(), deadline)
                send(frame(0, 1638.4, -3276.8))
                self.assertAlmostEqual(receive()["X_Pos"], 1)
                send(frame(1, 1802.24, -3276.8))
                self.assertGreater(receive()["X_speed"], 0)
                stopped = receive()  # watchdog must publish zero without another input frame
                self.assertEqual(stopped["X_speed"], 0)
                send(frame(2, 1966.08, -3276.8))
                resumed = receive()
                self.assertEqual(resumed["seq"], 2)
                self.assertEqual(resumed["X_speed"], 0)
                send(dict(frame(3, 0, 0), y_counts="bad"))
                send(frame(4, 2129.92, -3276.8))
                self.assertEqual(receive()["seq"], 4)
                child.stdin.close()
                self.assertEqual(receive()["X_speed"], 0)
                self.assertEqual(child.wait(timeout=5), 0)
            finally:
                if child.poll() is None:
                    child.kill()
                    child.wait(timeout=5)
                reader.join(2)
                child.stdout.close()


if __name__ == "__main__":
    unittest.main()
