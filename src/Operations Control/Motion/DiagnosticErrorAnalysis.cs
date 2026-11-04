using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Motion
{
    public sealed class DiagnosticErrorResult
    {
        public int Samples, Intervals;
        public double StartMs, EndMs, ValidDurationMs;
        public double Mean, Rms, MaximumAbsolute, MaximumTimeMs, PeakToPeak, AbsoluteP95;
        public double? Tolerance, ExceededDurationMs, ExceededRatio, Score;
        public bool HasData { get { return ValidDurationMs > 0; } }
        public double Coverage { get { return ValidDurationMs / (EndMs - StartMs); } }
    }

    public static class DiagnosticErrorAnalysis
    {
        private struct Interval
        {
            public double StartMs, EndMs, First, Last;
        }
        public const string ScoreFormula = "100 / (1 + 0.40×RMS/T + 0.25×P95/T + 0.15×最大绝对误差/T + 0.20×超差比例)";

        public static DiagnosticErrorResult Analyze(DiagnosticRecording recording, int motor, double startMs, double endMs, double? tolerance)
        {
            if (recording == null) throw new ArgumentNullException("recording");
            if (!PlanningOptions.Finite(startMs) || !PlanningOptions.Finite(endMs) || startMs < 0 || endMs <= startMs)
                throw new ArgumentException("结束时间需要大于开始时间。");
            if (tolerance.HasValue && (!PlanningOptions.Finite(tolerance.Value) || tolerance.Value <= 0))
                throw new ArgumentException("评分容差需要大于零。");
            DiagnosticErrorResult result = new DiagnosticErrorResult { StartMs = startMs, EndMs = endMs, Tolerance = tolerance, MaximumTimeMs = Double.PositiveInfinity };
            List<Interval> intervals = new List<Interval>();
            DiagnosticSample previous = null;
            double minimum = Double.PositiveInfinity, maximum = Double.NegativeInfinity;
            foreach (DiagnosticSample sample in recording.Samples)
            {
                if (sample.Motor != motor) continue;
                if (Valid(sample) && sample.TimeMs >= startMs && sample.TimeMs <= endMs)
                {
                    result.Samples++; double error = sample.FollowingError.Value;
                    minimum = Math.Min(minimum, error); maximum = Math.Max(maximum, error); Peak(result, error, sample.TimeMs);
                }
                if (previous != null && Valid(previous) && Valid(sample) && !sample.GapBefore)
                {
                    double dt = sample.TimeMs - previous.TimeMs;
                    double left = Math.Max(startMs, previous.TimeMs), right = Math.Min(endMs, sample.TimeMs);
                    if (dt > 0 && dt <= 1000 && right > left)
                    {
                        double first = Interpolate(previous.FollowingError.Value, sample.FollowingError.Value, (left - previous.TimeMs) / dt);
                        double last = Interpolate(previous.FollowingError.Value, sample.FollowingError.Value, (right - previous.TimeMs) / dt);
                        intervals.Add(new Interval { StartMs = left, EndMs = right, First = first, Last = last });
                        result.ValidDurationMs += right - left;
                        minimum = Math.Min(minimum, Math.Min(first, last)); maximum = Math.Max(maximum, Math.Max(first, last));
                        Peak(result, first, left); Peak(result, last, right);
                    }
                }
                previous = sample;
            }
            result.Intervals = intervals.Count;
            if (!result.HasData) return result;
            result.PeakToPeak = maximum - minimum;
            // Normalize before squaring so large finite CSV values cannot overflow RMS.
            double scale = result.MaximumAbsolute, mean = 0, square = 0;
            if (scale > 0)
            {
                foreach (Interval interval in intervals)
                {
                    double weight = (interval.EndMs - interval.StartMs) / result.ValidDurationMs;
                    double a = interval.First / scale, b = interval.Last / scale;
                    mean += weight * (a + b) / 2;
                    square += weight * (a * a + a * b + b * b) / 3;
                }
                result.Mean = Math.Max(-1, Math.Min(1, mean)) * scale;
                result.Rms = Math.Sqrt(Math.Max(0, Math.Min(1, square))) * scale;
                double low = 0, high = 1;
                for (int step = 0; step < 48; step++)
                {
                    double middle = (low + high) / 2;
                    if (WithinFraction(intervals, middle, scale, result.ValidDurationMs) >= 0.95) high = middle; else low = middle;
                }
                result.AbsoluteP95 = high * scale;
            }
            if (tolerance.HasValue)
            {
                double ratio = scale == 0 || tolerance.Value >= scale ? 0 : 1 - WithinFraction(intervals, tolerance.Value / scale, scale, result.ValidDurationMs);
                result.ExceededRatio = Math.Max(0, Math.Min(1, ratio));
                result.ExceededDurationMs = result.ExceededRatio.Value * result.ValidDurationMs;
                double penalty = 0.40 * (result.Rms / tolerance.Value) + 0.25 * (result.AbsoluteP95 / tolerance.Value) +
                    0.15 * (result.MaximumAbsolute / tolerance.Value) + 0.20 * result.ExceededRatio.Value;
                result.Score = 100 / (1 + penalty);
            }
            return result;
        }
        private static bool Valid(DiagnosticSample sample)
        {
            return sample.Status != null && sample.Status.Enabled && !sample.Status.OpenLoop && sample.FollowingError.HasValue &&
                PlanningOptions.Finite(sample.FollowingError.Value) && PlanningOptions.Finite(sample.TimeMs);
        }
        private static double Interpolate(double first, double last, double fraction)
        { return fraction <= 0 ? first : fraction >= 1 ? last : (1 - fraction) * first + fraction * last; }
        private static void Peak(DiagnosticErrorResult result, double error, double time)
        {
            double absolute = Math.Abs(error);
            if (absolute > result.MaximumAbsolute || (absolute == result.MaximumAbsolute && time < result.MaximumTimeMs))
            { result.MaximumAbsolute = absolute; result.MaximumTimeMs = time; }
        }
        // With linear interpolation, signed error is uniformly distributed between
        // each interval's endpoints. Intersect it with [-limit, limit] to integrate
        // the exact time inside tolerance, including crossings through zero.
        private static double WithinFraction(List<Interval> intervals, double limit, double scale, double duration)
        {
            double sum = 0;
            foreach (Interval interval in intervals)
            {
                double a = interval.First / scale, b = interval.Last / scale;
                double low = Math.Min(a, b), high = Math.Max(a, b);
                double fraction = low == high ? Math.Abs(low) <= limit ? 1 : 0 :
                    Math.Max(0, Math.Min(high, limit) - Math.Max(low, -limit)) / (high - low);
                sum += (interval.EndMs - interval.StartMs) / duration * fraction;
            }
            return Math.Max(0, Math.Min(1, sum));
        }
    }
}
