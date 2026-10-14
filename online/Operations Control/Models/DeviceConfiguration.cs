using System;
using System.Collections.Generic;

namespace Operations_Control.Models
{
    public sealed class AxisConfiguration
    {
        public int Motor = 1;
        public bool TravelConfigured;
        public double MinimumCounts = -1000, MaximumCounts = 1000;
        public decimal MaximumSpeed = 1;
        public double PositionToleranceCounts = 0.5;
        public string ResetCommand = String.Empty;
    }

    public sealed class IoConfiguration
    {
        public string Name = "输入信号";
        public string Query = String.Empty;
        public bool ActiveLow;
    }

    public sealed class DeviceConfiguration
    {
        public string DeviceName = String.Empty;
        public int XMotor = 1, YMotor = 2;
        public double XMin = -10, XMax = 10, YMin = -10, YMax = 10, XScale = 100, YScale = 100;
        public List<AxisConfiguration> Axes = new List<AxisConfiguration>();
        public List<IoConfiguration> Inputs = new List<IoConfiguration>();
        public AxisConfiguration Axis(int motor)
        {
            foreach (AxisConfiguration axis in Axes) if (axis.Motor == motor) return axis;
            AxisConfiguration next = new AxisConfiguration { Motor = motor }; Axes.Add(next); return next;
        }
        public void Validate()
        {
            if (XMotor < 1 || XMotor > 8 || YMotor < 1 || YMotor > 8 || XMotor == YMotor) throw new ArgumentException("X/Y 轴映射无效。");
            new CanvasSettings(XMin, XMax, YMin, YMax, XScale, YScale, "mm");
            if (Inputs.Count > 32) throw new ArgumentException("单次诊断最多配置 32 路 I/O。");
            HashSet<int> seen = new HashSet<int>();
            foreach (AxisConfiguration axis in Axes)
            {
                if (axis.Motor < 1 || axis.Motor > 8 || !seen.Add(axis.Motor) || axis.MaximumSpeed <= 0 || axis.MaximumSpeed > 15 ||
                    !PlanningOptions.Finite(axis.PositionToleranceCounts) || axis.PositionToleranceCounts <= 0 || axis.PositionToleranceCounts > 1000000 ||
                    !PlanningOptions.Finite(axis.MinimumCounts) || !PlanningOptions.Finite(axis.MaximumCounts) || axis.MinimumCounts >= axis.MaximumCounts ||
                    Math.Abs(axis.MinimumCounts) > 100000000 || Math.Abs(axis.MaximumCounts) > 100000000)
                    throw new ArgumentException("单轴行程或最大速度无效。");
                if (!String.IsNullOrEmpty(axis.ResetCommand) && (axis.ResetCommand.Length > 1024 || axis.ResetCommand.IndexOf('\r') >= 0 || axis.ResetCommand.IndexOf('\n') >= 0))
                    throw new ArgumentException("复位命令必须为单条指令。");
            }
            foreach (IoConfiguration input in Inputs)
                if (!System.Text.RegularExpressions.Regex.IsMatch(input.Query ?? "", @"^[MP]\d{1,5}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new ArgumentException("I/O 查询只允许已映射的 M/P 变量，例如 M100；不允许写入或执行命令。");
        }
    }
}
