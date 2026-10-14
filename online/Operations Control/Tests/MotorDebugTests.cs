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
        public int Reads;
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
            AxisSample axis = Axes[motor - 1]; string response = command.EndsWith("P") ? axis.Position.ToString(CultureInfo.InvariantCulture) :
                command.EndsWith("?") ? axis.Status.FirstWord.ToString("X6") + axis.Status.SecondWord.ToString("X6") : "0";
            return new CommandResult(response, 0);
        }
        public void SetSpeed(int motor, decimal speed) { }
        public void Jog(int motor, int direction, decimal speed, decimal? step)
        { Commands.Add("jog" + motor); Set(motor, Axes[motor - 1].Position, Status(false, true, false, false, false)); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, Func<bool> cancelled) { if (!cancelled()) Jog(motor, direction, speed, step); }
        public void Move(int motor, decimal target, decimal speed)
        { Commands.Add("move" + motor); Set(motor, Axes[motor - 1].Position, Status(false, true, false, false, false)); }
        public void Move(int motor, decimal target, decimal speed, Func<bool> cancelled) { if (!cancelled()) Move(motor, target, speed); }
        public void MovePair(int x, int y, MotionPoint point) { }
        public void ZeroPosition(int motor) { Commands.Add("zero" + motor); Set(motor, 0, Status(true, true, false, false, false)); }
        public void Stop(IEnumerable<int> motors)
        { Commands.Add("stop"); if (FailStop) throw new InvalidOperationException("stop failed"); foreach (int motor in motors) if (StopImmediately) Set(motor, Axes[motor - 1].Position, Status(true, true, false, false, false)); }
        public void Download(string path) { }
        public ControllerSnapshot ReadSnapshot(int x, int y, int cs)
        { double now = MonotonicClock.NowMs; return new ControllerSnapshot { XCounts = Axes[x - 1].Position, YCounts = Axes[y - 1].Position, XStatus = Axes[x - 1].Status, YStatus = Axes[y - 1].Status, XTimeMs = now, YTimeMs = now, Coordinate = new CoordinateStatus { SecondWord = 0x20000 } }; }
        public ControllerFeedback ReadFeedback(int x, int y, int selected, int cs)
        {
            Reads++; ControllerSnapshot snapshot = ReadSnapshot(x, y, cs); List<AxisSample> samples = new List<AxisSample>();
            foreach (int motor in new int[] { x, y, selected })
            { AxisSample source = Axes[motor - 1]; samples.Add(new AxisSample(motor, source.Position, 0, DateTime.Now) { Status = source.Status, FollowingError = source.FollowingError, TimeMs = snapshot.TimeMs }); }
            return new ControllerFeedback(snapshot, samples.ToArray());
        }
        public double StartPlan(MotionPlan plan, double lag, Func<bool> cancelled) { return MonotonicClock.NowMs; }
        public void StopPlan(int cs, int x, int y) { Stop(new int[] { x, y }); }
        public void SetEnabled(int motor, bool enabled) { Commands.Add(enabled ? "enable" : "disable"); Set(motor, Axes[motor - 1].Position, Status(true, enabled, false, false, false)); }
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
            form.Close(); Until(delegate { return form.IsDisposed; }, "Verified idle application closes");
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Guarding(); ManualMotion(); TimeoutAndAlarms(); ControllerProtocol();
            string directory = Path.Combine(Path.GetTempPath(), "pmac-motor-tests-" + Guid.NewGuid().ToString("N")); ConfigurationAndLogs(directory);
            Interface(args.Length == 0 ? directory : args[0]);
            Console.WriteLine("PASS: " + checks + " motor debugging assertions; simulation only, no hardware accessed."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
