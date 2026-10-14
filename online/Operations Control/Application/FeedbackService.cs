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
        public ControllerFeedback Latest { get; private set; }
        public event EventHandler Updated;
        public event Action<Exception> Failed;

        public FeedbackService(IControllerSession session) { this.session = session; }
        public void Clear() { generation++; Latest = null; }

        public void Poll(int x, int y, int selected, int cs)
        {
            if (disposed || !session.IsConnected || x == y) return;
            if (xMotor != x || yMotor != y || selectedMotor != selected || coordinateSystem != cs)
            {
                Clear(); xMotor = x; yMotor = y; selectedMotor = selected; coordinateSystem = cs;
            }
            if (pending) return;
            int version = generation;
            ControllerFeedback result = null; pending = true;
            session.Execute(delegate { result = session.ReadFeedback(x, y, selected, cs); }, delegate(Exception error)
            {
                pending = false;
                if (disposed || version != generation) return;
                Latest = error == null ? result : null;
                if (error != null) { if (Failed != null) Failed(error); }
                else if (Updated != null) Updated(this, EventArgs.Empty);
            });
        }

        public void Dispose() { disposed = true; Clear(); }
    }
}
