using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    // In-memory axes only. No PMAC driver or physical device is touched.
    public sealed class SimulatorTransport : IControllerTransport
    {
        private sealed class Axis
        {
            public double Position;
            public double Speed = 1;
            public double Direction;
            public double? Target;
        }

        private readonly Axis[] axes = new Axis[8];
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long lastTick;
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
            foreach (Axis axis in axes) { axis.Direction = 0; axis.Target = null; }
            IsConnected = false;
        }

        private void Update()
        {
            long now = clock.ElapsedMilliseconds;
            double elapsed = now - lastTick;
            lastTick = now;
            if (!IsConnected) return;
            foreach (Axis axis in axes)
            {
                double distance = axis.Speed * elapsed;
                if (axis.Target.HasValue)
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
            Match speed = Regex.Match(text, @"^I([1-8])22(?:=([+-]?\d+(?:\.\d+)?))?$");
            if (speed.Success)
            {
                Axis axis = axes[Int32.Parse(speed.Groups[1].Value) - 1];
                if (!speed.Groups[2].Success) return Result(axis.Speed.ToString(CultureInfo.InvariantCulture));
                double value = Double.Parse(speed.Groups[2].Value, CultureInfo.InvariantCulture);
                if (value <= 0 || value > 15) throw new InvalidOperationException("点动速度应大于 0 且不超过 15 ct/ms。");
                axis.Speed = value;
                return Result(String.Empty);
            }
            Match motor = Regex.Match(text, @"^#([1-8])(P|J\+|J-|J/|HOMEZ|J[=\^]([+-]?\d+(?:\.\d+)?))$");
            if (!motor.Success) throw new InvalidOperationException("模拟模式支持 #nP、#nJ+、#nJ-、#nJ/、#nJ=值、#nJ^值、#nHOMEZ、In22、VERSION。");
            Axis selected = axes[Int32.Parse(motor.Groups[1].Value) - 1];
            string operation = motor.Groups[2].Value;
            if (operation == "P") return Result(selected.Position.ToString("0.###", CultureInfo.InvariantCulture));
            if (operation == "J/") { selected.Direction = 0; selected.Target = null; }
            else if (operation == "HOMEZ") { selected.Position = 0; selected.Direction = 0; selected.Target = null; }
            else if (operation == "J+" || operation == "J-") { selected.Target = null; selected.Direction = operation == "J+" ? 1 : -1; }
            else
            {
                double value = Double.Parse(motor.Groups[3].Value, CultureInfo.InvariantCulture);
                selected.Target = operation[1] == '^' ? selected.Position + value : value;
                selected.Direction = 0;
            }
            return Result(String.Empty);
        }

        private static CommandResult Result(string response) { return new CommandResult(response, response.Length); }

        public void Download(string filePath)
        {
            if (!IsConnected) throw new InvalidOperationException("请先连接模拟控制器。");
            if (!File.Exists(filePath)) throw new FileNotFoundException("程序文件不存在。", filePath);
            // Download is deliberately only acknowledged in simulation; scripts are not executed.
        }

        public void Dispose() { Disconnect(); }
    }
}
