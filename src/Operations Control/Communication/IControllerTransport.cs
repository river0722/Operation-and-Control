using System;
using Operations_Control.Models;
using Operations_Control.Motion;
using System.Collections.Generic;

namespace Operations_Control.Communication
{
    public interface IStreamingTrajectoryTransport
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
    
    public interface IControllerTransport : IDisposable
    {
        bool IsConnected { get; }
        string DeviceName { get; }
        bool Connect(int ownerWindow);
        void Disconnect();
        CommandResult Send(string command);
        void Download(string filePath);
    }
    // Optional simulator capability; the session does not depend on a concrete simulator.
    public interface IPlannedTransport
    {
        void StartPlan(MotionPlan plan, double lagMs);
    }

    public interface IPointToPointTransport
    {
        void MovePointToPoint(int motor, double target, double speed, ManualMotionProfile profile);
    }

}
