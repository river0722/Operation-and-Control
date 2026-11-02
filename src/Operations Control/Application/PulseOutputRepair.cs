using System;
using System.Globalization;
using Operations_Control.Communication;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    public sealed class PulseOutputConfiguration
    {
        public int Motor, Address, ExpectedAddress, Flags, FeedbackAddress, Mode, Source;
        public bool NeedsRepair { get { return Address != ExpectedAddress; } }
        public string Description
        {
            get { return "电机 " + Motor + "：输出地址 $" + Address.ToString("X6") +
                (NeedsRepair ? "；当前内部脉冲反馈对应的 C 通道为 $" + ExpectedAddress.ToString("X6") : "，已与内部脉冲反馈对应的 C 通道一致"); }
        }
    }

    // Only repairs local Turbo PMAC2 pulse-output routing after outputs are disabled.
    // Infer the physical channel from flags AND feedback, never from motor number.
    public sealed class PulseOutputRepair
    {
        private readonly IControllerSession session;
        public PulseOutputRepair(IControllerSession session) { this.session = session; }
        private int Read(string query)
        {
            string text = session.Send(query).Response.Trim(); int value;
            bool valid = text.StartsWith("$") ? Int32.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) :
                Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            if (!valid) throw new InvalidOperationException("参数回读无效：" + query);
            return value;
        }
        private void RequireDisabled(int motor)
        {
            MotorStatus status = MotorStatus.Parse(session.Send("#" + motor + "?").Response);
            if (!status.Active) throw new InvalidOperationException("所选轴未激活。");
            if (status.Enabled) throw new InvalidOperationException("请先点击“解除等待并去使能”，确认去使能后再修复输出配置。");
        }
        public PulseOutputConfiguration Inspect(int motor)
        {
            if (motor < 1 || motor > 8) throw new ArgumentException("电机号应为 1–8。");
            RequireDisabled(motor);
            int prefix = motor * 100;
            if (Read("I" + (prefix + 1)) != 0) throw new InvalidOperationException("仅支持不由 PMAC 换相的本地脉冲轴。");
            int flagMode = Read("I" + (prefix + 24));
            if ((flagMode & 1) == 0 || (flagMode & 0x40000) != 0) throw new InvalidOperationException("仅支持本地 Turbo PMAC2 伺服通道。");
            int flags = Read("I" + (prefix + 25));
            int delta = flags - 0x78000, chip = delta / 0x100, channel = (delta % 0x100) / 8;
            if (delta < 0 || chip > 1 || delta % 0x100 > 24 || delta % 8 != 0)
                throw new InvalidOperationException("无法确认本地通道映射，请按实际接线配置。");
            int channelVariable = 7000 + chip * 100 + (channel + 1) * 10;
            int mode = Read("I" + (channelVariable + 6));
            if ((mode != 2 && mode != 3) || Read("I" + channelVariable) != 8)
                throw new InvalidOperationException("当前不是 C 通道 PFM＋内部脉冲反馈，未修改配置。");
            int feedback = Read("I" + (prefix + 3));
            if (feedback < 0x3501 || feedback > 0x35C0 || Read("I" + (prefix + 4)) != feedback)
                throw new InvalidOperationException("位置环与速度环反馈映射无法确认，未修改配置。");
            int source = Read("I" + (8000 + feedback - 0x3501));
            int method = source & 0xF00000;
            if ((source & 0x0FFFFF) != flags || (method != 0 && method != 0xC00000))
                throw new InvalidOperationException("反馈来源与脉冲通道不一致，未修改配置。");
            PulseOutputConfiguration result = new PulseOutputConfiguration { Motor = motor, Flags = flags, FeedbackAddress = feedback,
                Address = Read("I" + (prefix + 2)), ExpectedAddress = flags + 4, Mode = mode, Source = source };
            if (result.Address != flags + 2 && result.Address != flags + 3 && result.Address != result.ExpectedAddress)
                throw new InvalidOperationException("输出地址不属于该轴通道，不能自动修复。");
            RequireDisabled(motor); return result;
        }
        public string Repair(PulseOutputConfiguration approved)
        {
            PulseOutputConfiguration current = Inspect(approved.Motor);
            if (current.Address != approved.Address || current.ExpectedAddress != approved.ExpectedAddress || current.Flags != approved.Flags ||
                current.FeedbackAddress != approved.FeedbackAddress || current.Mode != approved.Mode || current.Source != approved.Source)
                throw new InvalidOperationException("配置在确认后发生变化，请重新检查。");
            if (!current.NeedsRepair) return current.Description + "；无需修改。";
            RequireDisabled(current.Motor);
            string variable = "I" + (current.Motor * 100 + 2);
            session.Send(variable + "=$" + current.ExpectedAddress.ToString("X6"));
            if (Read(variable) != current.ExpectedAddress) throw new InvalidOperationException("输出地址写入后回读不一致，保持去使能，请检查配置。");
            RequireDisabled(current.Motor);
            return "电机 " + current.Motor + " 输出已修复为 C 通道 $" + current.ExpectedAddress.ToString("X6") +
                "，回读已确认；保持去使能。核对接线后可重新使能；未执行 SAVE。";
        }
    }
}
