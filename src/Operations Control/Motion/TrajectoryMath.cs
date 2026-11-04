using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Motion
{
    public static class TrajectoryMath
    {
        public static MotionSegment SplineSegment(MotionPoint a, MotionPoint b, MotionPoint c, MotionPoint d, double time)
        {
            return new MotionSegment { DurationMs = time,
                Start = new MotionPoint((a.X + 4 * b.X + c.X) / 6, (a.Y + 4 * b.Y + c.Y) / 6),
                End = new MotionPoint((b.X + 4 * c.X + d.X) / 6, (b.Y + 4 * c.Y + d.Y) / 6),
                StartVelocity = new MotionPoint((c.X - a.X) * 500 / time, (c.Y - a.Y) * 500 / time),
                EndVelocity = new MotionPoint((d.X - b.X) * 500 / time, (d.Y - b.Y) * 500 / time) };
        }
        public static double PeakVelocity(double start, double end, double v0, double v1, double seconds)
        {
            double a = 6 * (start - end) / seconds + 3 * (v0 + v1);
            double b = 6 * (end - start) / seconds - 4 * v0 - 2 * v1;
            double peak = Math.Max(Math.Abs(v0), Math.Abs(v1));
            if (Math.Abs(a) > 1e-12)
            {
                double u = -b / (2 * a);
                if (u > 0 && u < 1) peak = Math.Max(peak, Math.Abs(a * u * u + b * u + v0));
            }
            return peak;
        }
        public static double PeakAcceleration(double start, double end, double v0, double v1, double seconds)
        {
            double a0 = 6 * (end - start) / (seconds * seconds) - (4 * v0 + 2 * v1) / seconds;
            double a1 = 6 * (start - end) / (seconds * seconds) + (2 * v0 + 4 * v1) / seconds;
            return Math.Max(Math.Abs(a0), Math.Abs(a1));
        }
    }

    // Keep three controls between appends. Starting/ending duplicates model the
    // automatic extra moves in Turbo PMAC SPLINE1; input controls are not replayed.
    public sealed class UniformSplineStream
    {
        private readonly List<MotionPoint> controls = new List<MotionPoint>();
        private readonly double time;
        private MotionPoint last;
        private bool ended;
        public UniformSplineStream(MotionPoint initial, double timeMs)
        {
            if (!PlanningOptions.Finite(timeMs) || timeMs < 10 || timeMs > 4000) throw new ArgumentOutOfRangeException("timeMs");
            time = timeMs; last = initial; controls.Add(initial); controls.Add(initial); controls.Add(initial);
        }
        public MotionSegment Append(MotionPoint point)
        {
            if (ended) throw new InvalidOperationException("Spline stream has ended.");
            controls.Add(point); last = point;
            MotionSegment segment = TrajectoryMath.SplineSegment(controls[0], controls[1], controls[2], controls[3], time);
            controls.RemoveAt(0); return segment;
        }
        public IList<MotionSegment> End()
        {
            List<MotionSegment> tail = new List<MotionSegment>();
            if (!ended) { tail.Add(Append(last)); tail.Add(Append(last)); ended = true; }
            return tail;
        }
    }
}
