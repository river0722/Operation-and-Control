using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Motion
{
    public static class TrajectoryPlanner
    {
        public static MotionPlan Build(DrawingPath drawing, PlanningOptions options, MotionPoint initial)
        {
            PathGeometry.Validate(drawing); options.Validate();
            if (!options.Mapping.Contains(initial)) throw new ArgumentException("当前位置超出已配置行程。");
            // 加速度估算仅保留为规划信息，不参与完整路径的时间约束。
            // 段时间由目标速度、每轴速度上限和最短段时间决定。
            PlanningOptions effective = CopyOptions(options);
            effective.Acceleration = EstimateAcceleration(drawing, effective.Speed, effective.Spacing);
            MotionPlan plan = new MotionPlan(); plan.Drawing = drawing.Copy(); plan.Options = CopyOptions(effective); plan.InitialPosition = initial;
            MotionPoint previous = initial;
            List<MotionPoint> continuous = null;
            foreach (List<MotionPoint> raw in drawing.Strokes)
            {
                foreach (MotionPoint p in raw) if (!options.Mapping.Contains(p)) throw new ArgumentException("路径超出行程。请调整图形大小或偏移后重新规划。");
                List<MotionPoint> points = PathGeometry.Resample(raw, options.Spacing);
                bool hasTransfer = PathGeometry.Distance(previous, points[0]) > 0.000001;
                if (hasTransfer)
                {
                    if (continuous != null) { AddDrawingBlock(plan, continuous, effective); continuous = null; }
                    PlannedStroke transfer = PVT(new List<MotionPoint>(new MotionPoint[] { previous, points[0] }), effective);
                    transfer.Transfer = true; transfer.StopAfter = true; AddBlock(plan, transfer);
                }
                if (continuous == null) continuous = new List<MotionPoint>(points);
                else if (effective.Mode == InterpolationMode.PVT && effective.ContinuousPvt)
                    for (int i = 1; i < points.Count; i++) continuous.Add(points[i]);
                else { AddDrawingBlock(plan, continuous, effective); continuous = new List<MotionPoint>(points); }
                previous = points[points.Count - 1];
            }
            if (continuous != null) AddDrawingBlock(plan, continuous, effective);
            if (plan.DurationMs > 3600000) throw new ArgumentException("运行时间超过一小时，请拆分任务。");
            return plan;
        }
        private static PlanningOptions CopyOptions(PlanningOptions o)
        {
            PlanningOptions n = new PlanningOptions(); n.Mode = o.Mode; n.Speed = o.Speed; n.Acceleration = o.Acceleration; n.Spacing = o.Spacing; n.ContinuousPvt = o.ContinuousPvt;
            n.MinimumSegmentMs = o.MinimumSegmentMs; n.CoordinateSystem = o.CoordinateSystem; n.ProgramNumber = o.ProgramNumber;
            n.XMotor = o.XMotor; n.YMotor = o.YMotor; n.Mapping = o.Mapping;
            n.MaximumXSpeed = o.MaximumXSpeed; n.MaximumYSpeed = o.MaximumYSpeed;
            n.MaximumXAcceleration = o.MaximumXAcceleration; n.MaximumYAcceleration = o.MaximumYAcceleration; return n;
        }
        private static void AddDrawingBlock(MotionPlan plan, List<MotionPoint> points, PlanningOptions options)
        {
            PlannedStroke block = options.Mode == InterpolationMode.PVT ? PVT(points, options) : Spline(points, options);
            block.StopAfter = true;
            AddBlock(plan, block);
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
                if (length1 < 1e-9 || length2 < 1e-9) continue;
                double tx = c.X - a.X, ty = c.Y - a.Y, tangentLength = Math.Sqrt(tx * tx + ty * ty);
                if (tangentLength < 1e-9) continue;
                double cosine = ((b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y)) / (length1 * length2);
                cosine = Math.Max(-1, Math.Min(1, cosine));
                // 以夹角的半角余弦平滑降速。连续切向速度不会在普通折点归零，
                // 只有真正反向时才会降到零，消除周期性速度尖峰。
                double cornerScale = Math.Sqrt(Math.Max(0, (1 + cosine) * 0.5));
                double magnitude = options.Speed * cornerScale;
                velocities[i] = new MotionPoint(magnitude * tx / tangentLength, magnitude * ty / tangentLength);
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
        private static double EstimateAcceleration(DrawingPath drawing, double speed, double spacing)
        {
            double minimumLength = Math.Max(spacing * 0.5, 0.001);
            double bound = speed * speed / minimumLength;
            foreach (List<MotionPoint> raw in drawing.Strokes)
            {
                List<MotionPoint> points = PathGeometry.Resample(raw, spacing);
                for (int i = 1; i < points.Count - 1; i++)
                {
                    double l1 = PathGeometry.Distance(points[i - 1], points[i]);
                    double l2 = PathGeometry.Distance(points[i], points[i + 1]);
                    if (l1 < 1e-9 || l2 < 1e-9) continue;
                    double ux = (points[i].X - points[i - 1].X) / l1, uy = (points[i].Y - points[i - 1].Y) / l1;
                    double vx = (points[i + 1].X - points[i].X) / l2, vy = (points[i + 1].Y - points[i].Y) / l2;
                    double cosine = Math.Max(-1, Math.Min(1, ux * vx + uy * vy));
                    double angle = Math.Acos(cosine);
                    bound = Math.Max(bound, speed * speed * angle / Math.Max(Math.Min(l1, l2), minimumLength));
                }
            }
            // 留出数值和控制器插补误差余量；上限保持与 PlanningOptions 兼容。
            return Math.Min(100000, Math.Max(0.001, bound * 1.25));
        }
        private static PlannedStroke Spline(List<MotionPoint> points, PlanningOptions options)
        {
            PlannedStroke block = new PlannedStroke(); block.Points.AddRange(points);
            double time = options.MinimumSegmentMs;
            for (int i = 1; i < points.Count; i++) time = Math.Max(time, 1000 * PathGeometry.Distance(points[i - 1], points[i]) / options.Speed);
            List<MotionPoint> control = new List<MotionPoint>(); control.Add(points[0]); control.Add(points[0]); control.AddRange(points); control.Add(points[points.Count - 1]); control.Add(points[points.Count - 1]);
            for (int i = 0; i + 3 < control.Count; i++)
            {
                block.Segments.Add(TrajectoryMath.SplineSegment(control[i], control[i + 1], control[i + 2], control[i + 3], time));
            }
            Stretch(block, options); block.SplineTimeMs = block.Segments[0].DurationMs;
            if (block.SplineTimeMs > 4000) throw new ArgumentException("Spline 分段时间超过 4000 ms，请缩小采样间距或提高速度限制。");
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
                factor = Math.Max(factor, Math.Max(vx / options.MaximumXSpeed, vy / options.MaximumYSpeed));
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
