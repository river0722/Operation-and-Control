using System;
using System.Collections.Generic;

namespace Operations_Control.Models
{
    public sealed class DrawingPath
    {
        public readonly List<List<MotionPoint>> Strokes = new List<List<MotionPoint>>();
        public string Source = "绘图";
        public int PointCount { get { int count = 0; foreach (List<MotionPoint> stroke in Strokes) count += stroke.Count; return count; } }
        public DrawingPath Copy()
        {
            DrawingPath copy = new DrawingPath(); copy.Source = Source;
            foreach (List<MotionPoint> stroke in Strokes) copy.Strokes.Add(new List<MotionPoint>(stroke));
            return copy;
        }
    }

    public enum InterpolationMode { PVT, Spline, Linear, Circular }
    public enum MotionSegmentMode { Cubic, Linear, Circular }

    public sealed class PlanningOptions
    {
        public InterpolationMode Mode = InterpolationMode.PVT;
        public double Speed = 5, Acceleration = 20, Spacing = 0.5;
        public double MaximumXSpeed = 1000, MaximumYSpeed = 1000;
        public double MaximumXAcceleration = 100000, MaximumYAcceleration = 100000;
        public bool ContinuousPvt = true;
        public double LinearizationTolerance = 0.01;
        public int LinearizationPeriodMs = 20;
        public int MinimumSegmentMs = 50, CoordinateSystem = 1, ProgramNumber = 20;
        public int XMotor = 1, YMotor = 2;
        public CanvasSettings Mapping = CanvasSettings.Default;
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(InterpolationMode), Mode)) throw new ArgumentException("插补模式无效。");
            if (!Finite(LinearizationTolerance) || LinearizationTolerance < 0.000001 || LinearizationTolerance > 1 ||
                LinearizationPeriodMs < 10 || LinearizationPeriodMs > 1000 || LinearizationPeriodMs % 5 != 0)
                throw new ArgumentException("直线离散误差应为 0.000001～1 mm，周期应为 10～1000 ms 且为 5 ms 的整数倍。");
            if (!Finite(Speed) || !Finite(Acceleration) || !Finite(Spacing) || Speed <= 0 || Speed > 1000 ||
                Acceleration <= 0 || Acceleration > 100000 || Spacing < 0.001 || Spacing > 100)
                throw new ArgumentException("速度、加速度或采样间距超出范围。");
            if (MinimumSegmentMs < 10 || MinimumSegmentMs > 4000 || CoordinateSystem < 1 || CoordinateSystem > 16 ||
                ProgramNumber < 1 || ProgramNumber > 32767 || XMotor < 1 || XMotor > 8 || YMotor < 1 || YMotor > 8 || XMotor == YMotor || Mapping == null)
                throw new ArgumentException("轴、坐标系统、程序号或分段时间无效。");
            foreach (double limit in new double[] { MaximumXSpeed, MaximumYSpeed, MaximumXAcceleration, MaximumYAcceleration })
                if (!Finite(limit) || limit <= 0) throw new ArgumentException("每轴速度和加速度上限必须为正有限数。");
        }
        public static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
    }

    public sealed class MotionSegment
    {
        public MotionPoint Start, End, StartVelocity, EndVelocity; // mm, mm/s
        public MotionSegmentMode Mode;
        public MotionPoint Center;
        public double SweepRadians;
        public double StartMs, DurationMs;
        public bool Transfer;
        public MotionPoint At(double fraction)
        {
            double u = Math.Max(0, Math.Min(1, fraction)), u2 = u * u, u3 = u2 * u, seconds = DurationMs / 1000;
            if (u == 0) return Start;
            if (u == 1) return End;
            if (Mode == MotionSegmentMode.Linear)
                return new MotionPoint(Start.X + (End.X - Start.X) * u, Start.Y + (End.Y - Start.Y) * u);
            if (Mode == MotionSegmentMode.Circular)
            {
                double angle = SweepRadians * u, x = Start.X - Center.X, y = Start.Y - Center.Y;
                return new MotionPoint(Center.X + x * Math.Cos(angle) - y * Math.Sin(angle), Center.Y + x * Math.Sin(angle) + y * Math.Cos(angle));
            }
            double a = 2 * u3 - 3 * u2 + 1, b = u3 - 2 * u2 + u, c = -2 * u3 + 3 * u2, d = u3 - u2;
            return new MotionPoint(a * Start.X + b * seconds * StartVelocity.X + c * End.X + d * seconds * EndVelocity.X,
                a * Start.Y + b * seconds * StartVelocity.Y + c * End.Y + d * seconds * EndVelocity.Y);
        }
        public MotionPoint VelocityAt(double u)
        {
            double t = DurationMs / 1000;
            if (Mode == MotionSegmentMode.Linear) return new MotionPoint((End.X - Start.X) / t, (End.Y - Start.Y) / t);
            if (Mode == MotionSegmentMode.Circular)
            {
                MotionPoint p = At(u);
                return new MotionPoint(-(p.Y - Center.Y) * SweepRadians / t, (p.X - Center.X) * SweepRadians / t);
            }
            return new MotionPoint(((6 * u * u - 6 * u) * Start.X + (-6 * u * u + 6 * u) * End.X) / t + (3 * u * u - 4 * u + 1) * StartVelocity.X + (3 * u * u - 2 * u) * EndVelocity.X,
                ((6 * u * u - 6 * u) * Start.Y + (-6 * u * u + 6 * u) * End.Y) / t + (3 * u * u - 4 * u + 1) * StartVelocity.Y + (3 * u * u - 2 * u) * EndVelocity.Y);
        }
    }

    public sealed class PlannedStroke
    {
        public readonly List<MotionPoint> Points = new List<MotionPoint>();
        public readonly List<MotionSegment> Segments = new List<MotionSegment>();
        public readonly List<MotionSegment> CurveSegments = new List<MotionSegment>();
        public bool Transfer, StopAfter;
        public double SplineTimeMs;
    }

    public sealed class MotionPlan
    {
        public readonly List<PlannedStroke> Blocks = new List<PlannedStroke>();
        public readonly List<MotionSegment> Segments = new List<MotionSegment>();
        public DrawingPath Drawing;
        public PlanningOptions Options;
        public MotionPoint InitialPosition;
        public double DurationMs;
        public string Program;
        public MotionPoint At(double milliseconds)
        {
            if (Segments.Count == 0) return InitialPosition;
            if (milliseconds <= 0) return Segments[0].Start;
            int left = 0, right = Segments.Count - 1;
            while (left < right) { int mid = (left + right) / 2; if (Segments[mid].StartMs + Segments[mid].DurationMs < milliseconds) left = mid + 1; else right = mid; }
            MotionSegment segment = Segments[left];
            return segment.At((milliseconds - segment.StartMs) / segment.DurationMs);
        }
    }

    public sealed class TrajectorySample
    {
        public double TimeMs, XTimeMs, YTimeMs, QueryMs;
        public MotionPoint Position;
        public TrajectorySample(double time, double xTime, double yTime, double query, MotionPoint point)
        { TimeMs = time; XTimeMs = xTime; YTimeMs = yTime; QueryMs = query; Position = point; }
    }

    public sealed class TrajectoryRun
    {
        public MotionPlan Plan;
        public readonly List<TrajectorySample> Samples = new List<TrajectorySample>();
        public bool Simulation, Completed;
        public DateTime StartedUtc;
        public string Result = "未开始";
        public double StartCommandMs;
    }
}
