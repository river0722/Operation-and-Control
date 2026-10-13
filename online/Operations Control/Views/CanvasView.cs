using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Views
{
    public partial class CanvasView : UserControl
    {
        private bool simulation = true, dirty, configuring;
        private CanvasSettings settings = CanvasSettings.Default;
        public event EventHandler SettingsApplied;
        public event EventHandler MappingChanged;
        [Browsable(false)]
        public CanvasSettings Settings { get { return settings; } }
        [Browsable(false)]
        public bool HardwareCalibrated { get; private set; }
        [Browsable(false)]
        public bool MappingReady { get { return !dirty && (simulation || HardwareCalibrated); } }

        public CanvasView()
        {
            InitializeComponent();
            foreach (NumericUpDown input in MappingInputs()) input.ValueChanged += MappingEdited;
        }

        private NumericUpDown[] MappingInputs() { return new NumericUpDown[] { XMin, XMax, YMin, YMax, XScale, YScale }; }

        public void ConfigureMode(bool simulated)
        {
            DrawingCanvas.EndStroke(); Follow.Checked = false;
            simulation = simulated; configuring = true;
            XMin.Value = YMin.Value = simulated ? -10 : 0;
            XMax.Value = YMax.Value = simulated ? 10 : 0;
            XScale.Value = YScale.Value = simulated ? 100 : 0;
            configuring = false; dirty = !simulated; HardwareCalibrated = false;
            settings = CanvasSettings.Default;
            ActualCanvas.Settings = DrawingCanvas.Settings = settings;
            ActualCanvas.ClearPath(); DrawingCanvas.ClearPath();
            traceInfo.Text = simulated ? "模拟比例 100 ct/mm · 范围 ±10 mm" : "未标定 · 请填写比例与行程并应用";
        }

        private void MappingEdited(object sender, EventArgs e)
        {
            if (configuring) return;
            DrawingCanvas.EndStroke(); Follow.Checked = false; dirty = true;
            traceInfo.Text = "坐标参数待应用 · 跟随已禁用";
            if (MappingChanged != null) MappingChanged(this, EventArgs.Empty);
        }

        private void ApplySettings(object sender, EventArgs e)
        {
            try
            {
                CanvasSettings next = new CanvasSettings((double)XMin.Value, (double)XMax.Value,
                    (double)YMin.Value, (double)YMax.Value, (double)XScale.Value, (double)YScale.Value, "mm");
                DrawingCanvas.EndStroke(); Follow.Checked = false;
                settings = next; dirty = false; HardwareCalibrated = !simulation;
                ActualCanvas.Settings = DrawingCanvas.Settings = next;
                ActualCanvas.ClearPath(); DrawingCanvas.ClearPath();
                traceInfo.Text = simulation ? "模拟坐标已应用" : "实机坐标已应用 · 电机零点为画布零点";
                if (SettingsApplied != null) SettingsApplied(this, EventArgs.Empty);
            }
            catch (ArgumentException error)
            {
                MessageBox.Show(this, error.Message, "坐标参数", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void Display(double xCounts, double yCounts)
        {
            if (!MappingReady) return;
            MotionPoint point = settings.FromCounts(xCounts, yCounts);
            if (settings.Contains(point)) ActualCanvas.Append(point);
            else ActualCanvas.BreakPath();
            traceInfo.Text = String.Format(CultureInfo.InvariantCulture, "{0} X {1:0.###} / Y {2:0.###} mm{3}",
                simulation ? "模拟" : "反馈", point.X, point.Y, settings.Contains(point) ? "" : " · 超出画布");
        }

        public void AxisMappingChanged()
        {
            ActualCanvas.BreakPath();
            if (simulation) return;
            dirty = true; HardwareCalibrated = false;
            DrawingCanvas.EndStroke(); Follow.Checked = false; ActualCanvas.ClearPath();
            traceInfo.Text = "电机映射已修改 · 请确认比例与行程并应用";
        }

        public void SetState(bool ready, bool busy, bool following)
        {
            Follow.Enabled = ready && !busy && MappingReady;
            if (!Follow.Enabled && !following) Follow.Checked = false;
            foreach (NumericUpDown input in MappingInputs()) input.Enabled = !busy && !following;
            applyButton.Enabled = !busy && !following;
            DrawingCanvas.Enabled = !busy;
        }

        public void BreakTrace() { ActualCanvas.BreakPath(); }
        public void ShowSettingsOnly()
        {
            plotsLayout.Visible = false; canvasLayout.RowStyles[1].SizeType = SizeType.Absolute;
            canvasLayout.RowStyles[1].Height = 0; Dock = DockStyle.Fill;
        }
        private void ClearTrace(object sender, EventArgs e) { ActualCanvas.ClearPath(); }
        private void ClearDrawing(object sender, EventArgs e) { DrawingCanvas.EndStroke(); DrawingCanvas.ClearPath(); }
    }
}
