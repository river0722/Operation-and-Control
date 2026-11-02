using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Operations_Control.Communication;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Application
{
    public sealed class ManualMotionTiming
    {
        public string Operation { get; internal set; }
        public int Motor { get; internal set; }
        public double RequestedAtMs { get; internal set; }
        public double? DequeuedAtMs { get; internal set; }
        public double? CheckedAtMs { get; internal set; }
        public double? AcceptedAtMs { get; internal set; }
        public double? FirstFeedbackAtMs { get; internal set; }
        public double? InPositionAtMs { get; internal set; }
        public double? FinishedAtMs { get; internal set; }
        private static string Milliseconds(double? value)
        { return value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "未确认"; }
        public string Describe(string result)
        {
            return "电机 " + (Motor == 0 ? "监测轴" : Motor.ToString(CultureInfo.InvariantCulture)) + " " + Operation + "耗时（" + result + "）：排队 " +
                Milliseconds(DequeuedAtMs - RequestedAtMs) + "；实时检查 " + Milliseconds(CheckedAtMs - DequeuedAtMs) +
                "；指令发送（含速度核对） " + Milliseconds(AcceptedAtMs - CheckedAtMs) +
                "；接受后首帧 " + Milliseconds(FirstFeedbackAtMs - AcceptedAtMs) +
                "；接受后首次到位 " + Milliseconds(InPositionAtMs - AcceptedAtMs) +
                "；总计 " + Milliseconds(FinishedAtMs - RequestedAtMs);
        }
    }

    public sealed class ManualMotionService
    {
        private readonly IControllerSession session;
        private readonly OperationState state;
        private readonly FeedbackService feedback;
        private readonly MotionGuard guard;
        private string operation;
        private int motor, direction, generation;
        private int? pairedMotor;
        private double target, started, deadline, stopAccepted, lastStopSample;
        private int stableStops;
        private int[] stopMotors;
        private bool recovering;
        private Action recoveryCompleted;
        private Func<bool> recoveryReady;
        private readonly Dictionary<int, double> stoppedPositions = new Dictionary<int, double>();
        public string StatusText { get; private set; }
        public bool StatusError { get; private set; }
        public ManualMotionTiming LastTiming { get; private set; }
        public event EventHandler StateChanged, StatusChanged;
        public event Action<string> TimingRecorded;
        public bool CanRecover { get { return session.IsConnected && (!recovering || state.ManualStopFailed) && !state.ManualBusy && !state.CommandBusy && !state.Computing && !state.ShuttingDown; } }
        public ManualMotionService(IControllerSession session, OperationState state, FeedbackService feedback, MotionGuard guard)
        { this.session = session; this.state = state; this.feedback = feedback; this.guard = guard; }
        private void Changed() { if (StateChanged != null) StateChanged(this, EventArgs.Empty); }
        private void Report(string text, bool error)
        { if (StatusText == text && StatusError == error) return; StatusText = text; StatusError = error; if (StatusChanged != null) StatusChanged(this, EventArgs.Empty); }
        private void FinishTiming(string result)
        {
            if (LastTiming == null || LastTiming.FinishedAtMs.HasValue) return;
            LastTiming.FinishedAtMs = MonotonicClock.NowMs;
            if (TimingRecorded != null) TimingRecorded(LastTiming.Describe(result));
        }
        private bool Available { get { return session.IsConnected && !state.ManualBusy && !state.ManualExecuting && !state.ShuttingDown && !state.Following && !state.WorkflowBusy && !state.FeedbackStale; } }
        private AxisSample Latest(int axis) { return feedback.Latest == null ? null : feedback.Latest.Find(axis); }
        private AxisSample ReadAxis(int axis)
        {
            double time = MonotonicClock.NowMs;
            double value = Double.Parse(session.Send("#" + axis + "P").Response.Trim(), CultureInfo.InvariantCulture);
            if (!PlanningOptions.Finite(value)) throw new InvalidOperationException("位置反馈无效。");
            return new AxisSample(axis, value, null, DateTime.Now) { Status = MotorStatus.Parse(session.Send("#" + axis + "?").Response), TimeMs = time };
        }
        public bool Execute(Action action, Action<Exception> completed)
        {
            if (state.ManualBusy || state.ManualExecuting || state.ShuttingDown || state.Following || state.WorkflowBusy) return false;
            state.ManualBusy = true; Changed();
            session.Execute(action, delegate(Exception error)
            { state.ManualBusy = false; if (completed != null) completed(error); Changed(); });
            return true;
        }
        public bool StartJog(int axis, int sign, decimal speed, decimal? step, Action<Exception> completed)
        { return StartJog(axis, sign, speed, step, 0.05M, completed); }
        public bool StartJog(int axis, int sign, decimal speed, decimal? step, decimal acceleration, Action<Exception> completed)
        {
            if (!Available) return false;
            if (sign != 1 && sign != -1) throw new ArgumentException("点动方向无效。");
            if (step.HasValue && step.Value <= 0) throw new ArgumentException("步长必须大于零。");
            if (step.HasValue) PointToPointProfile.Calculate((double)speed, (double)acceleration);
            AxisSample sample = Latest(axis);
            double? destination = step.HasValue && sample != null ? (double?)(sample.Position + (double)step.Value * sign) : null;
            guard.Require(sample, sign, destination, speed, true);
            Begin(axis, step.HasValue ? "定位" : "点动", destination ?? 0, speed, acceleration); direction = sign;
            if (!step.HasValue) { state.MovingMotor = null; state.JoggingMotor = axis; }
            int version = generation;
            QueueMotion(version, delegate(ManualMotionTiming timing)
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis);
                if (step.HasValue) target = fresh.Position + (double)step.Value * sign;
                guard.Require(fresh, sign, step.HasValue ? (double?)target : null, speed, true);
                timing.CheckedAtMs = MonotonicClock.NowMs;
                if (Thread.VolatileRead(ref generation) != version) return;
                session.Jog(axis, sign, speed, step, acceleration, delegate { return Thread.VolatileRead(ref generation) != version; });
            }, completed);
            Changed(); return true;
        }
        public bool Move(int axis, decimal destination, decimal speed)
        { return Move(axis, destination, speed, 0.05M); }
        public bool Move(int axis, decimal destination, decimal speed, decimal acceleration)
        {
            if (!Available) return false;
            PointToPointProfile.Calculate((double)speed, (double)acceleration);
            AxisSample sample = Latest(axis); int sign = sample == null ? 0 : Math.Sign((double)destination - sample.Position);
            guard.Require(sample, sign, (double)destination, speed, true);
            Begin(axis, "定位", (double)destination, speed, acceleration); direction = sign; int version = generation;
            QueueMotion(version, delegate(ManualMotionTiming timing)
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis); guard.Require(fresh, Math.Sign((double)destination - fresh.Position), (double)destination, speed, true);
                timing.CheckedAtMs = MonotonicClock.NowMs;
                if (Thread.VolatileRead(ref generation) != version) return;
                session.Move(axis, destination, speed, acceleration, delegate { return Thread.VolatileRead(ref generation) != version; });
            }, null);
            return true;
        }
        public bool MoveXYToZero(int x, int y, decimal speed, decimal acceleration)
        {
            if (!Available) return false;
            if (x == y) throw new ArgumentException("X/Y 不能使用同一电机。");
            ManualMotionProfile profile = PointToPointProfile.Calculate((double)speed, (double)acceleration);
            AxisSample first = Latest(x), second = Latest(y);
            guard.Require(first, first == null ? 0 : Math.Sign(-first.Position), 0, speed, true);
            guard.Require(second, second == null ? 0 : Math.Sign(-second.Position), 0, speed, true);
            Begin(x, "定位", 0, speed, acceleration);
            pairedMotor = y; state.ManualFeedbackMotor = null;
            LastTiming.Motor = 0; LastTiming.Operation = "XY轴回零";
            deadline = Math.Max(deadline, MonotonicClock.NowMs + PointToPointProfile.EstimateDuration(Math.Abs(second.Position), (double)speed, profile) + 5000);
            Report("X/Y 轴：正在定位到位置 0", false);
            int version = generation;
            QueueMotion(version, delegate(ManualMotionTiming timing)
            {
                if (version != Thread.VolatileRead(ref generation)) return;
                AxisSample freshX = ReadAxis(x), freshY = ReadAxis(y);
                guard.Require(freshX, Math.Sign(-freshX.Position), 0, speed, true);
                guard.Require(freshY, Math.Sign(-freshY.Position), 0, speed, true);
                timing.CheckedAtMs = MonotonicClock.NowMs;
                Func<bool> cancelled = delegate { return Thread.VolatileRead(ref generation) != version; };
                session.Move(x, 0, speed, acceleration, cancelled);
                session.Move(y, 0, speed, acceleration, cancelled);
            }, null);
            return true;
        }
        private void Begin(int axis, string name, double destination, decimal speed, decimal acceleration)
        {
            pairedMotor = null;
            motor = axis; operation = name; target = destination; state.MovingMotor = axis; state.ManualFeedbackMotor = axis; state.ManualBusy = true;
            started = Double.PositiveInfinity;
            LastTiming = name == "定位" || name == "点动" ?
                new ManualMotionTiming { Motor = axis, Operation = name, RequestedAtMs = MonotonicClock.NowMs } : null;
            AxisSample sample = Latest(axis);
            double distance = sample == null ? 0 : Math.Abs(destination - sample.Position);
            ManualMotionProfile profile = PointToPointProfile.Calculate((double)speed, (double)acceleration);
            deadline = MonotonicClock.NowMs + Math.Max(10000, PointToPointProfile.EstimateDuration(distance, (double)speed, profile) + 5000);
            Report("电机 " + axis + "：正在发送" + name + "指令", false); Changed();
        }
        private void QueueMotion(int version, Action<ManualMotionTiming> action, Action<Exception> completed)
        {
            ManualMotionTiming timing = LastTiming;
            session.Execute(delegate
            {
                if (Thread.VolatileRead(ref generation) != version) return;
                timing.DequeuedAtMs = MonotonicClock.NowMs;
                action(timing);
                timing.AcceptedAtMs = MonotonicClock.NowMs;
            }, delegate(Exception error) { Accepted(version, error, completed, timing); });
        }
        private void Accepted(int version, Exception error, Action<Exception> completed)
        { Accepted(version, error, completed, null); }
        private void Accepted(int version, Exception error, Action<Exception> completed, ManualMotionTiming timing)
        {
            if (version != generation) return;
            state.ManualBusy = false;
            if (error != null) { FinishTiming("发送失败"); Report(error.Message, true); Stop(new int[] { motor }); }
            else if (!state.ManualStopping)
            {
                started = timing != null && timing.AcceptedAtMs.HasValue ? timing.AcceptedAtMs.Value : MonotonicClock.NowMs;
                Report((pairedMotor.HasValue ? "X/Y 轴" : "电机 " + motor) + "：指令已接受，等待实际反馈确认", false);
                feedback.PollNow(state.ManualFeedbackMotor);
            }
            if (completed != null) completed(error); Changed();
        }
        public bool SetEnabled(int axis, bool enabled)
        {
            if (!Available) return false;
            AxisSample sample = Latest(axis);
            if (sample == null || sample.Status == null || !feedback.Fresh) throw new InvalidOperationException("等待有效轴反馈。");
            if (sample.Status.Enabled && (!sample.Status.VelocityZero || !sample.Status.InPosition)) throw new InvalidOperationException("请先停止并确认停稳。");
            if (!sample.Status.Enabled && (!sample.Velocity.HasValue || Math.Abs(sample.Velocity.Value) > 0.001)) throw new InvalidOperationException("未使能轴需要稳定位置反馈后再操作。");
            if (enabled && sample.Status.BlockingFault) throw new InvalidOperationException("请先处理故障并执行配置的复位流程。");
            Begin(axis, enabled ? "使能" : "去使能", sample.Position, 1, 0.05M); int version = generation;
            session.Execute(delegate
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis);
                if (fresh.Status.Enabled && (!fresh.Status.VelocityZero || !fresh.Status.InPosition)) throw new InvalidOperationException("轴尚未停稳。");
                if (!fresh.Status.Enabled && Math.Abs(fresh.Position - sample.Position) > 0.1) throw new InvalidOperationException("未使能轴位置正在变化。");
                if (enabled && fresh.Status.BlockingFault) throw new InvalidOperationException("轴出现故障，请先处理并复位。");
                if (Thread.VolatileRead(ref generation) != version) return;
                session.SetEnabled(axis, enabled);
            }, delegate(Exception error) { Accepted(version, error, null); }); return true;
        }
        public bool ZeroPosition(int axis)
        {
            if (!Available) return false;
            AxisSample sample = Latest(axis); guard.Require(sample, 0, sample == null ? (double?)null : sample.Position, 0.001M, true);
            Begin(axis, "当前位置置零", 0, 1, 0.05M); int version = generation;
            session.Execute(delegate
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis); guard.Require(fresh, 0, fresh.Position, 0.001M, true);
                if (Thread.VolatileRead(ref generation) == version) session.ZeroPosition(axis);
            }, delegate(Exception error) { Accepted(version, error, null); }); return true;
        }
        public bool Reset(int axis)
        {
            if (!Available) return false;
            string command = guard.Configuration.Axis(axis).ResetCommand;
            if (String.IsNullOrEmpty(command)) throw new InvalidOperationException("尚未配置该轴复位命令；请根据驱动器/PLC 的实际恢复流程配置。");
            AxisSample sample = Latest(axis);
            if (sample == null || sample.Status == null || !feedback.Fresh || (sample.Status.Enabled && !sample.Status.VelocityZero)) throw new InvalidOperationException("反馈未知或轴仍在运动，禁止复位。");
            Begin(axis, "复位", sample.Position, 1, 0.05M); int version = generation;
            session.Execute(delegate
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis);
                if (fresh.Status.Enabled && !fresh.Status.VelocityZero) throw new InvalidOperationException("轴仍在运动，禁止复位。");
                if (Thread.VolatileRead(ref generation) != version) return;
                session.Send(command.Replace("{motor}", axis.ToString(CultureInfo.InvariantCulture)));
            }, delegate(Exception error) { Accepted(version, error, null); }); return true;
        }
        public void StopJog(Action<int, Exception> completed)
        {
            if (!state.JoggingMotor.HasValue) return;
            int axis = state.JoggingMotor.Value;
            StopCore(new int[] { axis }, false, delegate(Exception error) { if (completed != null) completed(axis, error); });
        }
        public void Stop(int[] axes) { StopCore(axes, true, null); }
        // Recovery deliberately does not depend on InPosition or on cached feedback.
        // Outputs stay disabled; a separate enable command establishes a new hold position.
        public bool Recover(int[] axes, Func<Action> prepareWorkflow, Func<bool> workflowReady, Action completed)
        {
            if (!CanRecover) return false;
            HashSet<int> selected = new HashSet<int>(axes);
            if (pairedMotor.HasValue) selected.Add(pairedMotor.Value);
            if (stopMotors != null && state.ManualStopping) foreach (int axis in stopMotors) selected.Add(axis);
            if (state.MovingMotor.HasValue) selected.Add(state.MovingMotor.Value);
            if (state.JoggingMotor.HasValue) selected.Add(state.JoggingMotor.Value);
            if (selected.Count == 0) throw new ArgumentException("请选择要恢复的电机。");
            foreach (int axis in selected) if (axis < 1 || axis > 8) throw new ArgumentException("电机号应为 1–8。");
            Action stopWorkflow = prepareWorkflow == null ? null : prepareWorkflow();
            int version = ++generation;
            int[] requested = new List<int>(selected).ToArray(); stopMotors = requested;
            FinishTiming("解除等待");
            recovering = true; recoveryCompleted = completed; recoveryReady = workflowReady; operation = null; pairedMotor = null;
            state.MovingMotor = state.JoggingMotor = state.ManualFeedbackMotor = null;
            state.ManualStopping = state.ManualBusy = true; state.ManualStopFailed = false;
            stoppedPositions.Clear(); stableStops = 0; lastStopSample = 0; stopAccepted = Double.PositiveInfinity;
            deadline = MonotonicClock.NowMs + 5000;
            Report("正在解除等待：取消原动作并发送去使能，等待控制器回报。", false); Changed();
            session.Execute(delegate
            {
                if (version != Thread.VolatileRead(ref generation)) return;
                Exception failure = null;
                try { if (stopWorkflow != null) stopWorkflow(); } catch (Exception error) { failure = error; }
                foreach (int axis in requested)
                {
                    try { session.SetEnabled(axis, false); }
                    catch (Exception error) { if (failure == null) failure = error; }
                }
                if (failure != null) throw failure;
            }, delegate(Exception error)
            {
                if (version != generation) return;
                state.ManualBusy = false;
                if (error != null) { state.ManualStopFailed = true; Report("解除等待失败，未确认去使能；可重试：" + error.Message, true); }
                else { stopAccepted = MonotonicClock.NowMs; Report("去使能指令已接受，正在核对轴状态与位置反馈。", false); }
                Changed();
            });
            return true;
        }
        private void StopCore(int[] axes, bool cancelPending, Action<Exception> completed)
        {
            if (state.ShuttingDown || recovering) return;
            if (pairedMotor.HasValue)
            {
                HashSet<int> combined = new HashSet<int>(axes); combined.Add(motor); combined.Add(pairedMotor.Value);
                axes = new List<int>(combined).ToArray(); pairedMotor = null;
            }
            if (state.ManualStopping && !state.ManualStopFailed)
            {
                HashSet<int> combined = new HashSet<int>(stopMotors);
                int oldCount = combined.Count; foreach (int axis in axes) combined.Add(axis);
                if (combined.Count == oldCount) return;
                axes = new List<int>(combined).ToArray();
            }
            if (cancelPending) generation++;
            int version = generation;
            stopMotors = new List<int>(new HashSet<int>(axes)).ToArray();
            int[] requested = stopMotors;
            state.MovingMotor = null; state.JoggingMotor = null; state.ManualFeedbackMotor = requested.Length == 1 ? (int?)requested[0] : null;
            state.ManualStopping = true; state.ManualStopFailed = false;
            FinishTiming("停止请求");
            ManualMotionTiming timing = LastTiming = new ManualMotionTiming { Motor = requested.Length == 1 ? requested[0] : 0,
                Operation = "停止", RequestedAtMs = MonotonicClock.NowMs };
            stableStops = 0; stoppedPositions.Clear(); lastStopSample = 0; stopAccepted = Double.PositiveInfinity;
            deadline = MonotonicClock.NowMs + 10000; Changed(); Report("停止中：等待指令接收与实际停稳", false);
            session.Execute(delegate
            {
                timing.DequeuedAtMs = timing.CheckedAtMs = MonotonicClock.NowMs;
                if (session.IsConnected) session.Stop(requested);
                timing.AcceptedAtMs = MonotonicClock.NowMs;
            }, delegate(Exception error)
            {
                if (version != generation) return;
                state.ManualBusy = false;
                if (error != null) { state.ManualStopFailed = true; FinishTiming("停止发送失败"); Report("停止失败，状态未知：" + error.Message, true); }
                else
                {
                    stopAccepted = timing.AcceptedAtMs.Value;
                    feedback.PollNow(state.ManualFeedbackMotor);
                }
                if (completed != null) completed(error); Changed();
            });
        }
        public void Tick()
        {
            if (!state.ManualExecuting) return;
            if (state.ManualStopping)
            {
                if (feedback.Fresh && feedback.Latest.TimeMs > stopAccepted && feedback.Latest.TimeMs > lastStopSample)
                {
                    if (LastTiming != null && !LastTiming.FirstFeedbackAtMs.HasValue) LastTiming.FirstFeedbackAtMs = MonotonicClock.NowMs;
                    bool stopped = true;
                    foreach (int axis in stopMotors)
                    {
                        AxisSample sample = Latest(axis);
                        if (sample == null || sample.Status == null || sample.TimeMs <= stopAccepted ||
                            (recovering ? sample.Status.Enabled : sample.Status.Enabled && (!sample.Status.VelocityZero || !sample.Status.InPosition))) { stopped = false; stoppedPositions.Clear(); break; }
                        if (!stoppedPositions.ContainsKey(axis) || Math.Abs(stoppedPositions[axis] - sample.Position) > guard.Configuration.Axis(axis).PositionToleranceCounts) stopped = false;
                        stoppedPositions[axis] = sample.Position;
                    }
                    lastStopSample = feedback.Latest.TimeMs; stableStops = stopped ? stableStops + 1 : 0;
                    if (stableStops >= 1 && !state.ManualStopFailed && (!recovering || recoveryReady == null || recoveryReady()))
                    {
                        state.ManualStopping = state.ManualStopFailed = false; state.ManualFeedbackMotor = null; operation = null;
                        FinishTiming("停稳确认");
                        bool recovered = recovering; recovering = false;
                        Action completed = recoveryCompleted; recoveryCompleted = null; recoveryReady = null;
                        if (recovered && completed != null) completed();
                        Report(recovered ? "等待已解除：监测轴均已去使能，连续位置反馈稳定。核对配置后可重新使能；原动作不会续跑。" : "已停稳：速度零与反馈位置稳定均已确认", false);
                        Changed(); return;
                    }
                }
                if (MonotonicClock.NowMs > deadline && !state.ManualStopFailed) { state.ManualStopFailed = true; FinishTiming("停稳超时"); Report(recovering ? "解除等待确认超时（5 秒）；未确认去使能或反馈稳定，可重试解除等待。" : "停稳确认超时；设备状态未知，请检查设备或重试停止", true); Changed(); }
                return;
            }
            if (pairedMotor.HasValue) { MonitorXYZero(); return; }
            AxisSample current = Latest(motor);
            if (!feedback.Fresh || current == null || current.Status == null || MonotonicClock.NowMs - current.TimeMs > 1000)
            { Report("运动反馈过期或未知，已请求停止", true); Stop(new int[] { motor }); return; }
            if (state.ManualBusy || current.TimeMs <= started) return;
            if (LastTiming != null && !LastTiming.FirstFeedbackAtMs.HasValue) LastTiming.FirstFeedbackAtMs = MonotonicClock.NowMs;
            MotorStatus status = current.Status;
            if (LastTiming != null && status.VelocityZero && status.InPosition && !LastTiming.InPositionAtMs.HasValue)
                LastTiming.InPositionAtMs = MonotonicClock.NowMs;
            double tolerance = guard.Configuration.Axis(motor).PositionToleranceCounts;
            bool verified = operation == "使能" ? status.Enabled && !status.OpenLoop && !status.BlockingFault :
                operation == "去使能" ? !status.Enabled : operation == "复位" ? !status.BlockingFault && (!status.Enabled || (status.VelocityZero && status.InPosition)) && Math.Abs(current.Position - target) <= 0.5 :
                (operation == "定位" || operation == "当前位置置零") && status.VelocityZero && status.InPosition && Math.Abs(current.Position - target) <= tolerance;
            if (verified)
            { string done = operation; state.MovingMotor = null; state.ManualFeedbackMotor = null; operation = null; FinishTiming("反馈确认"); Report("电机 " + motor + "：" + (done == "定位" ? "已到位" : done + "完成") + "，反馈已确认", false); Changed(); return; }
            if (operation == "定位" || operation == "点动")
            {
                string reason = guard.Reason(current, direction, operation == "定位" ? (double?)target : null, 0.001M, false);
                if (reason != null) { Report("电机 " + motor + "：" + reason + "，已请求停止", true); Stop(new int[] { motor }); return; }
                if (operation == "定位" && status.VelocityZero && MonotonicClock.NowMs - started > 500)
                    Report("电机 " + motor + (status.InPosition ? "：PMAC 已到位，等待位置进入上位机目标容差。" :
                        "：指令已停止，PMAC 尚未到位（InPosition=0），继续核对反馈。"), false);
            }
            if (operation == "复位" && ((status.Enabled && !status.VelocityZero) || Math.Abs(current.Position - target) > 0.5))
            { Report("复位期间检测到意外运动，已请求停止；请检查复位命令/PLC。", true); Stop(new int[] { motor }); return; }
            if (operation != "点动" && MonotonicClock.NowMs > deadline)
            { FinishTiming("到位确认超时"); Report("电机 " + motor + "：" + operation + "确认超时", true); Stop(new int[] { motor }); }
        }
        private void MonitorXYZero()
        {
            AxisSample first = Latest(motor), second = Latest(pairedMotor.Value);
            if (!feedback.Fresh || first == null || second == null || first.Status == null || second.Status == null ||
                MonotonicClock.NowMs - Math.Min(first.TimeMs, second.TimeMs) > 1000)
            { Report("X/Y 轴反馈过期或未知，已请求停止", true); Stop(new int[] { motor }); return; }
            if (state.ManualBusy || first.TimeMs <= started || second.TimeMs <= started) return;
            if (!LastTiming.FirstFeedbackAtMs.HasValue) LastTiming.FirstFeedbackAtMs = MonotonicClock.NowMs;
            foreach (AxisSample sample in new AxisSample[] { first, second })
            {
                string reason = guard.Reason(sample, Math.Sign(-sample.Position), 0, 0.001M, false);
                if (reason != null) { Report("X/Y 轴：" + reason + "，已请求停止", true); Stop(new int[] { motor }); return; }
            }
            if (first.Status.VelocityZero && first.Status.InPosition && Math.Abs(first.Position) <= guard.Configuration.Axis(motor).PositionToleranceCounts &&
                second.Status.VelocityZero && second.Status.InPosition && Math.Abs(second.Position) <= guard.Configuration.Axis(pairedMotor.Value).PositionToleranceCounts)
            {
                LastTiming.InPositionAtMs = MonotonicClock.NowMs;
                pairedMotor = null; state.MovingMotor = state.ManualFeedbackMotor = null; operation = null;
                FinishTiming("反馈确认"); Report("X/Y 轴已到位置 0，两轴到位反馈已确认", false); Changed(); return;
            }
            if (MonotonicClock.NowMs > deadline)
            { FinishTiming("到位确认超时"); Report("X/Y 轴回零定位超时，已请求停止", true); Stop(new int[] { motor }); }
        }
        public void Disconnect(int[] motors, Action<Exception, Exception> completed)
        {
            if (state.ShuttingDown) return;
            generation++; pairedMotor = null; recovering = false; recoveryCompleted = null; recoveryReady = null; state.ShuttingDown = true; Changed(); Exception stopError = null;
            session.Execute(delegate
            {
                try { if (session.IsConnected) session.Stop(motors); }
                catch (Exception error) { stopError = error; }
                finally { session.Disconnect(); }
            }, delegate(Exception error)
            {
                state.ManualBusy = false; state.ShuttingDown = false; state.JoggingMotor = state.MovingMotor = state.ManualFeedbackMotor = null;
                state.ManualStopping = state.ManualStopFailed = false;
                if (completed != null) completed(stopError, error); Changed();
            });
        }
    }
}
