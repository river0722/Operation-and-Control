using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    public static class TrajectoryPlanner
    {
        public static MotionPlan Build(DrawingPath drawing, PlanningOptions options, MotionPoint initial)
        {
            PathGeometry.Validate(drawing); options.Validate();
            if (!options.Mapping.Contains(initial)) throw new ArgumentException("当前位置超出已配置行程。");
            MotionPlan plan = new MotionPlan(); plan.Drawing = drawing.Copy(); plan.Options = CopyOptions(options); plan.InitialPosition = initial;
            MotionPoint previous = initial;
            foreach (List<MotionPoint> raw in drawing.Strokes)
            {
                foreach (MotionPoint p in raw) if (!options.Mapping.Contains(p)) throw new ArgumentException("路径超出行程。请调整图形大小或偏移后重新规划。");
                List<MotionPoint> points = PathGeometry.Resample(raw, options.Spacing);
                if (PathGeometry.Distance(previous, points[0]) > 0.000001)
                {
                    PlannedStroke transfer = PVT(new List<MotionPoint>(new MotionPoint[] { previous, points[0] }), options);
                    transfer.Transfer = true; AddBlock(plan, transfer);
                }
                PlannedStroke block = options.Mode == InterpolationMode.PVT ? PVT(points, options) : Spline(points, options);
                AddBlock(plan, block); previous = points[points.Count - 1];
            }
            if (plan.DurationMs > 3600000) throw new ArgumentException("运行时间超过一小时，请拆分任务。");
            plan.Program = PmacProgramWriter.Write(plan); return plan;
        }
        private static PlanningOptions CopyOptions(PlanningOptions o)
        {
            PlanningOptions n = new PlanningOptions(); n.Mode = o.Mode; n.Speed = o.Speed; n.Acceleration = o.Acceleration; n.Spacing = o.Spacing;
            n.MinimumSegmentMs = o.MinimumSegmentMs; n.CoordinateSystem = o.CoordinateSystem; n.ProgramNumber = o.ProgramNumber;
            n.XMotor = o.XMotor; n.YMotor = o.YMotor; n.Mapping = o.Mapping; return n;
        }
        private static void AddBlock(MotionPlan plan, PlannedStroke block)
        {
            foreach (MotionSegment segment in block.Segments)
            {
                segment.StartMs = plan.DurationMs; segment.Transfer = block.Transfer;
                CheckExtrema(segment, plan.Options.Mapping);
                plan.DurationMs += segment.DurationMs; plan.Segments.Add(segment);
            }
            if (plan.Segments.Count > PathGeometry.MaximumPoints) throw new ArgumentException("规划段数超过 20000。");
            plan.Blocks.Add(block);
        }
        private static PlannedStroke PVT(List<MotionPoint> points, PlanningOptions options)
        {
            PlannedStroke block = new PlannedStroke(); block.Points.AddRange(points);
            double[] times = new double[points.Count - 1]; MotionPoint[] velocities = new MotionPoint[points.Count];
            for (int i = 0; i < times.Length; i++) times[i] = Math.Max(options.MinimumSegmentMs, 1000 * PathGeometry.Distance(points[i], points[i + 1]) / options.Speed);
            for (int i = 1; i < points.Count - 1; i++)
            {
                MotionPoint a = points[i - 1], b = points[i], c = points[i + 1];
                double length1 = PathGeometry.Distance(a, b), length2 = PathGeometry.Distance(b, c);
                double cosine = ((b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y)) / (length1 * length2);
                if (cosine < 0.7) continue;
                velocities[i] = new MotionPoint(Slope((b.X - a.X) / times[i - 1] * 1000, (c.X - b.X) / times[i] * 1000),
                    Slope((b.Y - a.Y) / times[i - 1] * 1000, (c.Y - b.Y) / times[i] * 1000));
            }
            for (int i = 0; i < times.Length; i++) block.Segments.Add(new MotionSegment { Start = points[i], End = points[i + 1], StartVelocity = velocities[i], EndVelocity = velocities[i + 1], DurationMs = times[i] });
            Stretch(block, options);
            List<MotionSegment> bounded = new List<MotionSegment>();
            foreach (MotionSegment s in block.Segments)
            {
                int count = (int)Math.Ceiling(s.DurationMs / 4000);
                if (count > PathGeometry.MaximumPoints || bounded.Count + count > PathGeometry.MaximumPoints) throw new ArgumentException("规划过长，请调整速度和路径。");
                for (int j = 0; j < count; j++) bounded.Add(new MotionSegment { Start = s.At((double)j / count), End = s.At((double)(j + 1) / count),
                    StartVelocity = s.VelocityAt((double)j / count), EndVelocity = s.VelocityAt((double)(j + 1) / count), DurationMs = s.DurationMs / count });
            }
            block.Segments.Clear(); block.Segments.AddRange(bounded); return block;
        }
        private static double Slope(double a, double b) { return a * b <= 0 ? 0 : 2 * a * b / (a + b); }
        private static PlannedStroke Spline(List<MotionPoint> points, PlanningOptions options)
        {
            PlannedStroke block = new PlannedStroke(); block.Points.AddRange(points);
            double time = options.MinimumSegmentMs;
            for (int i = 1; i < points.Count; i++) time = Math.Max(time, 1000 * PathGeometry.Distance(points[i - 1], points[i]) / options.Speed);
            List<MotionPoint> control = new List<MotionPoint>(); control.Add(points[0]); control.Add(points[0]); control.AddRange(points); control.Add(points[points.Count - 1]); control.Add(points[points.Count - 1]);
            for (int i = 0; i + 3 < control.Count; i++)
            {
                MotionPoint a = control[i], b = control[i + 1], c = control[i + 2], d = control[i + 3];
                block.Segments.Add(new MotionSegment { DurationMs = time,
                    Start = new MotionPoint((a.X + 4 * b.X + c.X) / 6, (a.Y + 4 * b.Y + c.Y) / 6),
                    End = new MotionPoint((b.X + 4 * c.X + d.X) / 6, (b.Y + 4 * c.Y + d.Y) / 6),
                    StartVelocity = new MotionPoint((c.X - a.X) * 500 / time, (c.Y - a.Y) * 500 / time),
                    EndVelocity = new MotionPoint((d.X - b.X) * 500 / time, (d.Y - b.Y) * 500 / time) });
            }
            Stretch(block, options); block.SplineTimeMs = block.Segments[0].DurationMs;
            if (block.SplineTimeMs > 4000) throw new ArgumentException("Spline 分段时间超过 4000 ms，请缩小采样间距或提高速度/加速度限制。");
            return block;
        }
        private static void Stretch(PlannedStroke block, PlanningOptions options)
        {
            double factor = 1;
            foreach (MotionSegment s in block.Segments)
            {
                double t = s.DurationMs / 1000;
                double ax = 6 * (s.Start.X - s.End.X) / t + 3 * (s.StartVelocity.X + s.EndVelocity.X);
                double bx = 6 * (s.End.X - s.Start.X) / t - 4 * s.StartVelocity.X - 2 * s.EndVelocity.X;
                double ay = 6 * (s.Start.Y - s.End.Y) / t + 3 * (s.StartVelocity.Y + s.EndVelocity.Y);
                double by = 6 * (s.End.Y - s.Start.Y) / t - 4 * s.StartVelocity.Y - 2 * s.EndVelocity.Y;
                double vx = VelocityBound(ax, bx, s.StartVelocity.X), vy = VelocityBound(ay, by, s.StartVelocity.Y);
                factor = Math.Max(factor, Math.Sqrt(vx * vx + vy * vy) / options.Speed);
                double a0 = Math.Sqrt(bx * bx + by * by) / t, a1 = Math.Sqrt((2 * ax + bx) * (2 * ax + bx) + (2 * ay + by) * (2 * ay + by)) / t;
                factor = Math.Max(factor, Math.Sqrt(Math.Max(a0, a1) / options.Acceleration));
            }
            factor *= 1.00001;
            foreach (MotionSegment s in block.Segments)
            {
                s.DurationMs *= factor; s.StartVelocity = new MotionPoint(s.StartVelocity.X / factor, s.StartVelocity.Y / factor); s.EndVelocity = new MotionPoint(s.EndVelocity.X / factor, s.EndVelocity.Y / factor);
            }
        }
        private static double VelocityBound(double a, double b, double c)
        {
            double bound = Math.Max(Math.Abs(c), Math.Abs(a + b + c));
            if (Math.Abs(a) > 1e-12) { double u = -b / (2 * a); if (u > 0 && u < 1) bound = Math.Max(bound, Math.Abs(a * u * u + b * u + c)); }
            return bound;
        }
        private static void CheckExtrema(MotionSegment s, CanvasSettings mapping)
        {
            List<double> candidates = new List<double>(new double[] { 0, 1 }); double t = s.DurationMs / 1000;
            Roots(6 * (s.Start.X - s.End.X) + 3 * t * (s.StartVelocity.X + s.EndVelocity.X), 6 * (s.End.X - s.Start.X) - t * (4 * s.StartVelocity.X + 2 * s.EndVelocity.X), t * s.StartVelocity.X, candidates);
            Roots(6 * (s.Start.Y - s.End.Y) + 3 * t * (s.StartVelocity.Y + s.EndVelocity.Y), 6 * (s.End.Y - s.Start.Y) - t * (4 * s.StartVelocity.Y + 2 * s.EndVelocity.Y), t * s.StartVelocity.Y, candidates);
            foreach (double u in candidates) if (u >= 0 && u <= 1)
            {
                MotionPoint p = s.At(u);
                if (p.X < mapping.XMin - 1e-8 || p.X > mapping.XMax + 1e-8 || p.Y < mapping.YMin - 1e-8 || p.Y > mapping.YMax + 1e-8) throw new ArgumentException("插补轨迹越过行程边界，请缩小图形。");
            }
        }
        private static void Roots(double a, double b, double c, List<double> roots)
        {
            if (Math.Abs(a) < 1e-12) { if (Math.Abs(b) > 1e-12) roots.Add(-c / b); return; }
            double d = b * b - 4 * a * c; if (d < 0) return; roots.Add((-b + Math.Sqrt(d)) / (2 * a)); roots.Add((-b - Math.Sqrt(d)) / (2 * a));
        }
    }
}
