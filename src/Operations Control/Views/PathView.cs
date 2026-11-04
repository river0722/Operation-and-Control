using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Operations_Control.Models;
using Operations_Control.Communication;
using Operations_Control.Motion;
using Operations_Control.Files;
using Operations_Control.Application;
using Operations_Control.Controls;

namespace Operations_Control.Views
{
    public partial class PathView : UserControl
    {
        private readonly IControllerSession session;
        private readonly OperationState operations;
        private readonly FeedbackService feedbackService;
        private readonly MotionWorkflow workflow;
        private bool ownsFeedback;
        private int selectedMotor;
        private MotionPlan plan { get { return workflow == null ? null : workflow.Plan; } set { workflow.Plan = value; } }
        private TrajectoryRun run { get { return workflow == null ? null : workflow.CurrentRun; } }
        private ControllerSnapshot lastSample { get { return workflow == null ? null : workflow.LatestSnapshot; } }
        private bool computing { get { return operations != null && operations.Computing; } set { operations.Computing = value; } }
        private bool externalBusy { get { return operations.ExternalBusy; } }
        private bool following { get { return operations.Following; } }
        private CanvasSettings mapping;
        private bool simulation, hardwareMappingReady;
        private int xMotor, yMotor;
        private DrawingPath drawing = new DrawingPath();
        private List<MotionPoint> currentStroke;
        private bool lastActivity;
        private double maximumXSpeed = 1000, maximumYSpeed = 1000, maximumXAcceleration = 100000, maximumYAcceleration = 100000;
        private int displayedSamples;
        private int generation;
        private readonly Label readinessLabel = new Label { AutoSize = false, Width = 460, Height = 32, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(8, 3, 3, 3) };
        public bool IsExecuting { get { return workflow != null && workflow.IsExecuting; } }
        public bool IsBusy { get { return IsExecuting || computing; } }
        public event EventHandler ActivityChanged;
        public event EventHandler StatusChanged;
        public event EventHandler SnapshotChanged;
        public event EventHandler PlanningSettingsChanged;
        public ControllerSnapshot LatestSnapshot { get { return lastSample; } }
        public string StatusText { get { return feedbackLabel.Text; } }
        public bool StatusError { get { return feedbackLabel.ForeColor == Color.Firebrick; } }
        public TabControl Pages { get { return tabs; } }
        public TabPage ProgramPage { get { return programTab; } }
        public TabPage SettingsPage { get { return deviceTab; } }
        public MotionPlan CurrentPlan { get { return plan; } }
        public TrajectoryRun CurrentRun { get { return run; } }
        public DrawingPath CurrentDrawing { get { return drawing.Copy(); } }
        public int CoordinateSystem { get { return (int)coordinate.Value; } }
        public InterpolationMode FollowInterpolation { get { return interpolation.SelectedIndex == 1 ? InterpolationMode.Spline : InterpolationMode.PVT; } }

        public PathView() { InitializeComponent(); }
        public PathView(IControllerSession controller, bool simulated, CanvasSettings settings, bool mappingReady, int x, int y, CanvasView canvases)
            : this(controller, simulated, settings, mappingReady, x, y, canvases, new OperationState(), new FeedbackService(controller))
        { ownsFeedback = true; }

        public PathView(IControllerSession controller, bool simulated, CanvasSettings settings, bool mappingReady, int x, int y,
            CanvasView canvases, OperationState operations, FeedbackService feedbackService)
        {
            session = controller; this.operations = operations; this.feedbackService = feedbackService;
            workflow = new MotionWorkflow(controller, operations, feedbackService);
            simulation = simulated; mapping = settings; hardwareMappingReady = mappingReady; xMotor = x; yMotor = y; selectedMotor = x;
            workflow.Configure(simulated, settings, mappingReady, x, y, selectedMotor);
            drawingCanvas = canvases.DrawingCanvas; actualCanvas = canvases.ActualCanvas;
            InitializeComponent();
            drawingCanvas.Settings = actualCanvas.Settings = mapping;
            ToolTip followSpeedHint = new ToolTip(components);
            followSpeedHint.SetToolTip(speed, "此速度用于生成完整路径规划。鼠标随动根据画速自动计算，并受每轴配置速度上限约束。");
            hardwareConfirmed.Enabled = !simulation; lag.Enabled = simulation;
            drawing = drawingCanvas.GetPath();
            geometry.Controls.Add(canvases.Follow); canvases.Follow.Text = "鼠标跟随";
            canvases.Follow.AutoSize = true; canvases.Follow.Dock = DockStyle.None; canvases.Follow.Margin = new Padding(8, 8, 3, 3);
            importButton.Click += ImportDxf;
            clearButton.Click += delegate { LoadDrawing(new DrawingPath()); };
            clearActualButton.Click += delegate { actualCanvas.ClearPath(); };
            undoButton.Click += delegate { if (drawing.Strokes.Count > 0) { drawing.Strokes.RemoveAt(drawing.Strokes.Count - 1); Changed(); } };
            shapeButton.Click += AddShape;
            transformButton.Click += delegate { Try(delegate { LoadDrawing(PathGeometry.Transform(drawing, (double)scale.Value, (double)offsetX.Value, (double)offsetY.Value)); }); };
            planButton.Click += delegate { GeneratePlan(); };
            runButton.Click += delegate { StartExecution(); };
            saveProgramButton.Click += SaveProgram;
            analyzeButton.Click += delegate { Analyze(); };
            exportButton.Click += Export;
            enableButton.Click += delegate { EnableAxes(true); };
            disableButton.Click += delegate { EnableAxes(false); };
            homeButton.Click += delegate { StartHoming(); };
            foreach (NumericUpDown input in new NumericUpDown[] { speed, spacing, coordinate, programNumber, unitlessScale, lag }) input.ValueChanged += delegate { InvalidatePlan(); if (PlanningSettingsChanged != null) PlanningSettingsChanged(this, EventArgs.Empty); };
            interpolation.SelectedIndexChanged += delegate { InvalidatePlan(); if (PlanningSettingsChanged != null) PlanningSettingsChanged(this, EventArgs.Empty); };
            continuousPvt.CheckedChanged += delegate { InvalidatePlan(); if (PlanningSettingsChanged != null) PlanningSettingsChanged(this, EventArgs.Empty); };
            hardwareConfirmed.CheckedChanged += delegate { UpdateButtons(); };
            drawingCanvas.StrokeStarted += delegate { currentStroke = new List<MotionPoint>(); drawing.Strokes.Add(currentStroke); InvalidatePlan(); };
            drawingCanvas.PointDrawn += delegate(object sender, DrawPointEventArgs e)
            {
                if (currentStroke == null) return;
                if (drawing.PointCount >= PathGeometry.MaximumPoints) { drawingCanvas.EndStroke(); Report("最多保留 20000 个输入点，请拆分路径。", true); return; }
                if (currentStroke.Count == 0 || PathGeometry.Distance(currentStroke[currentStroke.Count - 1], e.Point) > 0.001) currentStroke.Add(e.Point);
            };
            drawingCanvas.StrokeEnded += delegate
            {
                if (currentStroke != null && currentStroke.Count < 2) drawing.Strokes.Remove(currentStroke);
                currentStroke = null; Changed();
            };
            workflow.StateChanged += delegate { UpdateButtons(); };
            workflow.StatusChanged += delegate { Report(workflow.StatusText, workflow.StatusError); };
            workflow.SnapshotChanged += DisplaySnapshot;
            workflow.HomeCompleted += delegate { actualCanvas.ClearPath(); };
            feedbackService.Failed += FeedbackFailed;
            Disposed += delegate
            {
                if (workflow != null) workflow.Dispose();
                feedbackService.Failed -= FeedbackFailed;
                if (ownsFeedback) feedbackService.Dispose();
            };
            HandleCreated += delegate { pollTimer.Start(); };
            UpdateButtons();
        }

        public void UpdateContext(bool simulated, CanvasSettings settings, bool ready, int x, int y)
        { UpdateContext(simulated, settings, ready, x, y, selectedMotor); }

        public void UpdateContext(bool simulated, CanvasSettings settings, bool ready, int x, int y, int selected)
        {
            if (simulation == simulated && mapping == settings && hardwareMappingReady == ready && xMotor == x && yMotor == y && selectedMotor == selected) return;
            if (IsBusy) return;
            if (simulation == simulated && mapping == settings && hardwareMappingReady == ready && xMotor == x && yMotor == y)
            {
                selectedMotor = selected; workflow.Configure(simulated, settings, ready, x, y, selected); return;
            }
            Interlocked.Increment(ref generation);
            if (simulation != simulated || mapping != settings) { drawing = drawingCanvas.GetPath(); hardwareConfirmed.Checked = false; }
            simulation = simulated; mapping = settings; hardwareMappingReady = ready; xMotor = x; yMotor = y; selectedMotor = selected;
            workflow.Configure(simulated, settings, ready, x, y, selected);
            drawingCanvas.Settings = actualCanvas.Settings = mapping;
            hardwareConfirmed.Enabled = !simulated; lag.Enabled = simulated;
            InvalidatePlan();
        }
        public void RefreshState() { UpdatePollInterval(); UpdateButtons(); }
        public void ApplyMotionLimits(DeviceConfiguration configuration)
        {
            AxisConfiguration x = configuration.Axis(xMotor), y = configuration.Axis(yMotor);
            double vx = (double)x.MaximumSpeed * 1000 / mapping.XCountsPerUnit;
            double vy = (double)y.MaximumSpeed * 1000 / mapping.YCountsPerUnit;
            double ax = (double)x.MaximumAcceleration * 1000000 / mapping.XCountsPerUnit;
            double ay = (double)y.MaximumAcceleration * 1000000 / mapping.YCountsPerUnit;
            if (vx == maximumXSpeed && vy == maximumYSpeed && ax == maximumXAcceleration && ay == maximumYAcceleration) return;
            maximumXSpeed = vx; maximumYSpeed = vy; maximumXAcceleration = ax; maximumYAcceleration = ay;
            Interlocked.Increment(ref generation); InvalidatePlan();
        }
        public void ApplySavedPlanningSettings(DeviceConfiguration saved)
        {
            if (saved == null) return;
            interpolation.SelectedIndex = saved.Interpolation == InterpolationMode.Spline ? 1 : 0;
            speed.Value = (decimal)saved.Speed; acceleration.Value = (decimal)saved.Acceleration; spacing.Value = (decimal)saved.Spacing;
            coordinate.Value = saved.CoordinateSystem; programNumber.Value = saved.ProgramNumber;
            unitlessScale.Value = (decimal)saved.DxfScale; lag.Value = (decimal)saved.SimulatorLagMs;
            InvalidatePlan();
        }
        public void SavePlanningSettings(DeviceConfiguration target)
        {
            if (target == null) return;
            target.Interpolation = interpolation.SelectedIndex == 1 ? InterpolationMode.Spline : InterpolationMode.PVT;
            target.Speed = (double)speed.Value; target.Acceleration = (double)acceleration.Value; target.Spacing = (double)spacing.Value;
            target.CoordinateSystem = (int)coordinate.Value; target.ProgramNumber = (int)programNumber.Value;
            target.DxfScale = (double)unitlessScale.Value; target.SimulatorLagMs = (double)lag.Value;
        }
        public void CancelPendingWork() { Interlocked.Increment(ref generation); workflow.CancelPendingWork(); feedbackService.Clear(); pollTimer.Stop(); }
        public void ResumePolling() { if (!IsDisposed) pollTimer.Start(); }

        public void LoadDrawing(DrawingPath path)
        {
            if (IsBusy || externalBusy) throw new InvalidOperationException("操作期间不能修改路径。");
            if (path.PointCount > 0) PathGeometry.Validate(path);
            drawing = path.Copy(); Changed();
        }
        private void Changed()
        {
            drawingCanvas.LoadPath(drawing); InvalidatePlan();
            Report(drawing.Source + " · " + drawing.Strokes.Count + " 笔画 / " + drawing.PointCount + " 点", false);
        }
        private void InvalidatePlan() { plan = null; programText.Clear(); autoAcceleration.Text = "不设限"; UpdateButtons(); }
        private void Report(string message, bool error)
        {
            feedbackLabel.Text = message; feedbackLabel.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
            if (StatusChanged != null) StatusChanged(this, EventArgs.Empty);
        }
        private void Try(Action action) { try { action(); } catch (Exception error) { Report(error.Message, true); } }
        private bool HardwareReady { get { return workflow.HardwareReady(hardwareConfirmed.Checked); } }
        private bool Fresh { get { return workflow.Fresh; } }
        private void UpdateButtons()
        {
            bool locked = IsBusy || externalBusy;
            importButton.Enabled = clearButton.Enabled = clearActualButton.Enabled = undoButton.Enabled = shapeButton.Enabled = transformButton.Enabled = !locked;
            geometry.Enabled = planning.Enabled = advanced.Enabled = transforms.Enabled = !locked;
            hardwareConfirmed.Enabled = !locked && !simulation;
            lag.Enabled = !locked && simulation;
            drawingCanvas.DrawingEnabled = drawingCanvas.Enabled = !IsBusy && (!externalBusy || following);
            planButton.Enabled = !locked && drawing.PointCount >= 2 && xMotor != yMotor;
            bool connected = session != null && session.IsConnected;
            runButton.Enabled = !locked && plan != null && connected && HardwareReady && Fresh && lastSample.Ready && lastSample.InPosition;
            readinessLabel.Text = "运行条件：" + (locked ? "等待当前操作完成" : !connected ? "需连接设备" : plan == null ? "需生成规划" : !HardwareReady ? "请应用坐标并确认实机配置" : !Fresh ? "反馈过期／未知" : !lastSample.Ready ? "轴未就绪，请检查使能、故障与限位" : !lastSample.InPosition ? "等待轴停稳到位" : "已满足");
            readinessLabel.ForeColor = locked ? UiTheme.Primary : runButton.Enabled ? UiTheme.Success :
                !connected || plan == null ? UiTheme.Muted : UiTheme.Warning;
            saveProgramButton.Enabled = plan != null && !locked;
            analyzeButton.Enabled = exportButton.Enabled = !locked && run != null && run.Samples.Count > 0;
            enableButton.Enabled = disableButton.Enabled = !locked && connected && HardwareReady;
            homeButton.Enabled = workflow.CanStartHoming(hardwareConfirmed.Checked);
            homeTarget.Enabled = plcNumber.Enabled = !locked && simulation;
            homeTimeout.Enabled = !locked;
            if (lastActivity != IsBusy) { lastActivity = IsBusy; if (ActivityChanged != null) ActivityChanged(this, EventArgs.Empty); }
        }
        private void Background<T>(Func<T> work, Action<T> complete)
        {
            computing = true; UpdateButtons(); int version = generation;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = work(); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                worker.Dispose(); if (IsDisposed) return; computing = false;
                if (version == generation)
                {
                    if (e.Error != null) Report(e.Error.Message, true); else complete((T)e.Result);
                }
                UpdateButtons();
            };
            worker.RunWorkerAsync();
        }
        private void ImportDxf(object sender, EventArgs e)
        {
            if (IsBusy || externalBusy) return;
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "ASCII DXF (*.dxf)|*.dxf", Title = "导入二维图形" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string file = dialog.FileName; double units = (double)unitlessScale.Value;
                Report("正在解析 DXF…", false);
                Background(delegate { return DxfReader.Read(file, 0.01, units); }, delegate(DrawingPath value) { drawing = value; Changed(); });
            }
        }
        private void AddShape(object sender, EventArgs e)
        {
            Try(delegate
            {
                double x = (double)centerX.Value, y = (double)centerY.Value, r = (double)radius.Value;
                DrawingPath value = shape.SelectedIndex == 0 ? PathGeometry.Line(x, y, (double)endX.Value, (double)endY.Value) :
                    shape.SelectedIndex == 3 ? PathGeometry.Star(x, y, r) :
                    PathGeometry.Arc(x, y, r, shape.SelectedIndex == 1 ? 0 : (double)startAngle.Value, shape.SelectedIndex == 1 ? 360 : (double)sweepAngle.Value, 0.01);
                DrawingPath combined = drawing.Copy(); combined.Strokes.AddRange(value.Strokes); combined.Source = "图形与自由绘图"; LoadDrawing(combined);
            });
        }
        public void GeneratePlan()
        {
            if (IsBusy || externalBusy) return;
            Try(delegate
            {
                if (session.IsConnected && !Fresh) throw new InvalidOperationException("等待新的 X/Y 反馈后再规划。");
                if (!simulation && !hardwareMappingReady) throw new InvalidOperationException("请先在主界面填写实际比例和行程并应用坐标。");
                PlanningOptions options = new PlanningOptions { Mode = interpolation.SelectedIndex == 0 ? InterpolationMode.PVT : InterpolationMode.Spline,
                    Speed = (double)speed.Value, Acceleration = (double)acceleration.Value, Spacing = (double)spacing.Value,
                    ContinuousPvt = continuousPvt.Checked && interpolation.SelectedIndex == 0,
                    CoordinateSystem = (int)coordinate.Value, ProgramNumber = (int)programNumber.Value, XMotor = xMotor, YMotor = yMotor, Mapping = mapping };
                options.MaximumXSpeed = maximumXSpeed; options.MaximumYSpeed = maximumYSpeed;
                options.MaximumXAcceleration = maximumXAcceleration; options.MaximumYAcceleration = maximumYAcceleration;
                DrawingPath copy = drawing.Copy();
                MotionPoint initial = session.IsConnected ? mapping.FromCounts(lastSample.XCounts, lastSample.YCounts) : new MotionPoint(0, 0);
                Report("正在规划完整路径…", false);
                Background(delegate { return MotionPlanningService.Build(copy, options, initial); }, delegate(MotionPlan value)
                {
                    plan = value; programText.Text = value.Program;
                    autoAcceleration.Text = "不设限";
                    Report(value.Options.Mode + " · " + value.Segments.Count + " 段 · " + (value.DurationMs / 1000).ToString("0.00") + " s", false);
                });
            });
        }
        public void StartExecution()
        {
            Try(delegate
            {
                if (IsBusy || externalBusy || plan == null) return;
                workflow.StartExecution((double)lag.Value, hardwareConfirmed.Checked);
                if (workflow.IsExecuting) { actualCanvas.ClearPath(); analysisText.Clear(); displayedSamples = 0; }
            });
        }
        private void Poll(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            UpdatePollInterval();
            workflow.Poll();
        }
        private void UpdatePollInterval()
        {
            int interval = operations != null && operations.ManualExecuting ? 25 : 50;
            if (pollTimer.Interval != interval) pollTimer.Interval = interval;
        }
        private void DisplaySnapshot(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            ControllerSnapshot sample = lastSample;
            deviceLabel.Text = "X / " + xMotor + "：" + sample.XStatus + "，" + sample.XCounts.ToString("0.###", CultureInfo.InvariantCulture) + " ct\r\n" +
                "Y / " + yMotor + "：" + sample.YStatus + "，" + sample.YCounts.ToString("0.###", CultureInfo.InvariantCulture) + " ct\r\n" +
                "查询耗时 " + sample.QueryMs.ToString("0.0") + " ms；X/Y 时差 " + Math.Abs(sample.XTimeMs - sample.YTimeMs).ToString("0.0") + " ms";
            if (run != null && run.Samples.Count > displayedSamples)
            {
                actualCanvas.Append(run.Samples[run.Samples.Count - 1].Position);
                displayedSamples = run.Samples.Count;
            }
            if (SnapshotChanged != null) SnapshotChanged(this, EventArgs.Empty);
        }
        private void FeedbackFailed(Exception error) { if (!IsDisposed) deviceLabel.Text = "反馈读取失败：" + error.Message; }
        private void EnableAxes(bool enabled) { Try(delegate { workflow.EnableAxes(enabled, hardwareConfirmed.Checked); }); }
        public void StartHoming() { Try(delegate { workflow.StartHoming(homeTarget.SelectedIndex, (int)plcNumber.Value, (double)homeTimeout.Value, hardwareConfirmed.Checked); }); }
        public void StopExecution() { workflow.Stop(); }
        public Action PrepareRecovery() { Interlocked.Increment(ref generation); return workflow.PrepareRecovery(); }
        public bool RecoveryReady { get { return workflow.RecoveryReady; } }
        public void CompleteRecovery() { workflow.CompleteRecovery(); }
        public void StopExecution(Action<Exception> completed) { workflow.Stop(completed); }
        private void SaveProgram(object sender, EventArgs e)
        {
            if (IsBusy || externalBusy || plan == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "PMAC 程序 (*.pmc)|*.pmc", FileName = "trajectory.pmc" })
                if (dialog.ShowDialog(this) == DialogResult.OK) Try(delegate { ProgramFileStore.SaveGenerated(dialog.FileName, plan.Program); Report("程序已保存。", false); });
        }
        private void Analyze()
        {
            if (IsBusy || externalBusy || run == null) return;
            TrajectoryRun selected = run; Report("正在分析轨迹…", false);
            Background(delegate { return TrajectoryAnalysis.Analyze(selected); }, delegate(AnalysisResult result) { analysisText.Text = result.Text.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.None)[0]; tabs.SelectedTab = analysisTab; Report("分析完成", false); });
        }
        private void Export(object sender, EventArgs e)
        {
            if (run == null || IsBusy || externalBusy) return;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "选择运行数据导出目录" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string parent = dialog.SelectedPath; TrajectoryRun selected = run;
                Background(delegate { return RunDataExporter.Export(selected, parent); }, delegate(string output) { Report("完整运行数据已导出：" + output, false); });
            }
        }
    }
}
