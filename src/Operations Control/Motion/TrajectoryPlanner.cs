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
                List<MotionPoint> points = IsNative(effective.Mode) ? new List<MotionPoint>(raw) : PathGeometry.Resample(raw, options.Spacing);
                bool hasTransfer = PathGeometry.Distance(previous, points[0]) > 0.000001;
                if (hasTransfer)
                {
                    if (continuous != null) { AddDrawingBlock(plan, continuous, effective); continuous = null; }
                    List<MotionPoint> transferPoints = new List<MotionPoint>(new MotionPoint[] { previous, points[0] });
                    PlannedStroke transfer = IsNative(effective.Mode) ? Native(transferPoints, effective) : PVT(transferPoints, effective);
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
            n.LinearizationTolerance = o.LinearizationTolerance; n.LinearizationPeriodMs = o.LinearizationPeriodMs;
            n.MinimumSegmentMs = o.MinimumSegmentMs; n.CoordinateSystem = o.CoordinateSystem; n.ProgramNumber = o.ProgramNumber;
            n.XMotor = o.XMotor; n.YMotor = o.YMotor; n.Mapping = o.Mapping;
            n.MaximumXSpeed = o.MaximumXSpeed; n.MaximumYSpeed = o.MaximumYSpeed;
            n.MaximumXAcceleration = o.MaximumXAcceleration; n.MaximumYAcceleration = o.MaximumYAcceleration; return n;
        }
        private static void AddDrawingBlock(MotionPlan plan, List<MotionPoint> points, PlanningOptions options)
        {
            PlannedStroke block = IsNative(options.Mode) ? Native(points, options) : options.Mode == InterpolationMode.PVT ? PVT(points, options) : Spline(points, options);
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
        private static bool IsNative(InterpolationMode mode)
        { return mode == InterpolationMode.Linear || mode == InterpolationMode.Circular; }

        private static PlannedStroke Native(List<MotionPoint> input, PlanningOptions options)
        {
            List<MotionPoint> points = new List<MotionPoint>();
            foreach (MotionPoint p in input)
                if (points.Count == 0 || PathGeometry.Distance(points[points.Count - 1], p) >= 0.000001) points.Add(p);
            if (points.Count < 2) throw new ArgumentException("路径没有有效长度。");
            PlannedStroke block = new PlannedStroke(); block.Points.AddRange(points);
            for (int i = 0; i < points.Count - 1; )
            {
                MotionPoint center; double sweep; int end;
                if (options.Mode == InterpolationMode.Circular && TryArc(points, i, out end, out center, out sweep))
                {
                    AddNativeSegments(block, points[i], points[end], center, sweep, options); i = end;
                }
                else
                {
                    AddNativeSegments(block, points[i], points[i + 1], new MotionPoint(), 0, options); i++;
                }
            }
            return block;
        }
        private static bool TryArc(List<MotionPoint> points, int start, out int end, out MotionPoint center, out double sweep)
        {
            end = start; center = new MotionPoint(); sweep = 0;
            // Require four nearby, consistently ordered points on one circle. Sparse
            // polygon corners must remain straight, even when their vertices are cocircular.
            if (start + 3 >= points.Count) return false;
            MotionPoint a = points[start], b = points[start + 1], c = points[start + 2];
            double bx = b.X - a.X, by = b.Y - a.Y, cx = c.X - a.X, cy = c.Y - a.Y;
            double b2 = bx * bx + by * by, c2 = cx * cx + cy * cy, determinant = 2 * (bx * cy - by * cx);
            if (Math.Abs(determinant) < 1e-10 * Math.Sqrt(b2 * c2)) return false;
            center = new MotionPoint(a.X + (b2 * cy - c2 * by) / determinant, a.Y + (c2 * bx - b2 * cx) / determinant);
            double radius = PathGeometry.Distance(a, center), tolerance = 1e-7 * Math.Max(1, radius);
            if (!PlanningOptions.Finite(radius) || radius < 0.000001) return false;
            for (int i = start + 1; i < points.Count; i++)
            {
                MotionPoint p = points[i - 1], q = points[i];
                if (Math.Abs(PathGeometry.Distance(q, center) - radius) > tolerance) break;
                double px = p.X - center.X, py = p.Y - center.Y, qx = q.X - center.X, qy = q.Y - center.Y;
                double angle = Math.Atan2(px * qy - py * qx, px * qx + py * qy);
                if (Math.Abs(angle) < 1e-5 || Math.Abs(angle) > Math.PI / 6 + 1e-8 || (sweep != 0 && angle * sweep <= 0)) break;
                sweep += angle; end = i;
            }
            return end >= start + 3;
        }
        private static void AddNativeSegments(PlannedStroke block, MotionPoint start, MotionPoint end, MotionPoint center, double sweep, PlanningOptions options)
        {
            bool circular = sweep != 0;
            double length = circular ? PathGeometry.Distance(start, center) * Math.Abs(sweep) : PathGeometry.Distance(start, end);
            double time = 1000 * length / options.Speed;
            if (circular)
                time = Math.Max(time, 1000 * length / Math.Min(options.MaximumXSpeed, options.MaximumYSpeed));
            else
                time = Math.Max(time, 1000 * Math.Max(Math.Abs(end.X - start.X) / options.MaximumXSpeed, Math.Abs(end.Y - start.Y) / options.MaximumYSpeed));
            double countValue = Math.Max(1, Math.Max(Math.Ceiling(length / options.Spacing), Math.Ceiling(time / 4000)));
            if (circular) countValue = Math.Max(countValue, Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2)));
            if (!PlanningOptions.Finite(countValue) || countValue > PathGeometry.MaximumPoints - block.Segments.Count)
                throw new ArgumentException("规划段数超过 20000，请增大间距或调整速度。");
            int count = (int)countValue;
            // Native TM is rounded to milliseconds; align it to the 5 ms segmentation period.
            double duration = Math.Ceiling(Math.Max(options.MinimumSegmentMs, time / count) / 5) * 5;
            MotionSegment whole = new MotionSegment { Mode = circular ? MotionSegmentMode.Circular : MotionSegmentMode.Linear,
                Start = start, End = end, Center = center, SweepRadians = sweep, DurationMs = duration * count };
            for (int i = 0; i < count; i++)
            {
                MotionSegment segment = new MotionSegment { Mode = whole.Mode, Start = whole.At((double)i / count), End = whole.At((double)(i + 1) / count),
                    Center = center, SweepRadians = sweep / count, DurationMs = duration };
                segment.StartVelocity = segment.VelocityAt(0); segment.EndVelocity = segment.VelocityAt(1);
                block.Segments.Add(segment);
            }
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
        internal static void CheckExtrema(MotionSegment s, CanvasSettings mapping)
        {
            List<double> candidates = new List<double>(new double[] { 0, 1 }); double t = s.DurationMs / 1000;
            if (s.Mode == MotionSegmentMode.Circular)
            {
                double angle = Math.Atan2(s.Start.Y - s.Center.Y, s.Start.X - s.Center.X);
                for (int i = -4; i <= 4; i++) candidates.Add((i * Math.PI / 2 - angle) / s.SweepRadians);
            }
            else if (s.Mode == MotionSegmentMode.Cubic)
            {
                Roots(6 * (s.Start.X - s.End.X) + 3 * t * (s.StartVelocity.X + s.EndVelocity.X), 6 * (s.End.X - s.Start.X) - t * (4 * s.StartVelocity.X + 2 * s.EndVelocity.X), t * s.StartVelocity.X, candidates);
                Roots(6 * (s.Start.Y - s.End.Y) + 3 * t * (s.StartVelocity.Y + s.EndVelocity.Y), 6 * (s.End.Y - s.Start.Y) - t * (4 * s.StartVelocity.Y + 2 * s.EndVelocity.Y), t * s.StartVelocity.Y, candidates);
            }
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
