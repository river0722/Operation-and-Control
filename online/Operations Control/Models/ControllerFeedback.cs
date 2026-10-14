namespace Operations_Control.Models
{
    public sealed class ControllerFeedback
    {
        public ControllerSnapshot Snapshot { get; private set; }
        public AxisSample[] Axes { get; private set; }
        public ControllerFeedback(ControllerSnapshot snapshot, AxisSample[] axes)
        { Snapshot = snapshot; Axes = axes; }
    }
}
