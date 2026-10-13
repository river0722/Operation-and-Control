using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Operations_Control.Models;
using Operations_Control.Services;

namespace Operations_Control.Views
{
    public partial class PathView : UserControl
    {
        private readonly ControllerSession session;
        private CanvasSettings mapping;
        private bool simulation, hardwareMappingReady;
        private int xMotor, yMotor;
        private DrawingPath drawing = new DrawingPath();
        private List<MotionPoint> currentStroke;
        private MotionPlan plan;
        private TrajectoryRun run;
        private ControllerSnapshot lastSample;
        private bool computing, commandBusy, pollPending, running, homing, stopping, externalBusy, following, lastActivity;
        private Action<Exception> stopCompleted;
        private int generation;
        private double runStart, homeStart;
        private int homeX, homeY, activePlc = -1;
        private bool homeBoth, homeSawX, homeSawY;
        private bool planOwned;
        public bool IsExecuting { get { return running || homing || commandBusy || stopping; } }
        public bool IsBusy { get { return IsExecuting || computing; } }
        public event EventHandler ActivityChanged;
        public event EventHandler StatusChanged;
        public event EventHandler SnapshotChanged;
        public ControllerSnapshot LatestSnapshot { get { return lastSample; } }
        public string StatusText { get { return feedbackLabel.Text; } }
        public bool StatusError { get { return feedbackLabel.ForeColor == Color.Firebrick; } }
        public TabControl Pages { get { return tabs; } }
        public TabPage ProgramPage { get { return programTab; } }
        public TabPage SettingsPage { get { return deviceTab; } }
        public MotionPlan CurrentPlan { get { return plan; } }
        public TrajectoryRun CurrentRun { get { return run; } }
        public DrawingPath CurrentDrawing { get { return drawing.Copy(); } }

        public PathView() { InitializeComponent(); }
        public PathView(ControllerSession controller, bool simulated, CanvasSettings settings, bool mappingReady, int x, int y, CanvasView canvases)
        {
            session = controller; simulation = simulated; mapping = settings; hardwareMappingReady = mappingReady; xMotor = x; yMotor = y;
            drawingCanvas = canvases.DrawingCanvas; actualCanvas = canvases.ActualCanvas;
            InitializeComponent();
            drawingCanvas.Settings = actualCanvas.Settings = mapping;
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
            foreach (NumericUpDown input in new NumericUpDown[] { speed, acceleration, spacing, coordinate, programNumber }) input.ValueChanged += delegate { InvalidatePlan(); };
            interpolation.SelectedIndexChanged += delegate { InvalidatePlan(); };
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
            HandleCreated += delegate { pollTimer.Start(); };
            UpdateButtons();
        }

        public void UpdateContext(bool simulated, CanvasSettings settings, bool ready, int x, int y)
        {
            if (simulation == simulated && mapping == settings && hardwareMappingReady == ready && xMotor == x && yMotor == y) return;
            if (IsBusy) return;
            Interlocked.Increment(ref generation); lastSample = null;
            if (simulation != simulated || mapping != settings) { drawing = drawingCanvas.GetPath(); hardwareConfirmed.Checked = false; }
            simulation = simulated; mapping = settings; hardwareMappingReady = ready; xMotor = x; yMotor = y;
            drawingCanvas.Settings = actualCanvas.Settings = mapping;
            hardwareConfirmed.Enabled = !simulated; lag.Enabled = simulated;
            InvalidatePlan();
        }
        public void SetExternalState(bool busy, bool mouseFollowing)
        { externalBusy = busy; following = mouseFollowing; UpdateButtons(); }
        public void CancelPendingWork() { Interlocked.Increment(ref generation); pollTimer.Stop(); }

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
        private void InvalidatePlan() { plan = null; programText.Clear(); UpdateButtons(); }
        private void Report(string message, bool error)
        {
            feedbackLabel.Text = message; feedbackLabel.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
            if (StatusChanged != null) StatusChanged(this, EventArgs.Empty);
        }
        private void Try(Action action) { try { action(); } catch (Exception error) { Report(error.Message, true); } }
        private bool HardwareReady { get { return simulation || (hardwareMappingReady && hardwareConfirmed.Checked); } }
        private bool Fresh { get { return lastSample != null && MonotonicClock.NowMs - lastSample.TimeMs <= 1000; } }
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
            saveProgramButton.Enabled = plan != null && !locked;
            analyzeButton.Enabled = exportButton.Enabled = !locked && run != null && run.Samples.Count > 0;
            enableButton.Enabled = disableButton.Enabled = !locked && connected && HardwareReady;
            homeButton.Enabled = !locked && connected && HardwareReady && Fresh && lastSample.Ready && lastSample.InPosition;
            homeTarget.Enabled = plcNumber.Enabled = homeTimeout.Enabled = !locked;
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
                    CoordinateSystem = (int)coordinate.Value, ProgramNumber = (int)programNumber.Value, XMotor = xMotor, YMotor = yMotor, Mapping = mapping };
                DrawingPath copy = drawing.Copy();
                MotionPoint initial = session.IsConnected ? mapping.FromCounts(lastSample.XCounts, lastSample.YCounts) : new MotionPoint(0, 0);
                Report("正在规划完整路径…", false);
                Background(delegate { return TrajectoryPlanner.Build(copy, options, initial); }, delegate(MotionPlan value)
                {
                    plan = value; programText.Text = value.Program;
                    Report(value.Options.Mode + " · " + value.Segments.Count + " 段 · " + (value.DurationMs / 1000).ToString("0.00") + " s", false);
                });
            });
        }
        public void StartExecution()
        {
            if (IsBusy || externalBusy || plan == null) return;
            Try(delegate
            {
                if (!session.IsConnected || !Fresh || !HardwareReady) throw new InvalidOperationException("需连接、有效反馈和已确认的实机配置。");
                if (!lastSample.Ready || !lastSample.InPosition) throw new InvalidOperationException("轴尚未闭环就绪或未到位。");
                MotionPlan selected = plan; double delay = (double)lag.Value; int version = generation;
                run = new TrajectoryRun { Plan = selected, Simulation = simulation, StartedUtc = DateTime.UtcNow, Result = "启动中" };
                actualCanvas.ClearPath(); analysisText.Clear(); commandBusy = true; planOwned = true; UpdateButtons(); Report("正在准备并启动路径…", false);
                double start = 0, duration = 0;
                session.Execute(this, delegate
                {
                    if (Thread.VolatileRead(ref generation) != version) return;
                    start = session.StartPlan(selected, delay, delegate { return Thread.VolatileRead(ref generation) != version; });
                    duration = MonotonicClock.NowMs - start;
                }, delegate(Exception error)
                {
                    commandBusy = false;
                    if (version != generation) { UpdateButtons(); return; }
                    if (error != null) { planOwned = false; run.Result = "启动失败：" + error.Message; Report(run.Result, true); }
                    else { runStart = start; run.StartCommandMs = duration; running = true; run.Result = "运行中"; Report("运行中", false); }
                    UpdateButtons();
                });
            });
        }
        private void Poll(object sender, EventArgs e)
        {
            if (IsDisposed || externalBusy || pollPending || commandBusy || stopping || !session.IsConnected || xMotor == yMotor) return;
            pollPending = true; ControllerSnapshot sample = null; int version = generation;
            int cs = running ? run.Plan.Options.CoordinateSystem : 0;
            session.Execute(this, delegate { sample = session.ReadSnapshot(xMotor, yMotor, cs); }, delegate(Exception error)
            {
                pollPending = false; if (IsDisposed || version != generation) return;
                if (error != null)
                {
                    lastSample = null; deviceLabel.Text = "反馈读取失败：" + error.Message;
                    if (running || homing) StopWithReason("通信失败：" + error.Message); else Report(error.Message, true);
                    UpdateButtons(); return;
                }
                lastSample = sample;
                if (SnapshotChanged != null) SnapshotChanged(this, EventArgs.Empty);
                deviceLabel.Text = "X / " + xMotor + "：" + sample.XStatus + "，" + sample.XCounts.ToString("0.###", CultureInfo.InvariantCulture) + " ct\r\n" +
                    "Y / " + yMotor + "：" + sample.YStatus + "，" + sample.YCounts.ToString("0.###", CultureInfo.InvariantCulture) + " ct\r\n" +
                    "查询耗时 " + sample.QueryMs.ToString("0.0") + " ms；X/Y 时差 " + Math.Abs(sample.XTimeMs - sample.YTimeMs).ToString("0.0") + " ms";
                if (running)
                {
                    MotionPoint p = mapping.FromCounts(sample.XCounts, sample.YCounts);
                    run.Samples.Add(new TrajectorySample(sample.TimeMs - runStart, sample.XTimeMs - runStart, sample.YTimeMs - runStart, sample.QueryMs, p));
                    actualCanvas.Append(p);
                    if (!sample.Ready || !mapping.Contains(p) || (sample.Coordinate != null && sample.Coordinate.Fault)) StopWithReason("运行中检测到轴故障、限位、失能、反馈越界或坐标系运行错误。");
                    else if (sample.TimeMs - runStart >= run.Plan.DurationMs && sample.InPosition && sample.Coordinate != null && !sample.Coordinate.Running && sample.Coordinate.InPosition)
                    {
                        double errorMm = PathGeometry.Distance(p, run.Plan.At(run.Plan.DurationMs));
                        if (errorMm > 0.05) StopWithReason("结束位置与规划相差超过 0.05 mm，需检查执行状态。");
                        else { running = false; planOwned = false; run.Completed = true; run.Result = "完成"; Report("运行完成", false); }
                    }
                    else if (sample.TimeMs - runStart > run.Plan.DurationMs + Math.Max(10000, run.Plan.DurationMs * 0.5)) StopWithReason("运动超时，已请求停止。");
                    if (run.Samples.Count >= 100000) StopWithReason("采样达到 100000 点上限，已请求停止并保留数据。");
                }
                if (homing) MonitorHome(sample);
                UpdateButtons();
            });
        }
        private void EnableAxes(bool enabled)
        {
            if (IsBusy || externalBusy || !HardwareReady || !session.IsConnected) return;
            commandBusy = true; plan = null; UpdateButtons();
            session.Execute(this, delegate { session.SetEnabled(xMotor, enabled); session.SetEnabled(yMotor, enabled); }, delegate(Exception error)
            { commandBusy = false; Report(error == null ? enabled ? "已发送 X/Y 闭环使能，请查看状态。" : "已发送 X/Y 失能。" : error.Message, error != null); UpdateButtons(); });
        }
        public void StartHoming()
        {
            if (IsBusy || externalBusy || !session.IsConnected || !HardwareReady || !Fresh || !lastSample.Ready || !lastSample.InPosition) return;
            homeX = homeTarget.SelectedIndex == 1 ? yMotor : xMotor; homeY = homeX == xMotor ? yMotor : xMotor;
            homeBoth = homeTarget.SelectedIndex == 2; activePlc = (int)plcNumber.Value;
            if (simulation && activePlc >= 0) { Report("模拟器验证内置 HOME；用户 PLC 需在实机验收。", true); return; }
            homeSawX = homeSawY = false; plan = null; commandBusy = true; int version = generation; UpdateButtons();
            session.Execute(this, delegate
            {
                if (Thread.VolatileRead(ref generation) == version) session.Home(homeX, homeY, homeBoth, activePlc);
            }, delegate(Exception error)
            {
                commandBusy = false; if (version != generation) { UpdateButtons(); return; }
                if (error != null) { Report(error.Message, true); StopWithReason("回零启动失败：" + error.Message); }
                else { homing = true; homeStart = MonotonicClock.NowMs; Report("机械回零中，等待本次寻零状态变化与到位。", false); }
                UpdateButtons();
            });
        }
        private void MonitorHome(ControllerSnapshot sample)
        {
            MotorStatus x = homeX == xMotor ? sample.XStatus : sample.YStatus, y = homeY == yMotor ? sample.YStatus : sample.XStatus;
            if (x.Homing || !x.HomeComplete || !x.InPosition) homeSawX = true;
            if (y.Homing || !y.HomeComplete || !y.InPosition) homeSawY = true;
            if (x.Fault || !x.Enabled || (homeBoth && (y.Fault || !y.Enabled))) { StopWithReason("回零检测到故障或失能。"); return; }
            if (homeSawX && x.HomeComplete && x.InPosition && x.VelocityZero && (!homeBoth || (homeSawY && y.HomeComplete && y.InPosition && y.VelocityZero)))
            {
                homing = false; activePlc = -1; actualCanvas.ClearPath(); Report("机械回零完成，Home Complete 与到位状态均已确认。", false);
            }
            else if (MonotonicClock.NowMs - homeStart > (double)homeTimeout.Value * 1000) StopWithReason("回零超时或未观察到本次寻零状态变化。");
        }
        public void StopExecution() { StopWithReason("用户停止"); }
        public void StopExecution(Action<Exception> completed) { stopCompleted += completed; StopExecution(); }
        private void StopWithReason(string reason)
        {
            if (stopping || IsDisposed) return;
            Interlocked.Increment(ref generation); bool wasHome = homing || activePlc >= 0;
            bool abortPlan = planOwned;
            int plc = activePlc, cs = plan == null ? (int)coordinate.Value : plan.Options.CoordinateSystem;
            running = false; homing = false; stopping = true;
            if (run != null && !run.Completed) run.Result = reason;
            UpdateButtons();
            session.Execute(this, delegate
            {
                if (!session.IsConnected) return;
                if (wasHome) session.StopHome(xMotor, yMotor, plc);
                else if (abortPlan) session.StopPlan(cs, xMotor, yMotor);
                else session.Stop(new int[] { xMotor, yMotor });
            }, delegate(Exception error)
            {
                stopping = false; commandBusy = false; activePlc = -1;
                if (error == null) planOwned = false;
                Report(error == null ? reason + "；停止指令已接受。" : reason + "；停止失败：" + error.Message, error != null);
                UpdateButtons();
                Action<Exception> completed = stopCompleted; stopCompleted = null;
                if (completed != null) completed(error);
            });
        }
        private void SaveProgram(object sender, EventArgs e)
        {
            if (IsBusy || externalBusy || plan == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "PMAC 程序 (*.pmc)|*.pmc", FileName = "trajectory.pmc" })
                if (dialog.ShowDialog(this) == DialogResult.OK) Try(delegate { File.WriteAllText(dialog.FileName, plan.Program, Encoding.ASCII); Report("程序已保存。", false); });
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
                Background(delegate { return TrajectoryAnalysis.Export(selected, parent); }, delegate(string output) { Report("完整运行数据已导出：" + output, false); });
            }
        }
    }
}
