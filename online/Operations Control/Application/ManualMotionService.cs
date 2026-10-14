using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Operations_Control.Communication;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    public sealed class ManualMotionService
    {
        private readonly IControllerSession session;
        private readonly OperationState state;
        private readonly FeedbackService feedback;
        private readonly MotionGuard guard;
        private string operation;
        private int motor, direction, generation;
        private double target, started, deadline, stopAccepted, lastStopSample;
        private int stableStops;
        private int[] stopMotors;
        private readonly Dictionary<int, double> stoppedPositions = new Dictionary<int, double>();
        public string StatusText { get; private set; }
        public bool StatusError { get; private set; }
        public event EventHandler StateChanged, StatusChanged;
        public ManualMotionService(IControllerSession session, OperationState state, FeedbackService feedback, MotionGuard guard)
        { this.session = session; this.state = state; this.feedback = feedback; this.guard = guard; }
        private void Changed() { if (StateChanged != null) StateChanged(this, EventArgs.Empty); }
        private void Report(string text, bool error)
        { StatusText = text; StatusError = error; if (StatusChanged != null) StatusChanged(this, EventArgs.Empty); }
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
        {
            if (!Available) return false;
            if (sign != 1 && sign != -1) throw new ArgumentException("点动方向无效。");
            if (step.HasValue && step.Value <= 0) throw new ArgumentException("步长必须大于零。");
            AxisSample sample = Latest(axis);
            double? destination = step.HasValue && sample != null ? (double?)(sample.Position + (double)step.Value * sign) : null;
            guard.Require(sample, sign, destination, speed, true);
            Begin(axis, step.HasValue ? "定位" : "点动", destination ?? 0, speed); direction = sign;
            if (!step.HasValue) { state.MovingMotor = null; state.JoggingMotor = axis; }
            int version = generation;
            session.Execute(delegate
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis);
                if (step.HasValue) target = fresh.Position + (double)step.Value * sign;
                guard.Require(fresh, sign, step.HasValue ? (double?)target : null, speed, true);
                if (Thread.VolatileRead(ref generation) != version) return;
                session.Jog(axis, sign, speed, step, delegate { return Thread.VolatileRead(ref generation) != version; });
            }, delegate(Exception error) { Accepted(version, error, completed); });
            Changed(); return true;
        }
        public bool Move(int axis, decimal destination, decimal speed)
        {
            if (!Available) return false;
            AxisSample sample = Latest(axis); int sign = sample == null ? 0 : Math.Sign((double)destination - sample.Position);
            guard.Require(sample, sign, (double)destination, speed, true);
            Begin(axis, "定位", (double)destination, speed); direction = sign; int version = generation;
            session.Execute(delegate
            {
                if (version != generation) return;
                AxisSample fresh = ReadAxis(axis); guard.Require(fresh, Math.Sign((double)destination - fresh.Position), (double)destination, speed, true);
                if (Thread.VolatileRead(ref generation) != version) return;
                session.Move(axis, destination, speed, delegate { return Thread.VolatileRead(ref generation) != version; });
            }, delegate(Exception error) { Accepted(version, error, null); });
            return true;
        }
        private void Begin(int axis, string name, double destination, decimal speed)
        {
            motor = axis; operation = name; target = destination; state.MovingMotor = axis; state.ManualBusy = true;
            started = Double.PositiveInfinity;
            AxisSample sample = Latest(axis);
            double distance = sample == null ? 0 : Math.Abs(destination - sample.Position);
            deadline = MonotonicClock.NowMs + Math.Max(10000, distance / (double)speed * 2 + 5000);
            Report("电机 " + axis + "：正在发送" + name + "指令", false); Changed();
        }
        private void Accepted(int version, Exception error, Action<Exception> completed)
        {
            if (version != generation) return;
            state.ManualBusy = false;
            if (error != null) { Report(error.Message, true); Stop(new int[] { motor }); }
            else { started = MonotonicClock.NowMs; Report("电机 " + motor + "：" + operation + "中，等待实际反馈确认", false); }
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
            Begin(axis, enabled ? "使能" : "去使能", sample.Position, 1); int version = generation;
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
            Begin(axis, "当前位置置零", 0, 1); int version = generation;
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
            Begin(axis, "复位", sample.Position, 1); int version = generation;
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
        private void StopCore(int[] axes, bool cancelPending, Action<Exception> completed)
        {
            if (state.ShuttingDown) return;
            if (state.ManualStopping && !state.ManualStopFailed)
            {
                HashSet<int> combined = new HashSet<int>(stopMotors);
                int oldCount = combined.Count; foreach (int axis in axes) combined.Add(axis);
                if (combined.Count == oldCount) return;
                axes = new List<int>(combined).ToArray();
            }
            if (cancelPending) generation++;
            stopMotors = new List<int>(new HashSet<int>(axes)).ToArray();
            state.MovingMotor = null; state.JoggingMotor = null; state.ManualStopping = true; state.ManualStopFailed = false;
            stableStops = 0; stoppedPositions.Clear(); lastStopSample = 0; stopAccepted = Double.PositiveInfinity;
            deadline = MonotonicClock.NowMs + 10000; Changed(); Report("停止中：等待指令接收与实际停稳", false);
            session.Execute(delegate { if (session.IsConnected) session.Stop(stopMotors); }, delegate(Exception error)
            {
                state.ManualBusy = false;
                if (error != null) { state.ManualStopFailed = true; Report("停止失败，状态未知：" + error.Message, true); }
                else stopAccepted = MonotonicClock.NowMs;
                if (completed != null) completed(error); Changed();
            });
        }
        public void Tick()
        {
            if (!state.ManualExecuting) return;
            if (state.ManualStopping)
            {
                if (feedback.Fresh && feedback.Latest.Snapshot.TimeMs > stopAccepted && feedback.Latest.Snapshot.TimeMs > lastStopSample)
                {
                    bool stopped = true;
                    foreach (int axis in stopMotors)
                    {
                        AxisSample sample = Latest(axis);
                        if (sample == null || sample.Status == null || sample.TimeMs <= stopAccepted || (sample.Status.Enabled && (!sample.Status.VelocityZero || !sample.Status.InPosition))) { stopped = false; break; }
                        if (!stoppedPositions.ContainsKey(axis) || Math.Abs(stoppedPositions[axis] - sample.Position) > guard.Configuration.Axis(axis).PositionToleranceCounts) stopped = false;
                        stoppedPositions[axis] = sample.Position;
                    }
                    lastStopSample = feedback.Latest.Snapshot.TimeMs; stableStops = stopped ? stableStops + 1 : 0;
                    if (stableStops >= 1) { state.ManualStopping = state.ManualStopFailed = false; operation = null; Report("已停稳：速度零与反馈位置稳定均已确认", false); Changed(); return; }
                }
                if (MonotonicClock.NowMs > deadline && !state.ManualStopFailed) { state.ManualStopFailed = true; Report("停稳确认超时；设备状态未知，请检查设备或重试停止", true); Changed(); }
                return;
            }
            AxisSample current = Latest(motor);
            if (!feedback.Fresh || current == null || current.Status == null) { Report("运动反馈过期或未知，已请求停止", true); Stop(new int[] { motor }); return; }
            if (state.ManualBusy || current.TimeMs <= started) return;
            MotorStatus status = current.Status;
            double tolerance = guard.Configuration.Axis(motor).PositionToleranceCounts;
            bool verified = operation == "使能" ? status.Enabled && !status.OpenLoop && !status.BlockingFault :
                operation == "去使能" ? !status.Enabled : operation == "复位" ? !status.BlockingFault && (!status.Enabled || (status.VelocityZero && status.InPosition)) && Math.Abs(current.Position - target) <= 0.5 :
                (operation == "定位" || operation == "当前位置置零") && status.VelocityZero && status.InPosition && Math.Abs(current.Position - target) <= tolerance;
            if (verified)
            { string done = operation; state.MovingMotor = null; operation = null; Report("电机 " + motor + "：" + (done == "定位" ? "已到位" : done + "完成") + "，反馈已确认", false); Changed(); return; }
            if (operation == "定位" || operation == "点动")
            {
                string reason = guard.Reason(current, direction, operation == "定位" ? (double?)target : null, 0.001M, false);
                if (reason != null) { Report("电机 " + motor + "：" + reason + "，已请求停止", true); Stop(new int[] { motor }); return; }
            }
            if (operation == "复位" && ((status.Enabled && !status.VelocityZero) || Math.Abs(current.Position - target) > 0.5))
            { Report("复位期间检测到意外运动，已请求停止；请检查复位命令/PLC。", true); Stop(new int[] { motor }); return; }
            if (operation != "点动" && MonotonicClock.NowMs > deadline)
            { Report("电机 " + motor + "：" + operation + "确认超时", true); Stop(new int[] { motor }); }
        }
        public void Disconnect(int[] motors, Action<Exception, Exception> completed)
        {
            if (state.ShuttingDown) return;
            generation++; state.ShuttingDown = true; Changed(); Exception stopError = null;
            session.Execute(delegate
            {
                try { if (session.IsConnected) session.Stop(motors); }
                catch (Exception error) { stopError = error; }
                finally { session.Disconnect(); }
            }, delegate(Exception error)
            {
                state.ManualBusy = false; state.ShuttingDown = false; state.JoggingMotor = state.MovingMotor = null;
                state.ManualStopping = state.ManualStopFailed = false;
                if (completed != null) completed(stopError, error); Changed();
            });
        }
    }
}
