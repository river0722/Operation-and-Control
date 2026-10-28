using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Operations_Control;
using Operations_Control.Application;
using Operations_Control.Communication;
using Operations_Control.Files;
using Operations_Control.Models;
using Operations_Control.Motion;
using Operations_Control.Views;

internal static class MotorDebugTests
{
    private static int checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
    private static MotorStatus Status(bool stopped, bool enabled, bool fault, bool positive, bool negative)
    {
        int first = 0x800000 | (enabled ? 0x80000 : 0x40000) | (stopped ? 0x2000 : 0) | (positive ? 0x200000 : 0) | (negative ? 0x400000 : 0);
        return new MotorStatus { FirstWord = first, SecondWord = (stopped && enabled ? 1 : 0) | (fault ? 8 : 0) };
    }
    private sealed class Fake : IControllerSession
    {
        private readonly Queue<Action> queue = new Queue<Action>();
        public readonly AxisSample[] Axes = new AxisSample[3];
        public readonly List<string> Commands = new List<string>();
        public bool StopImmediately = true, FailStop;
        public bool FailDisable, IgnoreDisable, IgnoreAbort, CoordinateRunning, IgnoreParameterWrite;
        public readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public int Reads, AxisReads;
        public bool IsConnected { get; private set; }
        public string DeviceName { get { return "fake"; } }
        public Fake()
        { IsConnected = true; for (int i = 0; i < 3; i++) Axes[i] = new AxisSample(i + 1, 0, 0, DateTime.Now) { Status = Status(true, true, false, false, false), FollowingError = 0, TimeMs = MonotonicClock.NowMs }; }
        public void Set(int motor, double position, MotorStatus status)
        { Axes[motor - 1] = new AxisSample(motor, position, 0, DateTime.Now) { Status = status, FollowingError = 0, TimeMs = MonotonicClock.NowMs }; }
        public void Execute(Action action, Action<Exception> completed)
        { queue.Enqueue(delegate { Exception error = null; try { action(); } catch (Exception failure) { error = failure; } if (completed != null) completed(error); }); }
        public void Flush() { while (queue.Count > 0) queue.Dequeue()(); }
        public bool Connect(bool simulation, int owner) { return IsConnected = true; }
        public void Disconnect() { IsConnected = false; }
        public void ClearSamples() { }
        public CommandResult Send(string command)
        {
            if (command.StartsWith("SIMRESET")) { int reset = command[8] - '0'; Set(reset, Axes[reset - 1].Position, Status(true, false, false, false, false)); return new CommandResult("", 0); }
            Commands.Add(command); int motor = command.Length > 1 && command[0] == '#' ? command[1] - '0' : 1;
            if (command.StartsWith("&") && command.EndsWith("A")) { if (!IgnoreAbort) CoordinateRunning = false; return new CommandResult("", 0); }
            if (command.StartsWith("I"))
            {
                int assignment = command.IndexOf('=');
                if (assignment > 0) { if (!IgnoreParameterWrite) Parameters[command.Substring(0, assignment)] = command.Substring(assignment + 1); return new CommandResult("", 0); }
                return new CommandResult(Parameters[command], 0);
            }
            AxisSample axis = Axes[motor - 1]; string response = command.EndsWith("P") ? axis.Position.ToString(CultureInfo.InvariantCulture) :
                command.EndsWith("?") ? axis.Status.FirstWord.ToString("X6") + axis.Status.SecondWord.ToString("X6") : "0";
            return new CommandResult(response, 0);
        }
        public void SetSpeed(int motor, decimal speed) { }
        public void Jog(int motor, int direction, decimal speed, decimal? step)
        { Commands.Add("jog" + motor); Set(motor, Axes[motor - 1].Position, Status(false, true, false, false, false)); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, Func<bool> cancelled) { if (!cancelled()) Jog(motor, direction, speed, step); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, decimal acceleration) { Jog(motor, direction, speed, step); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, decimal acceleration, Func<bool> cancelled) { if (!cancelled()) Jog(motor, direction, speed, step, acceleration); }
        public void Move(int motor, decimal target, decimal speed)
        { Commands.Add("move" + motor); Set(motor, Axes[motor - 1].Position, Status(false, true, false, false, false)); }
        public void Move(int motor, decimal target, decimal speed, Func<bool> cancelled) { if (!cancelled()) Move(motor, target, speed); }
        public void Move(int motor, decimal target, decimal speed, decimal acceleration) { Move(motor, target, speed); }
        public void Move(int motor, decimal target, decimal speed, decimal acceleration, Func<bool> cancelled) { if (!cancelled()) Move(motor, target, speed, acceleration); }
        public void MovePair(int x, int y, MotionPoint point) { }
        public void ZeroPosition(int motor) { Commands.Add("zero" + motor); Set(motor, 0, Status(true, true, false, false, false)); }
        public void Stop(IEnumerable<int> motors)
        { Commands.Add("stop"); if (FailStop) throw new InvalidOperationException("stop failed"); foreach (int motor in motors) if (StopImmediately) Set(motor, Axes[motor - 1].Position, Status(true, true, false, false, false)); }
        public void Download(string path) { }
        public ControllerSnapshot ReadSnapshot(int x, int y, int cs)
        { double now = MonotonicClock.NowMs; return new ControllerSnapshot { XCounts = Axes[x - 1].Position, YCounts = Axes[y - 1].Position, XStatus = Axes[x - 1].Status, YStatus = Axes[y - 1].Status, XTimeMs = now, YTimeMs = now, Coordinate = new CoordinateStatus { FirstWord = CoordinateRunning ? 1 : 0, SecondWord = CoordinateRunning ? 0 : 0x20000 } }; }
        public ControllerFeedback ReadFeedback(int x, int y, int selected, int cs)
        {
            Reads++; ControllerSnapshot snapshot = ReadSnapshot(x, y, cs); List<AxisSample> samples = new List<AxisSample>();
            foreach (int motor in new int[] { x, y, selected })
            { AxisSample source = Axes[motor - 1]; samples.Add(new AxisSample(motor, source.Position, 0, DateTime.Now) { Status = source.Status, FollowingError = source.FollowingError, TimeMs = snapshot.TimeMs }); }
            return new ControllerFeedback(snapshot, samples.ToArray());
        }
        public AxisSample ReadAxisFeedback(int motor)
        {
            AxisReads++;
            AxisSample source = Axes[motor - 1];
            return new AxisSample(motor, source.Position, source.Velocity, DateTime.Now)
            { Status = source.Status, FollowingError = source.FollowingError, TimeMs = MonotonicClock.NowMs };
        }
        public double StartPlan(MotionPlan plan, double lag, Func<bool> cancelled) { CoordinateRunning = true; return MonotonicClock.NowMs; }
        public void StopPlan(int cs, int x, int y) { Stop(new int[] { x, y }); }
        public void SetEnabled(int motor, bool enabled)
        {
            Commands.Add(enabled ? "enable" : "disable");
            if (!enabled && FailDisable) throw new InvalidOperationException("disable failed");
            if (!enabled && IgnoreDisable) return;
            Set(motor, Axes[motor - 1].Position, Status(true, enabled, false, false, false));
        }
        public void Home(int x, int y, bool both, int plc) { }
        public void Home(int x, int y, bool both, int plc, Func<bool> cancelled) { if (!cancelled()) Home(x, y, both, plc); }
        public void StopHome(int x, int y, int plc) { Stop(new int[] { x, y }); }
        public void Dispose() { }
    }
    private static MotionGuard Guard()
    {
        MotionGuard guard = new MotionGuard { MappingReady = true, Simulation = true };
        for (int i = 1; i <= 3; i++) guard.Configuration.Axes.Add(new AxisConfiguration { Motor = i, TravelConfigured = true, MaximumSpeed = 15 });
        return guard;
    }
    private static void Refresh(Fake fake, FeedbackService feedback, ManualMotionService manual)
    { Thread.Sleep(2); feedback.Poll(1, 2, 3, 0); fake.Flush(); if (manual != null) manual.Tick(); }
    private static void Guarding()
    {
        MotionGuard guard = Guard(); AxisSample sample = new AxisSample(3, 0, 0, DateTime.Now) { Status = Status(true, true, false, true, false), TimeMs = MonotonicClock.NowMs };
        Check(guard.Reason(sample, 1, 10, 1, true).Contains("正限位"), "Positive limit blocks further positive travel");
        Check(guard.Reason(sample, -1, -10, 1, true) == null, "Safe negative retreat is allowed from positive limit");
        sample.Status = Status(true, true, false, false, true);
        Check(guard.Reason(sample, -1, -10, 1, true).Contains("负限位"), "Negative limit blocks further negative travel");
        Check(guard.Reason(sample, 1, 10, 1, true) == null, "Safe positive retreat is allowed from negative limit");
        sample.Status = Status(true, true, true, false, false); Check(guard.Reason(sample, 1, 10, 1, true).Contains("驱动器"), "Third axis fault is diagnosed");
        sample.Status = Status(true, false, false, false, false); Check(guard.Reason(sample, 1, 10, 1, true).Contains("使能"), "Disabled axis cannot move");
        sample.Status = Status(true, true, false, false, false); Check(guard.Reason(sample, 1, 1001, 1, true).Contains("行程"), "Target beyond calibrated range is rejected");
        sample.TimeMs = MonotonicClock.NowMs - 2000; Check(guard.Reason(sample, 1, 1, 1, true).Contains("过期"), "Stale feedback cannot authorize motion");
        sample = new AxisSample(3, 1000, 0, DateTime.Now) { Status = Status(true, true, false, false, false), TimeMs = MonotonicClock.NowMs };
        sample.Status.SecondWord |= 0x1000;
        Check(!sample.Status.BlockingFault && sample.Status.PositionLimitStopped, "Desired-position limit is classified separately from drive faults");
        Check(guard.Reason(sample, -1, 990, 1, true) == null, "Known software limit boundary permits inward retreat");
        Check(guard.Reason(sample, 1, 1000, 1, true) != null, "Software limit still blocks outward direction");
    }
    private static void ManualMotion()
    {
        Fake fake = new Fake(); OperationState state = new OperationState(); MotionGuard guard = Guard();
        using (FeedbackService feedback = new FeedbackService(fake))
        {
            ManualMotionService manual = new ManualMotionService(fake, state, feedback, guard); Refresh(fake, feedback, null);
            Check(manual.Move(3, 100, 1), "Selected third motor can move after validation"); fake.Flush();
            Check(state.MovingMotor == 3 && !state.ManualBusy, "Command acceptance retains movement ownership");
            Check(!manual.StartJog(1, 1, 1, null, null), "Moving motor blocks competing jog");
            fake.Set(3, 99, Status(true, true, false, false, false)); Refresh(fake, feedback, manual);
            Check(state.MovingMotor == 3, "In-position bit alone cannot confirm the wrong destination");
            fake.Set(3, 100, Status(true, true, false, false, false)); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting && manual.StatusText.Contains("已到位"), "Fresh actual destination confirms completion");
            Check(manual.ZeroPosition(3), "Position zeroing requires a guarded stationary axis"); fake.Flush();
            Check(state.MovingMotor == 3, "Zero acknowledgement still waits for actual position feedback"); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting && feedback.Latest.Find(3).Position == 0 && manual.StatusText.Contains("置零"), "Zero completion is confirmed without claiming mechanical homing");
            Check(manual.StartJog(1, 1, 1, null, null), "Continuous jog starts"); manual.StopJog(null); fake.Flush();
            Check(fake.Commands.IndexOf("jog1") < fake.Commands.IndexOf("stop") && state.ManualStopping, "Fast release preserves dispatch order but waits for physical stop");
            Refresh(fake, feedback, manual); Check(state.ManualStopping, "One fresh stopped sample is insufficient"); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting && manual.StatusText.Contains("已停稳"), "Two stable stopped samples release ownership");
            fake.StopImmediately = false; Check(manual.StartJog(1, 1, 1, null, null), "Jog before delayed stop"); fake.Flush(); manual.Stop(new int[] { 1 }); fake.Flush(); Refresh(fake, feedback, manual);
            Check(state.ManualStopping, "Accepted stop does not unlock a moving axis");
            fake.Set(1, 0, Status(true, true, false, false, false)); Refresh(fake, feedback, manual); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting, "Delayed physical stop completes after stable feedback");
            Check(manual.SetEnabled(3, false), "Stationary selected motor can be disabled"); fake.Flush(); Check(state.MovingMotor == 3, "Disable waits for readback"); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting && !feedback.Latest.Find(3).Status.Enabled, "Disable is confirmed independently of X/Y");
            Check(manual.SetEnabled(3, true), "Disabled stationary axis can be re-enabled"); fake.Flush(); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting && feedback.Latest.Find(3).Status.Enabled, "Enable waits for true closed-loop status");
            Reject(delegate { manual.Reset(3); }, "Unconfigured hardware reset is rejected");
            fake.Set(3, 100, Status(true, true, true, false, false)); Refresh(fake, feedback, null);
            Reject(delegate { manual.Move(3, 0, 1); }, "Third axis fault prevents any move dispatch");
            guard.Configuration.Axis(3).ResetCommand = "SIMRESET{motor}";
            Check(manual.Reset(3), "Configured reset can run for a stationary faulted axis"); fake.Flush(); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting && !feedback.Latest.Find(3).Status.BlockingFault && !feedback.Latest.Find(3).Status.Enabled, "Reset confirms recovery without automatically enabling or moving");
            fake.Set(3, 100, Status(true, true, false, false, false)); Refresh(fake, feedback, null);
            manual.Move(3, 0, 1); manual.Stop(new int[] { 3 }); fake.Flush();
            Check(fake.Commands.FindAll(delegate(string command) { return command == "move3"; }).Count == 1, "Stop cancels an unexecuted positioning command");
            fake.FailStop = true; manual.Stop(new int[] { 1 }); fake.Flush(); Check(state.ManualStopFailed && state.ManualExecuting, "Failed stop retains ownership");
            fake.FailStop = false; fake.StopImmediately = true; manual.Stop(new int[] { 1 }); fake.Flush(); Refresh(fake, feedback, manual); Refresh(fake, feedback, manual);
            Check(!state.ManualExecuting, "Stop retry waits for stable actual feedback");
        }
    }
    private static void FocusedFeedback()
    {
        Fake fake = new Fake(); OperationState state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(fake))
        {
            ManualMotionService manual = new ManualMotionService(fake, state, feedback, Guard());
            Refresh(fake, feedback, null); double xyTime = feedback.Latest.Snapshot.TimeMs;
            int fullReads = fake.Reads;
            Check(manual.Move(3, 10, 1), "Third axis move starts with guarded feedback"); fake.Flush(); manual.Tick();
            Check(fake.AxisReads > 0 && fake.Reads == fullReads && state.ManualExecuting,
                "Acceptance requests focused feedback immediately without waiting for a UI timer or accepting the old arrival bit");
            MotorStatus notArrived = Status(true, true, false, false, false); notArrived.SecondWord &= ~1;
            fake.Set(3, 10, notArrived); Thread.Sleep(2); feedback.Poll(1, 2, 3, 0, 3); fake.Flush(); manual.Tick();
            Check(state.ManualExecuting && feedback.Latest.Find(3).Position == 10,
                "Focused position at target cannot complete a move while PMAC InPosition is false");
            fake.Set(3, 11, Status(true, true, false, false, false)); Thread.Sleep(2); feedback.PollNow(3); fake.Flush(); manual.Tick();
            Check(state.ManualExecuting, "PMAC arrival cannot bypass the software target tolerance");
            string timing = null; manual.TimingRecorded += delegate(string text) { timing = text; };
            fake.Set(3, 10, Status(true, true, false, false, false)); Thread.Sleep(2); feedback.PollNow(3); fake.Flush(); manual.Tick();
            Check(!state.ManualExecuting && feedback.Fresh && feedback.Latest.Snapshot.TimeMs == xyTime,
                "Third axis completion uses its own fresh position while preserving X/Y timestamps");
            Check(manual.LastTiming.AcceptedAtMs.HasValue && manual.LastTiming.FirstFeedbackAtMs.HasValue &&
                manual.LastTiming.FinishedAtMs.HasValue && timing != null && timing.Contains("实时检查"),
                "Completed movement records queue, command, feedback and confirmation timing");
            fake.Set(2, 42, Status(true, true, false, false, false));
            Thread.Sleep(260); feedback.PollNow(3); fake.Flush();
            Check(fake.Reads > fullReads && feedback.Latest.Find(2).Position == 42,
                "Background complete reads continue to refresh other axes during focused monitoring");
            int axisReads = fake.AxisReads; feedback.PollNow(3); feedback.Clear(); fake.Flush();
            Check(feedback.Latest == null && fake.AxisReads == axisReads, "Context reset discards a queued focused read safely");
        }
        fake = new Fake(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(fake))
        {
            ManualMotionService manual = new ManualMotionService(fake, state, feedback, Guard()); Refresh(fake, feedback, null);
            double xyTime = feedback.Latest.Snapshot.TimeMs;
            manual.StartJog(3, 1, 1, null, null); fake.Flush(); manual.Tick(); Thread.Sleep(2);
            manual.StopJog(null); fake.Flush(); manual.Tick();
            Check(state.ManualStopping && state.ManualFeedbackMotor == 3, "Single-axis jog stop retains focused feedback ownership and requires a position baseline");
            Thread.Sleep(2); feedback.PollNow(3); fake.Flush(); manual.Tick();
            Check(!state.ManualExecuting && feedback.Latest.Snapshot.TimeMs == xyTime,
                "Two new stable third-axis samples confirm stopping without a new X/Y snapshot");
        }
        fake = new Fake();
        using (FeedbackService feedback = new FeedbackService(fake))
        {
            feedback.Poll(1, 2, 1, 0); fake.Flush(); fake.Set(1, 7, Status(true, true, false, false, false));
            feedback.PollNow(1); fake.Flush();
            Check(feedback.Latest.Axes[0] == feedback.Latest.Axes[2] && feedback.Latest.Snapshot.XCounts == 7,
                "Focused X update replaces both aliases when the selected axis is also X");
            feedback.Poll(1, 2, 1, 9, 1); fake.Flush(); int reads = fake.Reads;
            feedback.Poll(1, 2, 1, 9, 1); fake.Flush();
            Check(fake.Reads == reads + 1, "Coordinate program monitoring always reads the complete snapshot");
        }
        fake = new Fake(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(fake))
        {
            ManualMotionService manual = new ManualMotionService(fake, state, feedback, Guard()); Refresh(fake, feedback, null);
            manual.Move(3, 10, 1); fake.Set(3, 0, Status(true, true, true, false, false)); fake.Flush();
            Check(!fake.Commands.Contains("move3") && fake.Commands.Contains("stop"),
                "A new fault between button press and command execution is caught by the real-time preflight read");
        }
    }
    private static void Recovery()
    {
        Fake fake = new Fake(); OperationState state = new OperationState(); MotionGuard guard = Guard();
        using (FeedbackService feedback = new FeedbackService(fake))
        {
            ManualMotionService manual = new ManualMotionService(fake, state, feedback, guard);
            MotorStatus stalled = MotorStatus.Parse("892000070400"); fake.Set(1, 0, stalled);
            Check(stalled.ToString().Contains("指令已停止，未到位"), "A stalled hold is not labelled as movement");
            Refresh(fake, feedback, null); Reject(delegate { manual.Move(1, 100, 1); }, "Recovery never bypasses normal arrival guards");
            state.ManualStopping = state.ManualStopFailed = state.FeedbackStale = true; feedback.Clear();
            bool completed = false;
            Check(manual.CanRecover && manual.Recover(new int[] { 1, 2, 3 }, null, null, delegate { completed = true; }), "Unknown or not-in-position feedback does not lock out recovery");
            Check(!manual.CanRecover && !manual.Move(1, 10, 1), "Recovery serializes against further actions");
            fake.Flush(); Check(state.ManualExecuting && !completed, "Command acknowledgement alone does not release waiting");
            Refresh(fake, feedback, manual); Check(state.ManualExecuting, "Recovery needs more than one new sample");
            fake.Set(1, 2, Status(true, false, false, false, false)); Refresh(fake, feedback, manual);
            Check(state.ManualExecuting, "Changing feedback keeps recovery pending even when disabled");
            Refresh(fake, feedback, manual);
            Check(completed && !state.ManualExecuting && !fake.Axes[0].Status.Enabled && !fake.Axes[1].Status.Enabled && !fake.Axes[2].Status.Enabled, "Stable disabled feedback releases all monitored axes without InPosition");
            Check(!fake.Commands.Contains("enable") && !fake.Commands.Contains("zero1"), "Recovery does not enable, zero, or resume a target");
            fake.FailDisable = true;
            Check(manual.Recover(new int[] { 1, 2, 3 }, null, null, null), "Recovery may be requested again"); fake.Flush();
            Check(state.ManualStopFailed && state.ManualExecuting && manual.CanRecover, "A failed disable remains blocked but retry is available");
            Refresh(fake, feedback, manual); Refresh(fake, feedback, manual);
            Check(state.ManualExecuting, "Old disabled feedback cannot mask a command failure");
            fake.FailDisable = false; manual.Recover(new int[] { 1, 2, 3 }, null, null, null); fake.Flush();
            Refresh(fake, feedback, manual); Refresh(fake, feedback, manual); Check(!state.ManualExecuting, "A successful retry clears the failed recovery");
            fake.Set(1, 2, stalled); fake.IgnoreDisable = true;
            manual.Recover(new int[] { 1, 2 }, null, null, null); fake.Flush(); Refresh(fake, feedback, manual); Refresh(fake, feedback, manual);
            Check(state.ManualExecuting, "A controller that remains enabled cannot unlock the UI");
            typeof(ManualMotionService).GetField("deadline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manual, MonotonicClock.NowMs - 1);
            manual.Tick(); Check(state.ManualStopFailed && manual.StatusText.Contains("5 秒") && manual.CanRecover, "Recovery timeout is explicit and retryable");
        }
    }
    private static void WorkflowRecovery()
    {
        Fake fake = new Fake(); OperationState state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(fake))
        using (MotionWorkflow flow = new MotionWorkflow(fake, state, feedback))
        {
            flow.Configure(true, CanvasSettings.Default, true, 1, 2, 3); flow.Poll(); fake.Flush();
            flow.Plan = MotionPlanningService.Build(PathGeometry.Line(0, 0, 1, 1), new PlanningOptions { CoordinateSystem = 9, Mapping = CanvasSettings.Default }, new MotionPoint(0, 0));
            flow.StartExecution(0, true); fake.Flush(); Check(state.Running, "Recovery fixture has an owned coordinate program");
            ManualMotionService manual = new ManualMotionService(fake, state, feedback, Guard());
            fake.IgnoreAbort = true; flow.Stop();
            Check(manual.Recover(new int[] { 1, 2, 3 }, flow.PrepareRecovery, delegate { return flow.RecoveryReady; }, flow.CompleteRecovery), "Recovery can replace an owned workflow stop wait"); fake.Flush();
            for (int i = 0; i < 3; i++) { Thread.Sleep(2); flow.Poll(); fake.Flush(); manual.Tick(); }
            Check(fake.Commands.Contains("&9A") && state.ManualExecuting && state.Stopping, "Disabled axes still require an owned coordinate system to stop");
            fake.CoordinateRunning = false; Thread.Sleep(2); flow.Poll(); fake.Flush(); manual.Tick();
            Check(!state.ManualExecuting && !state.WorkflowBusy && !flow.CurrentRun.Completed && flow.CurrentRun.Result.Contains("解除等待"), "Confirmed coordinate stop releases ownership without completing or resuming the run");
        }
    }
    private static Fake PulseController()
    {
        Fake fake = new Fake(); fake.Set(1, 0, Status(true, false, false, false, false));
        foreach (string pair in new string[] { "I101=0", "I124=$120001", "I125=$78000", "I7016=3", "I7010=8", "I103=$3515", "I104=$3515", "I8020=$78000", "I102=$78002" })
        { int equals = pair.IndexOf('='); fake.Parameters.Add(pair.Substring(0, equals), pair.Substring(equals + 1)); }
        return fake;
    }
    private static void PulseRepair()
    {
        Fake fake = PulseController(); PulseOutputRepair repair = new PulseOutputRepair(fake);
        PulseOutputConfiguration candidate = repair.Inspect(1);
        Check(candidate.NeedsRepair && candidate.ExpectedAddress == 0x78004, "Live incident routing is diagnosed from mode and feedback");
        string result = repair.Repair(candidate);
        Check(fake.Parameters["I102"] == "$078004" && !fake.Axes[0].Status.Enabled && result.Contains("回读"), "Repair changes only the verified output address and leaves outputs disabled");
        Check(fake.Commands.FindAll(delegate(string command) { return command.Contains("="); }).Count == 1 && !fake.Commands.Contains("SAVE") && !fake.Commands.Contains("enable"), "Repair never changes tolerances, enables, or persists parameters");
        Check(!repair.Inspect(1).NeedsRepair, "Correct output routing is a no-op");
        fake = PulseController(); repair = new PulseOutputRepair(fake); fake.Set(1, 0, Status(true, true, false, false, false));
        Reject(delegate { repair.Inspect(1); }, "An enabled motor must not have its output rerouted");
        fake = PulseController(); repair = new PulseOutputRepair(fake); candidate = repair.Inspect(1); fake.Set(1, 0, Status(true, true, false, false, false));
        Reject(delegate { repair.Repair(candidate); }, "Enabled state is rechecked after user confirmation");
        fake = PulseController(); repair = new PulseOutputRepair(fake); candidate = repair.Inspect(1); fake.Parameters["I7016"] = "2";
        Reject(delegate { repair.Repair(candidate); }, "Changed configuration cannot reuse approval for an old candidate");
        fake = PulseController(); repair = new PulseOutputRepair(fake); fake.Parameters["I7010"] = "7";
        Reject(delegate { repair.Inspect(1); }, "External encoder axes are not auto-converted to pulse mode");
        fake = PulseController(); repair = new PulseOutputRepair(fake); fake.Parameters["I8020"] = "$78008";
        Reject(delegate { repair.Inspect(1); }, "A mismatched feedback source blocks automatic routing repair");
        fake = PulseController(); repair = new PulseOutputRepair(fake); candidate = repair.Inspect(1); fake.IgnoreParameterWrite = true;
        Reject(delegate { repair.Repair(candidate); }, "Write acknowledgement without matching readback is a failure");
        fake = PulseController(); repair = new PulseOutputRepair(fake);
        fake.Parameters["I125"] = "$78008"; fake.Parameters["I103"] = fake.Parameters["I104"] = "$3518";
        fake.Parameters["I8023"] = "$78008"; fake.Parameters["I7026"] = "3"; fake.Parameters["I7020"] = "8"; fake.Parameters["I102"] = "$7800A";
        Check(repair.Inspect(1).ExpectedAddress == 0x7800C, "Physical channel mapping is independent of motor number");
    }
    private static void TimeoutAndAlarms()
    {
        Fake fake = new Fake(); using (FeedbackService feedback = new FeedbackService(fake))
        {
            int failures = 0; feedback.Failed += delegate { failures++; };
            feedback.Poll(1, 2, 3, 0); Thread.Sleep(1050); feedback.CheckTimeout(); feedback.CheckTimeout();
            Check(failures == 1 && !feedback.Fresh, "Hung read is reported independently and only once"); fake.Flush(); Refresh(fake, feedback, null); Check(feedback.Fresh, "Successful fresh data recovers sampling");
            AlarmService alarms = new AlarmService(); fake.Set(3, 0, Status(true, true, true, false, false)); Refresh(fake, feedback, null);
            alarms.Update(feedback.Latest); alarms.Update(feedback.Latest); Check(alarms.ActiveCount == 1 && alarms.History.Count == 1, "Polling does not duplicate an active third axis alarm");
            alarms.Acknowledge(); Check(alarms.ActiveCount == 1 && alarms.History[0].Active && alarms.History[0].Acknowledged, "Acknowledgement cannot clear a fault");
            fake.Set(3, 0, Status(true, true, false, false, false)); Refresh(fake, feedback, null); alarms.Update(feedback.Latest);
            Check(alarms.ActiveCount == 0 && !alarms.History[0].Active && alarms.History[0].Recovered != DateTime.MinValue, "Only device recovery clears the active alarm");
        }
        Fake startupFake = new Fake(); using (FeedbackService feedback = new FeedbackService(startupFake))
        {
            int failures = 0; feedback.Failed += delegate { failures++; };
            Refresh(startupFake, feedback, null); Check(feedback.Fresh, "Initial feedback is fresh before path startup");
            feedback.PauseTimeoutForCommand(); Thread.Sleep(1100); feedback.CheckTimeout();
            Check(failures == 0 && feedback.Fresh, "Expected path-start communication does not expire the last snapshot");
            feedback.ResumeTimeoutAfterCommand(); feedback.CheckTimeout();
            Check(feedback.Fresh, "Freshness resumes with a response window after path startup");
            Thread.Sleep(1050); feedback.CheckTimeout();
            Check(failures == 1 && !feedback.Fresh, "Feedback timeout remains active after path startup finishes");
        }
    }
    private static void ConfigurationAndLogs(string directory)
    {
        Directory.CreateDirectory(directory); string profile = Path.Combine(directory, "profile.xml");
        DeviceConfiguration config = new DeviceConfiguration { DeviceName = "设备 A", XScale = 200, YScale = 300 };
        config.Axes.Add(new AxisConfiguration { Motor = 3, TravelConfigured = true, MinimumCounts = -200, MaximumCounts = 500, MaximumSpeed = 2, ResetCommand = "M100=1" });
        config.Inputs.Add(new IoConfiguration { Name = "急停反馈", Query = "M110", ActiveLow = true });
        DeviceConfigurationStore.Save(profile, config); DeviceConfiguration restored = DeviceConfigurationStore.Load(profile);
        Check(restored.DeviceName == "设备 A" && restored.XScale == 200 && restored.Axis(3).MaximumCounts == 500 && restored.Inputs[0].ActiveLow, "Device calibration, axis rules and I/O configuration roundtrip");
        restored.Axis(3).MaximumSpeed = 3; DeviceConfigurationStore.Save(profile, restored); Check(DeviceConfigurationStore.Load(profile).Axis(3).MaximumSpeed == 3, "Atomic replacement saves updated configuration");
        restored.Inputs[0].Query = "M110=1"; bool invalid = false; try { DeviceConfigurationStore.Save(profile, restored); } catch (ArgumentException) { invalid = true; } Check(invalid, "Diagnostic I/O cannot execute writes");
        using (LogView log = new LogView())
        {
            log.LogDirectory = Path.Combine(directory, "logs"); for (int i = 0; i < 520; i++) log.Append("测试", "record " + i);
            string text = File.ReadAllText(Directory.GetFiles(log.LogDirectory)[0]);
            Check(text.Contains("record 0") && text.Contains("record 519") && log.PersistenceError == null, "Visible log rollover preserves every disk record");
        }
    }
    private static void ControllerProtocol()
    {
        using (ControllerSession session = new ControllerSession(new WindowsFormsSynchronizationContext()))
        {
            bool done = false; Exception failure = null; ControllerFeedback feedback = null; bool remainsDisabled = false;
            session.Execute(delegate
            {
                session.Connect(true, 0); session.SetEnabled(3, false); session.Stop(new int[] { 1, 2, 3 });
                remainsDisabled = !MotorStatus.Parse(session.Send("#3?").Response).Enabled;
                session.SetSpeed(3, 2); session.Send("SIMFAULT3"); feedback = session.ReadFeedback(1, 2, 3, 0);
                int cancellationChecks = 0; session.Move(1, 100, 1, delegate { return ++cancellationChecks > 1; });
                Check(Double.Parse(session.Send("#1P").Response, CultureInfo.InvariantCulture) == 0 && cancellationChecks == 2, "Cancellation during speed setup suppresses the actual move command");
                session.SetSpeed(1, 1); session.Send("I122=9"); cancellationChecks = 0;
                session.Move(1, 100, 1, delegate { return ++cancellationChecks > 1; });
                Check(session.Send("I122").Response.Trim() == "1" && Double.Parse(session.Send("#1P").Response, CultureInfo.InvariantCulture) == 0,
                    "Speed changed outside the motion helper is corrected and verified before motion, with cancellation still honored");
                session.Move(1, 100, 1, 0.05M, delegate { return false; });
                Check(Math.Abs(Double.Parse(session.Send("I119").Response, CultureInfo.InvariantCulture) - 0.05) < 0.000001 &&
                    session.Send("I120").Response.Trim() == "27" && session.Send("I121").Response.Trim() == "6",
                    "Point-to-point motion writes the derived acceleration and S-curve timing before its target");
            }, delegate(Exception error) { failure = error; done = true; });
            Until(delegate { return done; }, "Serialized simulator protocol request completes"); if (failure != null) throw failure;
            Check(remainsDisabled, "Global stop never re-enables a disabled amplifier");
            Check(feedback.Find(3).Status.BlockingFault && !feedback.Find(3).Status.Enabled && feedback.Snapshot.Ready, "Actual session acquires selected axis fault independently of X/Y");
            Check(feedback.Find(3).FollowingError.HasValue && feedback.Find(1).CommandPosition.HasValue, "Following error and reconstructed command position reach the feedback model");
        }
    }
    private static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner); }
    private static void Pump(int milliseconds)
    { System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); } }
    private static void Until(Func<bool> condition, string message)
    { System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < 5000) Pump(20); Check(condition(), message); }
    private static void Snapshot(Form form, string path)
    { using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); } }
    private static void Interface(string directory)
    {
        Directory.CreateDirectory(directory);
        using (Form1 form = new Form1())
        {
            form.Show(); ConnectionView connection = Field<ConnectionView>(form, "connection"); MotionView motion = Field<MotionView>(form, "motion");
            ControllerSession session = Field<ControllerSession>(form, "session"); PathView path = Field<PathView>(form, "pathView"); DiagnosticsView diagnostics = Field<DiagnosticsView>(form, "diagnostics"); AxisStatusView states = Field<AxisStatusView>(form, "axisStates");
            connection.ConnectButton.PerformClick(); Until(delegate { return motion.MoveButton.Enabled; }, "UI connects with fresh guarded feedback");
            path.Pages.SelectedIndex = 1; motion.SelectedMotor.SelectedItem = 3; Until(delegate { return states.DisableButton.Enabled; }, "Third motor has live state and controls");
            states.DisableButton.PerformClick(); Until(delegate { return Field<FeedbackService>(form, "feedbackService").Fresh && !Field<FeedbackService>(form, "feedbackService").Latest.Find(3).Status.Enabled && !Field<OperationState>(form, "operations").ManualExecuting; }, "UI selected motor disable is verified");
            Check(!motion.MoveButton.Enabled, "UI disabled selected motor cannot position"); states.EnableButton.PerformClick(); Until(delegate { return motion.MoveButton.Enabled; }, "UI enables selected third motor without moving");
            motion.Target.Value = 100; motion.MoveButton.PerformClick(); Until(delegate { return !Field<OperationState>(form, "operations").ManualExecuting && Field<FeedbackService>(form, "feedbackService").Latest.Find(3).Position == 100; }, "UI waits for third motor arrival");
            Pump(150); Snapshot(form, Path.Combine(directory, "manual-control.png"));
            path.Pages.SelectedIndex = path.Pages.TabPages.Count - 1; Pump(100); Snapshot(form, Path.Combine(directory, "diagnostics-trend.png"));
            session.Execute(delegate { session.Send("SIMFAULT3"); }, null); Until(delegate { return Field<AlarmService>(form, "alarms").ActiveCount > 0; }, "UI reports third motor fault independently");
            TabControl inner = (TabControl)diagnostics.Controls[0]; inner.SelectedIndex = 1; Pump(100); Snapshot(form, Path.Combine(directory, "diagnostics-alarm.png"));
            diagnostics.AcknowledgeButton.PerformClick(); Check(Field<AlarmService>(form, "alarms").ActiveCount > 0, "UI acknowledgement retains the active fault");
            path.Pages.SelectedIndex = 1; states.ResetButton.PerformClick(); Until(delegate { return Field<AlarmService>(form, "alarms").ActiveCount == 0 && !Field<OperationState>(form, "operations").ManualExecuting; }, "UI configured simulation reset confirms recovery");
            Check(!motion.MoveButton.Enabled, "Fault reset does not automatically enable the motor"); states.EnableButton.PerformClick(); Until(delegate { return motion.MoveButton.Enabled; }, "Explicit enable recovers manual controls");
            path.Pages.SelectedIndex = path.Pages.TabPages.Count - 1; inner.SelectedIndex = 2; diagnostics.ReadSpeedButton.PerformClick(); Until(delegate { return diagnostics.ParameterResult.Text.Contains("实际点动速度"); }, "UI speed readback shows actual controller setting");
            Pump(100); Snapshot(form, Path.Combine(directory, "diagnostics-config.png"));
            using (ManualResetEvent blocked = new ManualResetEvent(false))
            using (ManualResetEvent release = new ManualResetEvent(false))
            {
                session.Execute(delegate { blocked.Set(); release.WaitOne(4000); }, null); Check(blocked.WaitOne(1000), "Communication worker can be held to exercise a real stall");
                Pump(1300); Check(!motion.MoveButton.Enabled && Field<OperationState>(form, "operations").FeedbackStale && Field<Button>(form, "globalStop").Enabled, "UI detects silent communication stall and preserves stop access");
                release.Set(); Until(delegate { return !Field<OperationState>(form, "operations").FeedbackStale; }, "Communication can recover without automatic movement");
                Check(!Field<OperationState>(form, "operations").ManualExecuting && Field<FeedbackService>(form, "feedbackService").Latest.Find(3).Position == 100, "Reconnection never resumes an old move");
            }
            path.Pages.SelectedIndex = 1;
            motion.Target.Value = 1000; motion.MoveButton.PerformClick();
            Until(delegate { return Field<OperationState>(form, "operations").MovingMotor.HasValue && !Field<OperationState>(form, "operations").ManualBusy; }, "UI recovery fixture begins a manual target");
            Button recover = Field<Button>(form, "globalRecover");
            Check(recover.Enabled, "The recovery button remains accessible while an action owns the UI");
            recover.PerformClick();
            Until(delegate { return !Field<OperationState>(form, "operations").ManualExecuting && !Field<OperationState>(form, "operations").WorkflowBusy; }, "Recovery button releases both manual and workflow waiting after verified feedback");
            Check(!Field<FeedbackService>(form, "feedbackService").Latest.Find(3).Status.Enabled && states.EnableButton.Enabled && !motion.MoveButton.Enabled,
                "Recovered UI offers deliberate enable while motion stays disabled");
            Pump(100); Snapshot(form, Path.Combine(directory, "recovery-controls.png"));
            form.Size = form.MinimumSize; Pump(100);
            Snapshot(form, Path.Combine(directory, "recovery-minimum-window.png"));
            Check(recover.Width >= 170 && recover.Height >= 24 && recover.Parent.ClientRectangle.Contains(recover.Bounds),
                "Recovery control stays visible in the minimum window: button=" + recover.Bounds + "; parent=" + recover.Parent.ClientRectangle);
            ((ScrollableControl)states.Parent.Parent).ScrollControlIntoView(states); Pump(100);
            Snapshot(form, Path.Combine(directory, "recovery-minimum-manual-controls.png"));
            Check(states.RepairOutputButton.Parent.ClientRectangle.Contains(states.RepairOutputButton.Bounds),
                "Pulse repair button fits the existing manual control row: button=" + states.RepairOutputButton.Bounds + "; parent=" + states.RepairOutputButton.Parent.ClientRectangle);
            form.Close(); Until(delegate { return form.IsDisposed; }, "Verified idle application closes");
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Guarding(); ManualMotion(); FocusedFeedback(); Recovery(); WorkflowRecovery(); PulseRepair(); TimeoutAndAlarms(); ControllerProtocol();
            string directory = Path.Combine(Path.GetTempPath(), "pmac-motor-tests-" + Guid.NewGuid().ToString("N")); ConfigurationAndLogs(directory);
            Interface(args.Length == 0 ? directory : args[0]);
            Console.WriteLine("PASS: " + checks + " motor debugging assertions; simulation only, no hardware accessed."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
