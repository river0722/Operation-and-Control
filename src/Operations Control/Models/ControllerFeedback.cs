using System;

namespace Operations_Control.Models
{
    public sealed class ControllerFeedback
    {
        public ControllerSnapshot Snapshot { get; private set; }
        public AxisSample[] Axes { get; private set; }
        // A focused single-axis refresh may be newer than the last complete
        // X/Y snapshot (especially when the selected motor is a third axis).
        public double TimeMs { get; private set; }
        public ControllerFeedback(ControllerSnapshot snapshot, AxisSample[] axes)
        { Snapshot = snapshot; Axes = axes; TimeMs = snapshot == null ? 0 : snapshot.TimeMs; }
        private ControllerFeedback(ControllerSnapshot snapshot, AxisSample[] axes, double timeMs)
        { Snapshot = snapshot; Axes = axes; TimeMs = timeMs; }
        public AxisSample Find(int motor)
        {
            foreach (AxisSample axis in Axes) if (axis.Motor == motor) return axis;
            return null;
        }

        public ControllerFeedback ReplaceAxis(AxisSample updated)
        {
            if (updated == null) return this;
            AxisSample[] copy = new AxisSample[Axes.Length];
            Array.Copy(Axes, copy, Axes.Length);
            for (int i = 0; i < copy.Length; i++) if (copy[i].Motor == updated.Motor) copy[i] = updated;
            ControllerSnapshot snapshot = new ControllerSnapshot
            {
                XCounts = Snapshot.XCounts, YCounts = Snapshot.YCounts,
                XTimeMs = Snapshot.XTimeMs, YTimeMs = Snapshot.YTimeMs,
                XStatus = Snapshot.XStatus, YStatus = Snapshot.YStatus,
                Coordinate = Snapshot.Coordinate, QueryMs = Snapshot.QueryMs
            };
            if (copy.Length > 0 && copy[0].Motor == updated.Motor)
            { snapshot.XCounts = updated.Position; snapshot.XTimeMs = updated.TimeMs; snapshot.XStatus = updated.Status; }
            if (copy.Length > 1 && copy[1].Motor == updated.Motor)
            { snapshot.YCounts = updated.Position; snapshot.YTimeMs = updated.TimeMs; snapshot.YStatus = updated.Status; }
            return new ControllerFeedback(snapshot, copy, updated.TimeMs);
        }
    }
}
