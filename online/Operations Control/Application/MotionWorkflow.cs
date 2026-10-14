using System;
using System.Threading;
using Operations_Control.Communication;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Application
{
    // Application-thread state machine. Controller work is serialized by the session.
    public sealed class MotionWorkflow : IDisposable
    {
        private readonly IControllerSession session;
        private readonly FeedbackService feedback;
        private readonly OperationState state;
        private CanvasSettings mapping;
        private int xMotor, yMotor, selectedMotor, generation;
        private bool simulation, mappingReady, disposed, planOwned, homeOwned;
        private double runStart, homeStart, homeTimeout;
        private int homeX, homeY, activePlc = -1;
        private bool homeBoth, homeSawX, homeSawY;
        private Action<Exception> stopCompleted;
        public MotionPlan Plan { get; set; }
        public TrajectoryRun CurrentRun { get; private set; }
        public ControllerSnapshot LatestSnapshot { get; private set; }
        public string StatusText { get; private set; }
        public bool StatusError { get; private set; }
        public bool IsExecuting { get { return state.WorkflowExecuting; } }
        public bool IsBusy { get { return state.WorkflowBusy; } }
        public bool Fresh { get { return LatestSnapshot != null && MonotonicClock.NowMs - LatestSnapshot.TimeMs <= 1000; } }
        public bool HardwareReady(bool confirmed) { return simulation || (mappingReady && confirmed); }
        public event EventHandler StateChanged, StatusChanged, SnapshotChanged, HomeCompleted;

        public MotionWorkflow(IControllerSession session, OperationState state, FeedbackService feedback)
        {
            this.session = session; this.state = state; this.feedback = feedback;
            feedback.Updated += FeedbackUpdated; feedback.Failed += FeedbackFailed;
        }

        public void Configure(bool simulated, CanvasSettings settings, bool ready, int x, int y, int selected)
        {
            if (simulation == simulated && mapping == settings && mappingReady == ready && xMotor == x && yMotor == y)
            {
                if (selectedMotor != selected) { selectedMotor = selected; feedback.Clear(); }
                return;
            }
            if (IsBusy) return;
            CancelPendingWork();
            simulation = simulated; mapping = settings; mappingReady = ready; xMotor = x; yMotor = y; selectedMotor = selected;
            Plan = null; LatestSnapshot = null; feedback.Clear();
        }
        public void CancelPendingWork() { Interlocked.Increment(ref generation); }
        public void Poll()
        {
            if (!disposed && state.CanPoll) feedback.Poll(xMotor, yMotor, selectedMotor, state.Running ? CurrentRun.Plan.Options.CoordinateSystem : 0);
        }
        private void Changed() { if (!disposed && StateChanged != null) StateChanged(this, EventArgs.Empty); }
        private void Report(string message, bool error)
        {
            StatusText = message; StatusError = error;
            if (!disposed && StatusChanged != null) StatusChanged(this, EventArgs.Empty);
        }

        public void StartExecution(double simulatorLagMs, bool confirmed)
        {
            if (disposed || IsBusy || state.ExternalBusy || Plan == null) return;
            if (!session.IsConnected || !Fresh || !HardwareReady(confirmed)) throw new InvalidOperationException("需连接、有效反馈和已确认的实机配置。");
            if (!LatestSnapshot.Ready || !LatestSnapshot.InPosition) throw new InvalidOperationException("轴尚未闭环就绪或未到位。");
            MotionPlan selected = Plan; int version = generation;
            CurrentRun = new TrajectoryRun { Plan = selected, Simulation = simulation, StartedUtc = DateTime.UtcNow, Result = "启动中" };
            state.CommandBusy = true; planOwned = true; Changed(); Report("正在准备并启动路径…", false);
            double start = 0, duration = 0;
            session.Execute(delegate
            {
                if (Thread.VolatileRead(ref generation) != version) return;
                start = session.StartPlan(selected, simulatorLagMs, delegate { return Thread.VolatileRead(ref generation) != version; });
                duration = MonotonicClock.NowMs - start;
            }, delegate(Exception error)
            {
                if (disposed || version != generation) return;
                state.CommandBusy = false;
                if (error != null) { CurrentRun.Result = "启动失败：" + error.Message; Report(CurrentRun.Result, true); StopWithReason(CurrentRun.Result); }
                else { runStart = start; CurrentRun.StartCommandMs = duration; state.Running = true; CurrentRun.Result = "运行中"; Report("运行中", false); }
                Changed();
            });
        }

        private void FeedbackUpdated(object sender, EventArgs e)
        {
            if (disposed) return;
            ControllerSnapshot sample = feedback.Latest.Snapshot; LatestSnapshot = sample;
            if (state.Running)
            {
                MotionPoint p = mapping.FromCounts(sample.XCounts, sample.YCounts);
                CurrentRun.Samples.Add(new TrajectorySample(sample.TimeMs - runStart, sample.XTimeMs - runStart, sample.YTimeMs - runStart, sample.QueryMs, p));
                if (!sample.Ready || !mapping.Contains(p) || (sample.Coordinate != null && sample.Coordinate.Fault)) StopWithReason("运行中检测到轴故障、限位、失能、反馈越界或坐标系运行错误。");
                else if (sample.TimeMs - runStart >= CurrentRun.Plan.DurationMs && sample.InPosition && sample.Coordinate != null && !sample.Coordinate.Running && sample.Coordinate.InPosition)
                {
                    if (PathGeometry.Distance(p, CurrentRun.Plan.At(CurrentRun.Plan.DurationMs)) > 0.05) StopWithReason("结束位置与规划相差超过 0.05 mm，需检查执行状态。");
                    else { state.Running = false; planOwned = false; CurrentRun.Completed = true; CurrentRun.Result = "完成"; Report("运行完成", false); }
                }
                else if (sample.TimeMs - runStart > CurrentRun.Plan.DurationMs + Math.Max(10000, CurrentRun.Plan.DurationMs * 0.5)) StopWithReason("运动超时，已请求停止。");
                if (CurrentRun.Samples.Count >= 100000) StopWithReason("采样达到 100000 点上限，已请求停止并保留数据。");
            }
            if (state.Homing) MonitorHome(sample);
            if (SnapshotChanged != null) SnapshotChanged(this, EventArgs.Empty);
            Changed();
        }
        private void FeedbackFailed(Exception error)
        {
            LatestSnapshot = null;
            if (state.Running || state.Homing) StopWithReason("通信失败：" + error.Message);
            else Report(error.Message, true);
            Changed();
        }

        public void EnableAxes(bool enabled, bool confirmed)
        {
            if (disposed || IsBusy || state.ExternalBusy || !HardwareReady(confirmed) || !session.IsConnected) return;
            state.CommandBusy = true; Plan = null; Changed(); int version = generation, x = xMotor, y = yMotor;
            session.Execute(delegate { if (Thread.VolatileRead(ref generation) == version) { session.SetEnabled(x, enabled); session.SetEnabled(y, enabled); } }, delegate(Exception error)
            {
                if (disposed || version != generation) return;
                state.CommandBusy = false;
                Report(error == null ? enabled ? "已发送 X/Y 闭环使能，请查看状态。" : "已发送 X/Y 失能。" : error.Message, error != null); Changed();
            });
        }

        public void StartHoming(int target, int plc, double timeoutSeconds, bool confirmed)
        {
            if (disposed || IsBusy || state.ExternalBusy || !session.IsConnected || !HardwareReady(confirmed) || !Fresh || !LatestSnapshot.Ready || !LatestSnapshot.InPosition) return;
            if (target < 0 || target > 2 || plc < -1 || plc > 31 || !PlanningOptions.Finite(timeoutSeconds) || timeoutSeconds <= 0) throw new ArgumentException("回零参数无效。");
            if (simulation && plc >= 0) { Report("模拟器验证内置 HOME；用户 PLC 需在实机验收。", true); return; }
            homeX = target == 1 ? yMotor : xMotor; homeY = homeX == xMotor ? yMotor : xMotor;
            homeBoth = target == 2; activePlc = plc; homeTimeout = timeoutSeconds * 1000; homeOwned = true;
            homeSawX = homeSawY = false; Plan = null; state.CommandBusy = true; int version = generation;
            Changed();
            session.Execute(delegate { if (Thread.VolatileRead(ref generation) == version) session.Home(homeX, homeY, homeBoth, plc); }, delegate(Exception error)
            {
                if (disposed || version != generation) return;
                state.CommandBusy = false;
                if (error != null) { Report(error.Message, true); StopWithReason("回零启动失败：" + error.Message); }
                else { state.Homing = true; homeStart = MonotonicClock.NowMs; Report("机械回零中，等待本次寻零状态变化与到位。", false); }
                Changed();
            });
        }
        private void MonitorHome(ControllerSnapshot sample)
        {
            MotorStatus x = homeX == xMotor ? sample.XStatus : sample.YStatus, y = homeY == yMotor ? sample.YStatus : sample.XStatus;
            if (x.Homing || !x.HomeComplete || !x.InPosition) homeSawX = true;
            if (y.Homing || !y.HomeComplete || !y.InPosition) homeSawY = true;
            if (x.Fault || !x.Enabled || (homeBoth && (y.Fault || !y.Enabled))) { StopWithReason("回零检测到故障或失能。"); return; }
            if (homeSawX && x.HomeComplete && x.InPosition && x.VelocityZero && (!homeBoth || (homeSawY && y.HomeComplete && y.InPosition && y.VelocityZero)))
            {
                state.Homing = false; activePlc = -1; homeOwned = false;
                if (HomeCompleted != null) HomeCompleted(this, EventArgs.Empty);
                Report("机械回零完成，Home Complete 与到位状态均已确认。", false);
            }
            else if (MonotonicClock.NowMs - homeStart > homeTimeout) StopWithReason("回零超时或未观察到本次寻零状态变化。");
        }

        public void Stop() { StopWithReason("用户停止"); }
        public void Stop(Action<Exception> completed) { stopCompleted += completed; Stop(); }
        private void StopWithReason(string reason)
        {
            if (state.Stopping || disposed) return;
            CancelPendingWork(); bool wasHome = homeOwned, abortPlan = planOwned;
            int plc = activePlc, x = xMotor, y = yMotor;
            int cs = CurrentRun != null && abortPlan ? CurrentRun.Plan.Options.CoordinateSystem : Plan == null ? 1 : Plan.Options.CoordinateSystem;
            state.Running = false; state.Homing = false; state.Stopping = true;
            if (CurrentRun != null && !CurrentRun.Completed) CurrentRun.Result = reason;
            Changed();
            session.Execute(delegate
            {
                if (!session.IsConnected) return;
                if (wasHome) session.StopHome(x, y, plc);
                else if (abortPlan) session.StopPlan(cs, x, y);
                else session.Stop(new int[] { x, y });
            }, delegate(Exception error)
            {
                if (disposed) return;
                state.Stopping = false; state.CommandBusy = false;
                state.StopFailed = error != null;
                if (error == null) { activePlc = -1; planOwned = false; homeOwned = false; }
                Report(error == null ? reason + "；停止指令已接受。" : reason + "；停止失败：" + error.Message, error != null);
                Changed(); Action<Exception> completed = stopCompleted; stopCompleted = null;
                if (completed != null) completed(error);
            });
        }

        public void Dispose()
        {
            disposed = true; CancelPendingWork();
            feedback.Updated -= FeedbackUpdated; feedback.Failed -= FeedbackFailed;
        }
    }
}
