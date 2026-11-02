# Motor debugging and motion control implementation plan

> **For agentic workers:** Execute this plan task by task in the current session. The user has authorized implementation; no additional execution approval is required.

**Goal:** Complete the motor debugging workflow with guarded manual motion, confirmed completion and stopping, live axis diagnostics, recoverable alarms, saved configuration and persistent logs.

**Architecture:** Keep the current WinForms/.NET 3.5 application and serialized STA communication. Extend axis feedback and existing application services; add focused configuration, alarm and diagnostics modules. Hardware I/O and reset commands are explicitly configured, never inferred from an unknown machine.

**Tech Stack:** C# 3.0, .NET Framework 3.5 x86, WinForms, PcommServer, XML configuration, existing simulator and executable test runners.

**Backup:** a5059d76c160f15486abb7b99a57f6fb202fe376; branch codex/backup-motor-debug-20261014. Implementation branch codex/motor-debug-hardening.

## Tasks and acceptance

- [x] Commit all current application code and modular refactoring before editing; create a named backup branch.
- [x] Extend `Models/ControllerModels.cs`, `Models/DeviceStatus.cs`, `Communication/ControllerSession.cs` and `SimulatorTransport.cs` to return status and following error for X, Y and the selected motor. Verify PMAC units and status definitions against the SRM. Test a third axis fault independently of X/Y.
- [x] Add `Application/MotionGuard.cs` and extend `ManualMotionService.cs` / `OperationState.cs`. Check freshness, enable, fault, direction-dependent limits and configured travel before dispatch. Track single-step and absolute moves until confirmed arrival or timeout. Hold ownership after stop acknowledgement until fresh feedback proves stopping; test queued cancellation and delayed stopping.
- [x] Extend `FeedbackService.cs` and `MotionWorkflow.cs` with independent feedback age checks and stop confirmation. A stalled communication operation must mark data unknown and lock motion; a queued stop is not a physical emergency stop. Test stale data without an exception callback.
- [x] Add `Models/DeviceConfiguration.cs`, `Files/DeviceConfigurationStore.cs` and `Views/DiagnosticsView.cs`. Save profiles separately for simulation and real devices; require explicit application of loaded calibration. Read back written speed, configure per-axis travel, reset command and optional I/O queries. Reset and enable operations wait for feedback confirmation.
- [x] Add `Application/AlarmService.cs`; keep current alarms distinct from history and from acknowledgement. Extend `LogView.cs` to append UTF-8 daily logs and retain a bounded visible tail without erasing disk history. Test alarm transitions and log rollover beyond 500 entries.
- [x] Update `Form1.cs`, `MotionView.cs`, `ReadoutView.cs` and designer layouts. Display live statuses, reasons for unavailable controls, actual/command position, following error and diagnostics trend. Preserve existing drawing/program workflow and minimum window usability.
- [x] Add `tests/MotorDebugTests.cs` to `../../../scripts/build.ps1`; update existing fake sessions and stop assertions to the new confirmation semantics. Run Release suites, Debug build, inspect generated screenshots and update operation documentation.

## Validation commands

```powershell
.\scripts\build.ps1 -Configuration Release -Test -ScreenshotDirectory '..\artifacts\output\motor-debug-hardening'
.\scripts\build.ps1 -Configuration Debug
git diff --check
```

Real device acceptance additionally checks axis direction, count/mm calibration, travel, following-error units, configured reset/PLC behavior, emergency-stop wiring and controller-side communication loss response. Simulator success does not establish physical safety behavior.

**Results (2026-10-14):** Release suites pass: 138 smoke assertions, 2273 workflow assertions, 32 module assertions, 69 motor debugging assertions. Debug compilation passes. Final review includes manual controls, diagnostics trends, alarm recovery, configuration and minimum window layout.
