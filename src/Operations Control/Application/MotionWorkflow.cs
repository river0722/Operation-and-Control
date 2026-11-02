using System;
using System.Globalization;
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
        private bool homeQueryPending;
        private bool recovering;
        private Action<Exception> stopCompleted;
        public MotionPlan Plan { get; set; }
        public TrajectoryRun CurrentRun { get; private set; }
        public ControllerSnapshot LatestSnapshot { get; private set; }
        public string StatusText { get; private set; }
        public bool StatusError { get; private set; }
        public bool IsExecuting { get { return state.WorkflowExecuting; } }
        public bool IsBusy { get { return state.WorkflowBusy; } }
        public bool Fresh { get { return !state.FeedbackStale && LatestSnapshot != null &&
            MonotonicClock.NowMs - Math.Min(LatestSnapshot.XTimeMs, LatestSnapshot.YTimeMs) <= 1000; } }
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
        public Action PrepareRecovery()
        {
            CancelPendingWork(); recovering = true;
            bool abortPlan = planOwned, stopHome = homeOwned;
            int plc = activePlc;
            int cs = CurrentRun != null && abortPlan ? CurrentRun.Plan.Options.CoordinateSystem : Plan == null ? 1 : Plan.Options.CoordinateSystem;
            state.Running = state.Homing = state.Verifying = false;
            state.Stopping = true; state.StopFailed = false;
            if (CurrentRun != null && !CurrentRun.Completed) CurrentRun.Result = "用户解除等待，原动作已取消";
            Changed();
            return delegate
            {
                Exception failure = null;
                try
                {
                    if (stopHome && !simulation) { session.Send("P801=0"); session.Send("P802=0"); }
                    else if (stopHome && plc >= 0) session.Send("DISABLE PLC " + plc);
                }
                catch (Exception error) { failure = error; }
                try { if (abortPlan) session.Send("&" + cs + "A"); } catch (Exception error) { if (failure == null) failure = error; }
                if (failure != null) throw failure;
            };
        }
        public void CompleteRecovery()
        {
            recovering = planOwned = homeOwned = false; activePlc = -1;
            state.CommandBusy = state.Running = state.Homing = state.Verifying = state.Stopping = state.StopFailed = false;
            stopPrevious = null; stopCompleted = null;
            Changed();
        }
        public bool RecoveryReady
        {
            get { return recovering && (!planOwned || (feedback.Fresh && LatestSnapshot != null && LatestSnapshot.Coordinate != null && !LatestSnapshot.Coordinate.Running)); }
        }
        public void Poll()
        {
            if (disposed) return;
            feedback.CheckTimeout(); CheckTimeout();
            if (state.CanPoll)
            {
                int? focused = state.ManualFeedbackMotor.HasValue ? state.ManualFeedbackMotor :
                    state.MovingMotor.HasValue ? state.MovingMotor : state.JoggingMotor;
                feedback.Poll(xMotor, yMotor, selectedMotor,
                    (state.Running || (state.Stopping && planOwned)) && CurrentRun != null ? CurrentRun.Plan.Options.CoordinateSystem : 0,
                    focused);
            }
        }
        private void Changed() { if (!disposed && StateChanged != null) StateChanged(this, EventArgs.Empty); }
        private void Report(string message, bool error)
        {
            StatusText = message; StatusError = error;
            if (!disposed && StatusChanged != null) StatusChanged(this, EventArgs.Empty);
        }
        private double stopAccepted = Double.PositiveInfinity, stopDeadline, stopSampleTime;
        private ControllerSnapshot stopPrevious;
        private string stoppingReason;
        private bool requestedEnable;
        private double enableAccepted, enableDeadline;
        public void CheckTimeout()
        {
            if (recovering) return;
            if (state.Verifying && MonotonicClock.NowMs > enableDeadline) StopWithReason("使能状态确认超时，已请求停止。");
            if (state.Stopping && MonotonicClock.NowMs > stopDeadline && !state.StopFailed)
            {
                state.StopFailed = true; Report("停稳确认超时，状态未知；请检查设备或重试停止。", true); Changed();
                Action<Exception> completed = stopCompleted; stopCompleted = null;
                if (completed != null) completed(new InvalidOperationException(StatusText));
            }
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
            feedback.PauseTimeoutForCommand();
            session.Execute(delegate
            {
                if (Thread.VolatileRead(ref generation) != version) return;
                start = session.StartPlan(selected, simulatorLagMs, delegate { return Thread.VolatileRead(ref generation) != version; });
                duration = MonotonicClock.NowMs - start;
            }, delegate(Exception error)
            {
                feedback.ResumeTimeoutAfterCommand();
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
            if (state.Verifying && sample.TimeMs > enableAccepted && sample.XStatus.Enabled == requestedEnable && sample.YStatus.Enabled == requestedEnable &&
                (!requestedEnable || (!sample.XStatus.OpenLoop && !sample.YStatus.OpenLoop && !sample.XStatus.BlockingFault && !sample.YStatus.BlockingFault)))
            { state.Verifying = false; Report(requestedEnable ? "X/Y 已使能，反馈已确认。" : "X/Y 已去使能，反馈已确认。", false); }
            if (!recovering && state.Stopping && sample.TimeMs > stopAccepted && sample.TimeMs > stopSampleTime)
            {
                bool axesStopped = (!sample.XStatus.Enabled || (sample.XStatus.VelocityZero && sample.XStatus.InPosition)) &&
                    (!sample.YStatus.Enabled || (sample.YStatus.VelocityZero && sample.YStatus.InPosition));
                bool coordinateStopped = !planOwned || (sample.Coordinate != null && !sample.Coordinate.Running);
                if (axesStopped && coordinateStopped && stopPrevious != null && Math.Abs(stopPrevious.XCounts - sample.XCounts) <= 0.1 && Math.Abs(stopPrevious.YCounts - sample.YCounts) <= 0.1)
                {
                    state.Stopping = state.StopFailed = false; activePlc = -1; planOwned = homeOwned = false;
                    Report(stoppingReason + "；已停稳，轴反馈与坐标系状态已确认。", false);
                    Action<Exception> completed = stopCompleted; stopCompleted = null; if (completed != null) completed(null);
                }
                stopPrevious = axesStopped && coordinateStopped ? sample : null; stopSampleTime = sample.TimeMs;
            }
            if (state.Running)
            {
                MotionPoint p = mapping.FromCounts(sample.XCounts, sample.YCounts);
                CurrentRun.Samples.Add(new TrajectorySample(sample.TimeMs - runStart, sample.XTimeMs - runStart, sample.YTimeMs - runStart, sample.QueryMs, p));
                if (!sample.Ready || !mapping.Contains(p) || (sample.Coordinate != null && sample.Coordinate.Fault)) StopWithReason("运行中检测到轴故障、限位、失能、反馈越界或坐标系运行错误。");
                else if (sample.TimeMs - runStart >= CurrentRun.Plan.DurationMs && sample.InPosition && sample.Coordinate != null && !sample.Coordinate.Running && sample.Coordinate.InPosition)
                {
                    if (PathGeometry.Distance(p, CurrentRun.Plan.At(CurrentRun.Plan.DurationMs)) > 0.1) StopWithReason("结束位置与规划相差超过 0.1 mm，需检查执行状态。");
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
            if (state.Running || state.Homing || state.CommandBusy || state.Verifying) StopWithReason("通信失败：" + error.Message);
            else Report(error.Message, true);
            Changed();
        }

        public void EnableAxes(bool enabled, bool confirmed)
        {
            if (disposed || IsBusy || state.ExternalBusy || !HardwareReady(confirmed) || !session.IsConnected) return;
            if (!Fresh || (LatestSnapshot.XStatus.Enabled && (!LatestSnapshot.XStatus.VelocityZero || !LatestSnapshot.XStatus.InPosition)) || (LatestSnapshot.YStatus.Enabled && (!LatestSnapshot.YStatus.VelocityZero || !LatestSnapshot.YStatus.InPosition)))
                throw new InvalidOperationException("使能/去使能前需要有效反馈并确认停稳。");
            if (enabled && (LatestSnapshot.XStatus.BlockingFault || LatestSnapshot.YStatus.BlockingFault)) throw new InvalidOperationException("请先处理并复位轴故障。");
            state.CommandBusy = true; Plan = null; Changed(); int version = generation, x = xMotor, y = yMotor;
            session.Execute(delegate
            {
                if (Thread.VolatileRead(ref generation) != version) return;
                ControllerSnapshot fresh = session.ReadSnapshot(x, y, 0);
                if ((fresh.XStatus.Enabled && (!fresh.XStatus.VelocityZero || !fresh.XStatus.InPosition)) || (fresh.YStatus.Enabled && (!fresh.YStatus.VelocityZero || !fresh.YStatus.InPosition))) throw new InvalidOperationException("轴仍在运动或未到位。");
                if (enabled && (fresh.XStatus.BlockingFault || fresh.YStatus.BlockingFault)) throw new InvalidOperationException("轴出现故障，请先处理并复位。");
                if (Thread.VolatileRead(ref generation) != version) return;
                session.SetEnabled(x, enabled); if (Thread.VolatileRead(ref generation) == version) session.SetEnabled(y, enabled);
            }, delegate(Exception error)
            {
                if (disposed || version != generation) return;
                state.CommandBusy = false;
                if (error == null) { state.Verifying = true; requestedEnable = enabled; enableAccepted = MonotonicClock.NowMs; enableDeadline = enableAccepted + 10000; }
                Report(error == null ? enabled ? "已发送 X/Y 闭环使能，请查看状态。" : "已发送 X/Y 失能。" : error.Message, error != null); Changed();
            });
        }

        public bool CanStartHoming(bool confirmed)
        {
            return !disposed && !IsBusy && !state.ExternalBusy && session.IsConnected && HardwareReady(confirmed) && Fresh &&
                (simulation ? LatestSnapshot.Ready && LatestSnapshot.InPosition :
                    ((xMotor == 1 && yMotor == 2) || (xMotor == 2 && yMotor == 1)) && LatestSnapshot.MechanicalHomeReady);
        }
        public void StartHoming(int target, int plc, double timeoutSeconds, bool confirmed)
        {
            if (!CanStartHoming(confirmed)) return;
            if (target < 0 || target > 2 || plc < -1 || plc > 31 || !PlanningOptions.Finite(timeoutSeconds) || timeoutSeconds <= 0) throw new ArgumentException("回零参数无效。");
            if (simulation && plc >= 0) { Report("模拟器验证内置 HOME；用户 PLC 需在实机验收。", true); return; }
            if (!simulation) { target = 2; plc = -1; }
            homeX = target == 1 ? yMotor : xMotor; homeY = homeX == xMotor ? yMotor : xMotor;
            homeBoth = target == 2; activePlc = plc; homeTimeout = timeoutSeconds * 1000; homeOwned = true;
            homeSawX = homeSawY = false; Plan = null; state.CommandBusy = true; int version = generation;
            Changed();
            session.Execute(delegate { if (Thread.VolatileRead(ref generation) == version) session.Home(homeX, homeY, homeBoth, plc, delegate { return Thread.VolatileRead(ref generation) != version; }); }, delegate(Exception error)
            {
                if (disposed || version != generation) return;
                state.CommandBusy = false;
                if (error != null) { Report(error.Message, true); StopWithReason("回零启动失败：" + error.Message); }
                else { state.Homing = true; homeStart = MonotonicClock.NowMs; Report(simulation ? "机械回零中，等待本次寻零状态变化与到位。" : "已发送 P801=1，等待下位机机械回零与到位。", false); }
                Changed();
            });
        }
        private void MonitorHome(ControllerSnapshot sample)
        {
            if (!simulation) { MonitorPlcHome(); return; }
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

        private static int ReadHomeState(string response)
        {
            double value;
            if (!Double.TryParse(response.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                !PlanningOptions.Finite(value) || value < -1 || value > 6 || value != Math.Floor(value))
                throw new FormatException("机械回零 PLC 状态无效：" + response);
            return (int)value;
        }
        private void MonitorPlcHome()
        {
            if (homeQueryPending) return;
            homeQueryPending = true; int version = generation, xState = 0, yState = 0;
            ControllerSnapshot current = null;
            session.Execute(delegate
            {
                if (Thread.VolatileRead(ref generation) != version) return;
                xState = ReadHomeState(session.Send("P801").Response);
                yState = ReadHomeState(session.Send("P802").Response);
                current = session.ReadSnapshot(xMotor, yMotor, 0);
            }, delegate(Exception error)
            {
                homeQueryPending = false;
                if (disposed || version != generation || !state.Homing) return;
                if (error != null) { StopWithReason("回零状态读取失败：" + error.Message); return; }
                if (current.XStatus.BlockingFault || current.YStatus.BlockingFault || !current.XStatus.Enabled || !current.YStatus.Enabled ||
                    current.XStatus.OpenLoop || current.YStatus.OpenLoop)
                { StopWithReason("回零检测到故障、开环或失能。"); return; }
                if (xState == 6 && yState == 6 && current.InPosition)
                {
                    state.Homing = false; activePlc = -1; homeOwned = false;
                    if (HomeCompleted != null) HomeCompleted(this, EventArgs.Empty);
                    Report("机械回零完成，P801=6、P802=6 且两轴已到位。", false);
                }
                else if (MonotonicClock.NowMs - homeStart > homeTimeout) StopWithReason("机械回零超时，PLC 尚未完成两轴回零或轴未到位。");
                else Report("机械回零中：X / P801=" + xState + "，Y / P802=" + yState + "。", false);
                Changed();
            });
        }

        public void Stop() { StopWithReason("用户停止"); }
        public void Stop(Action<Exception> completed) { stopCompleted += completed; Stop(); }
        private void StopWithReason(string reason)
        {
            if (recovering || (state.Stopping && !state.StopFailed) || disposed) return;
            CancelPendingWork(); int version = generation; bool wasHome = homeOwned, abortPlan = planOwned;
            stoppingReason = reason;
            int plc = activePlc, x = xMotor, y = yMotor;
            int cs = CurrentRun != null && abortPlan ? CurrentRun.Plan.Options.CoordinateSystem : Plan == null ? 1 : Plan.Options.CoordinateSystem;
            state.Running = false; state.Homing = false; state.Verifying = false; state.Stopping = true;
            state.StopFailed = false; stopPrevious = null; stopSampleTime = 0;
            stopAccepted = Double.PositiveInfinity; stopDeadline = MonotonicClock.NowMs + 10000;
            if (CurrentRun != null && !CurrentRun.Completed) CurrentRun.Result = reason;
            Changed();
            session.Execute(delegate
            {
                if (!session.IsConnected || Thread.VolatileRead(ref generation) != version) return;
                if (wasHome) session.StopHome(x, y, plc);
                else if (abortPlan) session.StopPlan(cs, x, y);
                else session.Stop(new int[] { x, y });
            }, delegate(Exception error)
            {
                if (disposed || version != generation) return;
                state.CommandBusy = false;
                state.StopFailed = error != null;
                if (error == null) stopAccepted = MonotonicClock.NowMs;
                Report(error == null ? reason + "；停止指令已接受，等待实际停稳。" : reason + "；停止失败：" + error.Message, error != null);
                Changed();
                if (error != null) { Action<Exception> completed = stopCompleted; stopCompleted = null; if (completed != null) completed(error); }
            });
        }

        public void Dispose()
        {
            disposed = true; CancelPendingWork();
            feedback.Updated -= FeedbackUpdated; feedback.Failed -= FeedbackFailed;
        }
    }
}
