using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    public sealed class AlarmRecord
    {
        public DateTime Occurred, Recovered;
        public string Source, Message, Advice;
        public bool Active = true, Acknowledged;
    }
    public sealed class AlarmService
    {
        private readonly Dictionary<string, AlarmRecord> active = new Dictionary<string, AlarmRecord>();
        public readonly List<AlarmRecord> History = new List<AlarmRecord>();
        public event Action<AlarmRecord> Transition;
        public event EventHandler Changed;
        public int ActiveCount { get { return active.Count; } }
        public void Set(string key, bool present, string source, string message, string advice)
        {
            AlarmRecord record;
            if (present && active.TryGetValue(key, out record) && record.Message != message)
            {
                record.Active = false; record.Recovered = DateTime.Now; active.Remove(key); if (Transition != null) Transition(record);
            }
            if (present && !active.ContainsKey(key))
            {
                record = new AlarmRecord { Occurred = DateTime.Now, Source = source, Message = message, Advice = advice };
                active.Add(key, record); History.Add(record); if (History.Count > 1000) History.RemoveAt(0);
            }
            else if (!present && active.TryGetValue(key, out record))
            { record.Active = false; record.Recovered = DateTime.Now; active.Remove(key); }
            else return;
            if (Transition != null) Transition(record); if (Changed != null) Changed(this, EventArgs.Empty);
        }
        public void Update(ControllerFeedback feedback)
        {
            HashSet<int> seen = new HashSet<int>();
            foreach (AxisSample sample in feedback.Axes)
            {
                if (sample.Status == null || !seen.Add(sample.Motor)) continue;
                string key = "axis" + sample.Motor, source = "电机 " + sample.Motor;
                Set(key + "/fault", sample.Status.BlockingFault, source, sample.Status.FaultDescription + " [" + sample.Status.FirstWord.ToString("X6") + sample.Status.SecondWord.ToString("X6") + "]", "检查驱动器和接线；消除原因后执行已配置的复位流程。");
                Set(key + "/positive", sample.Status.PositiveLimit, source, "正限位触发", "检查实际位置；按设备规则向负方向退出限位。");
                Set(key + "/negative", sample.Status.NegativeLimit, source, "负限位触发", "检查实际位置；按设备规则向正方向退出限位。");
                Set(key + "/position-limit", sample.Status.PositionLimitStopped, source, "控制器位置限位停止", "核对控制器软件限位与配置行程；按设备规则向安全方向退出。");
                Set(key + "/following", sample.Status.FollowingWarning, source, "跟随误差预警", "检查负载、速度、加速度与伺服整定。");
            }
        }
        public void Acknowledge()
        { foreach (AlarmRecord record in active.Values) record.Acknowledged = true; if (Changed != null) Changed(this, EventArgs.Empty); }
    }
}
