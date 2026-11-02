using Operations_Control.Models;
using System.Collections.Generic;

namespace Operations_Control.Communication
{
    // Optional capability used by mouse-follow. Keeping this separate from
    // IControllerSession preserves compatibility with existing test/fallback sessions.
    public interface IStreamingTrajectorySession
    {
        void BeginPvtFollow(int coordinateSystem, int xMotor, int yMotor, CanvasSettings mapping);
        void AppendPvtFollow(double durationMs, MotionPoint end, MotionPoint velocity);
        void EndPvtFollow();
        void AbortPvtFollow(int coordinateSystem, int xMotor, int yMotor);
        void BeginSplineFollow(int coordinateSystem, int xMotor, int yMotor, CanvasSettings mapping);
        void AppendSplineFollow(double segmentTimeMs, IList<MotionPoint> points);
        void EndSplineFollow();
        void AbortSplineFollow(int coordinateSystem, int xMotor, int yMotor);
    }
}
