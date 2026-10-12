using System;
using System.Threading;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    // Only one request may be in flight; mouse updates replace the pending target.
    public sealed class CanvasFollower
    {
        private readonly ControllerSession session;
        private readonly Control owner;
        private int generation, xMotor, yMotor;
        private bool pending, configured;
        private decimal speed;
        private MotionPoint? latest;
        public bool Active { get; private set; }
        public event EventHandler Failed;
        public string Error { get; private set; }

        public CanvasFollower(ControllerSession session, Control owner) { this.session = session; this.owner = owner; }
        public void Begin(int x, int y, decimal velocity)
        {
            if (x == y || x < 1 || x > 8 || y < 1 || y > 8) throw new ArgumentException("X/Y 必须选择不同的有效电机。");
            if (velocity <= 0 || velocity > 15) throw new ArgumentOutOfRangeException("velocity");
            Interlocked.Increment(ref generation);
            xMotor = x; yMotor = y; speed = velocity; latest = null; configured = false; Active = true;
        }
        public void Submit(MotionPoint target) { if (Active) latest = target; }
        public void Pump()
        {
            if (!Active || pending || !latest.HasValue) return;
            int epoch = generation, x = xMotor, y = yMotor;
            decimal velocity = speed;
            MotionPoint point = latest.Value; latest = null;
            bool setSpeed = !configured;
            pending = true;
            session.Execute(owner, delegate
            {
                if (Thread.VolatileRead(ref generation) != epoch) return;
                if (setSpeed) { session.SetSpeed(x, velocity); session.SetSpeed(y, velocity); }
                if (Thread.VolatileRead(ref generation) == epoch) session.MovePair(x, y, point);
            }, delegate(Exception failure)
            {
                pending = false;
                if (generation != epoch) return;
                if (failure == null) { configured = true; return; }
                Error = failure.Message; Cancel();
                if (Failed != null) Failed(this, EventArgs.Empty);
            });
        }
        public void Cancel()
        {
            bool stop = Active;
            Active = false; latest = null; Interlocked.Increment(ref generation);
            if (!stop) return;
            int x = xMotor, y = yMotor;
            session.Execute(owner, delegate { if (session.IsConnected) session.Stop(new int[] { x, y }); }, delegate(Exception failure)
            {
                if (failure != null) { Error = "绘图停止失败：" + failure.Message; if (Failed != null) Failed(this, EventArgs.Empty); }
            });
        }
    }
}
