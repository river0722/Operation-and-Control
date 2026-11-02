using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Operations_Control.Models;
using Operations_Control.Application;

namespace Operations_Control.Views
{
    public sealed class DiagnosticsView : UserControl
    {
        public readonly Button SaveButton = Button("保存设备配置"), LoadButton = Button("载入设备配置"), ApplyButton = Button("应用所选轴参数");
        public readonly Button ReadSpeedButton = Button("回读所选轴速度"), ReadIoButton = Button("读取 I/O"), AcknowledgeButton = Button("确认当前报警");
        public readonly NumericUpDown Minimum = Number(-100000000, 100000000, -1000), Maximum = Number(-100000000, 100000000, 1000), MaximumSpeed = Number(0.001M, AxisConfiguration.MaximumSupportedSpeedCtPerMs, 1);
        public readonly TextBox ResetCommand = new TextBox { Width = 220 };
        public readonly NumericUpDown PositionTolerance = Number(0.001M, 1000000, 0.5M);
        public readonly DataGridView IoGrid = Grid(), AlarmGrid = Grid();
        public readonly Label ParameterResult = new Label { AutoSize = true, Text = "参数尚未回读", Margin = new Padding(8) };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, Text = "等待轴反馈", AutoEllipsis = true, Padding = new Padding(10) };
        private readonly FollowingErrorPlot trend = new FollowingErrorPlot { Dock = DockStyle.Fill };
        private int motor;
        private double lastTime;
        public DiagnosticsView()
        {
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage axis = new TabPage("轴反馈与趋势"), alarms = new TabPage("当前与历史报警"), config = new TabPage("设备参数与 I/O");
            tabs.TabPages.AddRange(new TabPage[] { axis, alarms, config }); Controls.Add(tabs);
            TableLayoutPanel axisLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            axisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); axisLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            axisLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); axisLayout.Controls.Add(summary, 0, 0); axisLayout.Controls.Add(trend, 0, 1); axis.Controls.Add(axisLayout);
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
            Field(parameters, "所选轴下限 ct", Minimum); Field(parameters, "上限 ct", Maximum); Field(parameters, "最大速度 ct/ms", MaximumSpeed);
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
            motor = selected; trend.Clear(); lastTime = 0; AxisConfiguration axis = configuration.Axis(selected);
            Minimum.Value = (decimal)axis.MinimumCounts; Maximum.Value = (decimal)axis.MaximumCounts; MaximumSpeed.Value = axis.MaximumSpeed; PositionTolerance.Value = (decimal)axis.PositionToleranceCounts; ResetCommand.Text = axis.ResetCommand;
        }
        public void ApplyProfile(DeviceConfiguration configuration)
        {
            DeviceConfiguration candidate = new DeviceConfiguration();
            AxisConfiguration axis = new AxisConfiguration { Motor = motor, TravelConfigured = true, MinimumCounts = (double)Minimum.Value, MaximumCounts = (double)Maximum.Value, MaximumSpeed = MaximumSpeed.Value, PositionToleranceCounts = (double)PositionTolerance.Value, ResetCommand = ResetCommand.Text.Trim() };
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
            AxisSample sample = feedback == null ? null : feedback.Find(selected);
            summary.Text = "操作对象：电机 " + selected + "\r\n" + (!fresh || sample == null || sample.Status == null ? "反馈过期／未知；禁止运动" :
                sample.Status + "\r\n实际位置 " + sample.Position.ToString("0.###") + " ct；估算指令位置 " + (sample.Status.OpenLoop ? "开环无有效指令位置" : sample.CommandPosition.HasValue ? sample.CommandPosition.Value.ToString("0.###") + " ct" : "未知") +
                "；跟随误差 " + (sample.FollowingError.HasValue ? sample.FollowingError.Value.ToString("0.###") + " ct" : "未知") + "\r\n状态字 " + sample.Status.FirstWord.ToString("X6") + sample.Status.SecondWord.ToString("X6"));
            if (fresh && sample != null && sample.FollowingError.HasValue && sample.TimeMs > lastTime) { lastTime = sample.TimeMs; trend.Append(sample.FollowingError.Value); }
            trend.Stale = !fresh; trend.Invalidate();
            SaveButton.Enabled = LoadButton.Enabled = ApplyButton.Enabled = !busy;
            Minimum.Enabled = Maximum.Enabled = MaximumSpeed.Enabled = PositionTolerance.Enabled = ResetCommand.Enabled = !busy;
            ReadSpeedButton.Enabled = ReadIoButton.Enabled = fresh && !busy; IoGrid.ReadOnly = busy;
        }
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
        private sealed class FollowingErrorPlot : Control
        {
            private readonly List<double> values = new List<double>();
            public bool Stale;
            public FollowingErrorPlot() { DoubleBuffered = true; BackColor = Color.White; }
            public void Clear() { values.Clear(); Invalidate(); }
            public void Append(double value) { values.Add(value); if (values.Count > 300) values.RemoveAt(0); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e); e.Graphics.DrawString("跟随误差 / ct · 最近 300 个通信样本" + (Stale ? " · 数据过期" : ""), Font, Brushes.Black, 12, 8);
                Rectangle area = new Rectangle(55, 40, Math.Max(1, Width - 80), Math.Max(1, Height - 75));
                e.Graphics.DrawRectangle(Pens.LightGray, area); if (values.Count < 2) return;
                double scale = 1; foreach (double value in values) scale = Math.Max(scale, Math.Abs(value));
                e.Graphics.DrawString(scale.ToString("0.###"), Font, Brushes.Gray, 3, area.Top);
                e.Graphics.DrawString((-scale).ToString("0.###"), Font, Brushes.Gray, 3, area.Bottom - 16);
                e.Graphics.DrawLine(Pens.LightGray, area.Left, area.Top + area.Height / 2, area.Right, area.Top + area.Height / 2);
                PointF[] points = new PointF[values.Count];
                for (int i = 0; i < points.Length; i++) points[i] = new PointF(area.Left + area.Width * i / (float)(points.Length - 1), area.Top + area.Height / 2f - (float)(values[i] / scale) * area.Height / 2f);
                using (Pen pen = new Pen(Stale ? Color.Gray : Color.SteelBlue, 2)) e.Graphics.DrawLines(pen, points);
            }
        }
    }
}
