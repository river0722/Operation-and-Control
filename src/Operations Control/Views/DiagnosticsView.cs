using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Operations_Control.Models;
using Operations_Control.Application;

namespace Operations_Control.Views
{
    public sealed partial class DiagnosticsView : UserControl
    {
        public readonly Button SaveButton = Button("保存设备配置"), LoadButton = Button("载入设备配置"), ApplyButton = Button("应用所选轴参数");
        public readonly Button ReadSpeedButton = Button("回读所选轴速度"), ReadIoButton = Button("读取 I/O"), AcknowledgeButton = Button("确认当前报警");
        public readonly NumericUpDown Minimum = Number(-100000000, 100000000, -1000), Maximum = Number(-100000000, 100000000, 1000), MaximumSpeed = Number(0.001M, AxisConfiguration.MaximumSupportedSpeedCtPerMs, 1);
        public readonly TextBox ResetCommand = new TextBox { Width = 220 };
        public readonly NumericUpDown PositionTolerance = Number(0.001M, 1000000, 0.5M);
        public readonly NumericUpDown MaximumAcceleration = Number(0.000001M, decimal.MaxValue, 0.01M);
        public readonly DataGridView IoGrid = Grid(), AlarmGrid = Grid();
        public readonly Label ParameterResult = new Label { AutoSize = true, Text = "参数尚未回读", Margin = new Padding(8) };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, Text = "等待轴反馈", AutoEllipsis = true, Padding = new Padding(10) };
        private enum TrendKind { Position, Velocity, Error }
        private readonly AxisTrendPlot positionTrend = new AxisTrendPlot(TrendKind.Position) { Dock = DockStyle.Fill };
        private readonly AxisTrendPlot velocityTrend = new AxisTrendPlot(TrendKind.Velocity) { Dock = DockStyle.Fill };
        private readonly AxisTrendPlot trend = new AxisTrendPlot(TrendKind.Error) { Dock = DockStyle.Fill };
        private int motor;
        private double lastTime;
        private bool gapBeforeNextSample;
        public DiagnosticsView()
        {
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage axis = new TabPage("轴反馈与趋势"), alarms = new TabPage("当前与历史报警"), config = new TabPage("设备参数与 I/O");
            tabs.TabPages.AddRange(new TabPage[] { axis, alarms, config }); Controls.Add(tabs);
            axisViewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            axisLayout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 850, ColumnCount = 1, RowCount = 7 };
            axisViewport.SizeChanged += delegate { axisLayout.Height = Math.Max(reviewing ? 1150 : 890, axisViewport.ClientSize.Height); };
            axisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            axisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            axisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            axisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            axisLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35)); axisLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35)); axisLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            Label positionNote = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(10, 2, 0, 0),
                Text = "指令位置 = 实际位置 + 跟随误差（估算）；开环时不绘制指令曲线。\r\n速度由相邻位置样本按实际时间差估算（ct/s）；通信查询非同步，仅供趋势对比。" };
            axisLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); axisLayout.Controls.Add(summary, 0, 0);
            axisLayout.Controls.Add(CreateRecordingControls(), 0, 1); axisLayout.Controls.Add(CreateAnalysisPanel(), 0, 2); axisLayout.Controls.Add(positionNote, 0, 3);
            axisLayout.Controls.Add(positionTrend, 0, 4); axisLayout.Controls.Add(velocityTrend, 0, 5); axisLayout.Controls.Add(trend, 0, 6);
            axisViewport.Controls.Add(axisLayout); axis.Controls.Add(axisViewport);
            AlarmGrid.Columns.Add("state", "状态"); AlarmGrid.Columns.Add("source", "来源"); AlarmGrid.Columns.Add("time", "发生时间"); AlarmGrid.Columns.Add("message", "原因"); AlarmGrid.Columns.Add("advice", "处理建议");
            AlarmGrid.ReadOnly = true; AlarmGrid.Dock = DockStyle.Fill;
            AlarmGrid.AllowUserToAddRows = AlarmGrid.AllowUserToDeleteRows = false;
            AlarmGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            AlarmGrid.Columns[0].FillWeight = 70; AlarmGrid.Columns[1].FillWeight = 50; AlarmGrid.Columns[2].FillWeight = 100; AlarmGrid.Columns[3].FillWeight = 160; AlarmGrid.Columns[4].FillWeight = 200;
            FlowLayoutPanel alarmActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 }; alarmActions.Controls.Add(AcknowledgeButton);
            alarms.Controls.Add(AlarmGrid); alarms.Controls.Add(alarmActions);
            TableLayoutPanel settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(6) };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new int[] { 42, 82, 42, 45 }) settings.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel files = new FlowLayoutPanel { Dock = DockStyle.Fill }; files.Controls.AddRange(new Control[] { SaveButton, LoadButton, ReadSpeedButton }); settings.Controls.Add(files, 0, 0);
            FlowLayoutPanel parameters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            MaximumAcceleration.DecimalPlaces = 6; MaximumAcceleration.Increment = 0.001M;
            Field(parameters, "所选轴下限 ct", Minimum); Field(parameters, "上限 ct", Maximum); Field(parameters, "最大速度 ct/ms", MaximumSpeed);
            Field(parameters, "最大加速度 ct/ms²", MaximumAcceleration);
            Field(parameters, "到位/稳定容差 ct", PositionTolerance); Field(parameters, "复位命令", ResetCommand); parameters.Controls.Add(ApplyButton); settings.Controls.Add(parameters, 0, 1);
            settings.Controls.Add(ParameterResult, 0, 2);
            FlowLayoutPanel ioActions = new FlowLayoutPanel { Dock = DockStyle.Fill };
            ioActions.Controls.Add(ReadIoButton); ioActions.Controls.Add(new Label { AutoSize = true, Text = "I/O 按实际接线配置 M/P 查询变量；空行不采集。复位命令可用 {motor}。", Margin = new Padding(4, 8, 2, 2) }); settings.Controls.Add(ioActions, 0, 3);
            IoGrid.Columns.Add("name", "信号名称"); IoGrid.Columns.Add("query", "读取变量，如 M100");
            IoGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "low", HeaderText = "低电平有效" });
            IoGrid.Columns.Add("value", "采集结果"); IoGrid.Columns[3].ReadOnly = true; settings.Controls.Add(IoGrid, 0, 4); config.Controls.Add(settings);
        }
        private static Button Button(string text) { return new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(4) }; }
        private static NumericUpDown Number(decimal min, decimal max, decimal value) { return new NumericUpDown { Minimum = min, Maximum = max, Value = value, DecimalPlaces = 3, Width = 100 }; }
        private static DataGridView Grid()
        { return new DataGridView { Dock = DockStyle.Fill, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, BackgroundColor = SystemColors.Window, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells }; }
        private static void Field(FlowLayoutPanel panel, string label, Control input)
        { panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }); panel.Controls.Add(input); }
        public void ShowProfile(DeviceConfiguration configuration, int selected)
        {
            ShowAxisParameters(configuration, selected);
            IoGrid.Rows.Clear(); foreach (IoConfiguration input in configuration.Inputs) IoGrid.Rows.Add(input.Name, input.Query, input.ActiveLow, "未读取");
        }
        public void ShowAxisParameters(DeviceConfiguration configuration, int selected)
        {
            motor = selected; if (!reviewing) ClearTrends(); AxisConfiguration axis = configuration.Axis(selected);
            Minimum.Value = (decimal)axis.MinimumCounts; Maximum.Value = (decimal)axis.MaximumCounts; MaximumSpeed.Value = axis.MaximumSpeed; PositionTolerance.Value = (decimal)axis.PositionToleranceCounts; ResetCommand.Text = axis.ResetCommand;
            MaximumAcceleration.Value = axis.MaximumAcceleration;
        }
        public void ApplyProfile(DeviceConfiguration configuration)
        {
            DeviceConfiguration candidate = new DeviceConfiguration();
            AxisConfiguration axis = new AxisConfiguration { Motor = motor, TravelConfigured = true, MinimumCounts = (double)Minimum.Value, MaximumCounts = (double)Maximum.Value, MaximumSpeed = MaximumSpeed.Value, MaximumAcceleration = MaximumAcceleration.Value, PositionToleranceCounts = (double)PositionTolerance.Value, ResetCommand = ResetCommand.Text.Trim() };
            candidate.Axes.Add(axis); candidate.Inputs = ReadInputs(); candidate.Validate();
            configuration.Axes.RemoveAll(delegate(AxisConfiguration item) { return item.Motor == motor; }); configuration.Axes.Add(axis); configuration.Inputs = candidate.Inputs;
        }
        public List<IoConfiguration> ReadInputs()
        {
            IoGrid.EndEdit(); List<IoConfiguration> inputs = new List<IoConfiguration>();
            foreach (DataGridViewRow row in IoGrid.Rows)
            {
                string query = Convert.ToString(row.Cells[1].Value).Trim(); if (row.IsNewRow || query.Length == 0) continue;
                inputs.Add(new IoConfiguration { Name = Convert.ToString(row.Cells[0].Value), Query = query, ActiveLow = row.Cells[2].Value != null && Convert.ToBoolean(row.Cells[2].Value) });
            }
            DeviceConfiguration check = new DeviceConfiguration(); check.Inputs = inputs; check.Validate(); return inputs;
        }
        public void ShowIo(List<string> values)
        { int index = 0; foreach (DataGridViewRow row in IoGrid.Rows) if (!row.IsNewRow && !String.IsNullOrEmpty(Convert.ToString(row.Cells[1].Value))) row.Cells[3].Value = index < values.Count ? values[index++] : "未知"; }
        public void Display(ControllerFeedback feedback, bool fresh, int selected, bool busy)
        {
            latestFeedback = feedback; latestFresh = fresh && feedback != null;
            recorder.Observe(feedback, fresh, Simulation ? "simulation" : "hardware");
            UpdateRecordingButtons();
            if (motor != selected) { motor = selected; if (!reviewing) ClearTrends(); }
            AxisSample sample = feedback == null ? null : feedback.Find(selected);
            if (!reviewing) summary.Text = "操作对象：电机 " + selected + "\r\n" + (!fresh || sample == null || sample.Status == null ? "反馈过期／未知；禁止运动" :
                sample.Status + "\r\n实际位置 " + sample.Position.ToString("0.###") + " ct；估算指令位置 " + (sample.Status.OpenLoop ? "开环无有效指令位置" : sample.CommandPosition.HasValue ? sample.CommandPosition.Value.ToString("0.###") + " ct" : "未知") +
                "；跟随误差 " + (sample.FollowingError.HasValue ? sample.FollowingError.Value.ToString("0.###") + " ct" : "未知") + "\r\n状态字 " + sample.Status.FirstWord.ToString("X6") + sample.Status.SecondWord.ToString("X6"));
            bool valid = fresh && sample != null;
            if (!reviewing && valid && sample.TimeMs > lastTime)
            {
                lastTime = sample.TimeMs; positionTrend.Append(sample, gapBeforeNextSample); velocityTrend.Append(sample, gapBeforeNextSample); trend.Append(sample, gapBeforeNextSample);
                gapBeforeNextSample = false;
            }
            if (!reviewing)
            {
                if (!valid) gapBeforeNextSample = true;
                positionTrend.Stale = velocityTrend.Stale = trend.Stale = !valid; positionTrend.Invalidate(); velocityTrend.Invalidate(); trend.Invalidate();
            }
            SaveButton.Enabled = LoadButton.Enabled = ApplyButton.Enabled = !busy;
            Minimum.Enabled = Maximum.Enabled = MaximumSpeed.Enabled = PositionTolerance.Enabled = ResetCommand.Enabled = !busy;
            MaximumAcceleration.Enabled = !busy;
            ReadSpeedButton.Enabled = ReadIoButton.Enabled = fresh && !busy; IoGrid.ReadOnly = busy;
        }
        private void ClearTrends()
        { positionTrend.Clear(); velocityTrend.Clear(); trend.Clear(); lastTime = 0; gapBeforeNextSample = false; }
        public void ShowAlarms(AlarmService alarms)
        {
            AlarmGrid.Rows.Clear();
            for (int i = alarms.History.Count - 1; i >= 0; i--)
            {
                AlarmRecord item = alarms.History[i];
                int row = AlarmGrid.Rows.Add(item.Active ? item.Acknowledged ? "当前／已确认" : "当前／未确认" : "已恢复 " + item.Recovered.ToString("HH:mm:ss"), item.Source, item.Occurred.ToString("yyyy-MM-dd HH:mm:ss"), item.Message, item.Advice);
                if (item.Active) AlarmGrid.Rows[row].DefaultCellStyle.ForeColor = Color.Firebrick;
            }
        }
        public void SelectAlarms() { ((TabControl)Controls[0]).SelectedIndex = 1; }
        private sealed class AxisTrendPlot : Control
        {
            private struct TrendSample
            {
                public double TimeMs, Position;
                public double? Command, Error, Velocity, CommandVelocity;
                public bool GapBefore;
            }
            private readonly List<TrendSample> values = new List<TrendSample>();
            private readonly TrendKind kind;
            private bool errorOnly { get { return kind == TrendKind.Error; } }
            public bool Stale;
            public bool History;
            public double RangeStartMs, RangeEndMs;
            public AxisTrendPlot(TrendKind kind)
            {
                this.kind = kind; DoubleBuffered = true; BackColor = Color.White;
                AccessibleName = errorOnly ? "跟随误差趋势" : kind == TrendKind.Velocity ? "实际速度与估算指令速度趋势" : "估算指令位置与实际位置趋势";
                SetStyle(ControlStyles.ResizeRedraw, true);
            }
            public void Clear() { values.Clear(); Invalidate(); }
            public void Append(AxisSample sample, bool gapBefore)
            {
                TrendSample next = new TrendSample { TimeMs = sample.TimeMs, Position = sample.Position, Error = sample.FollowingError,
                    Command = sample.Status != null && !sample.Status.OpenLoop ? sample.CommandPosition : null, GapBefore = gapBefore };
                if (values.Count > 0)
                {
                    TrendSample previous = values[values.Count - 1]; double dt = next.TimeMs - previous.TimeMs;
                    next.GapBefore |= dt > 1000 || dt <= 0;
                    if (!next.GapBefore)
                    {
                        next.Velocity = (next.Position - previous.Position) * 1000 / dt;
                        if (next.Command.HasValue && previous.Command.HasValue) next.CommandVelocity = (next.Command.Value - previous.Command.Value) * 1000 / dt;
                    }
                }
                values.Add(next);
                if (!History && values.Count > 300) values.RemoveAt(0);
                if (!History) Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                int lineHeight = Font.Height + 5;
                e.Graphics.DrawString((errorOnly ? "跟随误差 / ct" : kind == TrendKind.Velocity ? "速度–时间 v–t / (ct/s) · 差分估算" : "指令与实际位置 / ct") + (History ? " · 历史数据" : " · 最近 300 个通信样本") + (Stale ? " · 数据过期" : ""), Font, Brushes.Black, 12, 6);
                if (!errorOnly)
                {
                    Legend(e.Graphics, Color.SteelBlue, false, 12, lineHeight + 6, kind == TrendKind.Velocity ? "实际速度（估算）" : "实际位置");
                    Legend(e.Graphics, Color.DarkOrange, true, 200, lineHeight + 6, kind == TrendKind.Velocity ? "指令速度（估算）" : "估算指令位置");
                }
                int top = 12 + lineHeight * (errorOnly ? 1 : 2);
                Rectangle area = new Rectangle(90, top, Math.Max(1, Width - 112), Math.Max(1, Height - top - lineHeight - 12));
                if (Width < 150 || Height < top + lineHeight + 30) return;
                e.Graphics.DrawRectangle(Pens.LightGray, area);
                if (values.Count == 0) { e.Graphics.DrawString("等待新的轴反馈", Font, Brushes.Gray, area.Left + 8, area.Top + 8); return; }
                double minimum = Double.PositiveInfinity, maximum = Double.NegativeInfinity;
                double start = History ? RangeStartMs : values[0].TimeMs, end = History ? RangeEndMs : values[values.Count - 1].TimeMs;
                foreach (TrendSample sample in values)
                {
                    if (sample.TimeMs < start || sample.TimeMs > end) continue;
                    double? first = Value(sample, false);
                    Include(first, ref minimum, ref maximum);
                    if (!errorOnly) Include(Value(sample, true), ref minimum, ref maximum);
                }
                for (int i = 1; i < values.Count; i++)
                {
                    IncludeBoundary(values[i - 1], values[i], start, ref minimum, ref maximum);
                    IncludeBoundary(values[i - 1], values[i], end, ref minimum, ref maximum);
                }
                if (Double.IsInfinity(minimum)) { minimum = -1; maximum = 1; }
                if (errorOnly) { maximum = Math.Max(1, Math.Max(Math.Abs(minimum), Math.Abs(maximum))); minimum = -maximum; }
                else { if (kind == TrendKind.Velocity) { minimum = Math.Min(0, minimum); maximum = Math.Max(0, maximum); } double padding = Math.Max(1, (maximum - minimum) * 0.05); minimum -= padding; maximum += padding; }
                using (StringFormat right = new StringFormat { Alignment = StringAlignment.Far })
                {
                    for (int tick = 0; tick <= 2; tick++)
                    {
                        float y = area.Top + area.Height * tick / 2f;
                        double value = maximum - (maximum - minimum) * tick / 2;
                        e.Graphics.DrawString(value.ToString("0.###"), Font, Brushes.Gray, new RectangleF(0, y - Font.Height / 2f, area.Left - 6, Font.Height + 2), right);
                        if (tick == 1) e.Graphics.DrawLine(Pens.LightGray, area.Left, y, area.Right, y);
                    }
                    e.Graphics.DrawString(((History ? end : end - start) / 1000).ToString("0.###") + " s", Font, Brushes.Gray,
                        new RectangleF(area.Left, area.Bottom + 4, area.Width, lineHeight), right);
                }
                e.Graphics.DrawString((History ? start / 1000 : 0).ToString("0.###") + " s", Font, Brushes.Gray, area.Left, area.Bottom + 4);
                if (values.Count < 2) return;
                double duration = Math.Max(1, end - start);
                using (Pen actual = new Pen(Stale ? Color.Gray : Color.SteelBlue, 2))
                using (Pen command = new Pen(Stale ? Color.DimGray : Color.DarkOrange, 2))
                {
                    command.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    for (int i = 1; i < values.Count; i++)
                    {
                        if (values[i].GapBefore || values[i].TimeMs < start || values[i - 1].TimeMs > end) continue;
                        DrawSegment(e.Graphics, actual, area, values[i - 1], values[i], false, minimum, maximum, duration, start);
                        if (!errorOnly) DrawSegment(e.Graphics, command, area, values[i - 1], values[i], true, minimum, maximum, duration, start);
                    }
                }
            }
            private static void Include(double? value, ref double minimum, ref double maximum)
            { if (value.HasValue) { minimum = Math.Min(minimum, value.Value); maximum = Math.Max(maximum, value.Value); } }
            private double? Value(TrendSample sample, bool command)
            { return kind == TrendKind.Velocity ? command ? sample.CommandVelocity : sample.Velocity : command ? sample.Command : errorOnly ? sample.Error : (double?)sample.Position; }
            private void IncludeBoundary(TrendSample a, TrendSample b, double time, ref double minimum, ref double maximum)
            {
                if (b.GapBefore || a.TimeMs >= time || b.TimeMs <= time) return;
                double fraction = (time - a.TimeMs) / (b.TimeMs - a.TimeMs);
                for (int series = 0; series < (errorOnly ? 1 : 2); series++)
                {
                    double? first = Value(a, series == 1), last = Value(b, series == 1);
                    if (first.HasValue && last.HasValue) Include((1 - fraction) * first.Value + fraction * last.Value, ref minimum, ref maximum);
                }
            }
            private void Legend(Graphics graphics, Color color, bool dashed, int x, int y, string label)
            {
                using (Pen pen = new Pen(color, 2))
                { if (dashed) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; graphics.DrawLine(pen, x, y + Font.Height / 2, x + 28, y + Font.Height / 2); }
                graphics.DrawString(label, Font, Brushes.Black, x + 34, y);
            }
            private void DrawSegment(Graphics graphics, Pen pen, Rectangle area, TrendSample a, TrendSample b, bool command, double minimum, double maximum, double duration, double start)
            {
                double? first = Value(a, command), second = Value(b, command);
                if (!first.HasValue || !second.HasValue || b.TimeMs <= a.TimeMs) return;
                double left = Math.Max(start, a.TimeMs), right = Math.Min(start + duration, b.TimeMs);
                if (right < left) return;
                double leftFraction = (left - a.TimeMs) / (b.TimeMs - a.TimeMs), rightFraction = (right - a.TimeMs) / (b.TimeMs - a.TimeMs);
                double leftValue = (1 - leftFraction) * first.Value + leftFraction * second.Value, rightValue = (1 - rightFraction) * first.Value + rightFraction * second.Value;
                graphics.DrawLine(pen,
                    area.Left + (float)((left - start) / duration * area.Width), area.Bottom - (float)((leftValue - minimum) / (maximum - minimum) * area.Height),
                    area.Left + (float)((right - start) / duration * area.Width), area.Bottom - (float)((rightValue - minimum) / (maximum - minimum) * area.Height));
            }
            public Bitmap RenderImage(int width, int height)
            {
                using (AxisTrendPlot copy = new AxisTrendPlot(kind) { Size = new Size(width, height), Font = Font, History = History, Stale = Stale, RangeStartMs = RangeStartMs, RangeEndMs = RangeEndMs })
                {
                    copy.values.AddRange(values); Bitmap image = new Bitmap(width, height);
                    copy.DrawToBitmap(image, new Rectangle(0, 0, width, height)); return image;
                }
            }
        }
    }
}
