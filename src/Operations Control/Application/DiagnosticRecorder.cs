using System;
using System.Collections.Generic;
using System.IO;
using Operations_Control.Files;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    public sealed class DiagnosticRecorder : IDisposable
    {
        private StreamWriter writer;
        private readonly Dictionary<int, double> lastTimes = new Dictionary<int, double>();
        private double origin, lastFlush;
        private bool gapBefore;
        private string source;
        public string OutputDirectory = DiagnosticRecordingStore.DirectoryPath;
        public bool Active { get { return writer != null; } }
        public string FilePath { get; private set; }
        public int SampleCount { get; private set; }
        public event Action<string> Changed;
        public event Action<Exception> Failed;

        public void StartManual(ControllerFeedback feedback, string dataSource)
        {
            if (Active) Stop();
            try { Start(feedback, dataSource); Capture(feedback); }
            catch { try { Stop(); } catch { } throw; }
        }
        private void Start(ControllerFeedback feedback, string dataSource)
        {
            if (feedback == null || feedback.Axes.Length == 0) throw new InvalidOperationException("需要有效反馈才能开始录制。");
            origin = Double.PositiveInfinity;
            foreach (AxisSample sample in feedback.Axes) origin = Math.Min(origin, sample.TimeMs);
            source = dataSource; lastTimes.Clear(); gapBefore = false; lastFlush = origin; SampleCount = 0;
            string path = Path.Combine(OutputDirectory, "diagnostics-" + dataSource + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".csv");
            writer = DiagnosticRecordingStore.Create(path, dataSource, DateTime.UtcNow); FilePath = path;
            Notify("正在手动录制：" + path);
        }
        public void Observe(ControllerFeedback feedback, bool fresh, string dataSource)
        {
            try
            {
                if (!Active) return;
                if (source != dataSource) { Stop(); return; }
                if (!fresh || feedback == null)
                {
                    gapBefore = true;
                    return;
                }
                Capture(feedback);
            }
            catch (Exception error)
            {
                try { Stop(); } catch { }
                if (Failed != null) Failed(error);
            }
        }
        private void Capture(ControllerFeedback feedback)
        {
            foreach (AxisSample axis in feedback.Axes)
            {
                if (lastTimes.ContainsKey(axis.Motor) && axis.TimeMs <= lastTimes[axis.Motor]) continue;
                if (SampleCount >= DiagnosticRecordingStore.MaximumSamples) { Stop(); Notify("录制已达 1000000 个轴样本上限，数据已保存。"); return; }
                DiagnosticRecordingStore.Append(writer, new DiagnosticSample { Motor = axis.Motor, TimeMs = axis.TimeMs - origin, Position = axis.Position,
                    FollowingError = axis.FollowingError, Status = axis.Status,
                    GapBefore = gapBefore || (lastTimes.ContainsKey(axis.Motor) && axis.TimeMs - lastTimes[axis.Motor] > 1000) });
                lastTimes[axis.Motor] = axis.TimeMs; SampleCount++;
            }
            gapBefore = false;
            if (feedback.TimeMs - lastFlush >= 1000) { writer.Flush(); lastFlush = feedback.TimeMs; }
        }
        public void Stop()
        {
            if (writer == null) return;
            StreamWriter closing = writer; writer = null;
            closing.Dispose(); Notify("已保存 " + SampleCount + " 个轴样本：" + FilePath);
        }
        public void Flush() { if (writer != null) writer.Flush(); }
        private void Notify(string text) { if (Changed != null) Changed(text); }
        public void Dispose() { Stop(); }
    }
}
