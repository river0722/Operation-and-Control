using Operations_Control.Files;
using Operations_Control.Motion;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Operations_Control.Models;

namespace Operations_Control.Communication
{
    // In-memory axes only. No PMAC driver or physical device is touched.
    public sealed class SimulatorTransport : IControllerTransport, IPlannedTransport, IPointToPointTransport
    {
        private sealed class Axis
        {
            public double Position;
            public double Speed = 1;
            public double Acceleration = 12;
            public double AccelerationTimeMs = 25, SCurveTimeMs = 8;
            public double Direction;
            public double? Target;
            public SCurveTrajectory Trajectory;
            public double TrajectoryStartMs;
            public bool Enabled = true, Homed, HomeSearching, Fault;
            public double HomeUntil;
        }

        private readonly Axis[] axes = new Axis[8];
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long lastTick;
        private MotionPlan plan;
        private double planStart, planLag;
        public bool PlanRunning { get { Update(); return plan != null; } }
        public bool IsConnected { get; private set; }
        public string DeviceName { get { return "模拟控制器 · 8 轴"; } }

        public SimulatorTransport()
        {
            for (int i = 0; i < axes.Length; i++) axes[i] = new Axis();
        }

        public bool Connect(int ownerWindow)
        {
            IsConnected = true;
            lastTick = clock.ElapsedMilliseconds;
            return true;
        }

        public void Disconnect()
        {
            Update();
            plan = null;
            foreach (Axis axis in axes) { axis.Direction = 0; axis.Target = null; axis.HomeSearching = false; }
            IsConnected = false;
        }

        private void Update()
        {
            long now = clock.ElapsedMilliseconds;
            double elapsed = now - lastTick;
            lastTick = now;
            if (!IsConnected) return;
            if (plan != null)
            {
                MotionPoint point = plan.At(Math.Max(0, now - planStart - planLag));
                axes[plan.Options.XMotor - 1].Position = point.X * plan.Options.Mapping.XCountsPerUnit;
                axes[plan.Options.YMotor - 1].Position = point.Y * plan.Options.Mapping.YCountsPerUnit;
                if (now - planStart >= plan.DurationMs + planLag) plan = null;
            }
            foreach (Axis axis in axes)
            {
                if (axis.HomeSearching && now >= axis.HomeUntil) { axis.Position = 0; axis.Target = null; axis.HomeSearching = false; axis.Homed = true; }
                if (!axis.Enabled || axis.Fault) continue;
                double distance = axis.Speed * elapsed;
                if (axis.Target.HasValue && axis.Trajectory != null)
                {
                    double trajectoryElapsed = now - axis.TrajectoryStartMs;
                    axis.Position = axis.Trajectory.PositionAt(trajectoryElapsed);
                    if (trajectoryElapsed >= axis.Trajectory.DurationMs) { axis.Position = axis.Target.Value; axis.Target = null; axis.Trajectory = null; }
                }
                else if (axis.Target.HasValue)
                {
                    double remaining = axis.Target.Value - axis.Position;
                    axis.Position += Math.Sign(remaining) * Math.Min(Math.Abs(remaining), distance);
                    if (axis.Position == axis.Target.Value) axis.Target = null;
                }
                else axis.Position += axis.Direction * distance;
            }
        }

        public CommandResult Send(string command)
        {
            if (!IsConnected) throw new InvalidOperationException("请先连接模拟控制器。");
            Update();
            string text = command.Trim().ToUpperInvariant();
            if (text == "VERSION") return Result("PMAC simulator 1.0");
            if (text == "?") return Result("模拟控制器已连接");
            Match coordinate = Regex.Match(text, @"^&([1-9]|1[0-6])\?\?$");
            if (coordinate.Success)
            {
                bool active = plan != null && plan.Options.CoordinateSystem == Int32.Parse(coordinate.Groups[1].Value);
                return Result(active ? "000001000000000000" : "000000020000000000");
            }
            if (Regex.IsMatch(text, @"^&(?:[1-9]|1[0-6])A$")) { StopPlan(); return Result(String.Empty); }
            if (Regex.IsMatch(text, @"^(ENABLE|DISABLE) PLC \d+$")) throw new InvalidOperationException("模拟器未执行用户 PLC；请选择内置 HOME 模拟回零。");
            Match diagnostic = Regex.Match(text, @"^SIM(FAULT|RESET)([1-8])$");
            if (diagnostic.Success)
            {
                int number = Int32.Parse(diagnostic.Groups[2].Value);
                if (diagnostic.Groups[1].Value == "FAULT") InjectFault(number);
                else { Axis axis = axes[number - 1]; axis.Fault = false; axis.Enabled = false; axis.Direction = 0; axis.Target = null; }
                return Result(String.Empty);
            }
            Match parameter = Regex.Match(text, @"^I([1-8])(19|20|21|22)(?:=([+-]?\d+(?:\.\d+)?))?$");
            if (parameter.Success)
            {
                Axis axis = axes[Int32.Parse(parameter.Groups[1].Value) - 1];
                string number = parameter.Groups[2].Value;
                double current = number == "19" ? axis.Acceleration : number == "20" ? axis.AccelerationTimeMs : number == "21" ? axis.SCurveTimeMs : axis.Speed;
                if (!parameter.Groups[3].Success) return Result(current.ToString(CultureInfo.InvariantCulture));
                double value = Double.Parse(parameter.Groups[3].Value, CultureInfo.InvariantCulture);
                if (number == "19")
                {
                    if (value <= 0 || value > 12) throw new InvalidOperationException("加速度应大于 0 且不超过 12 ct/ms^2。");
                    axis.Acceleration = value;
                }
                else if (number == "20")
                {
                    if (value < 4 || value > 1000000 || value != Math.Floor(value)) throw new InvalidOperationException("总加速时间应为 4–1000000 ms 的整数。");
                    axis.AccelerationTimeMs = value;
                }
                else if (number == "21")
                {
                    if (value <= 0 || value > 1000000 || value != Math.Floor(value)) throw new InvalidOperationException("S 曲线时间应为正整数毫秒。");
                    axis.SCurveTimeMs = value;
                }
                else
                {
                    if (value <= 0 || value > 15) throw new InvalidOperationException("点动速度应大于 0 且不超过 15 ct/ms。");
                    axis.Speed = value;
                }
                return Result(String.Empty);
            }
            Match motor = Regex.Match(text, @"^#([1-8])(P|F|\?|K|HM|HOME|J\+|J-|J/|HOMEZ|J[=\^]([+-]?\d+(?:\.\d+)?))$");
            if (!motor.Success) throw new InvalidOperationException("模拟模式支持 #nP、#nJ+、#nJ-、#nJ/、#nJ=值、#nJ^值、#nHOMEZ、In22、VERSION。");
            Axis selected = axes[Int32.Parse(motor.Groups[1].Value) - 1];
            string operation = motor.Groups[2].Value;
            if (operation == "P") return Result(selected.Position.ToString("0.###", CultureInfo.InvariantCulture));
            if (operation == "F")
            {
                double error = 0;
                if (plan != null)
                {
                    MotionPoint desired = plan.Options.Mapping.ToCounts(plan.At(Math.Min(plan.DurationMs, clock.ElapsedMilliseconds - planStart)));
                    int number = Int32.Parse(motor.Groups[1].Value);
                    if (number == plan.Options.XMotor) error = desired.X - selected.Position;
                    else if (number == plan.Options.YMotor) error = desired.Y - selected.Position;
                }
                return Result(error.ToString("0.###", CultureInfo.InvariantCulture));
            }
            if (operation == "?")
            {
                bool moving = selected.HomeSearching || selected.Target.HasValue || selected.Direction != 0 || (plan != null && (Int32.Parse(motor.Groups[1].Value) == plan.Options.XMotor || Int32.Parse(motor.Groups[1].Value) == plan.Options.YMotor));
                int word1 = 0x800000 | (selected.Enabled ? 0x080000 : 0x040000) | (!moving && selected.Enabled ? 0x002000 : 0) | (selected.HomeSearching ? 0x400 : 0);
                int word2 = (selected.Homed ? 0x400 : 0) | (selected.Fault ? 8 : 0) | (!moving && selected.Enabled && !selected.Fault ? 1 : 0);
                return Result(word1.ToString("X6") + word2.ToString("X6"));
            }
            if (operation == "K") { StopPlan(); selected.Enabled = false; selected.Direction = 0; selected.Target = null; selected.Trajectory = null; selected.HomeSearching = false; return Result(String.Empty); }
            if (operation != "J/" && operation != "HOMEZ" && (!selected.Enabled || selected.Fault)) throw new InvalidOperationException("轴未就绪。");
            if (operation == "J/") { StopPlan(); selected.Enabled = true; selected.Direction = 0; selected.Target = null; selected.Trajectory = null; selected.HomeSearching = false; }
            else if (operation == "HOMEZ") { selected.Position = 0; selected.Direction = 0; selected.Target = null; selected.Trajectory = null; }
            else if (operation == "HM" || operation == "HOME")
            {
                if (!selected.Enabled || selected.Fault) throw new InvalidOperationException("轴未就绪。");
                selected.Homed = false; selected.HomeSearching = true; selected.Target = 0; selected.Trajectory = null; selected.Direction = 0;
                selected.HomeUntil = clock.ElapsedMilliseconds + Math.Max(300, Math.Abs(selected.Position) / selected.Speed);
            }
            else if (operation == "J+" || operation == "J-") { selected.Target = null; selected.Trajectory = null; selected.Direction = operation == "J+" ? 1 : -1; }
            else
            {
                double value = Double.Parse(motor.Groups[3].Value, CultureInfo.InvariantCulture);
                selected.Target = operation[1] == '^' ? selected.Position + value : value;
                selected.Trajectory = null;
                selected.Direction = 0;
            }
            return Result(String.Empty);
        }

        public void MovePointToPoint(int motor, double target, double speed, ManualMotionProfile profile)
        {
            Update();
            if (motor < 1 || motor > 8 || Double.IsNaN(target) || Double.IsInfinity(target) ||
                Double.IsNaN(speed) || Double.IsInfinity(speed) || speed <= 0 || speed > 15 || profile == null)
                throw new ArgumentException("模拟点到点目标或参数无效。");
            Axis axis = axes[motor - 1];
            if (!axis.Enabled || axis.Fault) throw new InvalidOperationException("轴未就绪。");
            axis.Speed = speed;
            axis.Target = target;
            axis.Direction = 0;
            axis.Trajectory = new SCurveTrajectory(axis.Position, target, speed, profile.MaximumAcceleration,
                profile.AccelerationTimeMs, profile.SCurveTimeMs);
            axis.TrajectoryStartMs = clock.ElapsedMilliseconds;
        }

        private static CommandResult Result(string response) { return new CommandResult(response, response.Length); }

        public void StartPlan(MotionPlan value, double lagMs)
        {
            Update();
            if (!IsConnected || plan != null) throw new InvalidOperationException("模拟器未连接或已有路径运行。");
            if (lagMs < 0 || lagMs > 2000) throw new ArgumentException("模拟延迟应为 0–2000 ms。");
            foreach (int motor in new int[] { value.Options.XMotor, value.Options.YMotor })
                if (!axes[motor - 1].Enabled || axes[motor - 1].Fault || axes[motor - 1].Target.HasValue || axes[motor - 1].Direction != 0 || axes[motor - 1].HomeSearching) throw new InvalidOperationException("模拟轴必须闭环且已停止。");
            MotionPoint actual = value.Options.Mapping.FromCounts(axes[value.Options.XMotor - 1].Position, axes[value.Options.YMotor - 1].Position);
            if (PathGeometry.Distance(actual, value.InitialPosition) > 0.01) throw new InvalidOperationException("规划起点已变化，请重新规划。");
            plan = value; planStart = clock.ElapsedMilliseconds; planLag = lagMs;
        }
        public void StopPlan() { Update(); plan = null; }
        public void InjectFault(int motor)
        {
            Update(); Axis axis = axes[motor - 1]; axis.Fault = true; axis.Enabled = false;
            axis.Direction = 0; axis.Target = null; axis.HomeSearching = false; StopPlan();
        }

        public void Download(string filePath)
        {
            if (!IsConnected) throw new InvalidOperationException("请先连接模拟控制器。");
            if (!File.Exists(filePath)) throw new FileNotFoundException("程序文件不存在。", filePath);
            // Download is deliberately only acknowledged in simulation; scripts are not executed.
        }

        public void Dispose() { Disconnect(); }
    }
}
