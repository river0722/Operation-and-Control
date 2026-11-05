using System;
using Operations_Control.Models;

namespace Operations_Control.Motion
{
    // PVT/Hermite and cubic B-splines are evaluated on the host. PMAC receives LINEAR moves.
    public static class LinearTrajectorySampler
    {
        public const int MaximumSegments = 200000;

        public static MotionPlan Build(MotionPlan curves)
        {
            PlanningOptions options = curves.Options;
            options.Validate();
            if (options.Mode != InterpolationMode.PVT && options.Mode != InterpolationMode.Spline) return curves;
            MotionPlan result = new MotionPlan { Drawing = curves.Drawing, Options = options, InitialPosition = curves.InitialPosition };
            double period = options.LinearizationPeriodMs, seconds = period / 1000;
            double peakAcceleration = 0;
            foreach (MotionSegment s in curves.Segments)
            {
                double time = s.DurationMs / 1000;
                double ax = TrajectoryMath.PeakAcceleration(s.Start.X, s.End.X, s.StartVelocity.X, s.EndVelocity.X, time);
                double ay = TrajectoryMath.PeakAcceleration(s.Start.Y, s.End.Y, s.StartVelocity.Y, s.EndVelocity.Y, time);
                peakAcceleration = Math.Max(peakAcceleration, Math.Sqrt(ax * ax + ay * ay));
            }
            // For a C1 path with bounded acceleration, time-linear chord error <= A * dt^2 / 8.
            // Stretch the time law when the selected sampling period needs more spatial accuracy.
            double factor = Math.Max(1, seconds * Math.Sqrt(peakAcceleration / (8 * options.LinearizationTolerance * 0.999)));
            double speedLimit = Math.Min(options.Speed, Math.Min(options.MaximumXSpeed, options.MaximumYSpeed));
            factor = Math.Max(factor, 1 + 0.00000002 / (seconds * speedLimit));
            foreach (PlannedStroke source in curves.Blocks)
            {
                double sourceDuration = 0;
                foreach (MotionSegment s in source.Segments) sourceDuration += s.DurationMs;
                double countValue = Math.Ceiling(sourceDuration * factor / period);
                if (!PlanningOptions.Finite(countValue) || countValue > MaximumSegments - result.Segments.Count)
                    throw new ArgumentException("直线离散段数超过 200000，请调整速度、采样周期或误差要求。");
                int count = Math.Max(1, (int)countValue);
                if (result.DurationMs + count * period > 3600000) throw new ArgumentException("直线离散后的运行时间超过一小时，请拆分任务。");
                PlannedStroke block = new PlannedStroke { Transfer = source.Transfer, StopAfter = source.StopAfter, SplineTimeMs = source.SplineTimeMs };
                block.Points.AddRange(source.Points); block.CurveSegments.AddRange(source.Segments);
                MotionPoint previous = Coordinate(source.Segments[0].Start);
                int index = 0; double offset = 0;
                for (int i = 1; i <= count; i++)
                {
                    double sourceTime = sourceDuration * ((double)i / count);
                    while (index < source.Segments.Count - 1 && offset + source.Segments[index].DurationMs < sourceTime)
                    { offset += source.Segments[index].DurationMs; index++; }
                    MotionSegment curve = source.Segments[index];
                    MotionPoint next = Coordinate(i == count ? source.Segments[source.Segments.Count - 1].End : curve.At((sourceTime - offset) / curve.DurationMs));
                    MotionPoint velocity = new MotionPoint((next.X - previous.X) / seconds, (next.Y - previous.Y) / seconds);
                    MotionSegment linear = new MotionSegment { Mode = MotionSegmentMode.Linear, Start = previous, End = next,
                        StartVelocity = velocity, EndVelocity = velocity, DurationMs = period, StartMs = result.DurationMs, Transfer = source.Transfer };
                    TrajectoryPlanner.CheckExtrema(linear, options.Mapping);
                    block.Segments.Add(linear); result.Segments.Add(linear); result.DurationMs += period;
                    previous = next;
                }
                result.Blocks.Add(block);
            }
            return result;
        }

        private static MotionPoint Coordinate(MotionPoint point)
        { return new MotionPoint(Math.Round(point.X, 8), Math.Round(point.Y, 8)); }
    }
}
