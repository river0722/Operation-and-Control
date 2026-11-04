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

    private class FakeSession : IControllerSession
    {
        private readonly Queue<Action> work = new Queue<Action>();
        public readonly List<string> Commands = new List<string>();
        public ControllerSnapshot Snapshot;
        public bool FailStop, FailRead;
        public int StartCalls, StopPlanCalls, StopHomeCalls, ReadCalls, CoordinateSystem;
        public Action DuringStart;
        public double StartTime;
        public int HomeXState = 6, HomeYState = 6;
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
            if (command == "P801" || command == "P802") return new CommandResult((command == "P801" ? HomeXState : HomeYState).ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
            return new CommandResult(command.EndsWith("P") ? "0" : command.EndsWith("?") ? "882000000401" : "", 0);
        }
        public void SetSpeed(int motor, decimal speed) { }
        public void Jog(int motor, int direction, decimal speed, decimal? step) { Commands.Add("jog"); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, Func<bool> cancelled) { if (!cancelled()) Jog(motor, direction, speed, step); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, decimal acceleration) { Jog(motor, direction, speed, step); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, decimal acceleration, Func<bool> cancelled) { if (!cancelled()) Jog(motor, direction, speed, step, acceleration); }
        public void Move(int motor, decimal target, decimal speed) { }
        public void Move(int motor, decimal target, decimal speed, Func<bool> cancelled) { if (!cancelled()) Move(motor, target, speed); }
        public void Move(int motor, decimal target, decimal speed, decimal acceleration) { }
        public void Move(int motor, decimal target, decimal speed, decimal acceleration, Func<bool> cancelled) { if (!cancelled()) Move(motor, target, speed, acceleration); }
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
        public AxisSample ReadAxisFeedback(int motor)
        {
            if (FailRead) throw new InvalidOperationException("read failed");
            bool x = motor == 1;
            return new AxisSample(motor, x ? Snapshot.XCounts : Snapshot.YCounts, null, DateTime.Now)
            { Status = x ? Snapshot.XStatus : Snapshot.YStatus, TimeMs = MonotonicClock.NowMs };
        }
        public double StartPlan(MotionPlan plan, double lag, Func<bool> cancelled)
        {
            if (DuringStart != null) DuringStart();
            if (cancelled()) throw new OperationCanceledException();
            StartCalls++; StartTime = MonotonicClock.NowMs; return StartTime;
        }
        public void StopPlan(int cs, int x, int y) { CoordinateSystem = cs; StopPlanCalls++; Stop(new int[] { x, y }); }
        public void SetEnabled(int motor, bool enabled) { }
        public void Home(int x, int y, bool both, int plc) { Commands.Add("home"); HomeXState = 1; HomeYState = 0; }
        public void Home(int x, int y, bool both, int plc, Func<bool> cancelled) { if (!cancelled()) Home(x, y, both, plc); }
        public void StopHome(int x, int y, int plc) { StopHomeCalls++; Stop(new int[] { x, y }); }
        public void Dispose() { }
    }

    private sealed class FollowSession : FakeSession, IStreamingTrajectorySession
    {
        public readonly List<MotionPoint> Targets = new List<MotionPoint>();
        public readonly List<double> Durations = new List<double>();
        private readonly Queue<double> due = new Queue<double>();
        public double Time, BusyUntil, EndTime;
        public int Begins, Aborts, Retries, MaxDepth;
        public bool FailAppend;
        public int Capacity = 3;
        private void Append(double duration, MotionPoint target)
        {
            if (FailAppend) throw new InvalidOperationException("injected communication fault");
            while (due.Count > 0 && due.Peek() <= Time) due.Dequeue();
            if (Time < BusyUntil || due.Count >= Capacity) { Retries++; throw new FollowBufferBusyException(); }
            EndTime = Math.Max(Time, EndTime) + duration; due.Enqueue(EndTime);
            MaxDepth = Math.Max(MaxDepth, due.Count); Targets.Add(target); Durations.Add(duration);
        }
        public void BeginPvtFollow(int cs, int x, int y, CanvasSettings mapping) { Begins++; }
        public void AppendPvtFollow(double duration, MotionPoint end, MotionPoint velocity) { Append(duration, end); }
        public void EndPvtFollow() { }
        public void AbortPvtFollow(int cs, int x, int y) { Aborts++; due.Clear(); }
        public void BeginSplineFollow(int cs, int x, int y, CanvasSettings mapping) { Begins++; }
        public void AppendSplineFollow(double duration, IList<MotionPoint> points)
        { foreach (MotionPoint point in points) Append(duration, point); }
        public void EndSplineFollow() { }
        public void AbortSplineFollow(int cs, int x, int y) { Aborts++; due.Clear(); }
    }

    private static void FollowFlowControl()
    {
        foreach (InterpolationMode mode in new InterpolationMode[] { InterpolationMode.PVT, InterpolationMode.Spline })
        {
            FollowSession session = new FollowSession();
            CanvasFollower follower = new CanvasFollower(session, delegate { return session.Time; });
            follower.Begin(1, 2, 5, CanvasSettings.Default, 1, CanvasFollower.DefaultDelayMs, new MotionPoint(0, 0), mode);
            List<MotionPoint> original = new List<MotionPoint>();
            // Eight seconds at 1000 mouse events/s, with a consumer that holds
            // just three moves. No wall-clock sleep or UI timer is involved.
            for (int t = 0; t <= 8000; t++)
            {
                session.Time = t;
                double angle = t * Math.PI / 2000;
                MotionPoint point = new MotionPoint(2 * Math.Sin(angle), 2 * (1 - Math.Cos(angle)));
                original.Add(point); follower.SubmitMm(point);
                if (t % 20 == 0) { follower.Pump(); session.Flush(); }
            }
            for (int t = 8020; t <= 14000 && follower.PendingPointCount > 0; t += 20)
            { session.Time = t; follower.Pump(); session.Flush(); }
            Check(follower.Active && follower.Error == null && follower.PendingPointCount == 0, mode + " sustained dense drawing drains without a backlog stop");
            Check(session.Targets.Count < 300 && session.Targets.Count > 50, mode + " resamples dense input rather than transmitting every mouse event");
            Check(session.Aborts == 0 && session.Begins == 1 && session.MaxDepth <= 3,
                mode + " never restarts or overfills the streaming consumer");
            Check(PathGeometry.Distance(session.Targets[session.Targets.Count - 1], original[original.Count - 1]) < 1e-9,
                mode + " retains the final endpoint while retrying");
            double maximumError = 0;
            foreach (MotionPoint point in original)
            {
                double nearest = Double.PositiveInfinity; MotionPoint start = new MotionPoint(0, 0);
                foreach (MotionPoint end in session.Targets)
                {
                    double dx = end.X - start.X, dy = end.Y - start.Y, length2 = dx * dx + dy * dy;
                    double u = length2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / length2));
                    nearest = Math.Min(nearest, PathGeometry.Distance(point, new MotionPoint(start.X + u * dx, start.Y + u * dy)));
                    start = end;
                }
                maximumError = Math.Max(maximumError, nearest);
            }
            Check(maximumError <= 0.02001, mode + " simplification keeps the input polyline within the 0.02 mm contour budget");
            if (mode == InterpolationMode.Spline)
                Check(session.Durations.TrueForAll(delegate(double d) { return d == 60; }), "Spline1 keeps a fixed TM with no overlapping control-point replay");
            int delivered = session.Targets.Count;
            follower.SubmitMm(new MotionPoint(1, 1)); follower.Cancel(); session.Flush();
            session.Time += 1000; follower.Pump(); session.Flush();
            Check(!follower.Active && session.Aborts == 1 && session.Targets.Count == delivered, mode + " cancellation drops unsent input without replay");
        }

        FollowSession retry = new FollowSession { BusyUntil = 500 };
        CanvasFollower held = new CanvasFollower(retry, delegate { return retry.Time; });
        held.Begin(1, 2, 5, CanvasSettings.Default, 1, 0, new MotionPoint(0, 0));
        held.SubmitMm(new MotionPoint(1, 0)); held.Pump(); retry.Flush();
        Check(held.Active && held.WaitingForBuffer && retry.Targets.Count == 0, "A busy segment stays pending without consuming or stopping it");
        retry.Time = 500; held.Pump(); retry.Flush();
        Check(retry.Targets.Count == 1 && retry.Targets[0].X == 1 && retry.Begins == 1, "The exact pending segment is delivered once when capacity returns");
        retry.FailAppend = true; retry.Time = 1000; held.SubmitMm(new MotionPoint(2, 0)); held.Pump(); retry.Flush();
        Check(!held.Active && retry.Aborts == 1 && held.Error.Contains("communication"), "A genuine communication fault still aborts immediately");

        FollowSession corners = new FollowSession { Capacity = 20 };
        CanvasFollower trace = new CanvasFollower(corners, delegate { return corners.Time; });
        trace.Begin(1, 2, 5, CanvasSettings.Default, 1, 100, new MotionPoint(0, 0));
        MotionPoint[] shape = { new MotionPoint(1, 0), new MotionPoint(1, 1), new MotionPoint(0, 1), new MotionPoint(0, 0), new MotionPoint(-1, 0) };
        foreach (MotionPoint point in shape) { corners.Time += 5; trace.SubmitMm(point); }
        for (int t = 200; t < 2000 && trace.PendingPointCount > 0; t += 20) { corners.Time = t; trace.Pump(); corners.Flush(); }
        bool preserved = corners.Targets.Count == shape.Length;
        for (int i = 0; preserved && i < shape.Length; i++) preserved &= PathGeometry.Distance(corners.Targets[i], shape[i]) < 1e-9;
        Check(preserved, "Short-window simplification retains sharp corners, loops and reversals in order");
        corners.Time = 2100; trace.SubmitMm(new MotionPoint(0, 0)); corners.Time = 17200; trace.Pump(); corners.Flush();
        Check(!trace.Active && trace.Error.Contains("15 秒"), "An input queue that is actually stale for 15 seconds still stops");
    }

    private static void AutomaticFollowSpeed()
    {
        CanvasSettings mapping = new CanvasSettings(-20, 20, -20, 20, 200, 800, "mm");
        foreach (InterpolationMode mode in new InterpolationMode[] { InterpolationMode.PVT, InterpolationMode.Spline })
        {
            FollowSession session = new FollowSession { Capacity = 20 };
            CanvasFollower follower = new CanvasFollower(session, delegate { return session.Time; });
            follower.BeginAuto(1, 2, 15, 15, 25, 30, mapping, 1, 100, new MotionPoint(0, 0), mode);
            session.Time = 1000; follower.SubmitMm(new MotionPoint(3, 4)); // 5 mm / 1 s
            session.Time = 1500; follower.SubmitMm(new MotionPoint(6, 8)); // 5 mm / 0.5 s
            double slowTime = 0, fastTime = 0; bool first = true;
            int observed = 0;
            for (int t = 1600; t <= 5000 && follower.PendingPointCount > 0; t += 20)
            {
                session.Time = t; follower.Pump(); session.Flush();
                while (observed < session.Targets.Count)
                {
                    MotionPoint target = session.Targets[observed];
                    if (first) slowTime += session.Durations[observed]; else fastTime += session.Durations[observed];
                    if (PathGeometry.Distance(target, new MotionPoint(3, 4)) < 1e-8) first = false;
                    observed++;
                }
            }
            Check(follower.Active && follower.PendingPointCount == 0 && slowTime >= 1000 - 1e-7 && slowTime <= 1060 && fastTime >= 500 - 1e-7 && fastTime <= 560,
                mode + " derives both slow and fast move durations from mouse timestamps, with only fixed spline time quantization; slow=" + slowTime + ", fast=" + fastTime);
            Check(Math.Abs(follower.MouseSpeedMmPerSecond - 10) < 1e-8 && !follower.SpeedLimited,
                mode + " automatically increases reference speed when the mouse draws twice as fast");
            follower.Cancel(); session.Flush();

            session = new FollowSession { Capacity = 20 };
            FollowSession limitedSession = session;
            follower = new CanvasFollower(session, delegate { return limitedSession.Time; });
            follower.BeginAuto(1, 2, 1, 0.8M, mapping, 1, 100, new MotionPoint(0, 0), mode);
            session.Time = 100; follower.SubmitMm(new MotionPoint(2, 2));
            session.Time = 200; follower.Pump(); session.Flush();
            Check(follower.SpeedLimited && Math.Abs(follower.MouseSpeedMmPerSecond - Math.Sqrt(8) * 10) < 1e-7 &&
                follower.FollowSpeedMmPerSecond <= Math.Sqrt(8) / 2 + 1e-8,
                mode + " converts unequal axis scales and limits automatically to the slower configured Y axis");
            for (int t = 220; t < 4000 && follower.PendingPointCount > 0; t += 20)
            { session.Time = t; follower.Pump(); session.Flush(); }
            double total = 0;
            foreach (double duration in session.Durations) total += duration;
            Check(follower.Active && follower.PendingPointCount == 0 && total >= 3000 - 1e-7 && total <= 3060 &&
                PathGeometry.Distance(session.Targets[session.Targets.Count - 1], new MotionPoint(2, 2)) < 1e-8,
                mode + " stretches an overspeed mouse stroke while preserving its endpoint and safe axis timing");
            follower.Cancel(); session.Flush();
        }
    }

    private static void ConstraintMath()
    {
        double peak = TrajectoryMath.PeakVelocity(0, 1, 0, 10, 0.1);
        Check(peak > 13.3 && peak < 13.34, "Cubic PVT peak velocity is evaluated inside the segment");
        Check(TrajectoryMath.PeakAcceleration(0, 1, 0, 10, 0.1) > 0, "Cubic PVT acceleration includes the segment interior");
        UniformSplineStream stream = new UniformSplineStream(new MotionPoint(0, 0), 60);
        MotionSegment first = stream.Append(new MotionPoint(0.06, 0));
        MotionSegment second = stream.Append(new MotionPoint(0.06, 0.06));
        Check(PathGeometry.Distance(first.EndVelocity, second.StartVelocity) < 1e-9, "Uniform spline keeps shared velocity continuous");
        Check(stream.End().Count == 2, "Uniform spline emits explicit stopping tail segments");
        MotionPlan constrained = MotionPlanningService.Build(PathGeometry.Line(0, 0, 1, 1), new PlanningOptions
        { Speed = 10, Acceleration = 100, Spacing = 0.25, MaximumXSpeed = 0.5, MaximumYSpeed = 0.25,
            MaximumXAcceleration = 0.1, MaximumYAcceleration = 0.05 }, new MotionPoint(0, 0));
        double maxX = 0, maxY = 0;
        foreach (MotionSegment segment in constrained.Segments)
            for (int i = 0; i <= 20; i++) { MotionPoint v = segment.VelocityAt(i / 20.0); maxX = Math.Max(maxX, Math.Abs(v.X)); maxY = Math.Max(maxY, Math.Abs(v.Y)); }
        Check(maxX <= 0.5 + 1e-6 && maxY <= 0.25 + 1e-6, "Complete PVT honors independent axis velocity limits");
        DeviceConfiguration configuration = new DeviceConfiguration();
        configuration.Axis(1).MaximumAcceleration = 25;
        configuration.Validate();
        Check(configuration.Axis(1).MaximumAcceleration == 25, "Device configuration accepts an acceleration above 12 ct/ms squared");
        foreach (InterpolationMode mode in new InterpolationMode[] { InterpolationMode.PVT, InterpolationMode.Spline })
        {
            DrawingPath circle = PathGeometry.Arc(0, 0, 25, 0, 360, 0.01);
            PlanningOptions options = new PlanningOptions { Mode = mode, Speed = 5, Spacing = 1,
                Mapping = new CanvasSettings(-30, 30, -30, 30, 1000, 1000, "mm"),
                MaximumXSpeed = 50, MaximumYSpeed = 50, MaximumXAcceleration = 0.00001, MaximumYAcceleration = 0.00001 };
            MotionPlan slow = MotionPlanningService.Build(circle, options, new MotionPoint(0, 0));
            options.MaximumXAcceleration = options.MaximumYAcceleration = 100000;
            MotionPlan relaxed = MotionPlanningService.Build(circle, options, new MotionPoint(0, 0));
            Check(slow.Program == relaxed.Program, mode + " complete circle and transfer ignore configured acceleration ceilings");
            options.Speed = 10;
            options.MaximumXAcceleration = options.MaximumYAcceleration = 0.00001;
            MotionPlan fast = MotionPlanningService.Build(circle, options, new MotionPoint(0, 0));
            Check(fast.DurationMs < slow.DurationMs * 0.6, mode + " circle responds to a higher target speed despite low acceleration settings");
            if (mode == InterpolationMode.Spline)
                Check(fast.Blocks[1].SplineTimeMs < 100, "Spline circle TM no longer stalls at 264.744 ms");
        }
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
    private static void PlcHoming()
    {
        FakeSession session = new FakeSession(); OperationState state = new OperationState();
        using (FeedbackService feedback = new FeedbackService(session))
        using (MotionWorkflow flow = new MotionWorkflow(session, state, feedback))
        {
            flow.Configure(false, CanvasSettings.Default, true, 1, 2, 1);
            session.Snapshot = Sample(0, 0, 0, false, false);
            session.Snapshot.XStatus = MotorStatus.Parse("C82000000000"); session.Snapshot.YStatus = MotorStatus.Parse("882000000001");
            flow.Poll(); session.Flush();
            Check(flow.CanStartHoming(true), "PLC home permits a stationary negative-limit axis without InPosition");
            int completed = 0; flow.HomeCompleted += delegate { completed++; };
            flow.StartHoming(2, 1, 10, true); session.Flush(); flow.Poll(); session.Flush();
            Check(state.Homing && completed == 0, "PLC home starts without rejecting the expected negative limit");
            session.Snapshot = Sample(0, 0, 0, true, false); session.HomeXState = 6; session.HomeYState = 3;
            flow.Poll(); session.Flush();
            Check(state.Homing && completed == 0, "X complete and both Home Complete bits cannot finish the Y PLC sequence");
            session.HomeYState = 6; session.Snapshot = Sample(0, 0, 0, false, false); flow.Poll(); session.Flush();
            Check(state.Homing, "PLC completion states still require both axes to arrive");
            session.Snapshot = Sample(0, 0, 0, true, false);
            session.Snapshot.XStatus = session.Snapshot.YStatus = MotorStatus.Parse("882000000001");
            flow.Poll(); session.Flush();
            Check(completed == 1 && !flow.IsExecuting && flow.StatusText.Contains("P801=6"), "PLC completion plus arrival works after HOMEZ without Home Complete bits");
            flow.StartHoming(2, 1, 0.001, true); session.Flush(); Thread.Sleep(5); flow.Poll(); session.Flush();
            Check(session.StopHomeCalls == 1 && state.Stopping, "PLC home timeout requests a stop"); ConfirmStopped(flow, session);
            flow.StartHoming(2, 1, 10, true); session.Flush(); session.HomeXState = 7; flow.Poll(); session.Flush();
            Check(session.StopHomeCalls == 2 && state.Stopping, "Invalid PLC state cannot claim homing complete"); ConfirmStopped(flow, session);
            flow.StartHoming(2, 1, 10, true); session.Flush();
            Action recover = flow.PrepareRecovery(); recover();
            Check(session.Commands.Contains("P801=0") && session.Commands.Contains("P802=0") && !session.Commands.Contains("DISABLE PLC 1"),
                "Recovery clears both PLC state machines while keeping PLC1 running");
            flow.CompleteRecovery();
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
            string profile = Path.Combine(directory, "profile.xml");
            DeviceConfiguration configuration = new DeviceConfiguration();
            configuration.Axis(1).MaximumAcceleration = 25;
            DeviceConfigurationStore.Save(profile, configuration);
            Check(DeviceConfigurationStore.Load(profile).Axis(1).MaximumAcceleration == 25,
                "Acceleration values above 12 survive profile save and load");
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

    private static void PointToPointProfile()
    {
        ManualMotionProfile profile = Operations_Control.Motion.PointToPointProfile.Calculate(1, 0.05);
        Check(profile.AccelerationTimeMs >= 20 && profile.SCurveTimeMs > 0 && 2 * profile.SCurveTimeMs < profile.AccelerationTimeMs,
            "Point-to-point profile derives a valid acceleration and S-curve time");
        SCurveTrajectory longMove = new SCurveTrajectory(0, 100, 1, profile.MaximumAcceleration, profile.AccelerationTimeMs, profile.SCurveTimeMs);
        Check(Operations_Control.Motion.PointToPointProfile.EstimateDuration(100, 1, profile) == longMove.DurationMs,
            "Point-to-point wait time uses the same distance, speed and acceleration relation");
        Check(longMove.PositionAt(0) == 0 && longMove.PositionAt(longMove.DurationMs) == 100 && longMove.DurationMs > 100,
            "Long point-to-point move reaches its target with a bounded acceleration profile");
        double previous = longMove.PositionAt(0);
        for (int i = 1; i <= 100; i++)
        {
            double current = longMove.PositionAt(longMove.DurationMs * i / 100);
            Check(current >= previous - 0.000001, "Point-to-point profile remains monotonic");
            previous = current;
        }
        SCurveTrajectory shortMove = new SCurveTrajectory(10, 10.1, 1, profile.MaximumAcceleration, profile.AccelerationTimeMs, profile.SCurveTimeMs);
        Check(shortMove.PositionAt(shortMove.DurationMs / 2) > 10 && shortMove.PositionAt(shortMove.DurationMs) == 10.1,
            "Short point-to-point move uses a triangular profile without overshoot");
        ManualMotionProfile maximum = Operations_Control.Motion.PointToPointProfile.Calculate(50, 12);
        Check(maximum.MaximumAcceleration == 12, "Manual profile accepts the requested 50 ct/ms software maximum");
        bool excessiveSpeed = false;
        try { Operations_Control.Motion.PointToPointProfile.Calculate(50.001, 12); } catch (ArgumentOutOfRangeException) { excessiveSpeed = true; }
        Check(excessiveSpeed, "Manual profile rejects values above the 50 ct/ms software cap");
    }
    private static DiagnosticSample ErrorSample(int motor, double time, double error)
    { return new DiagnosticSample { Motor = motor, TimeMs = time, FollowingError = error, Status = new MotorStatus { FirstWord = 0x880000 } }; }
    private static void Near(double actual, double expected, string message)
    { Check(Math.Abs(actual - expected) <= 1e-8 * Math.Max(1, Math.Abs(expected)), message + "; expected=" + expected + ", actual=" + actual); }
    private static void DiagnosticErrors()
    {
        DiagnosticRecording ramp = new DiagnosticRecording(); ramp.Samples.Add(ErrorSample(1, 0, -10)); ramp.Samples.Add(ErrorSample(2, 0, 99));
        ramp.Samples.Add(ErrorSample(1, 1000, 10)); ramp.Samples.Add(ErrorSample(2, 1000, 99));
        DiagnosticErrorResult result = DiagnosticErrorAnalysis.Analyze(ramp, 1, 0, 1000, 5);
        Near(result.Mean, 0, "Signed mean cancels a symmetric ramp");
        Near(result.Rms, 10 / Math.Sqrt(3), "RMS integrates squared linear error rather than averaging endpoint squares");
        Near(result.MaximumAbsolute, 10, "Absolute peak uses only the selected motor"); Near(result.MaximumTimeMs, 0, "Tied peaks select the earliest timestamp");
        Near(result.PeakToPeak, 20, "Peak-to-peak retains both error signs"); Near(result.AbsoluteP95, 9.5, "P95 is weighted by time, including zero crossings");
        Near(result.ExceededDurationMs.Value, 500, "Tolerance crossing is interpolated within an interval"); Near(result.ExceededRatio.Value, 0.5, "Exceedance ratio divides by valid duration");
        Near(result.Score.Value, 100 / (1 + 0.4 * (10 / Math.Sqrt(3) / 5) + 0.25 * 9.5 / 5 + 0.15 * 10 / 5 + 0.2 * 0.5), "Score follows its disclosed formula");
        result = DiagnosticErrorAnalysis.Analyze(ramp, 1, 250, 750, 5);
        Near(result.MaximumAbsolute, 5, "Window boundaries interpolate endpoint errors"); Near(result.MaximumTimeMs, 250, "Peak timestamps use absolute CSV time after clipping");
        Near(result.ValidDurationMs, 500, "Only the requested duration contributes"); Near(result.Rms, 5 / Math.Sqrt(3), "Changing the window changes RMS");
        Near(result.AbsoluteP95, 4.75, "Changing the window changes P95"); Near(result.ExceededRatio.Value, 0, "Equality with tolerance does not count as exceedance");
        DiagnosticRecording uneven = new DiagnosticRecording(); uneven.Samples.Add(ErrorSample(1, 0, 0)); uneven.Samples.Add(ErrorSample(1, 900, 0)); uneven.Samples.Add(ErrorSample(1, 1000, 10));
        result = DiagnosticErrorAnalysis.Analyze(uneven, 1, 0, 1000, null);
        Near(result.Mean, 0.5, "Uneven sample intervals weight the mean by duration"); Near(result.Rms, Math.Sqrt(10.0 / 3), "Uneven sample intervals weight RMS by duration");
        Near(result.AbsoluteP95, 5, "Time-weighted P95 does not overweight dense sampling");
        Check(result.HasData && !result.Score.HasValue && !result.ExceededRatio.HasValue, "Statistics remain available without an arbitrary tolerance or score");
        DiagnosticRecording constant = new DiagnosticRecording(); constant.Samples.Add(ErrorSample(1, 0, 2)); constant.Samples.Add(ErrorSample(1, 100, 2));
        DiagnosticSample gap = ErrorSample(1, 200, 2); gap.GapBefore = true; constant.Samples.Add(gap); constant.Samples.Add(ErrorSample(1, 300, 2));
        result = DiagnosticErrorAnalysis.Analyze(constant, 1, 0, 300, 1);
        Near(result.ValidDurationMs, 200, "Gap intervals do not contribute zero error or fabricated data"); Near(result.Coverage, 2.0 / 3, "Coverage reports omitted time");
        Near(result.Mean, 2, "Constant errors retain their signed bias"); Near(result.Rms, 2, "Constant error has the same RMS"); Near(result.AbsoluteP95, 2, "Constant error P95 equals the constant");
        Near(result.ExceededRatio.Value, 1, "All valid time above tolerance is counted");
        double lowScore = result.Score.Value;
        result = DiagnosticErrorAnalysis.Analyze(constant, 1, 0, 300, 2);
        Near(result.ExceededRatio.Value, 0, "Constant error exactly on tolerance is accepted"); Check(result.Score.Value > lowScore, "Score responds to the chosen tolerance");
        constant.Samples[2].FollowingError = 99; constant.Samples[3].GapBefore = true;
        result = DiagnosticErrorAnalysis.Analyze(constant, 1, 0, 300, 2);
        Near(result.MaximumAbsolute, 99, "An isolated valid peak next to missing data remains visible in peak statistics");
        Near(result.Rms, 2, "Isolated peaks do not fabricate time-weighted intervals across gaps");
        DiagnosticRecording zero = new DiagnosticRecording(); zero.Samples.Add(ErrorSample(1, 100, 0)); zero.Samples.Add(ErrorSample(1, 200, 0));
        result = DiagnosticErrorAnalysis.Analyze(zero, 1, 100, 200, 1);
        Near(result.Score.Value, 100, "Zero error earns exactly 100"); Near(result.MaximumTimeMs, 100, "Zero error peak time remains inside the valid data");
        result = DiagnosticErrorAnalysis.Analyze(zero, 1, 0, 500, 1); Near(result.Coverage, 0.2, "Time outside the record is not treated as zero error");
        zero.Samples[1].Status.FirstWord |= 0x040000;
        Check(!DiagnosticErrorAnalysis.Analyze(zero, 1, 0, 500, 1).HasData, "Open-loop feedback cannot create a perfect score");
        zero.Samples[1].Status.FirstWord = 0x800000;
        Check(!DiagnosticErrorAnalysis.Analyze(zero, 1, 0, 500, 1).HasData, "Disabled feedback is excluded");
        zero.Samples[1].Status.FirstWord = 0x880000; zero.Samples[1].FollowingError = null;
        Check(!DiagnosticErrorAnalysis.Analyze(zero, 1, 0, 500, 1).Score.HasValue, "Missing errors cannot produce a score");
        zero.Samples[1].FollowingError = 0; zero.Samples[1].TimeMs = 2000;
        Check(!DiagnosticErrorAnalysis.Analyze(zero, 1, 0, 2500, 1).HasData, "Long communication gaps are excluded");
        Check(!DiagnosticErrorAnalysis.Analyze(zero, 3, 0, 2500, 1).HasData, "Absent motor data is reported as unavailable");
        zero.Samples[1].TimeMs = 200; zero.Samples[0].FollowingError = zero.Samples[1].FollowingError = 1e200;
        result = DiagnosticErrorAnalysis.Analyze(zero, 1, 100, 200, 1);
        Check(!Double.IsInfinity(result.Rms) && !Double.IsNaN(result.Score.Value), "Large finite errors cannot overflow RMS or produce a NaN score");
        bool invalid = false; try { DiagnosticErrorAnalysis.Analyze(ramp, 1, 1000, 0, 5); } catch (ArgumentException) { invalid = true; }
        Check(invalid, "Invalid time ranges are rejected");
    }
    private static int Main()
    {
        try { Execution(); Cancellation(); Homing(); PlcHoming(); FeedbackAndManual(); Files(); PointToPointProfile(); FollowFlowControl(); AutomaticFollowSpeed(); ConstraintMath(); DiagnosticErrors(); Console.WriteLine("PASS: " + checks + " module assertions; no UI or hardware accessed."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
