using System;
using Operations_Control.Communication;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    // One in-flight query and one feedback source for every screen.
    public sealed class FeedbackService : IDisposable
    {
        private readonly IControllerSession session;
        private bool pending, disposed;
        private int generation, xMotor, yMotor, selectedMotor, coordinateSystem;
        private double requestedAt;
        private bool expired;
        public double AgeMs { get { return Latest == null ? Double.PositiveInfinity : MonotonicClock.NowMs - Latest.Snapshot.TimeMs; } }
        public bool Fresh { get { return Latest != null && AgeMs <= 1000 && !expired; } }
        public ControllerFeedback Latest { get; private set; }
        public event EventHandler Updated;
        public event Action<Exception> Failed;

        public FeedbackService(IControllerSession session) { this.session = session; }
        public void Clear() { generation++; Latest = null; }
        public void CheckTimeout()
        {
            if (disposed || !session.IsConnected || expired) return;
            if ((pending && MonotonicClock.NowMs - requestedAt > 1000) || (Latest != null && AgeMs > 1000))
            {
                expired = true; Latest = null;
                if (Failed != null) Failed(new InvalidOperationException("通信反馈超过 1 秒未更新；设备状态未知，停止指令仍依赖通信。"));
            }
        }

        public void Poll(int x, int y, int selected, int cs)
        {
            if (disposed || !session.IsConnected || x == y) return;
            if (xMotor != x || yMotor != y || selectedMotor != selected || coordinateSystem != cs)
            {
                Clear(); xMotor = x; yMotor = y; selectedMotor = selected; coordinateSystem = cs;
            }
            if (pending) return;
            int version = generation;
            ControllerFeedback result = null; pending = true; requestedAt = MonotonicClock.NowMs;
            session.Execute(delegate { result = session.ReadFeedback(x, y, selected, cs); }, delegate(Exception error)
            {
                pending = false;
                if (disposed || version != generation) return;
                Latest = error == null ? result : null;
                if (error == null) expired = false;
                if (error != null) { if (Failed != null) Failed(error); }
                else if (Updated != null) Updated(this, EventArgs.Empty);
            });
        }

        public void Dispose() { disposed = true; Clear(); }
    }
}
