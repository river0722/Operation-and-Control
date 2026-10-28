using System;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    public sealed class MotionGuard
    {
        public DeviceConfiguration Configuration = new DeviceConfiguration();
        public CanvasSettings Mapping = CanvasSettings.Default;
        public bool MappingReady, Simulation;
        public int XMotor = 1, YMotor = 2;
        public void Range(int motor, out double minimum, out double maximum)
        {
            if (motor == XMotor || motor == YMotor)
            {
                if (!MappingReady) throw new InvalidOperationException("请先确认比例与行程并应用坐标。");
                minimum = motor == XMotor ? Mapping.XMin * Mapping.XCountsPerUnit : Mapping.YMin * Mapping.YCountsPerUnit;
                maximum = motor == XMotor ? Mapping.XMax * Mapping.XCountsPerUnit : Mapping.YMax * Mapping.YCountsPerUnit;
            }
            else
            {
                AxisConfiguration axis = Configuration.Axis(motor);
                if (!axis.TravelConfigured) throw new InvalidOperationException("电机 " + motor + " 尚未配置单轴行程。");
                minimum = axis.MinimumCounts; maximum = axis.MaximumCounts;
            }
        }
        public string Reason(AxisSample sample, int direction, double? target, decimal speed, bool requireStopped)
        {
            if (sample == null || sample.Status == null || MonotonicClock.NowMs - sample.TimeMs > 1000) return "反馈未知或已过期";
            MotorStatus status = sample.Status;
            if (!status.Active) return "电机未激活";
            if (!status.Enabled) return "电机未使能";
            if (status.OpenLoop) return "电机处于开环";
            if (status.BlockingFault) return status.FaultDescription;
            if (status.Homing) return "电机正在寻零";
            if (requireStopped && !status.VelocityZero) return "电机指令仍在运动，请先停止";
            if (requireStopped && !status.InPosition) return "指令已停止，但位置未到位；可用“解除等待并去使能”进入恢复";
            if (status.PositiveLimit && direction >= 0) return "正限位触发，只允许向负方向退出";
            if (status.NegativeLimit && direction <= 0) return "负限位触发，只允许向正方向退出";
            if (speed <= 0 || speed > Configuration.Axis(sample.Motor).MaximumSpeed) return "速度超过该轴配置的上限";
            double minimum, maximum;
            try { Range(sample.Motor, out minimum, out maximum); } catch (InvalidOperationException error) { return error.Message; }
            if (status.PositionLimitStopped && !status.PositiveLimit && !status.NegativeLimit &&
                !((sample.Position >= maximum - 0.5 && direction < 0) || (sample.Position <= minimum + 0.5 && direction > 0)))
                return "控制器位置限位停止；请核对软件限位与行程后按设备规则退出";
            if (target.HasValue && (target.Value < minimum || target.Value > maximum)) return "目标超出该轴行程";
            if (!target.HasValue && ((direction > 0 && sample.Position >= maximum) || (direction < 0 && sample.Position <= minimum))) return "已到达该方向的软件行程边界";
            return null;
        }
        public void Require(AxisSample sample, int direction, double? target, decimal speed, bool requireStopped)
        {
            string reason = Reason(sample, direction, target, speed, requireStopped);
            if (reason != null) throw new InvalidOperationException("禁止运动：" + reason);
        }
    }
}
