namespace Operations_Control.Application
{
    // Shared by application services; updated only on the caller's application thread.
    public sealed class OperationState
    {
        public bool ManualBusy, ShuttingDown, Following, Computing;
        public bool CommandBusy, Running, Homing, Stopping, StopFailed;
        public int? JoggingMotor;
        public bool WorkflowExecuting { get { return CommandBusy || Running || Homing || Stopping || StopFailed; } }
        public bool WorkflowBusy { get { return WorkflowExecuting || Computing; } }
        public bool ExternalBusy { get { return ManualBusy || ShuttingDown || Following || JoggingMotor.HasValue; } }
        public bool CanPoll { get { return !ManualBusy && !ShuttingDown && !CommandBusy && !Stopping; } }
    }
}
