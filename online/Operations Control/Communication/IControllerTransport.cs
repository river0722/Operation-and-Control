using System;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Communication
{
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
