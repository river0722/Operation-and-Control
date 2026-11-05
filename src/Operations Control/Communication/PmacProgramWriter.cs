using System;
using System.Globalization;
using System.Text;
using Operations_Control.Models;

namespace Operations_Control.Communication
{
    public static class PmacProgramWriter
    {
        private static string N(double value) { return value.ToString("0.########", CultureInfo.InvariantCulture); }
        public static string Write(MotionPlan plan)
        {
            PlanningOptions o = plan.Options;
            int feedTimeVariable = 5090 + o.CoordinateSystem * 100;
            StringBuilder text = new StringBuilder();
            text.AppendLine("; Generated XY trajectory. Units: mm, mm/s, milliseconds.");
            text.AppendLine("; Host planning mode: " + o.Mode);
            text.AppendLine("; Reserved coordinate system and program must be verified before hardware use.");
            text.AppendLine("; I42=0 selects TM timing globally. Isx90=1000 selects seconds for velocity.");
            text.AppendLine("CLOSE"); text.AppendLine("&" + o.CoordinateSystem);
            text.AppendLine("#" + o.XMotor + "->" + N(o.Mapping.XCountsPerUnit) + "X");
            text.AppendLine("#" + o.YMotor + "->" + N(o.Mapping.YCountsPerUnit) + "Y");
            text.AppendLine("I42=0"); text.AppendLine("I" + feedTimeVariable + "=1000");
            text.AppendLine("I" + (5013 + o.CoordinateSystem * 100) + "=5");
            text.AppendLine("OPEN PROG " + o.ProgramNumber + " CLEAR"); text.AppendLine("ABS");
            text.AppendLine("; 5 ms controller segmentation. Curved host plans arrive as sampled LINEAR moves.");
            text.AppendLine("INC(R)"); text.AppendLine("NORMAL K-1"); text.AppendLine("TA0 TS0");
            foreach (PlannedStroke block in plan.Blocks)
            {
                text.AppendLine(block.Transfer ? "; TRANSFER to next stroke (no tool control)" : "; DRAW stroke");
                foreach (MotionSegment s in block.Segments)
                {
                    if (s.Mode == MotionSegmentMode.Cubic) throw new InvalidOperationException("PVT/Spline 曲线必须先在上位机离散为直线段，再生成下位机程序。");
                    bool circular = s.Mode == MotionSegmentMode.Circular;
                    text.AppendLine(circular ? (s.SweepRadians > 0 ? "CIRCLE2" : "CIRCLE1") : "LINEAR");
                    text.AppendLine("TM " + N(s.DurationMs));
                    string move = "X" + N(s.End.X) + " Y" + N(s.End.Y);
                    if (circular) move += " I" + N(s.Center.X - s.Start.X) + " J" + N(s.Center.Y - s.Start.Y);
                    text.AppendLine(move);
                }
                if (block.StopAfter) text.AppendLine("DWELL 0");
            }
            text.AppendLine("CLOSE"); return text.ToString();
        }
    }
}
