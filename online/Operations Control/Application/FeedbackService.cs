using System;
using System.Threading;
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
        private double completeReadAt;
        private double freshnessAgeOffset;
        private bool timeoutPaused;
        private bool expired;
        public double AgeMs { get { return Latest == null ? Double.PositiveInfinity : timeoutPaused ? 0 : Math.Max(0, MonotonicClock.NowMs - Latest.TimeMs - freshnessAgeOffset); } }
        public bool Fresh { get { return Latest != null && AgeMs <= 1000 && !expired; } }
        public ControllerFeedback Latest { get; private set; }
        public event EventHandler Updated;
        public event Action<Exception> Failed;

        public FeedbackService(IControllerSession session) { this.session = session; }
        public void Clear() { generation++; Latest = null; freshnessAgeOffset = 0; }
        public void PauseTimeoutForCommand() { timeoutPaused = true; }
        public void ResumeTimeoutAfterCommand()
        {
            if (!timeoutPaused) return;
            timeoutPaused = false;
            double now = MonotonicClock.NowMs;
            if (Latest != null) freshnessAgeOffset = Math.Max(0, now - Latest.TimeMs);
            if (pending) requestedAt = now;
            completeReadAt = now;
        }
        public void CheckTimeout()
        {
            if (disposed || !session.IsConnected || expired || timeoutPaused) return;
            if ((pending && MonotonicClock.NowMs - requestedAt > 1000) || (Latest != null && AgeMs > 1000))
            {
                expired = true; Latest = null;
                if (Failed != null) Failed(new InvalidOperationException("通信反馈超过 1 秒未更新；设备状态未知，停止指令仍依赖通信。"));
            }
        }

        public void Poll(int x, int y, int selected, int cs)
        { Poll(x, y, selected, cs, null); }

        public void PollNow(int? focusedMotor)
        { Poll(xMotor, yMotor, selectedMotor, coordinateSystem, focusedMotor); }

        public void Poll(int x, int y, int selected, int cs, int? focusedMotor)
        {
            if (disposed || !session.IsConnected || x == y) return;
            if (xMotor != x || yMotor != y || selectedMotor != selected || coordinateSystem != cs)
            {
                Clear(); xMotor = x; yMotor = y; selectedMotor = selected; coordinateSystem = cs;
            }
            if (pending) return;
            int version = generation;
            ControllerFeedback result = null; pending = true; requestedAt = MonotonicClock.NowMs;
            ControllerFeedback previous = Latest;
            // Keep all monitored axes current as well as the moving axis. A
            // coordinate program always needs the full coordinate snapshot.
            bool canFocus = cs == 0 && focusedMotor.HasValue && Fresh && previous.Find(focusedMotor.Value) != null &&
                requestedAt - completeReadAt < 250;
            session.Execute(delegate
            {
                if (version != Thread.VolatileRead(ref generation)) return;
                result = canFocus ? previous.ReplaceAxis(session.ReadAxisFeedback(focusedMotor.Value)) : session.ReadFeedback(x, y, selected, cs);
            }, delegate(Exception error)
            {
                pending = false;
                if (disposed || version != generation) return;
                Latest = error == null ? result : null;
                if (error == null) { expired = false; freshnessAgeOffset = 0; if (!canFocus) completeReadAt = result.TimeMs; }
                if (error != null) { if (Failed != null) Failed(error); }
                else if (Updated != null) Updated(this, EventArgs.Empty);
            });
        }

        public void Dispose() { disposed = true; Clear(); }
    }
}
