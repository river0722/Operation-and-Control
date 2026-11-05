using System;
using System.Globalization;
using System.IO;
using System.Text;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Files
{
    public static class RunDataExporter
    {
        private static string N(double value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        public static string Export(TrajectoryRun run, string parentDirectory)
        {
            AnalysisResult result = TrajectoryAnalysis.Analyze(run);
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
                writer.WriteLine("block,segment,transfer,stop_after,start_ms,duration_ms,start_x_mm,start_y_mm,end_x_mm,end_y_mm,start_vx_mm_s,start_vy_mm_s,end_vx_mm_s,end_vy_mm_s,segment_mode,center_x_mm,center_y_mm,sweep_radians");
                for (int blockIndex = 0; blockIndex < run.Plan.Blocks.Count; blockIndex++)
                {
                    PlannedStroke block = run.Plan.Blocks[blockIndex];
                    for (int segmentIndex = 0; segmentIndex < block.Segments.Count; segmentIndex++)
                    {
                        MotionSegment s = block.Segments[segmentIndex];
                        bool stopAfter = block.StopAfter && segmentIndex == block.Segments.Count - 1;
                        writer.WriteLine(blockIndex + "," + segmentIndex + "," + block.Transfer + "," + stopAfter + "," + N(s.StartMs) + "," + N(s.DurationMs) + "," + N(s.Start.X) + "," + N(s.Start.Y) + "," + N(s.End.X) + "," + N(s.End.Y) + "," + N(s.StartVelocity.X) + "," + N(s.StartVelocity.Y) + "," + N(s.EndVelocity.X) + "," + N(s.EndVelocity.Y) + "," + s.Mode + "," + N(s.Center.X) + "," + N(s.Center.Y) + "," + N(s.SweepRadians));
                    }
                }
            }
            return directory;
        }
    }
}
