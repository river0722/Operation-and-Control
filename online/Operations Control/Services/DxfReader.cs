using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    public static class DxfReader
    {
        private sealed class Pair { public int Code; public string Value; public Pair(int code, string value) { Code = code; Value = value; } }
        private sealed class Entity
        {
            public string Type;
            public readonly List<Pair> Fields = new List<Pair>();
            public double Number(int code, double fallback)
            {
                foreach (Pair p in Fields) if (p.Code == code) return Parse(p.Value);
                return fallback;
            }
            public List<double> Numbers(int code) { List<double> result = new List<double>(); foreach (Pair p in Fields) if (p.Code == code) result.Add(Parse(p.Value)); return result; }
        }
        private static double Parse(string value)
        {
            double number;
            if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || !PlanningOptions.Finite(number)) throw new FormatException("DXF 含有无效数值：" + value);
            return number;
        }
        public static DrawingPath Read(string filePath, double chordToleranceMm, double unitlessToMm)
        {
            if (!String.Equals(Path.GetExtension(filePath), ".dxf", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请选择 ASCII DXF；DWG 请先在 AutoCAD 中另存为 DXF。");
            if (new FileInfo(filePath).Length > 16 * 1024 * 1024) throw new ArgumentException("DXF 超过 16 MB。");
            using (StreamReader reader = new StreamReader(filePath)) return ReadText(reader.ReadToEnd(), chordToleranceMm, unitlessToMm);
        }
        public static DrawingPath ReadText(string text, double chordToleranceMm, double unitlessToMm)
        {
            if (String.IsNullOrEmpty(text) || text.StartsWith("AutoCAD Binary DXF") || text.IndexOf('\0') >= 0) throw new FormatException("仅支持 ASCII DXF，不支持二进制 DXF。");
            if (!PlanningOptions.Finite(chordToleranceMm) || chordToleranceMm <= 0 || !PlanningOptions.Finite(unitlessToMm) || unitlessToMm <= 0) throw new ArgumentException("DXF 弦差和无单位文件比例必须为正。");
            List<Pair> pairs = new List<Pair>();
            using (StringReader reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0 && reader.Peek() < 0) break;
                    int code; string value = reader.ReadLine();
                    if (!Int32.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out code) || value == null) throw new FormatException("DXF 组码和值必须成对出现。");
                    pairs.Add(new Pair(code, value.Trim()));
                }
            }
            string section = String.Empty; bool entitiesFound = false, eof = false;
            int units = 0; List<Entity> entities = new List<Entity>(); Entity current = null;
            for (int i = 0; i < pairs.Count; i++)
            {
                Pair p = pairs[i];
                if (p.Code == 0 && p.Value == "SECTION")
                {
                    if (i + 1 >= pairs.Count || pairs[i + 1].Code != 2) throw new FormatException("DXF SECTION 缺少名称。");
                    section = pairs[++i].Value; current = null; if (section == "ENTITIES") entitiesFound = true; continue;
                }
                if (p.Code == 0 && p.Value == "ENDSEC") { section = String.Empty; current = null; continue; }
                if (p.Code == 0 && p.Value == "EOF") { eof = true; break; }
                if (section == "HEADER" && p.Code == 9 && p.Value == "$INSUNITS")
                {
                    if (i + 1 >= pairs.Count || pairs[i + 1].Code != 70) throw new FormatException("DXF 单位字段无效。");
                    units = (int)Parse(pairs[++i].Value);
                }
                if (section != "ENTITIES") continue;
                if (p.Code == 0) { current = new Entity(); current.Type = p.Value; entities.Add(current); }
                else if (current != null) current.Fields.Add(p);
            }
            if (!entitiesFound || !eof) throw new FormatException("DXF 缺少 ENTITIES 或 EOF，文件可能不完整。");
            double factor;
            switch (units)
            {
                case 0: factor = unitlessToMm; break; case 1: factor = 25.4; break; case 2: factor = 304.8; break;
                case 4: factor = 1; break; case 5: factor = 10; break; case 6: factor = 1000; break;
                case 9: factor = 0.0254; break; case 13: factor = 0.001; break;
                default: throw new FormatException("暂不支持此 DXF 单位编号：" + units + "，请转换为毫米。");
            }
            DrawingPath path = new DrawingPath(); path.Source = "DXF · " + (units == 0 ? "无单位，使用指定比例 " : "单位转换比例 ") + factor.ToString(CultureInfo.InvariantCulture) + " mm/单位";
            double tolerance = chordToleranceMm / factor;
            for (int i = 0; i < entities.Count; i++)
            {
                Entity e = entities[i]; CheckPlane(e);
                if (e.Type == "LINE") path.Strokes.Add(new List<MotionPoint>(new MotionPoint[] { Point(e, 10, 20), Point(e, 11, 21) }));
                else if (e.Type == "CIRCLE" || e.Type == "ARC")
                {
                    double start = e.Type == "CIRCLE" ? 0 : e.Number(50, Double.NaN), sweep = e.Type == "CIRCLE" ? 360 : e.Number(51, Double.NaN) - start;
                    if (sweep <= 0) sweep += 360;
                    path.Strokes.Add(PathGeometry.Arc(e.Number(10, Double.NaN), e.Number(20, Double.NaN), e.Number(40, Double.NaN), start, sweep, tolerance).Strokes[0]);
                }
                else if (e.Type == "LWPOLYLINE") path.Strokes.Add(Polyline(e, null, tolerance));
                else if (e.Type == "POLYLINE")
                {
                    List<Entity> vertices = new List<Entity>();
                    while (++i < entities.Count && entities[i].Type == "VERTEX") { CheckPlane(entities[i]); vertices.Add(entities[i]); }
                    if (i >= entities.Count || entities[i].Type != "SEQEND") throw new FormatException("POLYLINE 缺少 SEQEND。");
                    path.Strokes.Add(Polyline(e, vertices, tolerance));
                }
                else if (e.Type == "SPLINE") path.Strokes.Add(Spline(e, tolerance));
                else throw new FormatException("不支持实体 " + e.Type + "。请在 CAD 中删除标注/文字并分解块，避免遗漏图形。");
                if (path.PointCount > PathGeometry.MaximumPoints) throw new FormatException("DXF 超过 20000 点，请简化图形或增大弦差。");
            }
            return PathGeometry.Transform(path, factor, 0, 0);
        }
        private static MotionPoint Point(Entity e, int x, int y) { return new MotionPoint(e.Number(x, Double.NaN), e.Number(y, Double.NaN)); }
        private static void CheckPlane(Entity e)
        {
            foreach (Pair p in e.Fields)
                if ((p.Code == 30 || p.Code == 31 || p.Code == 38 || p.Code == 210 || p.Code == 220) && Math.Abs(Parse(p.Value)) > 0.0000001)
                    throw new FormatException("仅支持 XY 平面二维 DXF，请投影到 Z=0。");
            if (Math.Abs(e.Number(230, 1) - 1) > 0.0000001) throw new FormatException("不支持倾斜或翻转的 DXF OCS 法向，请转换到世界 XY 平面。");
        }
        private static List<MotionPoint> Polyline(Entity e, List<Entity> vertices, double tolerance)
        {
            int flags = (int)e.Number(70, 0);
            if ((flags & ~129) != 0) throw new FormatException("仅支持普通二维多段线，请先分解拟合曲线或三维多段线。");
            List<MotionPoint> controls = new List<MotionPoint>(); List<double> bulges = new List<double>();
            if (vertices != null) foreach (Entity v in vertices) { controls.Add(Point(v, 10, 20)); bulges.Add(v.Number(42, 0)); }
            else
            {
                foreach (Pair p in e.Fields)
                {
                    if (p.Code == 10) { controls.Add(new MotionPoint(Parse(p.Value), Double.NaN)); bulges.Add(0); }
                    else if (p.Code == 20 || p.Code == 42)
                    {
                        if (controls.Count == 0) throw new FormatException("多段线顶点顺序无效。");
                        int last = controls.Count - 1;
                        if (p.Code == 20) controls[last] = new MotionPoint(controls[last].X, Parse(p.Value)); else bulges[last] = Parse(p.Value);
                    }
                }
                if (e.Number(90, controls.Count) != controls.Count) throw new FormatException("多段线声明的顶点数与实际不符。");
            }
            if (controls.Count < 2) throw new FormatException("多段线至少需要两个顶点。");
            List<MotionPoint> result = new List<MotionPoint>(); result.Add(controls[0]);
            int edges = controls.Count - ((flags & 1) == 0 ? 1 : 0);
            for (int i = 0; i < edges; i++)
            {
                MotionPoint a = controls[i], b = controls[(i + 1) % controls.Count]; double bulge = bulges[i];
                if (Math.Abs(bulge) < 1e-10) { result.Add(b); continue; }
                double length = PathGeometry.Distance(a, b); if (length < 1e-10) throw new FormatException("圆弧多段线端点重合。");
                double offset = length * (1 - bulge * bulge) / (4 * bulge);
                MotionPoint center = new MotionPoint((a.X + b.X) / 2 - (b.Y - a.Y) / length * offset, (a.Y + b.Y) / 2 + (b.X - a.X) / length * offset);
                List<MotionPoint> arc = PathGeometry.Arc(center.X, center.Y, PathGeometry.Distance(a, center), Math.Atan2(a.Y - center.Y, a.X - center.X) * 180 / Math.PI, 4 * Math.Atan(bulge) * 180 / Math.PI, tolerance).Strokes[0];
                for (int j = 1; j < arc.Count; j++) result.Add(arc[j]);
                if (result.Count > PathGeometry.MaximumPoints) throw new FormatException("多段线采样点过多。");
            }
            return result;
        }
        private static List<MotionPoint> Spline(Entity e, double tolerance)
        {
            int degree = (int)e.Number(71, 0); List<double> xs = e.Numbers(10), ys = e.Numbers(20), knots = e.Numbers(40), weights = e.Numbers(41);
            if (degree < 1 || degree > 10 || xs.Count <= degree || ys.Count != xs.Count || knots.Count != xs.Count + degree + 1 || (weights.Count != 0 && weights.Count != xs.Count))
                throw new FormatException("SPLINE 需要完整的控制点、次数和节点矢量；仅拟合点格式请在 CAD 中转换。");
            if (e.Number(72, knots.Count) != knots.Count || e.Number(73, xs.Count) != xs.Count) throw new FormatException("SPLINE 声明的点数不符。");
            if (((int)e.Number(70, 0) & 2) != 0) throw new FormatException("周期 SPLINE 请先在 CAD 中转换为非周期样条或多段线。");
            for (int i = 1; i < knots.Count; i++) if (knots[i] < knots[i - 1]) throw new FormatException("SPLINE 节点必须非递减。");
            foreach (double w in weights) if (w <= 0) throw new FormatException("SPLINE 权重必须为正。");
            if (knots[xs.Count] <= knots[degree]) throw new FormatException("SPLINE 参数区间为空。");
            List<MotionPoint> result = new List<MotionPoint>();
            Func<double, MotionPoint> evaluate = delegate(double t)
            {
                int span = xs.Count - 1;
                for (int k = degree; k < xs.Count; k++) if (t < knots[k + 1]) { span = k; break; }
                double[] x = new double[degree + 1], y = new double[degree + 1], w = new double[degree + 1];
                for (int j = 0; j <= degree; j++) { int index = span - degree + j; w[j] = weights.Count == 0 ? 1 : weights[index]; x[j] = xs[index] * w[j]; y[j] = ys[index] * w[j]; }
                for (int r = 1; r <= degree; r++) for (int j = degree; j >= r; j--)
                {
                    int k = span - degree + j; double denominator = knots[k + degree - r + 1] - knots[k]; double alpha = denominator == 0 ? 0 : (t - knots[k]) / denominator;
                    x[j] = (1 - alpha) * x[j - 1] + alpha * x[j]; y[j] = (1 - alpha) * y[j - 1] + alpha * y[j]; w[j] = (1 - alpha) * w[j - 1] + alpha * w[j];
                }
                return new MotionPoint(x[degree] / w[degree], y[degree] / w[degree]);
            };
            result.Add(evaluate(knots[degree]));
            for (int k = degree; k < xs.Count; k++) if (knots[k + 1] > knots[k])
                for (int j = 0; j < 16; j++) Subdivide(evaluate, knots[k] + (knots[k + 1] - knots[k]) * j / 16, knots[k] + (knots[k + 1] - knots[k]) * (j + 1) / 16, tolerance, result, 0);
            return result;
        }
        private static void Subdivide(Func<double, MotionPoint> evaluate, double a, double b, double tolerance, List<MotionPoint> output, int depth)
        {
            MotionPoint start = evaluate(a), end = evaluate(b), middle = evaluate((a + b) / 2);
            if (PathGeometry.SegmentDistance(middle, start, end) > tolerance)
            {
                if (depth >= 15) throw new FormatException("SPLINE 无法满足指定弦差，请简化曲线。");
                Subdivide(evaluate, a, (a + b) / 2, tolerance, output, depth + 1); Subdivide(evaluate, (a + b) / 2, b, tolerance, output, depth + 1);
            }
            else output.Add(end);
            if (output.Count > PathGeometry.MaximumPoints) throw new FormatException("SPLINE 采样点超过 20000。");
        }
    }
}
