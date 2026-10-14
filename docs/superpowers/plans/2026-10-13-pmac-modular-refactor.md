# PMAC Modular Refactor Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in the current session. The user has authorized implementation while retaining the current interface.

**Goal:** Separate UI, application workflows, communication, motion algorithms and file operations without changing the existing WinForms design or user operations.

**Architecture:** Retain one .NET Framework 3.5/x86 application. Views display data and collect inputs; application services own shared operation state and motion/homing transitions. A single feedback service supplies all views, and a private STA dispatcher serializes controller access through an interface without accepting UI controls.

**Tech Stack:** C#, WinForms, .NET Framework 3.5, x86, PcommServer COM, existing simulator.

### Task 1: Establish the baseline
- [x] Run `online/build.ps1 -Test -ScreenshotDirectory "$env:TEMP\pmac-modular-baseline"`; retain the simulation results and screenshots.
- [x] Record Designer.cs and resx content; their layouts remain untouched.

### Task 2: Establish module boundaries
- [x] Move existing implementations into Communication, Motion and Files; update namespaces and explicit csproj entries.
- [x] Extract STA scheduling into Communication/StaCommandDispatcher.cs. Expose controller operations through IControllerSession; Execute accepts actions and returns through the configured application SynchronizationContext; Form1 supplies a WindowsFormsSynchronizationContext explicitly.
- [x] Move program read/write and export into Files. Move screen projections into Controls/CanvasProjection.cs so Models has no Drawing dependency.
- [x] Add Application/MotionPlanningService.cs to compose trajectory planning with PMAC program generation.

### Task 3: Extract application workflows
- [x] Add OperationState, FeedbackService and MotionWorkflow in Application.
- [x] Move start, completion, fault, timeout, homing and stop transitions from PathView into MotionWorkflow, preserving cancellation versions and retained run records.
- [x] Supply positions and status from one FeedbackService; use the same shared state for manual operations, jogging, following and workflows.
- [x] Keep UI messages, controls, layout and dialog handling in Form1/Views. Delegate program encoding and files to ProgramFileStore.

### Task 4: Verify behavior and independent use
- [x] Run existing smoke/workflow tests after updating imports and the controller Execute call signature.
- [x] Add headless tests for workflow completion, fault stop, stale feedback, home transitions/timeouts, queued-start cancellation and failed-stop ownership.
- [x] Validate the interface designs against unchanged Designer.cs/resx files and baseline screenshots; also test designer loading.
- [x] Run the Release build and all simulation tests through build.ps1. Update README and an architecture document with final responsibilities and validation results.

## Verification result

Release build succeeded. SmokeTests: 138; WorkflowTests: 2273; ModuleTests: 32; total: 2443 passing assertions. Verified 24 unchanged Designer/resource files and module import boundaries. Hardware was not accessed.
