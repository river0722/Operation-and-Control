#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
check_nodes.py —— 纯客户端节点检查器（不起服务器，不影响正在运行的桥接）

用途：在 NX 里配外部信号之前，先确认 OPC UA 服务器上到底有哪些节点、
       类型对不对、值是不是在变。它只连不建，可以和 4_real / 3_simulate
       同时运行（check_opcua.py 会占用端口，不能在桥接运行时用，这个可以）。

用法：
    python check_nodes.py                        # 默认读 config.json 里的端点
    python check_nodes.py --endpoint opc.tcp://127.0.0.1:4843/pmac/
    python check_nodes.py --watch 5              # 连续采样 5 秒，看数值是否在变
"""

from __future__ import annotations

import argparse
import asyncio
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent

try:
    from asyncua import Client, ua  # noqa: F401
except ImportError:
    print("[ERROR] asyncua not installed. Run this with py32\\python.exe")
    sys.exit(2)


def load_cfg():
    p = HERE / "config.json"
    if not p.exists():
        return {}
    with p.open("r", encoding="utf-8") as f:
        return json.load(f)


async def walk(node, depth=0, max_depth=6, out=None):
    """递归列出节点树（限深度，避免刷屏）"""
    if out is None:
        out = []
    if depth > max_depth:
        return out
    try:
        children = await node.get_children()
    except Exception:
        return out
    for ch in children:
        try:
            name = await ch.read_browse_name()
            label = f"{name.Name}"
        except Exception:
            label = "<?>"
        try:
            nc = await ch.read_node_class()
            cls = nc.name if hasattr(nc, "name") else str(nc)
        except Exception:
            cls = "?"
        dtype = ""
        val = None
        if cls == "Variable":
            try:
                dv = await ch.read_data_value()
                v = dv.Value
                if v is not None:
                    tn = v.VariantType.name if hasattr(v, "VariantType") else ""
                    dtype = tn
                    val = v.Value
                else:
                    dtype = "null"
            except Exception as exc:
                dtype = f"err:{type(exc).__name__}"
        print("   " + "  " * depth + f"- {label:<14} [{cls}] {dtype}"
              + ("" if val is None else f"  = {val}"))
        out.append((ch, label, cls, dtype))
        await walk(ch, depth + 1, max_depth, out)
    return out


async def main_async(args):
    cfg = load_cfg()
    opc = (cfg.get("sink") or {}).get("opcua") or {}
    endpoint = args.endpoint or opc.get("endpoint") or "opc.tcp://127.0.0.1:4843/pmac/"
    namespace = opc.get("namespace", "PMAC")
    objname = opc.get("object", "XYPlatform")
    expected = [s["name"] for s in cfg.get("signals", [])] or \
               ["X_Pos", "Y_Pos", "X_speed", "Y_speed"]

    print("=" * 62)
    print(f"  endpoint : {endpoint}")
    print(f"  namespace: {namespace}   object: {objname}")
    print(f"  expected : {', '.join(expected)}")
    print("=" * 62)

    client = Client(endpoint, timeout=5)
    # 服务器没有配证书，客户端也不要用安全策略
    client.application_uri = "urn:PMAC:checker"
    try:
        await client.connect()
    except Exception as exc:
        print(f"\n[FAIL] 连不上服务器：{type(exc).__name__}: {exc}")
        print("  -> 桥接程序没在跑？先双击 4_real.bat 或 3_simulate.bat 并保持窗口开着。")
        print("  -> URL 里 /pmac/ 不能少。")
        return 2

    try:
        print("\n[OK] 已连接\n")
        print(f"--- 节点树（只看 {objname}，忽略服务器自带诊断节点）---")
        objects = client.nodes.objects
        target = None
        for ch in await objects.get_children():
            bn = await ch.read_browse_name()
            if bn.Name == objname:
                target = ch
                break
        if target is None:
            print(f"  [!] Objects 下找不到 {objname}，退回全树浏览（可能内容很多）")
            nodes = await walk(objects)
        else:
            print(f"  Objects/{objname}")
            nodes = await walk(target)

        # 汇总可变节点
        variables = [n for (n, _, cls, _) in nodes if cls == "Variable"]
        print("\n--- 可读变量 ---")
        found = {}
        for node in variables:
            try:
                bn = await node.read_browse_name()
                dv = await node.read_data_value()
                v = dv.Value
                val = v.Value if v is not None else None
                tn = v.VariantType.name if (v is not None and hasattr(v, "VariantType")) else "?"
                found[bn.Name] = (val, tn, node)
                print(f"  {bn.Name:<12} {tn:<8} = {val}")
            except Exception as exc:
                print(f"  ? 读取失败 {type(exc).__name__}: {exc}")

        print("\n--- 期望节点核对 ---")
        miss = []
        for name in expected:
            if name in found:
                val, tn, _ = found[name]
                flag = "OK  " if tn in ("Double", "Float") else "WARN"
                print(f"  [{flag}] {name:<10} 类型={tn:<8} 值={val}")
            else:
                miss.append(name)
                print(f"  [MISS] {name:<10} 服务器上不存在")
        if miss:
            print(f"\n  缺少 {len(miss)} 个：{', '.join(miss)}")
            print("  -> 检查 config.json 的 signals 是否包含它们，改完要重启桥接。")

        if args.watch:
            print(f"\n--- 连续采样 {args.watch}s（看数值是否在变）---")
            t0 = time.time()
            prev = {}
            while time.time() - t0 < args.watch:
                line = []
                for name in expected:
                    if name not in found:
                        continue
                    try:
                        node = found[name][2]
                        dv = await node.read_data_value()
                        v = dv.Value
                        val = v.Value if v is not None else None
                        line.append(f"{name}={val:+.4f}" if isinstance(val, float) else f"{name}={val}")
                    except Exception:
                        line.append(f"{name}=ERR")
                print("  " + "  ".join(line))
                await asyncio.sleep(0.5)

        print("\n[OK] 检查完成")
        return 0 if not miss else 1
    finally:
        try:
            await client.disconnect()
        except Exception:
            pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--endpoint", default=None)
    ap.add_argument("--watch", type=float, default=0.0,
                    help="连续采样秒数，例如 5")
    args = ap.parse_args()
    try:
        rc = asyncio.run(main_async(args))
    except KeyboardInterrupt:
        rc = 130
    sys.exit(rc)


if __name__ == "__main__":
    main()
