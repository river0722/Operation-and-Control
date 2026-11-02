using Operations_Control.Communication;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Application
{
    public static class MotionPlanningService
    {
        public static MotionPlan Build(DrawingPath drawing, PlanningOptions options, MotionPoint initial)
        {
            MotionPlan plan = TrajectoryPlanner.Build(drawing, options, initial);
            plan.Program = PmacProgramWriter.Write(plan);
            return plan;
        }
    }
}
