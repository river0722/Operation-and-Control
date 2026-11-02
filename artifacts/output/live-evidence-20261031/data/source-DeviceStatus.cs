using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Operations_Control.Models
{
    public static class MonotonicClock
    {
        public static double NowMs { get { return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency; } }
    }
    public sealed class MotorStatus
    {
        public int FirstWord, SecondWord;
        public bool Active { get { return (FirstWord & 0x800000) != 0; } }
        public bool NegativeLimit { get { return (FirstWord & 0x400000) != 0; } }
        public bool PositiveLimit { get { return (FirstWord & 0x200000) != 0; } }
        public bool OpenLoop { get { return (FirstWord & 0x040000) != 0; } }
        public bool VelocityZero { get { return (FirstWord & 0x002000) != 0; } }
        public bool Homing { get { return (FirstWord & 0x000400) != 0; } }
        // Turbo PMAC SRM 6-43..6-47: amplifier enable is word 1 bit 19.
        // Word 2 bit 14 is reserved (the legacy example in the SRM is stale).
        public bool Enabled { get { return (FirstWord & 0x080000) != 0; } }
        public bool HomeComplete { get { return (SecondWord & 0x000400) != 0; } }
        public bool InPosition { get { return (SecondWord & 1) != 0; } }
        public bool Fault { get { return (SecondWord & 0x00196C) != 0 || (FirstWord & 0x004000) != 0; } }
        public bool FollowingWarning { get { return (SecondWord & 2) != 0; } }
        public bool BlockingFault { get { return (SecondWord & 0x00016C) != 0 || (FirstWord & 0x004000) != 0; } }
        public bool PositionLimitStopped { get { return (SecondWord & 0x001800) != 0; } }
        public string FaultDescription
        {
            get
            {
                string text = String.Empty;
                if ((SecondWord & 8) != 0) text += "驱动器报警；";
                if ((SecondWord & 4) != 0) text += "跟随误差超限；";
                if ((SecondWord & 0x20) != 0) text += "I²t 电流保护；";
                if ((SecondWord & 0x40) != 0) text += "积分跟随误差保护；";
                if ((SecondWord & 0x100) != 0) text += "相位搜索错误；";
                if ((SecondWord & 0x1000) != 0) text += "期望位置软件限位停止；";
                if ((FirstWord & 0x4000) != 0) text += "运动数据块准备不足；";
                if ((SecondWord & 0x800) != 0) text += "位置限位停止；";
                return text.Length == 0 ? "轴故障，检查状态字" : text.TrimEnd('；');
            }
        }
        public bool Ready { get { return Active && Enabled && !OpenLoop && !Fault && !NegativeLimit && !PositiveLimit; } }
        public static MotorStatus Parse(string text)
        {
            text = Regex.Replace((text ?? String.Empty).Trim(), @"\s", String.Empty).TrimStart('$');
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            if (!Regex.IsMatch(text, "^[0-9a-fA-F]{12}$")) throw new FormatException("电机状态应为两个 24 位字（12 位十六进制）：" + text);
            return new MotorStatus { FirstWord = Int32.Parse(text.Substring(0, 6), NumberStyles.HexNumber), SecondWord = Int32.Parse(text.Substring(6, 6), NumberStyles.HexNumber) };
        }
        public override string ToString()
        {
            return (Enabled ? "已使能" : "未使能") + (OpenLoop ? " / 开环" : " / 闭环") + (Fault ? " / " + FaultDescription : "") +
                (PositiveLimit ? " / 正限位" : "") + (NegativeLimit ? " / 负限位" : "") +
                (FollowingWarning ? " / 跟随误差预警" : "") + (Homing ? " / 寻零中" : HomeComplete ? " / 已寻零" : " / 未寻零") +
                (InPosition ? " / 到位" : Enabled ? VelocityZero ? " / 指令已停止，未到位" : " / 指令运动中" : " / 位置监测");
        }
    }
    public sealed class CoordinateStatus
    {
        public int FirstWord, SecondWord, ThirdWord;
        public bool Running { get { return (FirstWord & 1) != 0; } }
        public bool Fault { get { return (SecondWord & 0x580000) != 0; } }
        public bool InPosition { get { return (SecondWord & 0x020000) != 0; } }
        public static CoordinateStatus Parse(string text)
        {
            text = Regex.Replace((text ?? String.Empty).Trim(), @"\s", String.Empty).TrimStart('$');
            if (!Regex.IsMatch(text, "^[0-9a-fA-F]{18}$")) throw new FormatException("Turbo 坐标系状态应为 18 位十六进制。");
            return new CoordinateStatus { FirstWord = Int32.Parse(text.Substring(0, 6), NumberStyles.HexNumber),
                SecondWord = Int32.Parse(text.Substring(6, 6), NumberStyles.HexNumber), ThirdWord = Int32.Parse(text.Substring(12, 6), NumberStyles.HexNumber) };
        }
    }
    public sealed class ControllerSnapshot
    {
        public double XCounts, YCounts, XTimeMs, YTimeMs, QueryMs;
        public MotorStatus XStatus, YStatus;
        public CoordinateStatus Coordinate;
        public double TimeMs { get { return (XTimeMs + YTimeMs) / 2; } }
        public bool Ready { get { return XStatus.Ready && YStatus.Ready; } }
        public bool InPosition { get { return XStatus.InPosition && YStatus.InPosition && XStatus.VelocityZero && YStatus.VelocityZero; } }
    }
}
