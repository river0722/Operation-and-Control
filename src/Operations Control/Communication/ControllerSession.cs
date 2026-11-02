using Operations_Control.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Communication
{
    public sealed class ControllerSession : IControllerSession, IStreamingTrajectorySession
    {
        private readonly StaCommandDispatcher dispatcher = new StaCommandDispatcher();
        private readonly SynchronizationContext completionContext;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<int, double> positions = new Dictionary<int, double>();
        private readonly Dictionary<int, long> timestamps = new Dictionary<int, long>();
        private IControllerTransport transport;
        private RotaryProgramStream rotaryFollow;
        private bool disposed;
        public bool IsConnected { get { return transport != null && transport.IsConnected; } }
        public string DeviceName { get { return transport == null ? String.Empty : transport.DeviceName; } }

        public ControllerSession() : this(SynchronizationContext.Current) { }
        public ControllerSession(SynchronizationContext completionContext) { this.completionContext = completionContext; }

        public void Execute(Action action, Action<Exception> completed)
        {
            if (!disposed) dispatcher.Execute(action, completed, completionContext);
        }

        public bool Connect(bool simulation, int ownerWindow)
        {
            Disconnect();
            transport = simulation ? (IControllerTransport)new SimulatorTransport() : new PcommTransport();
            try { return transport.Connect(ownerWindow); }
            catch { Disconnect(); throw; }
        }

        public void Disconnect()
        {
            try { if (transport != null) transport.Dispose(); }
            finally { transport = null; rotaryFollow = null; positions.Clear(); timestamps.Clear(); }
        }

        public void ZeroPosition(int motor)
        {
            ValidateMotor(motor); Send("#" + motor + "HOMEZ"); ClearSamples();
        }

        public ControllerFeedback ReadFeedback(int x, int y, int selectedMotor, int coordinateSystem)
        {
            ValidateMotor(selectedMotor);
            ControllerSnapshot snapshot = ReadSnapshot(x, y, coordinateSystem);
            AxisSample first = Sample(x, snapshot.XCounts), second = Sample(y, snapshot.YCounts);
            first.TimeMs = snapshot.XTimeMs; second.TimeMs = snapshot.YTimeMs;
            first.Status = snapshot.XStatus; second.Status = snapshot.YStatus;
            first.FollowingError = ReadNumber("#" + x + "F"); second.FollowingError = ReadNumber("#" + y + "F");
            AxisSample selected = selectedMotor == x ? first : selectedMotor == y ? second : ReadAxisFeedback(selectedMotor);
            return new ControllerFeedback(snapshot, new AxisSample[] { first, second, selected });
        }

        public AxisSample ReadAxisFeedback(int motor)
        {
            ValidateMotor(motor);
            double begin = MonotonicClock.NowMs;
            AxisSample sample = Sample(motor, ReadPosition(motor));
            // The position must have been queried after a command was accepted
            // before its status can be used to confirm that command.
            sample.TimeMs = begin;
            sample.Status = MotorStatus.Parse(Send("#" + motor + "?").Response);
            sample.FollowingError = ReadNumber("#" + motor + "F");
            return sample;
        }

        private AxisSample Sample(int motor, double position)
        {
            ValidateMotor(motor); long now = clock.ElapsedMilliseconds; double? velocity = null;
            if (positions.ContainsKey(motor) && now > timestamps[motor]) velocity = (position - positions[motor]) / (now - timestamps[motor]);
            positions[motor] = position; timestamps[motor] = now;
            return new AxisSample(motor, position, velocity, DateTime.Now) { TimeMs = MonotonicClock.NowMs };
        }

        public void ClearSamples() { positions.Clear(); timestamps.Clear(); }

        public CommandResult Send(string command)
        {
            if (rotaryFollow != null && rotaryFollow.EntryOpen &&
                !System.Text.RegularExpressions.Regex.IsMatch(command ?? "", @"^\s*(#[1-8](P|F|\?|K|J/)|&([1-9]|1[0-6])(\?\?|A))\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new InvalidOperationException("实时随动期间仅允许位置、跟随误差和轴/坐标系状态查询；请先停止随动再发送其他命令。");
            return SendCore(command);
        }

        private CommandResult SendCore(string command)
        {
            if (!IsConnected) throw new InvalidOperationException("请先连接设备。");
            if (String.IsNullOrEmpty(command) || command.Length > 1024 || command.IndexOf('\r') >= 0 || command.IndexOf('\n') >= 0)
                throw new ArgumentException("请输入一条不超过 1024 字符的命令。");
            return transport.Send(command.Trim());
        }

        public void SetSpeed(int motor, decimal speed)
        {
            ValidateMotor(motor);
            if (speed <= 0 || speed > AxisConfiguration.MaximumSupportedSpeedCtPerMs) throw new ArgumentOutOfRangeException("speed");
            Send("I" + motor + "22=" + speed.ToString(CultureInfo.InvariantCulture));
            if (Math.Abs(ReadNumber("I" + motor + "22") - (double)speed) > 0.000001) throw new InvalidOperationException("速度写入后回读不一致。");
        }

        private void EnsureSpeed(int motor, decimal speed)
        {
            ValidateMotor(motor);
            if (speed <= 0 || speed > AxisConfiguration.MaximumSupportedSpeedCtPerMs) throw new ArgumentOutOfRangeException("speed");
            // Read the actual parameter even when another program or PLC may
            // have changed it.  An unchanged speed costs one query, no write.
            if (Math.Abs(ReadNumber("I" + motor + "22") - (double)speed) > 0.000001) SetSpeed(motor, speed);
        }

        public void Jog(int motor, int direction, decimal speed, decimal? step)
        { Jog(motor, direction, speed, step, delegate { return false; }); }
        public void Jog(int motor, int direction, decimal speed, decimal? step, Func<bool> cancelled)
        {
            if (cancelled()) return;
            EnsureSpeed(motor, speed);
            if (cancelled()) return;
            Send("#" + motor + (step.HasValue ? "J^" + (step.Value * direction).ToString(CultureInfo.InvariantCulture) : direction > 0 ? "J+" : "J-"));
        }

        public void Jog(int motor, int direction, decimal speed, decimal? step, decimal acceleration)
        { Jog(motor, direction, speed, step, acceleration, delegate { return false; }); }

        public void Jog(int motor, int direction, decimal speed, decimal? step, decimal acceleration, Func<bool> cancelled)
        {
            if (!step.HasValue) { Jog(motor, direction, speed, step, cancelled); return; }
            ValidateMotor(motor);
            if (cancelled()) return;
            ManualMotionProfile profile = PointToPointProfile.Calculate((double)speed, (double)acceleration);
            if (!EnsureMotionProfile(motor, profile, cancelled)) return;
            EnsureSpeed(motor, speed);
            if (cancelled()) return;
            Send("#" + motor + "J^" + (step.Value * direction).ToString(CultureInfo.InvariantCulture));
        }

        public void Move(int motor, decimal target, decimal speed)
        { Move(motor, target, speed, delegate { return false; }); }
        public void Move(int motor, decimal target, decimal speed, Func<bool> cancelled)
        {
            if (cancelled()) return;
            EnsureSpeed(motor, speed);
            if (cancelled()) return;
            Send("#" + motor + "J=" + target.ToString(CultureInfo.InvariantCulture));
        }

        public void Move(int motor, decimal target, decimal speed, decimal acceleration)
        { Move(motor, target, speed, acceleration, delegate { return false; }); }

        public void Move(int motor, decimal target, decimal speed, decimal acceleration, Func<bool> cancelled)
        {
            ValidateMotor(motor);
            if (cancelled()) return;
            ManualMotionProfile profile = PointToPointProfile.Calculate((double)speed, (double)acceleration);
            if (!EnsureMotionProfile(motor, profile, cancelled)) return;
            EnsureSpeed(motor, speed);
            if (cancelled()) return;
            IPointToPointTransport simulated = transport as IPointToPointTransport;
            if (simulated != null) { simulated.MovePointToPoint(motor, (double)target, (double)speed, profile); return; }
            Send("#" + motor + "J=" + target.ToString(CultureInfo.InvariantCulture));
        }

        private bool EnsureMotionProfile(int motor, ManualMotionProfile profile, Func<bool> cancelled)
        {
            string prefix = "I" + motor;
            if (cancelled()) return false;
            EnsureParameter(prefix + "19", profile.MaximumAcceleration);
            if (cancelled()) return false;
            EnsureParameter(prefix + "20", profile.AccelerationTimeMs);
            if (cancelled()) return false;
            EnsureParameter(prefix + "21", profile.SCurveTimeMs);
            return !cancelled();
        }

        private void EnsureParameter(string variable, double value)
        {
            if (Math.Abs(ReadNumber(variable) - value) <= 0.000001) return;
            Send(variable + "=" + value.ToString("0.########", CultureInfo.InvariantCulture));
            if (Math.Abs(ReadNumber(variable) - value) > 0.000001)
                throw new InvalidOperationException(variable + " 写入后回读不一致。");
        }

        public void Stop(IEnumerable<int> motors)
        {
            StopMotors(motors);
        }

        // Independent axis target following; coordinated interpolation needs a configured PMAC coordinate system.
        public void MovePair(int xMotor, int yMotor, MotionPoint point)
        {
            ValidateMotor(xMotor); ValidateMotor(yMotor);
            if (xMotor == yMotor || Double.IsNaN(point.X) || Double.IsNaN(point.Y) || Double.IsInfinity(point.X) || Double.IsInfinity(point.Y) ||
                Math.Abs(point.X) > 100000000 || Math.Abs(point.Y) > 100000000)
                throw new ArgumentException("双轴目标或电机映射无效。");
            try
            {
                Send("#" + xMotor + "J=" + point.X.ToString("0.###", CultureInfo.InvariantCulture));
                Send("#" + yMotor + "J=" + point.Y.ToString("0.###", CultureInfo.InvariantCulture));
            }
            catch
            {
                try { StopMotors(new int[] { xMotor, yMotor }); } catch { }
                throw;
            }
        }

        public void BeginPvtFollow(int coordinateSystem, int xMotor, int yMotor, CanvasSettings mapping)
        {
            ValidateCoordinateSystem(coordinateSystem);
            ValidateMotor(xMotor); ValidateMotor(yMotor);
            if (xMotor == yMotor || mapping == null) throw new ArgumentException("随动坐标映射无效。");
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.BeginPvtFollow(coordinateSystem, xMotor, yMotor, mapping); return; }
            if (rotaryFollow == null) rotaryFollow = new RotaryProgramStream(delegate(string command) { return SendCore(command).Response; });
            rotaryFollow.Begin(coordinateSystem, xMotor, yMotor, mapping);
        }

        public void AppendPvtFollow(double durationMs, MotionPoint end, MotionPoint velocity)
        {
            if (durationMs < 10 || durationMs > 4000 || !PlanningOptions.Finite(durationMs) ||
                !PlanningOptions.Finite(end.X) || !PlanningOptions.Finite(end.Y) ||
                !PlanningOptions.Finite(velocity.X) || !PlanningOptions.Finite(velocity.Y))
                throw new ArgumentException("随动 PVT 段参数无效。");
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.AppendPvtFollow(durationMs, end, velocity); return; }
            if (rotaryFollow == null) throw new InvalidOperationException("请先启动 PVT 随动。");
            rotaryFollow.Append(new string[] {
                "PVT " + durationMs.ToString("0.########", CultureInfo.InvariantCulture),
                "X " + end.X.ToString("0.########", CultureInfo.InvariantCulture) + ":" + velocity.X.ToString("0.########", CultureInfo.InvariantCulture) +
                " Y " + end.Y.ToString("0.########", CultureInfo.InvariantCulture) + ":" + velocity.Y.ToString("0.########", CultureInfo.InvariantCulture) });
        }

        public void EndPvtFollow()
        {
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.EndPvtFollow(); return; }
            if (rotaryFollow != null) rotaryFollow.End();
        }

        public void AbortPvtFollow(int coordinateSystem, int xMotor, int yMotor)
        {
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.AbortPvtFollow(coordinateSystem, xMotor, yMotor); return; }
            AbortRotaryFollow(coordinateSystem, xMotor, yMotor);
        }

        public void BeginSplineFollow(int coordinateSystem, int xMotor, int yMotor, CanvasSettings mapping)
        {
            ValidateCoordinateSystem(coordinateSystem);
            ValidateMotor(xMotor); ValidateMotor(yMotor);
            if (xMotor == yMotor || mapping == null) throw new ArgumentException("随动坐标映射无效。");
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.BeginSplineFollow(coordinateSystem, xMotor, yMotor, mapping); return; }
            if (rotaryFollow == null) rotaryFollow = new RotaryProgramStream(delegate(string command) { return SendCore(command).Response; });
            rotaryFollow.Begin(coordinateSystem, xMotor, yMotor, mapping);
        }

        public void AppendSplineFollow(double segmentTimeMs, IList<MotionPoint> points)
        {
            if (!PlanningOptions.Finite(segmentTimeMs) || segmentTimeMs < 10 || segmentTimeMs > 4000 || points == null || points.Count < 1 || points.Count > 46)
                throw new ArgumentException("随动 Spline1 段参数无效。");
            foreach (MotionPoint point in points)
                if (!PlanningOptions.Finite(point.X) || !PlanningOptions.Finite(point.Y)) throw new ArgumentException("随动 Spline1 点无效。");
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.AppendSplineFollow(segmentTimeMs, points); return; }
            if (rotaryFollow == null) throw new InvalidOperationException("请先启动 Spline1 随动。");
            List<string> lines = new List<string>();
            lines.Add("SPLINE1 TM " + segmentTimeMs.ToString("0.########", CultureInfo.InvariantCulture));
            foreach (MotionPoint point in points)
                lines.Add("X" + point.X.ToString("0.########", CultureInfo.InvariantCulture) + " Y" + point.Y.ToString("0.########", CultureInfo.InvariantCulture));
            rotaryFollow.Append(lines);
        }

        public void EndSplineFollow()
        {
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.EndSplineFollow(); return; }
            if (rotaryFollow != null) rotaryFollow.End();
        }

        public void AbortSplineFollow(int coordinateSystem, int xMotor, int yMotor)
        {
            IStreamingTrajectoryTransport stream = transport as IStreamingTrajectoryTransport;
            if (stream != null) { stream.AbortSplineFollow(coordinateSystem, xMotor, yMotor); return; }
            AbortRotaryFollow(coordinateSystem, xMotor, yMotor);
        }

        private void AbortRotaryFollow(int coordinateSystem, int xMotor, int yMotor)
        {
            Exception failure = null;
            try { if (rotaryFollow != null) rotaryFollow.Abort(); } catch (Exception error) { failure = error; }
            try { StopPlan(coordinateSystem, xMotor, yMotor); } catch (Exception error) { if (failure == null) failure = error; }
            if (failure != null) throw failure;
        }

        private static void ValidateCoordinateSystem(int coordinateSystem)
        { if (coordinateSystem < 1 || coordinateSystem > 16) throw new ArgumentException("坐标系统必须为 1–16。"); }

        private void StopMotors(IEnumerable<int> motors)
        {
            Exception failure = null;
            foreach (int motor in new HashSet<int>(motors))
            {
                try
                {
                    ValidateMotor(motor); MotorStatus status = MotorStatus.Parse(Send("#" + motor + "?").Response);
                    // J/ also enables the amplifier. Stopping must never re-enable a killed axis.
                    if (status.Enabled) Send("#" + motor + (status.OpenLoop ? "K" : "J/"));
                }
                catch (Exception error) { failure = error; }
            }
            if (failure != null) throw failure;
        }

        private static void ValidateMotor(int motor)
        {
            if (motor < 1 || motor > 8) throw new ArgumentOutOfRangeException("motor", "电机号应为 1–8。");
        }

        public void Download(string path) { if (!IsConnected) throw new InvalidOperationException("请先连接设备。"); transport.Download(path); }

        public ControllerSnapshot ReadSnapshot(int x, int y)
        { return ReadSnapshot(x, y, 0); }
        public ControllerSnapshot ReadSnapshot(int x, int y, int coordinateSystem)
        {
            ValidateMotor(x); ValidateMotor(y);
            if (x == y) throw new ArgumentException("X/Y 不能使用同一电机。");
            ControllerSnapshot sample = new ControllerSnapshot(); double begin = MonotonicClock.NowMs, start = begin;
            sample.XCounts = ReadPosition(x); sample.XTimeMs = (start + MonotonicClock.NowMs) / 2;
            start = MonotonicClock.NowMs; sample.YCounts = ReadPosition(y); sample.YTimeMs = (start + MonotonicClock.NowMs) / 2;
            sample.XStatus = MotorStatus.Parse(Send("#" + x + "?").Response); sample.YStatus = MotorStatus.Parse(Send("#" + y + "?").Response);
            if (coordinateSystem != 0)
            {
                if (coordinateSystem < 1 || coordinateSystem > 16) throw new ArgumentException("坐标系统必须为 1–16。");
                sample.Coordinate = CoordinateStatus.Parse(Send("&" + coordinateSystem + "??").Response);
            }
            sample.QueryMs = MonotonicClock.NowMs - begin; return sample;
        }
        private double ReadPosition(int motor)
        {
            double value;
            if (!Double.TryParse(Send("#" + motor + "P").Response.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !PlanningOptions.Finite(value)) throw new FormatException("位置反馈无效。");
            return value;
        }
        private double ReadNumber(string query)
        {
            double value;
            if (!Double.TryParse(Send(query).Response.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !PlanningOptions.Finite(value))
                throw new FormatException("反馈无效：" + query);
            return value;
        }
        public double StartPlan(MotionPlan plan, double simulatorLagMs)
        { return StartPlan(plan, simulatorLagMs, delegate { return false; }); }
        public double StartPlan(MotionPlan plan, double simulatorLagMs, Func<bool> cancelled)
        {
            if (cancelled()) throw new OperationCanceledException("路径启动已取消。");
            ControllerSnapshot state = ReadSnapshot(plan.Options.XMotor, plan.Options.YMotor);
            if (!state.Ready || !state.InPosition) throw new InvalidOperationException("X/Y 必须闭环就绪、无报警限位且已到位。");
            if (PathGeometry.Distance(plan.Options.Mapping.FromCounts(state.XCounts, state.YCounts), plan.InitialPosition) > 0.01)
                throw new InvalidOperationException("当前位置与规划起点相差超过 0.01 mm，请重新规划。");
            IPlannedTransport simulator = transport as IPlannedTransport;
            if (cancelled()) throw new OperationCanceledException("路径启动已取消。");
            if (simulator != null) { double now = MonotonicClock.NowMs; simulator.StartPlan(plan, simulatorLagMs); return now; }
            CoordinateStatus coordinateState = CoordinateStatus.Parse(Send("&" + plan.Options.CoordinateSystem + "??").Response);
            if (coordinateState.Running || coordinateState.Fault || !coordinateState.InPosition) throw new InvalidOperationException("预留坐标系必须空闲、到位且无运行错误。");
            using (TemporaryProgram file = ProgramFileStore.CreateTemporary(plan.Program))
            {
                try
                {
                    Download(file.Path);
                    if (cancelled()) throw new OperationCanceledException("下载后已取消，未运行路径。");
                    WaitForCoordinateReady(plan.Options.CoordinateSystem, cancelled);
                    double now = MonotonicClock.NowMs;
                    Send("&" + plan.Options.CoordinateSystem + "B" + plan.Options.ProgramNumber + "R"); return now;
                }
                catch { try { StopPlan(plan.Options.CoordinateSystem, plan.Options.XMotor, plan.Options.YMotor); } catch { } throw; }
            }
        }

        private void WaitForCoordinateReady(int coordinateSystem, Func<bool> cancelled)
        {
            Exception lastFailure = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (cancelled()) throw new OperationCanceledException("下载后已取消，未运行路径。");
                try
                {
                    CoordinateStatus state = CoordinateStatus.Parse(Send("&" + coordinateSystem + "??").Response);
                    if (!state.Running && !state.Fault && state.InPosition) return;
                }
                catch (Exception error) { lastFailure = error; }
                Thread.Sleep(100);
            }
            if (lastFailure != null) throw new InvalidOperationException("程序下载后无法重新读取坐标系状态。", lastFailure);
            throw new InvalidOperationException("程序下载后坐标系未恢复为空闲、无故障且已到位状态。");
        }

        public void StopPlan(int coordinateSystem, int x, int y)
        {
            if (coordinateSystem < 1 || coordinateSystem > 16) throw new ArgumentException("坐标系统必须为 1–16。");
            Exception failure = null;
            try { Send("&" + coordinateSystem + "A"); } catch (Exception error) { failure = error; }
            try { Stop(new int[] { x, y }); } catch (Exception error) { failure = error; }
            if (failure != null) throw failure;
        }
        public void SetEnabled(int motor, bool enabled) { ValidateMotor(motor); Send("#" + motor + (enabled ? "J/" : "K")); }
        public void Home(int x, int y, bool both, int plc)
        { Home(x, y, both, plc, delegate { return false; }); }
        public void Home(int x, int y, bool both, int plc, Func<bool> cancelled)
        {
            ValidateMotor(x); ValidateMotor(y);
            if (plc < -1 || plc > 31) throw new ArgumentException("PLC 号为 0–31，-1 表示内置 HOME。");
            if (cancelled()) return;
            ControllerSnapshot current = ReadSnapshot(x, y, 0);
            bool simulated = transport is SimulatorTransport;
            if (simulated ? !current.Ready || !current.InPosition : !current.MechanicalHomeReady)
                throw new InvalidOperationException("回零前轴必须闭环使能、无驱动器故障且已停稳。");
            if (cancelled()) return;
            // The machine PLC owns the hardware homing sequence.
            if (!simulated)
            {
                if (!((x == 1 && y == 2) || (x == 2 && y == 1))) throw new InvalidOperationException("PLC1 的机械回零固定控制电机 1、2，请核对 X/Y 映射。");
                Send("ENABLE PLC 1");
                Stopwatch delay = Stopwatch.StartNew();
                while (delay.ElapsedMilliseconds < 500)
                {
                    if (cancelled()) return;
                    Thread.Sleep(10);
                }
                if (cancelled()) return;
                Send("P801=1"); return;
            }
            if (plc >= 0) Send("ENABLE PLC " + plc);
            else { Send("#" + x + "HM"); if (both && !cancelled()) Send("#" + y + "HM"); }
        }
        public void StopHome(int x, int y, int plc)
        {
            Exception failure = null;
            try
            {
                if (transport is SimulatorTransport) { if (plc >= 0) Send("DISABLE PLC " + plc); }
                else { Send("P801=0"); Send("P802=0"); }
            }
            catch (Exception error) { failure = error; }
            try { Stop(new int[] { x, y }); } catch (Exception error) { failure = error; }
            if (failure != null) throw failure;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            dispatcher.Shutdown(Disconnect);
        }
    }
}
