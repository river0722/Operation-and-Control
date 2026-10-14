using System;
using Operations_Control.Communication;

namespace Operations_Control.Application
{
    public sealed class ManualMotionService
    {
        private readonly IControllerSession session;
        private readonly OperationState state;
        public event EventHandler StateChanged;
        public ManualMotionService(IControllerSession session, OperationState state) { this.session = session; this.state = state; }
        private void Changed() { if (StateChanged != null) StateChanged(this, EventArgs.Empty); }

        public bool Execute(Action action, Action<Exception> completed)
        {
            if (state.ManualBusy || state.ShuttingDown || state.Following || state.WorkflowBusy) return false;
            state.ManualBusy = true; Changed();
            session.Execute(action, delegate(Exception error)
            {
                state.ManualBusy = false;
                if (completed != null) completed(error);
                Changed();
            });
            return true;
        }
        public bool StartJog(int motor, int direction, decimal speed, decimal? step, Action<Exception> completed)
        {
            if (!session.IsConnected || state.JoggingMotor.HasValue || state.ManualBusy || state.ShuttingDown || state.Following || state.WorkflowBusy) return false;
            if (!step.HasValue) state.JoggingMotor = motor;
            return Execute(delegate { session.Jog(motor, direction, speed, step); }, delegate(Exception error)
            {
                if (error != null && state.JoggingMotor == motor) StopJog(null);
                if (completed != null) completed(error);
            });
        }
        public void StopJog(Action<int, Exception> completed)
        {
            if (!state.JoggingMotor.HasValue) return;
            int motor = state.JoggingMotor.Value; state.JoggingMotor = null;
            // Queue release even while start is pending, preserving the single STA order.
            session.Execute(delegate { if (session.IsConnected) session.Stop(new int[] { motor }); }, delegate(Exception error)
            { if (completed != null) completed(motor, error); Changed(); });
            Changed();
        }
        public void Disconnect(int[] motors, Action<Exception, Exception> completed)
        {
            if (state.ShuttingDown) return;
            state.ShuttingDown = true; state.JoggingMotor = null; Changed();
            Exception stopError = null;
            session.Execute(delegate
            {
                try { if (session.IsConnected) session.Stop(motors); }
                catch (Exception error) { stopError = error; }
                finally { session.Disconnect(); }
            }, delegate(Exception error)
            {
                state.ManualBusy = false; state.ShuttingDown = false;
                if (completed != null) completed(stopError, error);
                Changed();
            });
        }
    }
}
