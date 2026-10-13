using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    public sealed class AnalysisResult
    {
        public int Samples, ContourSamples;
        public double TrackingRms, TrackingMax, ContourRms, ContourMax, QueryMean, QueryMax, AxisSkewMax;
        public double? EstimatedLagMs;
        public string Text;
    }
    public static class TrajectoryAnalysis
    {
        private sealed class Edge { public MotionPoint A, B; }
        private sealed class Node
        {
            public double MinX, MaxX, MinY, MaxY;
            public Node Left, Right;
            public List<Edge> Edges;
        }
        private static Node Index(List<Edge> edges, int depth)
        {
            Node n = new Node { MinX = Double.PositiveInfinity, MinY = Double.PositiveInfinity, MaxX = Double.NegativeInfinity, MaxY = Double.NegativeInfinity };
            foreach (Edge e in edges) { n.MinX = Math.Min(n.MinX, Math.Min(e.A.X, e.B.X)); n.MaxX = Math.Max(n.MaxX, Math.Max(e.A.X, e.B.X)); n.MinY = Math.Min(n.MinY, Math.Min(e.A.Y, e.B.Y)); n.MaxY = Math.Max(n.MaxY, Math.Max(e.A.Y, e.B.Y)); }
            if (edges.Count <= 12) { n.Edges = edges; return n; }
            bool x = n.MaxX - n.MinX >= n.MaxY - n.MinY;
            edges.Sort(delegate(Edge a, Edge b) { return (x ? a.A.X + a.B.X : a.A.Y + a.B.Y).CompareTo(x ? b.A.X + b.B.X : b.A.Y + b.B.Y); });
            int mid = edges.Count / 2; n.Left = Index(edges.GetRange(0, mid), depth + 1); n.Right = Index(edges.GetRange(mid, edges.Count - mid), depth + 1); return n;
        }
        private static double Bounds(Node node, MotionPoint p)
        { double dx = Math.Max(0, Math.Max(node.MinX - p.X, p.X - node.MaxX)), dy = Math.Max(0, Math.Max(node.MinY - p.Y, p.Y - node.MaxY)); return Math.Sqrt(dx * dx + dy * dy); }
        private static double Nearest(Node node, MotionPoint p, double best)
        {
            if (Bounds(node, p) > best) return best;
            if (node.Edges != null) { foreach (Edge e in node.Edges) best = Math.Min(best, PathGeometry.SegmentDistance(p, e.A, e.B)); return best; }
            Node first = Bounds(node.Left, p) <= Bounds(node.Right, p) ? node.Left : node.Right, second = first == node.Left ? node.Right : node.Left;
            best = Nearest(first, p, best); return Nearest(second, p, best);
        }
        private static Node DrawingIndex(DrawingPath drawing)
        {
            List<Edge> edges = new List<Edge>(); foreach (List<MotionPoint> stroke in drawing.Strokes) for (int i = 1; i < stroke.Count; i++) edges.Add(new Edge { A = stroke[i - 1], B = stroke[i] });
            return Index(edges, 0);
        }
        private static bool IsTransfer(MotionPlan plan, double time)
        {
            int left = 0, right = plan.Segments.Count - 1;
            while (left < right) { int mid = (left + right) / 2; if (plan.Segments[mid].StartMs + plan.Segments[mid].DurationMs < time) left = mid + 1; else right = mid; }
            return plan.Segments[left].Transfer;
        }
        private static double TrackingSquared(TrajectoryRun run, TrajectorySample sample, double lag)
        {
            MotionPoint rx = run.Plan.At(sample.XTimeMs - lag), ry = run.Plan.At(sample.YTimeMs - lag);
            double x = sample.Position.X - rx.X, y = sample.Position.Y - ry.Y; return x * x + y * y;
        }
        public static AnalysisResult Analyze(TrajectoryRun run)
        {
            if (run == null || run.Plan == null || run.Samples.Count == 0) throw new InvalidOperationException("尚无本次运行反馈数据。");
            AnalysisResult result = new AnalysisResult(); result.Samples = run.Samples.Count; Node index = DrawingIndex(run.Plan.Drawing);
            double trackingSum = 0, contourSum = 0, querySum = 0, movement = 0;
            for (int i = 0; i < run.Samples.Count; i++)
            {
                TrajectorySample s = run.Samples[i]; double squared = TrackingSquared(run, s, 0);
                trackingSum += squared; result.TrackingMax = Math.Max(result.TrackingMax, Math.Sqrt(squared));
                if (!IsTransfer(run.Plan, s.TimeMs))
                {
                    double error = Nearest(index, s.Position, Double.PositiveInfinity); contourSum += error * error; result.ContourMax = Math.Max(result.ContourMax, error); result.ContourSamples++;
                }
                querySum += s.QueryMs; result.QueryMax = Math.Max(result.QueryMax, s.QueryMs); result.AxisSkewMax = Math.Max(result.AxisSkewMax, Math.Abs(s.XTimeMs - s.YTimeMs));
                if (i > 0) movement += PathGeometry.Distance(run.Samples[i - 1].Position, s.Position);
            }
            result.TrackingRms = Math.Sqrt(trackingSum / result.Samples); result.ContourRms = result.ContourSamples == 0 ? Double.NaN : Math.Sqrt(contourSum / result.ContourSamples); result.QueryMean = querySum / result.Samples;
            if (result.Samples >= 10 && movement > 0.1)
            {
                double best = Double.PositiveInfinity; int bestLag = 0, stride = Math.Max(1, result.Samples / 2000);
                for (int lag = -1000; lag <= 1000; lag += 10)
                {
                    double sum = 0; for (int i = 0; i < result.Samples; i += stride) sum += TrackingSquared(run, run.Samples[i], lag);
                    if (sum < best) { best = sum; bestLag = lag; }
                }
                if (Math.Abs(bestLag) < 1000) result.EstimatedLagMs = bestLag;
            }
            StringBuilder text = new StringBuilder();
            text.AppendLine("来源：" + (run.Simulation ? "模拟反馈（不代表实机精度）" : "PMAC 位置反馈"));
            text.AppendLine("运行状态：" + run.Result + "；" + (run.Completed ? "完整运行" : "未完整运行，统计仅覆盖已采样部分"));
            text.AppendLine("采样数量：" + result.Samples + "；轮廓统计点数：" + result.ContourSamples);
            text.AppendLine("时间配对误差 RMS / 最大值：" + N(result.TrackingRms) + " / " + N(result.TrackingMax) + " mm");
            text.AppendLine("原始绘图轮廓偏差 RMS / 最大值：" + (result.ContourSamples == 0 ? "无绘图阶段样本" : N(result.ContourRms) + " / " + N(result.ContourMax) + " mm"));
            text.AppendLine("估计跟随滞后：" + (result.EstimatedLagMs.HasValue ? N(result.EstimatedLagMs.Value) + " ms" : "不可可靠估计（数据不足、轨迹无变化或达到搜索边界）"));
            text.AppendLine("反馈查询耗时 平均 / 最大：" + N(result.QueryMean) + " / " + N(result.QueryMax) + " ms");
            text.AppendLine("X/Y 查询时间差最大值：" + N(result.AxisSkewMax) + " ms");
            text.AppendLine("启动命令耗时：" + N(run.StartCommandMs) + " ms；计划时长：" + N(run.Plan.DurationMs / 1000) + " s");
            text.AppendLine();
            text.AppendLine("时间误差按每轴查询中点与计划时间配对；通信中点近似及控制器启动延迟会影响结果。");
            text.AppendLine("轮廓误差为反馈点到原始绘图折线的最短距离，排除计划中的转移段；它不反映沿路径的滞后。");
            text.AppendLine("滞后估计使用 ±1000 ms、10 ms 步长的轨迹拟合。周期/重复/短路径可能产生歧义，不能当作精密时延测量。");
            text.AppendLine("可能偏差来源：DXF/绘图离散、Spline 位置松弛、机械标定、伺服跟随、通信轮询和两轴非同时采样。精度验收需实机数据及既定指标。");
            result.Text = text.ToString(); return result;
        }
        private static string N(double value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        public static string Export(TrajectoryRun run, string parentDirectory)
        {
            AnalysisResult result = Analyze(run);
            string directory = Path.Combine(parentDirectory, "PMAC-" + run.StartedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "analysis.txt"), result.Text, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "program.pmc"), run.Plan.Program, Encoding.ASCII);
            using (StreamWriter writer = new StreamWriter(Path.Combine(directory, "actual.csv"), false, Encoding.UTF8))
            {
                writer.WriteLine("source,completed,time_ms,x_query_ms,y_query_ms,x_mm,y_mm,reference_x_mm,reference_y_mm,error_x_mm,error_y_mm,query_duration_ms");
                foreach (TrajectorySample s in run.Samples)
                {
                    double x = run.Plan.At(s.XTimeMs).X, y = run.Plan.At(s.YTimeMs).Y;
                    writer.WriteLine((run.Simulation ? "simulation" : "hardware") + "," + run.Completed + "," + N(s.TimeMs) + "," + N(s.XTimeMs) + "," + N(s.YTimeMs) + "," + N(s.Position.X) + "," + N(s.Position.Y) + "," + N(x) + "," + N(y) + "," + N(s.Position.X - x) + "," + N(s.Position.Y - y) + "," + N(s.QueryMs));
                }
            }
            using (StreamWriter writer = new StreamWriter(Path.Combine(directory, "drawing.csv"), false, Encoding.UTF8))
            {
                writer.WriteLine("stroke,point,x_mm,y_mm");
                for (int stroke = 0; stroke < run.Plan.Drawing.Strokes.Count; stroke++) for (int i = 0; i < run.Plan.Drawing.Strokes[stroke].Count; i++)
                { MotionPoint p = run.Plan.Drawing.Strokes[stroke][i]; writer.WriteLine(stroke + "," + i + "," + N(p.X) + "," + N(p.Y)); }
            }
            using (StreamWriter writer = new StreamWriter(Path.Combine(directory, "planned.csv"), false, Encoding.UTF8))
            {
                writer.WriteLine("segment,transfer,start_ms,duration_ms,start_x_mm,start_y_mm,end_x_mm,end_y_mm,start_vx_mm_s,start_vy_mm_s,end_vx_mm_s,end_vy_mm_s");
                for (int i = 0; i < run.Plan.Segments.Count; i++)
                {
                    MotionSegment s = run.Plan.Segments[i]; writer.WriteLine(i + "," + s.Transfer + "," + N(s.StartMs) + "," + N(s.DurationMs) + "," + N(s.Start.X) + "," + N(s.Start.Y) + "," + N(s.End.X) + "," + N(s.End.Y) + "," + N(s.StartVelocity.X) + "," + N(s.StartVelocity.Y) + "," + N(s.EndVelocity.X) + "," + N(s.EndVelocity.Y));
                }
            }
            return directory;
        }
    }
}
