using System;
using System.Collections.Generic;
using Operations_Control.Models;

namespace Operations_Control.Communication
{
    // Execute serializes work; completion returns through the configured application context.
    public interface IControllerSession : IDisposable
    {
        bool IsConnected { get; }
        string DeviceName { get; }
        void Execute(Action action, Action<Exception> completed);
        bool Connect(bool simulation, int ownerWindow);
        void Disconnect();
        void ClearSamples();
        CommandResult Send(string command);
        void SetSpeed(int motor, decimal speed);
        void Jog(int motor, int direction, decimal speed, decimal? step);
        void Jog(int motor, int direction, decimal speed, decimal? step, Func<bool> cancelled);
        void Move(int motor, decimal target, decimal speed);
        void Move(int motor, decimal target, decimal speed, Func<bool> cancelled);
        void MovePair(int x, int y, MotionPoint point);
        void ZeroPosition(int motor);
        void Stop(IEnumerable<int> motors);
        void Download(string path);
        ControllerSnapshot ReadSnapshot(int x, int y, int coordinateSystem);
        ControllerFeedback ReadFeedback(int x, int y, int selectedMotor, int coordinateSystem);
        double StartPlan(MotionPlan plan, double simulatorLagMs, Func<bool> cancelled);
        void StopPlan(int coordinateSystem, int x, int y);
        void SetEnabled(int motor, bool enabled);
        void Home(int x, int y, bool both, int plc);
        void Home(int x, int y, bool both, int plc, Func<bool> cancelled);
        void StopHome(int x, int y, int plc);
    }
}
