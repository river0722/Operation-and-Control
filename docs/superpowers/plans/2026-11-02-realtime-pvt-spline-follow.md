# Realtime PVT/Spline Follow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make mouse drawing drive the machine while drawing, with selectable realtime PVT and Spline1 modes, bounded latency, and safe interruption.

**Architecture:** Reuse the existing MotionCanvas input and ControllerSession streaming boundary. Add a follow interpolation mode and a small realtime Spline1 window builder; keep PVT as the exact low-latency stream. The sender remains serialized through ControllerSession so PMAC commands cannot interleave, while the follower owns buffering, timing, validation, and shutdown.

**Tech Stack:** C# WinForms, .NET Framework 3.5, PMAC PVT/SPLINE1 command stream, existing simulator and NUnit-style executable checks.

---

### Task 1: Establish the baseline

**Files:** `scripts/build.ps1`, `tests/`

- [ ] Run the existing Release simulation checks and record the result before edits.
- [ ] Inspect existing follow and planner assertions so new tests use the project’s current executable test style.

### Task 2: Add explicit realtime follow mode and low-latency settings

**Files:** `src/Operations Control/Models/PathModels.cs`, `src/Operations Control/Application/CanvasFollower.cs`, `src/Operations Control/Form1.cs`

- [ ] Add a follow interpolation mode that defaults to PVT and is independent of offline plan generation.
- [ ] Replace the fixed 80 ms follower delay with a bounded realtime setting (default 20 ms), preserving validation and allowing a small queue lookahead.
- [ ] Pass the selected mode into the follower and display the active mode in the status message.

### Task 3: Implement realtime Spline1 windowing

**Files:** `src/Operations Control/Application/RealtimeSplineBuilder.cs` (create), `src/Operations Control/Communication/IStreamingTrajectorySession.cs`, `src/Operations Control/Communication/ControllerSession.cs`, `src/Operations Control/Communication/SimulatorTransport.cs`

- [ ] Define a small window builder that waits for enough points to form a cubic segment, carries overlapping endpoint/control points between windows, and flushes duplicated endpoints at stroke end.
- [ ] Add a Spline1 append operation to the streaming session boundary without changing offline `PmacProgramWriter` behavior.
- [ ] Validate whether the transport supports online append; expose a clear capability error when it does not rather than silently pretending it is realtime.
- [ ] Simulate the same window queue and endpoint continuity in `SimulatorTransport`.

### Task 4: Connect mouse input to both stream modes

**Files:** `src/Operations Control/Application/CanvasFollower.cs`, `src/Operations Control/Form1.cs`, `src/Operations Control/Views/PathView.cs` and designer if needed

- [ ] Route each sampled mouse point into the selected PVT or Spline1 stream without waiting for the whole drawing.
- [ ] Keep the current start-anchor safety rule, add queue underflow/overflow checks, and stop on stale feedback, limit violations, communication failure, or excessive segment duration.
- [ ] End the active stream on mouse release and abort it on focus loss, Escape, or fault.

### Task 5: Add focused verification and run the full checks

**Files:** `tests/ModuleTests.cs`, `tests/WorkflowTests.cs`, `tests/MotorDebugTests.cs` as needed

- [ ] Add tests for low-latency PVT append timing, Spline1 overlap/flush continuity, mode selection, and safe abort.
- [ ] Run targeted checks, then the full Release simulation suite and a Debug build.
- [ ] Update README/help text only where the actual behavior differs from the old 80 ms description.

Success is demonstrated by passing existing checks plus the new mode, continuity, and abort assertions, with no change to offline PVT/Spline1 program generation.
