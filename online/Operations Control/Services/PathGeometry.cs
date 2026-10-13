using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    public static class PathGeometry
    {
        public const int MaximumPoints = 20000;
        public static double Distance(MotionPoint a, MotionPoint b) { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
        public static MotionPoint Lerp(MotionPoint a, MotionPoint b, double t) { return new MotionPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
        public static DrawingPath Line(double x, double y, double endX, double endY)
        { DrawingPath path = new DrawingPath(); path.Source = "直线"; path.Strokes.Add(new List<MotionPoint>(new MotionPoint[] { new MotionPoint(x, y), new MotionPoint(endX, endY) })); return path; }
        public static DrawingPath Arc(double x, double y, double radius, double startDegrees, double sweepDegrees, double tolerance)
        {
            if (!PlanningOptions.Finite(x) || !PlanningOptions.Finite(y) || !PlanningOptions.Finite(startDegrees) || !PlanningOptions.Finite(radius) || radius <= 0 || !PlanningOptions.Finite(sweepDegrees) || Math.Abs(sweepDegrees) > 360 || Math.Abs(sweepDegrees) < 0.00001 || !PlanningOptions.Finite(tolerance) || tolerance <= 0)
                throw new ArgumentException("圆弧半径、角度或精度无效。");
            double sweep = sweepDegrees * Math.PI / 180, start = startDegrees * Math.PI / 180;
            double step = Math.Min(Math.PI / 18, 2 * Math.Acos(Math.Max(-1, Math.Min(1, 1 - tolerance / radius))));
            double countValue = Math.Ceiling(Math.Abs(sweep) / step);
            if (!PlanningOptions.Finite(countValue) || countValue > MaximumPoints - 1) throw new ArgumentException("圆弧点数过多，请增大弦差。");
            int count = (int)countValue;
            DrawingPath path = new DrawingPath(); path.Source = Math.Abs(sweepDegrees) == 360 ? "圆" : "圆弧";
            List<MotionPoint> points = new List<MotionPoint>();
            for (int i = 0; i <= count; i++) { double angle = start + sweep * i / count; points.Add(new MotionPoint(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle))); }
            path.Strokes.Add(points); return path;
        }
        public static DrawingPath Star(double x, double y, double radius)
        {
            if (!PlanningOptions.Finite(radius) || radius <= 0) throw new ArgumentException("星形半径必须为正。");
            DrawingPath path = new DrawingPath(); path.Source = "五角星"; List<MotionPoint> points = new List<MotionPoint>();
            for (int i = 0; i <= 10; i++) { double angle = Math.PI / 2 + i * Math.PI / 5, r = i % 2 == 0 ? radius : radius * 0.38196601125; points.Add(new MotionPoint(x + r * Math.Cos(angle), y + r * Math.Sin(angle))); }
            path.Strokes.Add(points); return path;
        }
        public static void Validate(DrawingPath path)
        {
            if (path == null || path.Strokes.Count == 0 || path.PointCount > MaximumPoints) throw new ArgumentException("路径为空或超过 20000 点。");
            foreach (List<MotionPoint> stroke in path.Strokes)
            {
                if (stroke.Count < 2) throw new ArgumentException("每条笔画至少需要两个点。");
                foreach (MotionPoint point in stroke)
                    if (!PlanningOptions.Finite(point.X) || !PlanningOptions.Finite(point.Y) || Math.Abs(point.X) > 1000000 || Math.Abs(point.Y) > 1000000)
                        throw new ArgumentException("路径坐标无效或超过 ±1000000 mm。");
            }
        }
        public static DrawingPath Transform(DrawingPath path, double scale, double xOffset, double yOffset)
        {
            if (!PlanningOptions.Finite(scale) || scale <= 0 || !PlanningOptions.Finite(xOffset) || !PlanningOptions.Finite(yOffset)) throw new ArgumentException("缩放和偏移必须为有效数值。");
            DrawingPath result = new DrawingPath(); result.Source = path.Source;
            foreach (List<MotionPoint> stroke in path.Strokes)
            {
                List<MotionPoint> points = new List<MotionPoint>();
                foreach (MotionPoint point in stroke) points.Add(new MotionPoint(point.X * scale + xOffset, point.Y * scale + yOffset));
                result.Strokes.Add(points);
            }
            Validate(result); return result;
        }
        public static List<MotionPoint> Resample(List<MotionPoint> input, double spacing)
        {
            if (input == null || input.Count < 2 || !PlanningOptions.Finite(spacing) || spacing <= 0) throw new ArgumentException("采样间距和输入路径无效。");
            List<MotionPoint> result = new List<MotionPoint>(); result.Add(input[0]);
            for (int i = 1; i < input.Count; i++)
            {
                double length = Distance(input[i - 1], input[i]); if (length < 0.000001) continue;
                double countValue = Math.Ceiling(length / spacing);
                if (!PlanningOptions.Finite(countValue) || countValue > MaximumPoints - result.Count) throw new ArgumentException("规划点数超过 20000，请增大间距或缩小路径。");
                int count = (int)countValue;
                for (int j = 1; j <= count; j++) result.Add(Lerp(input[i - 1], input[i], (double)j / count));
            }
            if (result.Count < 2) throw new ArgumentException("路径没有有效长度。");
            return result;
        }
        public static double SegmentDistance(MotionPoint p, MotionPoint a, MotionPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, length = dx * dx + dy * dy;
            double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length));
            return Distance(p, Lerp(a, b, t));
        }
    }
}
