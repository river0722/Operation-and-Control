using System;

namespace Operations_Control.Models
{
    public sealed class CommandResult
    {
        public string Response { get; private set; }
        public int Status { get; private set; }

        public CommandResult(string response, int status)
        {
            Response = response ?? String.Empty;
            Status = status;
        }

        public static void Validate(string response, int status)
        {
            // PcommServer: high nibble is status; low 28 bits are response length.
            uint code = unchecked((uint)status) & 0xF0000000;
            if (code != 0 && code != 0x80000000)
                throw new InvalidOperationException("通信应答异常，状态 0x" + unchecked((uint)status).ToString("X8") + "：" + response);
            if (!String.IsNullOrEmpty(response) &&
                (response.IndexOf('\a') >= 0 || response.TrimStart().StartsWith("ERR", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("控制器拒绝命令：" + response.Trim());
        }
    }

    public sealed class AxisSample
    {
        public int Motor { get; private set; }
        public double Position { get; private set; }
        public double? Velocity { get; private set; }
        public DateTime Timestamp { get; private set; }

        public AxisSample(int motor, double position, double? velocity, DateTime timestamp)
        {
            Motor = motor;
            Position = position;
            Velocity = velocity;
            Timestamp = timestamp;
        }
    }
}
