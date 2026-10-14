using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Operations_Control.Application;
using Operations_Control.Communication;
using Operations_Control.Files;
using Operations_Control.Models;
using Operations_Control.Motion;

// Deliberately has no WinForms imports, controls, timers or message pump.
internal static class ModuleTests
{
    private static int checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static void Reject(Action action, string message)
    {
        bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }

    private sealed class FakeSession : IControllerSession
    {
        private readonly Queue<Action> work = new Queue<Action>();
        public readonly List<string> Commands = new List<string>();
        public ControllerSnapshot Snapshot;
        public bool FailStop, FailRead;
        public int StartCalls, StopPlanCalls, StopHomeCalls, ReadCalls, CoordinateSystem;
        public Action DuringStart;
        public double StartTime;
        public bool IsConnected { get; private set; }
        public string DeviceName { get { return "test controller"; } }
        public FakeSession() { IsConnected = true; Snapshot = Sample(0, 0, 0, true, false); }
        public void Execute(Action action, Action<Exception> completed)
        {
            work.Enqueue(delegate
            {
                Exception failure = null; try { action(); } catch (Exception error) { failure = error; }
                if (completed != null) completed(failure);
            });
        }
        public void Flush() { while (work.Count > 0) work.Dequeue()(); }
        public bool Connect(bool simulation, int ownerWindow) { return IsConnected = true; }
        public void Disconnect() { IsConnected = false; Commands.Add("disconnect"); }
        public void ClearSamples() { }
        public CommandResult Send(string command)
        {
            Commands.Add(command);
            return new CommandResult(command.EndsWith("P") ? "0" : command.EndsWith("?") ? "882000000401" : "", 0);
        }
        public void SetSpeed(int motor, decimal speed) { }
        public void Jog(int motor, int direction, decimal speed, decimal? step) { Commands.Add("jog"); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, Func<bool> cancelled) { if (!cancelled()) Jog(motor, direction, speed, step); }
        public void Move(int motor, decimal target, decimal speed) { }
        public void Move(int motor, decimal target, decimal speed, Func<bool> cancelled) { if (!cancelled()) Move(motor, target, speed); }
        public void MovePair(int x, int y, MotionPoint point) { Commands.Add("follow"); }
        public void ZeroPosition(int motor) { }
        public void Stop(IEnumerable<int> motors) { Commands.Add("stop"); if (FailStop) throw new InvalidOperationException("stop failed"); }
        public void Download(string path) { }
        public ControllerSnapshot ReadSnapshot(int x, int y, int cs) { return Snapshot; }
        public ControllerFeedback ReadFeedback(int x, int y, int selectedMotor, int cs)
        {
            ReadCalls++; if (FailRead) throw new InvalidOperationException("read failed");
            return new ControllerFeedback(Snapshot, new AxisSample[] {
                new AxisSample(x, Snapshot.XCounts, null, DateTime.Now) { Status = Snapshot.XStatus, TimeMs = Snapshot.TimeMs },
                new AxisSample(y, Snapshot.YCounts, null, DateTime.Now) { Status = Snapshot.YStatus, TimeMs = Snapshot.TimeMs },
                new AxisSample(selectedMotor, 0, null, DateTime.Now) { Status = Snapshot.XStatus, TimeMs = Snapshot.TimeMs } });
        }
        public double StartPlan(MotionPlan plan, double lag, Func<bool> cancelled)
        {
            if (DuringStart != null) DuringStart();
            if (cancelled()) throw new OperationCanceledException();
            StartCalls++; StartTime = MonotonicClock.NowMs; return StartTime;
        }
        public void StopPlan(int cs, int x, int y) { CoordinateSystem = cs; StopPlanCalls++; Stop(new int[] { x, y }); }
        public void SetEnabled(int motor, bool enabled) { }
        public void Home(int x, int y, bool both, int plc) { Commands.Add("home"); }
        public void Home(int x, int y, bool both, int plc, Func<bool> cancelled) { if (!cancelled()) Home(x, y, both, plc); }
        public void StopHome(int x, int y, int plc) { StopHomeCalls++; Stop(new int[] { x, y }); }
        public void Dispose() { }
    }

    private static ControllerSnapshot Sample(double x, double y, double time, bool inPosition, bool homing)
    {
        if (time == 0) time = MonotonicClock.NowMs;
        MotorStatus status = MotorStatus.Parse(homing ? "880400000000" : inPosition ? "882000000401" : "880000000000");
        return new ControllerSnapshot { XCounts = x, YCounts = y, XTimeMs = time, YTimeMs = time,
            XStatus = status, YStatus = status, Coordinate = CoordinateStatus.Parse(inPosition ? "000000020000000000" : "000001000000000000") };
    }
    private static MotionWorkflow Workflow(FakeSession session, OperationState state, FeedbackService feedback)
    {
        MotionWorkflow flow = new MotionWorkflow(session, state, feedback);
        CanvasSettings mapping = CanvasSettings.Default;
        flow.Configure(true, mapping, true, 1, 2, 1);
        flow.Poll(); session.Flush();
        flow.Plan = MotionPlanningService.Build(PathGeometry.Line(0, 0, 1, 1), new PlanningOptions { CoordinateSystem = 9, Mapping = mapping }, new MotionPoint(0, 0));
        return flow;
    }
    private static void Execution()
    {
        FakeSession session = new FakeSession(); OperationState state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            Check(flow.Fresh, "Feedback supplies fresh readiness without a window");
            MotionPlan planned = flow.Plan;
            flow.Configure(true, planned.Options.Mapping, true, 1, 2, 3);
            Check(flow.Plan == planned, "Changing only the monitored third motor preserves a prepared plan");
            flow.StartExecution(0, false); Check(flow.IsExecuting, "Queued start locks competing operations"); session.Flush();
            session.Snapshot = Sample(100, 100, session.StartTime + flow.Plan.DurationMs + 1, true, false);
            flow.Poll(); session.Flush();
            Check(flow.CurrentRun.Completed && !flow.IsBusy && flow.CurrentRun.Samples.Count == 1, "Completion and retained feedback are independent of UI");
        }
        session = new FakeSession(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            flow.StartExecution(0, false); session.Flush();
            session.Snapshot = Sample(0, 0, session.StartTime + 10, false, false);
            session.Snapshot.XStatus = MotorStatus.Parse("882000000009");
            flow.Poll(); session.Flush();
            Check(session.StopPlanCalls == 1 && !flow.CurrentRun.Completed && flow.StatusText.Contains("故障"), "Fault stops the owned coordinate system and cannot claim completion");
            Check(session.CoordinateSystem == 9, "Stop uses the executing plan's coordinate system");
        }
        session = new FakeSession(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            session.Snapshot = Sample(0, 0, MonotonicClock.NowMs - 2000, true, false); flow.Poll(); session.Flush();
            Reject(delegate { flow.StartExecution(0, false); }, "Stale feedback blocks motion in the business layer");
            Check(session.StartCalls == 0, "Stale feedback never queues a controller start");
        }
        session = new FakeSession(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            flow.StartExecution(0, false); session.Flush(); session.FailRead = true; flow.Poll(); session.Flush();
            Check(session.StopPlanCalls == 1 && flow.LatestSnapshot == null && flow.CurrentRun.Result.Contains("通信失败"), "Read failure cancels a running plan");
        }
    }
    private static void ConfirmStopped(MotionWorkflow flow, FakeSession session)
    {
        for (int i = 0; i < 2; i++)
        {
            Thread.Sleep(2); session.Snapshot = Sample(session.Snapshot.XCounts, session.Snapshot.YCounts, 0, true, false);
            flow.Poll(); session.Flush();
        }
    }
    private static void Cancellation()
    {
        FakeSession session = new FakeSession(); OperationState state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            flow.StartExecution(0, false); flow.Stop(); session.Flush(); ConfirmStopped(flow, session);
            Check(session.StartCalls == 0 && session.StopPlanCalls == 1 && !flow.IsExecuting, "Queued start cancellation preserves start-stop ordering");
            Check(!flow.CurrentRun.Completed, "Cancelled start never marks a run complete");
        }
        session = new FakeSession(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            session.DuringStart = delegate { flow.Stop(); };
            flow.StartExecution(0, false); session.Flush(); ConfirmStopped(flow, session);
            Check(session.StartCalls == 0 && session.StopPlanCalls == 1 && !state.Running, "Cancellation during startup suppresses a late successful callback");
        }
        session = new FakeSession(); state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            flow.StartExecution(0, false); session.Flush(); session.FailStop = true;
            flow.Stop(); session.Flush();
            Check(flow.IsExecuting && state.StopFailed, "Failed stop keeps operation ownership and competing controls locked");
            flow.StartExecution(0, false); Check(session.StartCalls == 1, "Failed stop cannot launch another plan");
            session.FailStop = false; flow.Stop(); session.Flush(); ConfirmStopped(flow, session);
            Check(session.StopPlanCalls == 2 && !flow.IsExecuting, "Retry stops the original plan and releases ownership");
        }
    }
    private static void Homing()
    {
        FakeSession session = new FakeSession(); OperationState state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = Workflow(session, state, feedback))
        {
            int completed = 0; flow.HomeCompleted += delegate { completed++; };
            flow.StartHoming(2, -1, 10, false); session.Flush();
            flow.Poll(); session.Flush(); Check(state.Homing && completed == 0, "Previous Home Complete bits cannot finish a new home");
            session.Snapshot = Sample(0, 0, 0, false, true); flow.Poll(); session.Flush();
            session.Snapshot = Sample(0, 0, 0, true, false); flow.Poll(); session.Flush();
            Check(completed == 1 && !flow.IsExecuting, "Both axes must transition through the new home and arrive");
            flow.StartHoming(0, -1, 0.001, false); session.Flush(); Thread.Sleep(10);
            flow.Poll(); session.Flush();
            Check(session.StopHomeCalls == 1 && flow.StatusText.Contains("回零超时"), "Home timeout requests the correct controller stop"); ConfirmStopped(flow, session);
            flow.StartHoming(0, -1, 10, false); session.Flush(); session.FailStop = true;
            flow.Stop(); session.Flush(); session.FailStop = false; flow.Stop(); session.Flush(); ConfirmStopped(flow, session);
            Check(session.StopHomeCalls == 3 && !flow.IsExecuting, "Failed built-in home stop retains ownership for retry");
        }
    }
    private static void FeedbackAndManual()
    {
        FakeSession session = new FakeSession();
        using (FeedbackService feedback = new FeedbackService(session))
        {
            int deliveries = 0; feedback.Updated += delegate { deliveries++; };
            feedback.Poll(1, 2, 3, 0); feedback.Poll(1, 2, 3, 0); session.Flush();
            Check(session.ReadCalls == 1 && deliveries == 1, "Multiple refresh requests share one in-flight read");
            feedback.Poll(1, 2, 3, 0); feedback.Clear(); session.Flush();
            Check(deliveries == 1 && feedback.Latest == null, "Disconnect or context reset discards queued feedback");
            feedback.Poll(1, 2, 3, 0); feedback.Poll(2, 3, 4, 0); session.Flush();
            Check(deliveries == 1, "Axis remapping discards an old query");
            feedback.Poll(2, 3, 4, 0); session.Flush();
            Check(feedback.Latest.Axes[0].Motor == 2 && deliveries == 2, "New context resumes after old query finishes");
        }
        session = new FakeSession(); OperationState state = new OperationState();
        FeedbackService manualFeedback = new FeedbackService(session); manualFeedback.Poll(1, 2, 1, 0); session.Flush();
        MotionGuard guard = new MotionGuard { MappingReady = true };
        ManualMotionService manual = new ManualMotionService(session, state, manualFeedback, guard);
        Check(manual.StartJog(1, 1, 1, null, null), "Manual service accepts idle jogging");
        manual.StopJog(null); session.Flush();
        Check(session.Commands.IndexOf("jog") >= 0 && session.Commands.IndexOf("stop") > session.Commands.IndexOf("jog"), "Fast press/release queues jog then stop");
        state.Running = true; Check(!manual.StartJog(1, 1, 1, null, null), "Trajectory ownership blocks manual jogging"); state.Running = false;
        session.FailStop = true; Exception stopFailure = null;
        manual.Disconnect(new int[] { 1, 2 }, delegate(Exception stopped, Exception error) { stopFailure = stopped; }); session.Flush();
        Check(stopFailure != null && !session.IsConnected && !state.ShuttingDown, "Disconnect cleans up the session even when pre-stop fails");
    }
    private static void Files()
    {
        string directory = Path.Combine(Path.GetTempPath(), "pmac-module-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "program.pmc");
            ProgramFileStore.Save(file, "; 中文程序\r\n#1P", Encoding.GetEncoding(936));
            ProgramDocument document = ProgramFileStore.Read(file);
            Check(document.Text.Contains("中文程序") && document.Encoding.CodePage == 936, "Program files preserve GBK fallback");
            ProgramFileStore.Save(file, document.Text, document.Encoding);
            Check(ProgramFileStore.Read(file).Text == document.Text, "Saving preserves content and encoding");
            string temporary;
            using (TemporaryProgram program = ProgramFileStore.CreateTemporary("#1P")) { temporary = program.Path; Check(File.ReadAllText(temporary) == "#1P", "Generated temporary file contains the program"); }
            Check(!File.Exists(temporary), "Download temporary file is released deterministically");
            MotionPlan pure = TrajectoryPlanner.Build(PathGeometry.Line(0, 0, 1, 1), new PlanningOptions(), new MotionPoint(0, 0));
            Check(pure.Program == null && pure.DurationMs > 0, "Motion algorithm produces a plan independently of PMAC program generation");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static int Main()
    {
        try { Execution(); Cancellation(); Homing(); FeedbackAndManual(); Files(); Console.WriteLine("PASS: " + checks + " module assertions; no UI or hardware accessed."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
