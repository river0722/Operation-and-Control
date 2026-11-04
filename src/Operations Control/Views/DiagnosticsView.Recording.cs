using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Operations_Control.Application;
using Operations_Control.Files;
using Operations_Control.Models;

namespace Operations_Control.Views
{
    public sealed partial class DiagnosticsView
    {
        private readonly DiagnosticRecorder recorder = new DiagnosticRecorder();
        private readonly Button recordStart = Button("开始录制"), recordStop = Button("结束录制"), saveData = Button("导出 CSV");
        private readonly Button openData = Button("打开 CSV 回顾"), reviewLatest = Button("回顾最近记录"), exportImage = Button("导出 PNG"), returnLive = Button("返回实时");
        private readonly ComboBox reviewMotor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 64 };
        private readonly NumericUpDown reviewStart = Number(0, 1000000000, 0), reviewEnd = Number(0, 1000000000, 1);
        private readonly HistoryRangeSelector historyRange = new HistoryRangeSelector { Height = 58, Dock = DockStyle.Fill };
        private readonly Label recordingStatus = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "点击“开始录制”记录 X、Y 和所选轴，完成后点击“结束录制”。", Padding = new Padding(8, 2, 0, 0) };
        private ControllerFeedback latestFeedback;
        private bool latestFresh, reviewing, loadingHistory;
        private DiagnosticRecording history;
        private string historyPath;
        private double historyDurationMs;
        public bool Simulation { get; set; }

        private Control CreateRecordingControls()
        {
            Simulation = true;
            TableLayoutPanel controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false };
            actions.Controls.AddRange(new Control[] { recordStart, recordStop, saveData, openData, reviewLatest, exportImage, returnLive });
            FlowLayoutPanel range = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false };
            Field(range, "回顾电机", reviewMotor); Field(range, "开始 s", reviewStart); Field(range, "结束 s", reviewEnd);
            Button applyRange = Button("查看时间段"), all = Button("完整记录"); range.Controls.AddRange(new Control[] { applyRange, all });
            controls.Controls.Add(actions, 0, 0); controls.Controls.Add(range, 0, 1); controls.Controls.Add(historyRange, 0, 2); controls.Controls.Add(recordingStatus, 0, 3);
            recorder.Changed += delegate(string message) { recordingStatus.Text = message; UpdateRecordingButtons(); };
            recorder.Failed += delegate(Exception error) { recordingStatus.Text = "录制失败，已停止写入：" + error.Message; UpdateRecordingButtons(); };
            recordStart.Click += delegate { WithRecordingError(delegate { StartRecording(); }); };
            recordStop.Click += delegate { WithRecordingError(delegate { recorder.Stop(); UpdateRecordingButtons(); }); };
            saveData.Click += delegate
            {
                using (SaveFileDialog dialog = new SaveFileDialog { Filter = "诊断数据 CSV|*.csv", FileName = "diagnostics.csv" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) WithRecordingError(delegate { SaveRecordingCopy(dialog.FileName); recordingStatus.Text = "数据已导出：" + dialog.FileName; });
            };
            openData.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog { Filter = "诊断数据 CSV|*.csv", InitialDirectory = Directory.Exists(DiagnosticRecordingStore.DirectoryPath) ? DiagnosticRecordingStore.DirectoryPath : "" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) LoadHistoryInBackground(dialog.FileName);
            };
            reviewLatest.Click += delegate { WithRecordingError(delegate { recorder.Flush(); LoadHistoryInBackground(recorder.FilePath); }); };
            exportImage.Click += delegate
            {
                using (SaveFileDialog dialog = new SaveFileDialog { Filter = "曲线图片 PNG|*.png", FileName = "diagnostics-trends.png" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) WithRecordingError(delegate { ExportTrendImage(dialog.FileName); recordingStatus.Text = "曲线图片已导出：" + dialog.FileName; });
            };
            returnLive.Click += delegate { ReturnToLive(); };
            applyRange.Click += delegate { WithRecordingError(delegate { DrawHistory(); }); };
            all.Click += delegate { WithRecordingError(delegate { if (history != null) { SetFullHistoryRange(); DrawHistory(); } }); };
            historyRange.RangeChanged += delegate(object sender, EventArgs e)
            {
                if (!reviewing || loadingHistory) return;
                decimal start = (decimal)historyRange.StartMs / 1000M, end = (decimal)historyRange.EndMs / 1000M;
                if (end <= start) return;
                reviewStart.Value = Math.Max(reviewStart.Minimum, Math.Min(reviewStart.Maximum, start));
                reviewEnd.Value = Math.Max(reviewEnd.Minimum, Math.Min(reviewEnd.Maximum, end));
            };
            reviewMotor.SelectedIndexChanged += delegate { if (!loadingHistory && history != null) WithRecordingError(delegate { DrawHistory(); }); };
            Disposed += delegate { try { recorder.Dispose(); } catch { } };
            UpdateRecordingButtons(); return controls;
        }
        private void WithRecordingError(Action action)
        { try { action(); } catch (Exception error) { recordingStatus.Text = "操作失败：" + error.Message; } }
        public void StartRecording()
        {
            if (!latestFresh || latestFeedback == null) throw new InvalidOperationException("请先连接设备并取得有效反馈。");
            recorder.StartManual(latestFeedback, Simulation ? "simulation" : "hardware"); UpdateRecordingButtons();
        }
        public void StopRecording() { recorder.Stop(); UpdateRecordingButtons(); }
        public void SaveRecordingCopy(string path)
        {
            recorder.Flush(); string source = reviewing ? historyPath : recorder.FilePath;
            if (String.IsNullOrEmpty(source)) throw new InvalidOperationException("请先录制或打开一份诊断记录。");
            if (recorder.Active && String.Equals(Path.GetFullPath(path), Path.GetFullPath(recorder.FilePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择其他文件名，以保留正在录制的数据。");
            if (String.Equals(Path.GetFullPath(path), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase)) return;
            if (reviewing) DiagnosticRecordingStore.Save(path, history);
            else DiagnosticRecordingStore.CopySnapshot(source, path);
        }
        private void LoadHistoryInBackground(string path)
        {
            if (loadingHistory) return;
            recorder.Flush();
            string snapshot = null, readPath = path;
            if (recorder.Active && String.Equals(Path.GetFullPath(path), Path.GetFullPath(recorder.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                snapshot = Path.Combine(Path.GetTempPath(), "pmac-diagnostics-" + Guid.NewGuid().ToString("N") + ".csv");
                DiagnosticRecordingStore.CopySnapshot(path, snapshot); readPath = snapshot;
            }
            loadingHistory = true; recordingStatus.Text = "正在读取诊断记录…"; UpdateRecordingButtons();
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs args) { args.Result = DiagnosticRecordingStore.Load(readPath); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                worker.Dispose(); if (snapshot != null) { try { File.Delete(snapshot); } catch { } }
                if (IsDisposed || Disposing) return;
                loadingHistory = false;
                if (args.Error != null) recordingStatus.Text = "读取失败：" + args.Error.Message;
                else WithRecordingError(delegate { ShowHistory((DiagnosticRecording)args.Result, path); });
                UpdateRecordingButtons();
            };
            worker.RunWorkerAsync();
        }
        public void ReviewRecording(string path)
        { recorder.Flush(); ShowHistory(DiagnosticRecordingStore.Load(path), path); }
        private void ShowHistory(DiagnosticRecording recording, string path)
        {
            history = recording; historyPath = path; reviewing = true; loadingHistory = true;
            SetAnalysisVisible(true);
            reviewMotor.Items.Clear();
            foreach (DiagnosticSample sample in recording.Samples) if (!reviewMotor.Items.Contains(sample.Motor)) reviewMotor.Items.Add(sample.Motor);
            reviewMotor.SelectedItem = reviewMotor.Items.Contains(motor) ? motor : reviewMotor.Items[0];
            SetFullHistoryRange(); loadingHistory = false; DrawHistory();
            recordingStatus.Text = "回顾文件：" + path + (recorder.Active ? "（后台仍在录制实时数据）" : ""); UpdateRecordingButtons();
        }
        private void SetFullHistoryRange()
        {
            double end = 0; foreach (DiagnosticSample sample in history.Samples) end = Math.Max(end, sample.TimeMs);
            historyDurationMs = Math.Max(1, end);
            reviewStart.Value = 0; reviewEnd.Value = (decimal)Math.Max(0.001, Math.Ceiling(end) / 1000);
            historyRange.SetData(history.Samples, historyDurationMs);
            historyRange.SetRange(0, historyDurationMs);
        }
        private void SyncHistoryRangeSelector()
        {
            if (!reviewing || loadingHistory || historyDurationMs <= 0) return;
            historyRange.SetRange((double)reviewStart.Value * 1000, (double)reviewEnd.Value * 1000);
        }
        private void DrawHistory()
        {
            historyRangeTimer.Stop();
            if (history == null || reviewMotor.SelectedItem == null) return;
            if (reviewEnd.Value <= reviewStart.Value)
            {
                ClearTrends(); InvalidateHistoryAnalysis("时间范围无效：结束时间需要大于开始时间。");
                throw new InvalidOperationException("结束时间需要大于开始时间。");
            }
            int selected = (int)reviewMotor.SelectedItem;
            foreach (AxisTrendPlot plot in new AxisTrendPlot[] { positionTrend, velocityTrend, trend })
            {
                plot.History = true; plot.Stale = false; plot.RangeStartMs = (double)reviewStart.Value * 1000; plot.RangeEndMs = (double)reviewEnd.Value * 1000; plot.Clear();
            }
            int count = 0;
            foreach (DiagnosticSample sample in history.Samples)
            {
                if (sample.Motor != selected) continue;
                AxisSample axis = sample.ToAxisSample();
                positionTrend.Append(axis, sample.GapBefore); velocityTrend.Append(axis, sample.GapBefore); trend.Append(axis, sample.GapBefore);
                if (sample.TimeMs >= (double)reviewStart.Value * 1000 && sample.TimeMs <= (double)reviewEnd.Value * 1000) count++;
            }
            positionTrend.Invalidate(); velocityTrend.Invalidate(); trend.Invalidate();
            summary.Text = "历史回顾：电机 " + selected + "（实时监控与录制继续运行）\r\n" +
                "来源：" + (history.Source == "simulation" ? "模拟数据" : "实机通信采样") + "；开始时间：" + history.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") +
                "\r\n当前时间段 " + reviewStart.Value + "～" + reviewEnd.Value + " s；该轴 " + count + " 个样本\r\n位置 ct；速度 ct/s（按原始时间戳差分估算）";
            recordingStatus.Text = "回顾文件：" + historyPath + (recorder.Active ? "（后台仍在录制实时数据）" : "");
            RequestHistoryAnalysis(); UpdateRecordingButtons();
        }
        public void ReturnToLive()
        {
            reviewing = false; history = null; historyPath = null;
            historyRangeTimer.Stop(); InvalidateHistoryAnalysis(""); SetAnalysisVisible(false);
            historyRange.ClearData();
            positionTrend.History = velocityTrend.History = trend.History = false; ClearTrends();
            UpdateRecordingButtons(); summary.Text = "等待新的实时反馈";
        }
        public void ExportTrendImage(string path)
        {
            if (reviewing && (historyAnalysis == null || historyRangeTimer.Enabled)) throw new InvalidOperationException("请等待当前时间段的偏差分析完成后再导出。");
            int analysisHeight = reviewing ? 125 : 0;
            using (Bitmap image = new Bitmap(1440, 1080 + analysisHeight))
            using (Graphics graphics = Graphics.FromImage(image))
            using (Font exportFont = new Font("Microsoft YaHei UI", 10))
            {
                graphics.Clear(Color.White);
                string title = reviewing ? summary.Text : summary.Text + "\r\n来源：" + (Simulation ? "模拟数据" : "实机通信采样");
                graphics.DrawString(title, exportFont, Brushes.Black, new RectangleF(16, 8, 1408, 110));
                if (reviewing) graphics.DrawString(AnalysisExportText(), exportFont, Brushes.Black, new RectangleF(16, 120, 1408, analysisHeight));
                int top = 120 + analysisHeight;
                foreach (AxisTrendPlot plot in new AxisTrendPlot[] { positionTrend, velocityTrend, trend })
                {
                    using (Bitmap chart = plot.RenderImage(1440, 300)) graphics.DrawImageUnscaled(chart, 0, top);
                    top += 300;
                }
                graphics.DrawString("指令位置 = 实际位置 + 跟随误差（估算）。速度按相邻通信样本差分估算；非控制器内部同步采集。", exportFont, Brushes.Black, 16, 1040 + analysisHeight);
                image.Save(path, ImageFormat.Png);
            }
        }
        private void UpdateRecordingButtons()
        {
            recordStart.Enabled = latestFresh && !recorder.Active; recordStop.Enabled = recorder.Active;
            saveData.Enabled = !loadingHistory && (reviewing || !String.IsNullOrEmpty(recorder.FilePath));
            openData.Enabled = !loadingHistory; reviewLatest.Enabled = !loadingHistory && !String.IsNullOrEmpty(recorder.FilePath);
            returnLive.Enabled = reviewing; reviewMotor.Enabled = reviewStart.Enabled = reviewEnd.Enabled = reviewing && !loadingHistory;
            historyRange.Enabled = reviewing && !loadingHistory;
            analysisTolerance.Enabled = reviewing && !loadingHistory;
            exportImage.Enabled = !loadingHistory && (!reviewing || (historyAnalysis != null && !historyRangeTimer.Enabled));
        }

        private sealed class HistoryRangeSelector : Control
        {
            private IList<DiagnosticSample> samples = new List<DiagnosticSample>();
            private double durationMs = 1, startMs, endMs = 1;
            private int dragMode;
            private double dragOffsetMs;
            public event EventHandler RangeChanged;
            public double StartMs { get { return startMs; } }
            public double EndMs { get { return endMs; } }

            public HistoryRangeSelector()
            {
                DoubleBuffered = true; BackColor = Color.White; TabStop = true;
                AccessibleName = "历史记录时间范围选择器";
                AccessibleDescription = "拖动左右边界调整回顾时间，拖动选区中间可平移整个时间窗口";
                SetStyle(ControlStyles.ResizeRedraw, true);
            }
            public void SetData(IList<DiagnosticSample> value, double duration)
            {
                samples = value ?? new List<DiagnosticSample>(); durationMs = Math.Max(1, duration);
                startMs = Clamp(startMs, 0, durationMs); endMs = Clamp(endMs, startMs + Math.Max(1, durationMs / 1000), durationMs);
                Invalidate();
            }
            public void SetRange(double start, double end)
            {
                startMs = Clamp(start, 0, durationMs); endMs = Clamp(end, startMs + Math.Max(1, durationMs / 1000), durationMs);
                if (endMs <= startMs) { startMs = 0; endMs = durationMs; }
                Invalidate();
            }
            public void ClearData()
            { samples = new List<DiagnosticSample>(); durationMs = 1; startMs = 0; endMs = 1; Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Rectangle bar = new Rectangle(12, 24, Math.Max(1, Width - 24), 16);
                using (SolidBrush background = new SolidBrush(Color.FromArgb(232, 236, 241))) e.Graphics.FillRectangle(background, bar);
                using (Pen border = new Pen(Color.Silver)) e.Graphics.DrawRectangle(border, bar);
                if (samples.Count > 0)
                {
                    using (Pen density = new Pen(Color.FromArgb(155, 183, 207), 2))
                    {
                        bool[] occupied = new bool[Math.Max(1, bar.Width)];
                        for (int i = 0; i < samples.Count; i++)
                        {
                            int x = (int)ToX(samples[i].TimeMs, bar) - bar.Left;
                            if (x >= 0 && x < occupied.Length) occupied[x] = true;
                        }
                        for (int x = 0; x < occupied.Length; x++) if (occupied[x]) e.Graphics.DrawLine(density, bar.Left + x, bar.Top + 3, bar.Left + x, bar.Bottom - 3);
                    }
                }
                Rectangle selected = new Rectangle((int)ToX(startMs, bar), bar.Top - 3, Math.Max(4, (int)(ToX(endMs, bar) - ToX(startMs, bar))), bar.Height + 6);
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(70, 55, 125, 185))) e.Graphics.FillRectangle(fill, selected);
                using (Pen outline = new Pen(Color.SteelBlue, 2)) e.Graphics.DrawRectangle(outline, selected);
                using (SolidBrush handle = new SolidBrush(Color.SteelBlue))
                { e.Graphics.FillRectangle(handle, selected.Left - 3, selected.Top, 6, selected.Height); e.Graphics.FillRectangle(handle, selected.Right - 3, selected.Top, 6, selected.Height); }
                e.Graphics.DrawString("整段记录（拖动边界调整，拖动选区平移）", Font, Brushes.Black, 12, 4);
                using (StringFormat right = new StringFormat { Alignment = StringAlignment.Far })
                    e.Graphics.DrawString((durationMs / 1000).ToString("0.###") + " s", Font, Brushes.Gray, new RectangleF(12, 4, Width - 24, Font.Height), right);
            }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
                Rectangle bar = Bar(); float left = ToX(startMs, bar), right = ToX(endMs, bar);
                if (Math.Abs(e.X - left) <= 9) dragMode = 1;
                else if (Math.Abs(e.X - right) <= 9) dragMode = 2;
                else if (e.X > left && e.X < right) { dragMode = 3; dragOffsetMs = FromX(e.X, bar) - startMs; }
                else return;
                Focus(); Capture = true;
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (dragMode == 0)
                {
                    Rectangle hoverBar = Bar(); float hoverLeft = ToX(startMs, hoverBar), hoverRight = ToX(endMs, hoverBar);
                    Cursor = Math.Abs(e.X - hoverLeft) <= 9 || Math.Abs(e.X - hoverRight) <= 9 ? Cursors.SizeWE : e.X > hoverLeft && e.X < hoverRight ? Cursors.Hand : Cursors.Default;
                    return;
                }
                Rectangle bar = Bar(); double time = FromX(e.X, bar), minimum = Math.Max(1, durationMs / 1000);
                if (dragMode == 1) startMs = Clamp(time, 0, endMs - minimum);
                else if (dragMode == 2) endMs = Clamp(time, startMs + minimum, durationMs);
                else
                {
                    double length = endMs - startMs, nextStart = Clamp(time - dragOffsetMs, 0, durationMs - length);
                    startMs = nextStart; endMs = nextStart + length;
                }
                Invalidate(); if (RangeChanged != null) RangeChanged(this, EventArgs.Empty);
            }
            protected override void OnMouseUp(MouseEventArgs e)
            { base.OnMouseUp(e); dragMode = 0; Capture = false; }
            private Rectangle Bar() { return new Rectangle(12, 24, Math.Max(1, Width - 24), 16); }
            private float ToX(double time, Rectangle bar) { return bar.Left + (float)(Clamp(time, 0, durationMs) / durationMs * bar.Width); }
            private double FromX(int x, Rectangle bar) { return Clamp((x - bar.Left) * durationMs / Math.Max(1, bar.Width), 0, durationMs); }
            private static double Clamp(double value, double minimum, double maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
        }
    }
}
