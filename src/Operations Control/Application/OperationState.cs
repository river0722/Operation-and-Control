namespace Operations_Control.Application
{
    // Shared by application services; updated only on the caller's application thread.
    public sealed class OperationState
    {
        public bool ManualBusy, ShuttingDown, Following, Computing;
        public bool CommandBusy, Running, Homing, Stopping, StopFailed, Verifying;
        public int? JoggingMotor;
        public int? MovingMotor;
        // Axis to prioritize while a manual command is being verified or stopped.
        public int? ManualFeedbackMotor;
        public bool ManualStopping, ManualStopFailed, FeedbackStale;
        public bool WorkflowExecuting { get { return CommandBusy || Running || Homing || Stopping || StopFailed || Verifying; } }
        public bool WorkflowBusy { get { return WorkflowExecuting || Computing; } }
        public bool ManualExecuting { get { return MovingMotor.HasValue || JoggingMotor.HasValue || ManualStopping || ManualStopFailed; } }
        public bool ExternalBusy { get { return ManualBusy || ShuttingDown || Following || ManualExecuting; } }
        public bool CanPoll { get { return !ManualBusy && !ShuttingDown && !CommandBusy; } }
    }
}
