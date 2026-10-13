using System.Globalization;
using System.Text;
using Operations_Control.Models;

namespace Operations_Control.Services
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
            text.AppendLine("; Reserved coordinate system and program must be verified before hardware use.");
            text.AppendLine("; I42=0 selects TM timing globally. Isx90=1000 selects seconds for velocity.");
            text.AppendLine("CLOSE"); text.AppendLine("&" + o.CoordinateSystem);
            text.AppendLine("#" + o.XMotor + "->" + N(o.Mapping.XCountsPerUnit) + "X");
            text.AppendLine("#" + o.YMotor + "->" + N(o.Mapping.YCountsPerUnit) + "Y");
            text.AppendLine("I42=0"); text.AppendLine("I" + feedTimeVariable + "=1000");
            text.AppendLine("OPEN PROG " + o.ProgramNumber + " CLEAR"); text.AppendLine("ABS");
            foreach (PlannedStroke block in plan.Blocks)
            {
                text.AppendLine(block.Transfer ? "; TRANSFER to next stroke (no tool control)" : "; DRAW stroke");
                if (o.Mode == InterpolationMode.Spline && !block.Transfer)
                {
                    text.AppendLine("SPLINE1 TM" + N(block.SplineTimeMs));
                    for (int i = 1; i < block.Points.Count; i++) text.AppendLine("X" + N(block.Points[i].X) + " Y" + N(block.Points[i].Y));
                }
                else foreach (MotionSegment s in block.Segments)
                {
                    text.AppendLine("PVT" + N(s.DurationMs));
                    text.AppendLine("X" + N(s.End.X) + ":" + N(s.EndVelocity.X) + " Y" + N(s.End.Y) + ":" + N(s.EndVelocity.Y));
                }
                text.AppendLine("DWELL0");
            }
            text.AppendLine("CLOSE"); return text.ToString();
        }
    }
}
