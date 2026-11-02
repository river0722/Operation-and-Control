using System;

namespace Operations_Control.Models
{
    public struct MotionPoint
    {
        public double X;
        public double Y;
        public MotionPoint(double x, double y) { X = x; Y = y; }
    }

    public sealed class CanvasSettings
    {
        public readonly double XMin, XMax, YMin, YMax, XCountsPerUnit, YCountsPerUnit;
        public readonly string Unit;

        public CanvasSettings(double xMin, double xMax, double yMin, double yMax, double xScale, double yScale, string unit)
        {
            double[] values = new double[] { xMin, xMax, yMin, yMax, xScale, yScale };
            foreach (double value in values)
                if (Double.IsNaN(value) || Double.IsInfinity(value)) throw new ArgumentException("坐标参数必须为有限数值。");
            if (xMin >= xMax || yMin >= yMax || xScale <= 0 || yScale <= 0)
                throw new ArgumentException("坐标上限必须大于下限，比例必须大于零。");
            if (Math.Max(Math.Abs(xMin * xScale), Math.Abs(xMax * xScale)) > 100000000 ||
                Math.Max(Math.Abs(yMin * yScale), Math.Abs(yMax * yScale)) > 100000000)
                throw new ArgumentException("换算后的坐标必须在 ±100000000 ct 内。");
            XMin = xMin; XMax = xMax; YMin = yMin; YMax = yMax;
            XCountsPerUnit = xScale; YCountsPerUnit = yScale; Unit = unit;
        }

        public static CanvasSettings Default { get { return new CanvasSettings(-10, 10, -10, 10, 100, 100, "mm"); } }
        public bool Contains(MotionPoint point) { return point.X >= XMin && point.X <= XMax && point.Y >= YMin && point.Y <= YMax; }
        public MotionPoint ToCounts(MotionPoint point)
        {
            if (!Contains(point)) throw new ArgumentOutOfRangeException("point", "绘图目标超出已设置的坐标范围。");
            return new MotionPoint(point.X * XCountsPerUnit, point.Y * YCountsPerUnit);
        }
        public MotionPoint FromCounts(double x, double y) { return new MotionPoint(x / XCountsPerUnit, y / YCountsPerUnit); }

    }

    public sealed class DrawPointEventArgs : EventArgs
    {
        public MotionPoint Point { get; private set; }
        public DrawPointEventArgs(MotionPoint point) { Point = point; }
    }
}
