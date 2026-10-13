using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    public sealed class ControllerSession : IDisposable
    {
        private readonly Thread thread;
        private readonly Control dispatcher;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<int, double> positions = new Dictionary<int, double>();
        private readonly Dictionary<int, long> timestamps = new Dictionary<int, long>();
        private IControllerTransport transport;
        private bool disposed;
        public bool IsConnected { get { return transport != null && transport.IsConnected; } }
        public string DeviceName { get { return transport == null ? String.Empty : transport.DeviceName; } }

        public ControllerSession()
        {
            Control worker = null;
            using (ManualResetEvent ready = new ManualResetEvent(false))
            {
                thread = new Thread(delegate()
                {
                    worker = new Control();
                    IntPtr handle = worker.Handle;
                    ready.Set();
                    Application.Run();
                    worker.Dispose();
                });
                thread.IsBackground = true;
                thread.Name = "PMAC communication (STA)";
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.WaitOne();
            }
            dispatcher = worker;
        }

        public void Execute(Control owner, Action action, Action<Exception> completed)
        {
            if (disposed) return;
            dispatcher.BeginInvoke((MethodInvoker)delegate
            {
                Exception failure = null;
                try { action(); }
                catch (Exception error) { failure = error; }
                if (!owner.IsDisposed && owner.IsHandleCreated)
                {
                    try { owner.BeginInvoke((MethodInvoker)delegate { completed(failure); }); }
                    catch (InvalidOperationException) { }
                }
            });
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
            finally { transport = null; positions.Clear(); timestamps.Clear(); }
        }

        public void ClearSamples() { positions.Clear(); timestamps.Clear(); }

        public CommandResult Send(string command)
        {
            if (!IsConnected) throw new InvalidOperationException("请先连接设备。");
            if (String.IsNullOrEmpty(command) || command.Length > 1024 || command.IndexOf('\r') >= 0 || command.IndexOf('\n') >= 0)
                throw new ArgumentException("请输入一条不超过 1024 字符的命令。");
            return transport.Send(command.Trim());
        }

        public void SetSpeed(int motor, decimal speed)
        {
            ValidateMotor(motor);
            if (speed <= 0 || speed > 15) throw new ArgumentOutOfRangeException("speed");
            Send("I" + motor + "22=" + speed.ToString(CultureInfo.InvariantCulture));
        }

        public void Jog(int motor, int direction, decimal speed, decimal? step)
        {
            SetSpeed(motor, speed);
            Send("#" + motor + (step.HasValue ? "J^" + (step.Value * direction).ToString(CultureInfo.InvariantCulture) : direction > 0 ? "J+" : "J-"));
        }

        public void Move(int motor, decimal target, decimal speed)
        {
            SetSpeed(motor, speed);
            Send("#" + motor + "J=" + target.ToString(CultureInfo.InvariantCulture));
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

        private void StopMotors(IEnumerable<int> motors)
        {
            Exception failure = null;
            foreach (int motor in new HashSet<int>(motors))
            {
                try { ValidateMotor(motor); Send("#" + motor + "J/"); }
                catch (Exception error) { failure = error; }
            }
            if (failure != null) throw failure;
        }

        public AxisSample[] ReadAxes(int[] motors)
        {
            Dictionary<int, AxisSample> samples = new Dictionary<int, AxisSample>();
            foreach (int motor in motors)
            {
                ValidateMotor(motor);
                if (samples.ContainsKey(motor)) continue;
                string response = Send("#" + motor + "P").Response.Trim();
                double position;
                if (!Double.TryParse(response, NumberStyles.Float, CultureInfo.InvariantCulture, out position) || Double.IsNaN(position) || Double.IsInfinity(position))
                    throw new InvalidOperationException("电机 " + motor + " 的位置应答无效：" + response);
                long now = clock.ElapsedMilliseconds;
                double? velocity = null;
                if (positions.ContainsKey(motor) && now > timestamps[motor])
                    velocity = (position - positions[motor]) / (now - timestamps[motor]);
                positions[motor] = position;
                timestamps[motor] = now;
                samples.Add(motor, new AxisSample(motor, position, velocity, DateTime.Now));
            }
            AxisSample[] result = new AxisSample[motors.Length];
            for (int i = 0; i < motors.Length; i++) result[i] = samples[motors[i]];
            return result;
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
        public double StartPlan(MotionPlan plan, double simulatorLagMs)
        { return StartPlan(plan, simulatorLagMs, delegate { return false; }); }
        public double StartPlan(MotionPlan plan, double simulatorLagMs, Func<bool> cancelled)
        {
            if (cancelled()) throw new OperationCanceledException("路径启动已取消。");
            ControllerSnapshot state = ReadSnapshot(plan.Options.XMotor, plan.Options.YMotor);
            if (!state.Ready || !state.InPosition) throw new InvalidOperationException("X/Y 必须闭环就绪、无报警限位且已到位。");
            if (PathGeometry.Distance(plan.Options.Mapping.FromCounts(state.XCounts, state.YCounts), plan.InitialPosition) > 0.01)
                throw new InvalidOperationException("当前位置与规划起点相差超过 0.01 mm，请重新规划。");
            SimulatorTransport simulator = transport as SimulatorTransport;
            if (cancelled()) throw new OperationCanceledException("路径启动已取消。");
            if (simulator != null) { double now = MonotonicClock.NowMs; simulator.StartPlan(plan, simulatorLagMs); return now; }
            CoordinateStatus coordinateState = CoordinateStatus.Parse(Send("&" + plan.Options.CoordinateSystem + "??").Response);
            if (coordinateState.Running || coordinateState.Fault || !coordinateState.InPosition) throw new InvalidOperationException("预留坐标系必须空闲、到位且无运行错误。");
            string file = Path.Combine(Path.GetTempPath(), "pmac-trajectory-" + Guid.NewGuid().ToString("N") + ".pmc");
            try
            {
                File.WriteAllText(file, plan.Program, Encoding.ASCII); Download(file);
                if (cancelled()) throw new OperationCanceledException("下载后已取消，未运行路径。");
                double now = MonotonicClock.NowMs;
                Send("&" + plan.Options.CoordinateSystem + "B" + plan.Options.ProgramNumber + "R"); return now;
            }
            catch { try { StopPlan(plan.Options.CoordinateSystem, plan.Options.XMotor, plan.Options.YMotor); } catch { } throw; }
            finally { if (File.Exists(file)) File.Delete(file); }
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
        {
            ValidateMotor(x); ValidateMotor(y);
            if (plc < -1 || plc > 31) throw new ArgumentException("PLC 号为 0–31，-1 表示内置 HOME。");
            if (plc >= 0) Send("ENABLE PLC " + plc);
            else { Send("#" + x + "HM"); if (both) Send("#" + y + "HM"); }
        }
        public void StopHome(int x, int y, int plc)
        {
            Exception failure = null;
            if (plc >= 0) try { Send("DISABLE PLC " + plc); } catch (Exception error) { failure = error; }
            try { Stop(new int[] { x, y }); } catch (Exception error) { failure = error; }
            if (failure != null) throw failure;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            dispatcher.BeginInvoke((MethodInvoker)delegate
            {
                try { Disconnect(); }
                finally { Application.ExitThread(); }
            });
        }
    }
}
