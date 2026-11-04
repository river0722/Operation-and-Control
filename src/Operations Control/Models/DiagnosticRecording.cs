using System;
using System.Collections.Generic;

namespace Operations_Control.Models
{
    public sealed class DiagnosticSample
    {
        public int Motor;
        public double TimeMs, Position;
        public double? FollowingError;
        public MotorStatus Status;
        public bool GapBefore;
        public AxisSample ToAxisSample()
        {
            return new AxisSample(Motor, Position, null, DateTime.MinValue)
                { TimeMs = TimeMs, FollowingError = FollowingError, Status = Status };
        }
    }

    public sealed class DiagnosticRecording
    {
        public string Source;
        public DateTime StartedUtc;
        public readonly List<DiagnosticSample> Samples = new List<DiagnosticSample>();
    }
}
