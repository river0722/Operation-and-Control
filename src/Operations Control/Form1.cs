using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Operations_Control.Controls;
using Operations_Control.Models;
using Operations_Control.Communication;
using Operations_Control.Files;
using Operations_Control.Application;
using Operations_Control.Views;
using Operations_Control.Motion;

namespace Operations_Control
{
    // Composes views and application services; device access stays in Communication.
    public partial class Form1 : Form
    {
        private ControllerSession session;
        private CanvasFollower follower;
        private ProgramEditorForm editorWindow;
        private LogForm logWindow;
        private LogView log;
        private AxisSample[] currentSamples;
        private bool samplesReady;
        private bool connected, closeReady;
        private OperationState operations;
        private ManualMotionService manual;
        private FeedbackService feedbackService;
        private NxLinkBridge nxLink;
        private bool busy { get { return operations.ManualBusy; } }
        private bool shuttingDown { get { return operations.ShuttingDown; } }
        private int? joggingMotor { get { return operations.JoggingMotor; } set { operations.JoggingMotor = value; } }
        private string filePath;
        private string savedText = String.Empty;
        private Encoding fileEncoding = new UTF8Encoding(false);
        private PathView pathView;
        private MotionGuard motionGuard;
        private DeviceConfiguration configuration;
        private AxisStatusView axisStates;
        private DiagnosticsView diagnostics;
        private AlarmService alarms;
        private bool disconnectPending, closePending;
        private bool loadingConfiguration;
        private bool followAnchorValid;
        private MotionPoint followAnchor;

        public Form1()
        {
            InitializeComponent();
            // The designer must load controls without starting controller services.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
            Rectangle workspace = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, workspace.Width - 24), Math.Min(Height, workspace.Height - 24));
            operations = new OperationState();
            session = new ControllerSession(new WindowsFormsSynchronizationContext());
            feedbackService = new FeedbackService(session);
            configuration = NewConfiguration(true);
            motionGuard = new MotionGuard { Configuration = configuration };
            manual = new ManualMotionService(session, operations, feedbackService, motionGuard);
            alarms = new AlarmService();
            follower = new CanvasFollower(session);
            editorWindow = new ProgramEditorForm(); logWindow = new LogForm(); log = logWindow.Log;
            nxLink = new NxLinkBridge();
            nxLink.Message += delegate(string text)
            {
                if (!IsHandleCreated || IsDisposed || Disposing) return;
                try { BeginInvoke((Action)delegate { if (!IsDisposed && !Disposing) log.Append("NX", text); }); }
                catch (InvalidOperationException) { }
            };
            editorWindow.Icon = Icon; logWindow.Icon = Icon;
            components.Add(editorWindow); components.Add(logWindow);
            canvases.ConfigureMode(connection.IsSimulation);
            IntegrateViews();
            WireEvents();
            manual.StatusChanged += delegate { if (!IsDisposed) Report(manual.StatusText, manual.StatusError); };
            manual.TimingRecorded += delegate(string text) { if (!IsDisposed) log.Append("耗时", text); };
            alarms.Transition += delegate(AlarmRecord alarm) { log.Append(alarm.Active ? "报警" : "恢复", alarm.Source + "：" + alarm.Message + "；" + alarm.Advice); };
            alarms.Changed += delegate
            {
                if (IsDisposed) return;
                diagnostics.ShowAlarms(alarms);
                alarmSummary.Text = alarms.ActiveCount == 0 ? "无报警 · 点击查看诊断" : "当前报警：" + alarms.ActiveCount + " · 点击查看";
                alarmSummary.ForeColor = alarms.ActiveCount == 0 ? UiTheme.Muted : UiTheme.Danger;
                for (int i = alarms.History.Count - 1; i >= 0; i--) if (alarms.History[i].Active) { alarmSummary.Text += " · " + alarms.History[i].Source + " " + alarms.History[i].Message; break; }
            };
            alarmSummary.Click += delegate { pathView.Pages.SelectedIndex = pathView.Pages.TabPages.Count - 1; diagnostics.SelectAlarms(); };
            manual.StateChanged += delegate
            {
                if (IsDisposed) return;
                UpdateState();
                if (disconnectPending && !operations.ManualExecuting && !busy) { bool close = closePending; disconnectPending = closePending = false; FinishDisconnect(close); }
                else if (disconnectPending && operations.ManualStopFailed) { disconnectPending = closePending = false; Report("无法确认停稳，连接已保留；请检查设备或重试停止。", true); }
            };
            feedbackService.Updated += delegate
            {
                if (connected && !connection.IsSimulation && feedbackService.Fresh) nxLink.Publish(feedbackService.Latest);
                RefreshData(null, EventArgs.Empty);
            };
            feedbackService.Failed += delegate(Exception error)
            {
                if (IsDisposed || shuttingDown) return;
                operations.FeedbackStale = true;
                alarms.Set("communication", true, "通信", error.Message, "检查连接和控制器；恢复通信后核实设备状态，手动重新启动。");
                Report("反馈未知：" + error.Message, true); samplesReady = false;
                if (follower.Active) CancelDrawing(); UpdateState();
            };
            readouts.Clear(motion.XAxis, motion.YAxis, motion.Motor);
            refreshTimer.Start();
            drawTimer.Start();
            UpdateState();
            log.Append("信息", "请选择连接模式并连接设备。模拟模式不会访问实际控制卡。");
        }

        private void IntegrateViews()
        {
            pathView = new PathView(session, connection.IsSimulation, canvases.Settings, canvases.MappingReady,
                motion.XAxis, motion.YAxis, canvases, operations, feedbackService) { Dock = DockStyle.Fill };
            pathView.AddCoordinateSettings(canvases);
            workspaceTabs.TabPages.Remove(manualPage);
            // Rebuild page order before handles exist; Framework 3.5 Insert can lose a native tab.
            TabPage[] pages = new TabPage[] { pathView.Pages.TabPages[0], manualPage,
                pathView.ProgramPage, pathView.Pages.TabPages[2], pathView.SettingsPage };
            pathView.Pages.TabPages.Clear(); pathView.Pages.TabPages.AddRange(pages);
            Control generated = pathView.ProgramPage.Controls[0];
            TableLayoutPanel programLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            programLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            programLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); programLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            GroupBox generatedGroup = new GroupBox { Text = "规划程序", Dock = DockStyle.Fill };
            GroupBox fileGroup = new GroupBox { Text = "自定义程序", Dock = DockStyle.Fill };
            generatedGroup.Controls.Add(generated);
            TableLayoutPanel files = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            files.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            files.RowStyles.Add(new RowStyle(SizeType.Absolute, 230)); files.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editorWindow.TopLevel = false; editorWindow.FormBorderStyle = FormBorderStyle.None; editorWindow.MinimumSize = Size.Empty; editorWindow.Dock = DockStyle.Fill;
            editorWindow.HideSaveToolbar();
            files.Controls.Add(programs, 0, 0); files.Controls.Add(editorWindow, 0, 1); fileGroup.Controls.Add(files);
            programLayout.Controls.Add(generatedGroup, 0, 0); programLayout.Controls.Add(fileGroup, 1, 0); pathView.ProgramPage.Controls.Add(programLayout); editorWindow.Show();
            logPage = new TabPage("日志"); logWindow.TopLevel = false; logWindow.FormBorderStyle = FormBorderStyle.None; logWindow.MinimumSize = Size.Empty; logWindow.Dock = DockStyle.Fill;
            logPage.Controls.Add(logWindow); pathView.Pages.TabPages.Add(logPage); logWindow.Show();
            axisStates = new AxisStatusView { Dock = DockStyle.Fill };
            manualLayout.RowCount = 4; manualLayout.Height = 546; manualLayout.RowStyles[0].Height = 142;
            manualLayout.RowStyles[2].SizeType = SizeType.Absolute; manualLayout.RowStyles[2].Height = 132;
            manualLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); manualLayout.Controls.Add(axisStates, 0, 2);
            diagnostics = new DiagnosticsView { Dock = DockStyle.Fill };
            TabPage diagnosticsPage = new TabPage("诊断与配置"); diagnosticsPage.Controls.Add(diagnostics); pathView.Pages.TabPages.Add(diagnosticsPage);
            diagnostics.ShowProfile(configuration, motion.Motor);
            contentPanel.Controls.Remove(workspaceTabs); workspaceTabs.Dispose(); contentPanel.Controls.Add(pathView);
            pathView.ActivityChanged += delegate { UpdateState(); };
            pathView.StatusChanged += delegate { Report(pathView.StatusText, pathView.StatusError); };
            pathView.SnapshotChanged += delegate
            {
                ControllerSnapshot sample = pathView.LatestSnapshot;
                DisplayPosition(sample.XCounts, sample.YCounts);
            };
            globalStop.Click += delegate { StopMonitoredAxes(); };
            globalRecover.Click += delegate { RecoverControl(); };
            pathView.Pages.Selecting += delegate { StopJog(); CancelDrawing(); };
        }

        private void WireEvents()
        {
            connection.ConnectButton.Click += delegate
            {
                bool simulation = connection.IsSimulation; int owner = Handle.ToInt32(); bool opened = false;
                Run("正在连接设备…", delegate { opened = session.Connect(simulation, owner); }, delegate
                {
                    connected = opened;
                    operations.FeedbackStale = false;
                    if (opened) { if (!simulation) { LoadSavedConfiguration(false); canvases.AxisMappingChanged(); } pathView.ResumePolling(); }
                    feedbackService.Clear(); samplesReady = false; currentSamples = null; canvases.BreakTrace();
                    Report(opened ? "已连接 " + session.DeviceName : "已取消设备选择。", false);
                    if (opened && !simulation) nxLink.Start();
                    UpdateState(); if (connected) RefreshData(null, EventArgs.Empty);
                });
            };
            connection.DisconnectButton.Click += delegate { Disconnect(false); };
            connection.Mode.SelectedIndexChanged += delegate
            {
                if (!connected && !busy) { canvases.ConfigureMode(connection.IsSimulation); configuration = NewConfiguration(connection.IsSimulation); motionGuard.Configuration = configuration; if (!connection.IsSimulation) LoadSavedConfiguration(false); UpdateState(); }
            };
            motion.JogStarted += StartJog;
            motion.JogReleased += delegate { StopJog(); };
            motion.MoveButton.Click += delegate
            {
                int motor = motion.Motor; decimal target = motion.Target.Value; decimal speed = motion.Speed.Value; decimal acceleration = motion.Acceleration.Value;
                TryMotion(delegate { manual.Move(motor, target, speed, acceleration); });
            };
            motion.ZeroButton.Click += delegate
            {
                int motor = motion.Motor;
                AxisSample selected = feedbackService.Latest == null ? null : feedbackService.Latest.Find(motor);
                if (selected == null || selected.Status == null || !feedbackService.Fresh || !selected.Status.VelocityZero || !selected.Status.InPosition) { Report("当前位置置零前需要有效反馈并确认停稳。", true); return; }
                if (MessageBox.Show(this, "将电机 " + motor + " 的当前位置设为零点（HOMEZ）？",
                    "当前位置置零", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                TryMotion(delegate { manual.ZeroPosition(motor); });
            };
            motion.ApplySpeedButton.Click += delegate
            {
                int x = motion.XAxis; int y = motion.YAxis; decimal speed = motion.Speed.Value;
                if (speed > configuration.Axis(x).MaximumSpeed || speed > configuration.Axis(y).MaximumSpeed) { Report("速度超过 X/Y 配置上限，请调整速度或核对每轴参数。", true); return; }
                Run("正在设置速度…", delegate { session.SetSpeed(x, speed); if (x != y) session.SetSpeed(y, speed); },
                    delegate { Report("X / Y 轴点动速度已写入并回读确认：" + speed + " ct/ms。", false); });
            };
            EventHandler axesChanged = delegate { CancelDrawing(); canvases.AxisMappingChanged(); ResetSamples(); UpdateState(); };
            motion.XMotor.SelectedIndexChanged += axesChanged;
            motion.YMotor.SelectedIndexChanged += axesChanged;
            motion.SelectedMotor.SelectedIndexChanged += delegate { CancelDrawing(); ResetSamples(); diagnostics.ShowAxisParameters(configuration, motion.Motor); UpdateState(); };
            axisStates.EnableButton.Click += delegate { TryMotion(delegate { manual.SetEnabled(motion.Motor, true); }); };
            axisStates.DisableButton.Click += delegate
            {
                if (!connection.IsSimulation && MessageBox.Show(this, "去使能会撤销电机保持力。已确认该轴停稳和抱闸流程？", "所选轴去使能", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                TryMotion(delegate { manual.SetEnabled(motion.Motor, false); });
            };
            axisStates.ResetButton.Click += delegate { TryMotion(delegate { manual.Reset(motion.Motor); }); };
            axisStates.RepairOutputButton.Click += delegate { RepairPulseOutput(); };
            axisStates.XYZeroButton.Click += delegate { TryMotion(delegate { manual.MoveXYToZero(motion.XAxis, motion.YAxis, motion.Speed.Value, motion.Acceleration.Value); }); };
            diagnostics.ApplyButton.Click += delegate
            {
                TryMotion(delegate
                {
                    diagnostics.ApplyProfile(configuration);
                    AxisConfiguration axis = configuration.Axis(motion.Motor);
                    if (canvases.MappingReady && (motion.Motor == motion.XAxis || motion.Motor == motion.YAxis))
                    {
                        if (motion.Motor == motion.XAxis) { canvases.XMin.Value = (decimal)(axis.MinimumCounts / canvases.Settings.XCountsPerUnit); canvases.XMax.Value = (decimal)(axis.MaximumCounts / canvases.Settings.XCountsPerUnit); }
                        else { canvases.YMin.Value = (decimal)(axis.MinimumCounts / canvases.Settings.YCountsPerUnit); canvases.YMax.Value = (decimal)(axis.MaximumCounts / canvases.Settings.YCountsPerUnit); }
                        canvases.ApplyCurrentSettings();
                    }
                    Report("所选轴行程、速度上限和复位命令已应用。", false); UpdateState();
                });
            };
            diagnostics.SaveButton.Click += delegate { SaveConfiguration(); };
            diagnostics.LoadButton.Click += delegate { LoadConfiguration(); };
            diagnostics.ReadSpeedButton.Click += delegate
            {
                int axis = motion.Motor; string response = null;
                Run("正在回读速度…", delegate { response = session.Send("I" + axis + "22").Response.Trim(); }, delegate { diagnostics.ParameterResult.Text = "电机 " + axis + " 实际点动速度：" + response + " ct/ms"; });
            };
            diagnostics.ReadIoButton.Click += delegate { ReadIo(); };
            diagnostics.AcknowledgeButton.Click += delegate { alarms.Acknowledge(); Report("当前报警已确认；仍存在的故障继续显示。", false); };
            programs.SendButton.Click += delegate { SendCommand(); };
            programs.Command.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (programs.SendButton.Enabled) SendCommand(); }
            };
            programs.BrowseButton.Click += delegate { OpenProgram(); };
            programs.SaveButton.Click += delegate { SaveProgram(); };
            editorWindow.SaveRequested += delegate { if (programs.SaveButton.Enabled) SaveProgram(); };
            programs.DownloadButton.Click += delegate { DownloadProgram(); };
            canvases.DrawingCanvas.StrokeStarted += BeginDrawing;
            canvases.DrawingCanvas.PointDrawn += delegate(object sender, DrawPointEventArgs e)
            {
                if (follower.Active) follower.SubmitMm(e.Point);
            };
            canvases.DrawingCanvas.StrokeEnded += delegate
            {
                bool active = follower.Active;
                follower.Cancel(); if (active) manual.Stop(new int[] { motion.XAxis, motion.YAxis }); UpdateState();
            };
            canvases.Follow.CheckedChanged += delegate
            {
                canvases.DrawingCanvas.FollowHandleEnabled = canvases.Follow.Checked;
                if (!canvases.Follow.Checked)
                {
                    followAnchorValid = false; CancelDrawing(); return;
                }
                if (!connected || !samplesReady || operations.FeedbackStale || !feedbackService.Fresh || !canvases.MappingReady || motion.XAxis == motion.YAxis)
                {
                    canvases.Follow.Checked = false;
                    Report("当前没有有效的 X/Y 反馈，无法启用鼠标随动。", true); return;
                }
                followAnchor = canvases.Settings.FromCounts(currentSamples[0].Position, currentSamples[1].Position);
                if (!canvases.Settings.Contains(followAnchor))
                {
                    canvases.Follow.Checked = false;
                    Report("当前下位机位置不在画布范围内，无法启用鼠标随动。", true); return;
                }
                followAnchorValid = true;
                Report("鼠标随动已启用，按住橙色十字拖动即可随动，松开停止。", false);
            };
            canvases.SettingsApplied += delegate { CancelDrawing(); SyncMappedAxes(); diagnostics.ShowAxisParameters(configuration, motion.Motor); PersistCurrentConfiguration(); UpdateState(); };
            pathView.PlanningSettingsChanged += delegate { PersistCurrentConfiguration(); };
            canvases.MappingChanged += delegate { CancelDrawing(); UpdateState(); };
            follower.Failed += delegate { CancelDrawing(); if (connected && !shuttingDown) manual.Stop(new int[] { motion.XAxis, motion.YAxis }); Report(follower.Error, true); };
            Deactivate += delegate { StopJog(); CancelDrawing(); }; FormClosing += CloseForm;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { StopMonitoredAxes(); e.SuppressKeyPress = true; }
            };
            FormClosed += delegate { refreshTimer.Stop(); drawTimer.Stop(); nxLink.Dispose(); feedbackService.Dispose(); session.Dispose(); };
            if (!connection.IsSimulation) LoadSavedConfiguration(false);
        }

        private void ResetSamples()
        {
            feedbackService.Clear(); samplesReady = false; currentSamples = null;
            readouts.Clear(motion.XAxis, motion.YAxis, motion.Motor); canvases.BreakTrace(); canvases.ClearCurrentPosition();
            xPosition.Text = "X —"; yPosition.Text = "Y —";
        }
        private static DeviceConfiguration NewConfiguration(bool simulation)
        {
            DeviceConfiguration config = new DeviceConfiguration();
            for (int axis = 1; axis <= 8; axis++) config.Axes.Add(new AxisConfiguration { Motor = axis, TravelConfigured = simulation, MaximumSpeed = simulation ? 15 : 1, MaximumAcceleration = simulation ? 12 : 0.01M, ResetCommand = simulation ? "SIMRESET{motor}" : String.Empty });
            return config;
        }
        private void TryMotion(Action action)
        { try { action(); } catch (Exception error) { Report(error.Message, true); } }
        private void RecoverControl()
        {
            if (!manual.CanRecover) return;
            if (!connection.IsSimulation && MessageBox.Show(this,
                "将取消原动作，并对 X、Y 和所选监测轴去使能，电机保持力会撤销。\r\n" +
                "请先确认机械支撑与抱闸。恢复后保持去使能，原动作不会续跑。是否继续？",
                "解除等待并去使能", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            TryMotion(delegate
            {
                disconnectPending = closePending = false;
                CancelDrawing();
                manual.Recover(MonitoredAxes(), pathView.PrepareRecovery, delegate { return pathView.RecoveryReady; }, delegate { pathView.CompleteRecovery(); });
            });
        }
        private void RepairPulseOutput()
        {
            if (!connected || connection.IsSimulation) return;
            int selected = motion.Motor; PulseOutputConfiguration candidate = null;
            PulseOutputRepair repair = new PulseOutputRepair(session);
            Run("正在核对所选轴脉冲输出与反馈通道…", delegate { candidate = repair.Inspect(selected); }, delegate
            {
                if (!candidate.NeedsRepair) { Report(candidate.Description + "；无需修改。", false); return; }
                if (MessageBox.Show(this, candidate.Description + "。\r\n" +
                    "确认该驱动器接在对应 C 通道的脉冲／方向输出后，将修复输出地址并回读。\r\n" +
                    "修复后保持去使能，不自动启动，也不写入永久存储。是否继续？",
                    "修复脉冲输出", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                string result = null;
                Run("正在修复脉冲输出…", delegate { result = repair.Repair(candidate); }, delegate { Report(result, false); });
            });
        }
        private void SyncMappedAxes()
        {
            if (!canvases.MappingReady) return;
            AxisConfiguration x = configuration.Axis(motion.XAxis), y = configuration.Axis(motion.YAxis);
            x.MinimumCounts = canvases.Settings.XMin * canvases.Settings.XCountsPerUnit; x.MaximumCounts = canvases.Settings.XMax * canvases.Settings.XCountsPerUnit; x.TravelConfigured = true;
            y.MinimumCounts = canvases.Settings.YMin * canvases.Settings.YCountsPerUnit; y.MaximumCounts = canvases.Settings.YMax * canvases.Settings.YCountsPerUnit; y.TravelConfigured = true;
        }
        private void SaveConfiguration()
        {
            TryMotion(delegate
            {
                string path = PersistCurrentConfiguration();
                diagnostics.ParameterResult.Text = "配置已保存：" + path; Report("设备配置已保存。", false);
            });
        }
        private string PersistCurrentConfiguration()
        {
            if (loadingConfiguration || !canvases.MappingReady) return null;
            diagnostics.ApplyProfile(configuration); pathView.SavePlanningSettings(configuration);
            CanvasSettings mapping = canvases.Settings;
            configuration.XMotor = motion.XAxis; configuration.YMotor = motion.YAxis;
            configuration.XMin = mapping.XMin; configuration.XMax = mapping.XMax; configuration.YMin = mapping.YMin; configuration.YMax = mapping.YMax;
            configuration.XScale = mapping.XCountsPerUnit; configuration.YScale = mapping.YCountsPerUnit; configuration.DeviceName = connected ? session.DeviceName : configuration.DeviceName;
            string path = DeviceConfigurationStore.ProfilePath(connection.IsSimulation); DeviceConfigurationStore.Save(path, configuration);
            return path;
        }
        private bool LoadSavedConfiguration(bool report)
        {
            string path = DeviceConfigurationStore.ProfilePath(connection.IsSimulation);
            if (!System.IO.File.Exists(path)) return false;
            try
            {
                loadingConfiguration = true;
                DeviceConfiguration loaded = DeviceConfigurationStore.Load(path);
                if (connected && !connection.IsSimulation && !String.IsNullOrEmpty(loaded.DeviceName) && loaded.DeviceName != session.DeviceName) throw new InvalidOperationException("配置设备与当前连接设备不一致，请选择对应设备。");
                if (connected && !connection.IsSimulation && String.IsNullOrEmpty(loaded.DeviceName)) loaded.DeviceName = session.DeviceName;
                configuration = loaded; motionGuard.Configuration = loaded;
                motion.XMotor.SelectedItem = loaded.XMotor; motion.YMotor.SelectedItem = loaded.YMotor;
                canvases.XMin.Value = (decimal)loaded.XMin; canvases.XMax.Value = (decimal)loaded.XMax; canvases.YMin.Value = (decimal)loaded.YMin; canvases.YMax.Value = (decimal)loaded.YMax;
                canvases.XScale.Value = (decimal)loaded.XScale; canvases.YScale.Value = (decimal)loaded.YScale;
                pathView.ApplySavedPlanningSettings(loaded);
                canvases.InvalidateLoadedMapping();
                diagnostics.ShowProfile(configuration, motion.Motor); ResetSamples(); UpdateState();
                if (report) Report("配置已载入；请核对设备、比例、行程和零点，再点击应用坐标。", false);
                return true;
            }
            catch (Exception error)
            {
                if (report) Report("配置载入失败：" + error.Message, true);
                return false;
            }
            finally { loadingConfiguration = false; }
        }
        private void LoadConfiguration()
        {
            if (!LoadSavedConfiguration(true)) Report("没有找到可载入的配置文件。", true);
        }
        private void ReadIo()
        {
            TryMotion(delegate
            {
                List<IoConfiguration> inputs = diagnostics.ReadInputs(); List<string> values = new List<string>();
                if (inputs.Count == 0) { Report("请先按实际接线填写 I/O 查询变量。", true); return; }
                Run("正在读取 I/O…", delegate
                {
                    foreach (IoConfiguration input in inputs)
                    {
                        double value; string response = session.Send(input.Query).Response.Trim();
                        if (!Double.TryParse(response, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || !PlanningOptions.Finite(value)) throw new InvalidOperationException("I/O 反馈无效：" + input.Query);
                        bool active = input.ActiveLow ? value == 0 : value != 0; values.Add(response + (active ? " · 有效" : " · 无效"));
                    }
                }, delegate { diagnostics.ShowIo(values); Report("I/O 已读取；按实际接线确认信号含义。", false); });
            });
        }

        private void DisplayPosition(double x, double y)
        {
            xPosition.Text = "X " + (canvases.MappingReady ? (x / canvases.Settings.XCountsPerUnit).ToString("0.000") + " mm" : x.ToString("0.###") + " ct");
            yPosition.Text = "Y " + (canvases.MappingReady ? (y / canvases.Settings.YCountsPerUnit).ToString("0.000") + " mm" : y.ToString("0.###") + " ct");
        }

        private void BeginDrawing(object sender, EventArgs e)
        {
            if (pathView.IsBusy) return;
            if (!canvases.Follow.Checked) return;
            if (!connected || busy || shuttingDown || joggingMotor.HasValue || !samplesReady ||
                operations.ManualExecuting || operations.FeedbackStale || !canvases.MappingReady || motion.XAxis == motion.YAxis) return;
            try
            {
                decimal xSpeed = configuration.Axis(motion.XAxis).MaximumSpeed;
                decimal ySpeed = configuration.Axis(motion.YAxis).MaximumSpeed;
                motionGuard.Require(feedbackService.Latest.Find(motion.XAxis), 0, currentSamples[0].Position, xSpeed, true);
                motionGuard.Require(feedbackService.Latest.Find(motion.YAxis), 0, currentSamples[1].Position, ySpeed, true);
            }
            catch (InvalidOperationException error) { Report(error.Message, true); return; }
            catch (ArgumentException error) { Report(error.Message, true); return; }
            MotionPoint position = canvases.Settings.FromCounts(currentSamples[0].Position, currentSamples[1].Position);
            if (!canvases.Settings.Contains(position) || (DateTime.Now - currentSamples[0].Timestamp).TotalSeconds > 1)
            {
                canvases.Follow.Checked = false;
                Report("反馈超出画布范围或已过期，请确认坐标参数与当前位置。", true); return;
            }
            if (canvases.DrawingCanvas.IsDraggingPosition)
            {
                followAnchor = position; followAnchorValid = true;
            }
            if (!followAnchorValid || PathGeometry.Distance(position, followAnchor) > CanvasFollower.StartToleranceMm ||
                !canvases.DrawingCanvas.HasPointerPoint || PathGeometry.Distance(followAnchor, canvases.DrawingCanvas.PointerPoint) > CanvasFollower.StartToleranceMm)
            {
                canvases.Follow.Checked = false;
                Report("鼠标随动只能从启用时锁定的当前下位机位置开始。请取消后重新定位并启用随动。", true); return;
            }
            follower.BeginPath(motion.XAxis, motion.YAxis, configuration.Axis(motion.XAxis).MaximumSpeed, configuration.Axis(motion.YAxis).MaximumSpeed,
                configuration.Axis(motion.XAxis).MaximumAcceleration, configuration.Axis(motion.YAxis).MaximumAcceleration,
                canvases.Settings, pathView.CoordinateSystem, CanvasFollower.DefaultDelayMs,
                new MotionPoint(followAnchor.X, followAnchor.Y), pathView.FollowInterpolation);
            pathView.ShowFollowTiming(follower.Timing, true);
            Report((connection.IsSimulation ? "模拟" : "实机") + " 轨迹随动：按顺序复刻绘图路径，运动时间由几何规划决定，不计算鼠标速度，松开停止。", false);
            UpdateState();
        }

        private void CancelDrawing()
        {
            canvases.DrawingCanvas.EndStroke(); follower.Cancel(); UpdateState();
        }

        private void PumpDrawing(object sender, EventArgs e)
        {
            if (shuttingDown) return;
            follower.Pump();
            if (pathView != null) pathView.ShowFollowTiming(follower.Timing, follower.Active);
            if (follower.Active)
            {
                feedback.Text = pathView.FollowInterpolation + " 轨迹随动 · 几何规划 " + follower.FollowSpeedMmPerSecond.ToString("0.#") +
                    " mm/s · 待发送 " + follower.PendingPointCount + " 点 · " + (follower.WaitingForBuffer ? "缓冲等待，轨迹保留" : "按顺序复刻路径") + " · 松开停止";
                feedback.ForeColor = follower.WaitingForBuffer ? UiTheme.Warning : UiTheme.Primary;
            }
        }

        private void UpdateState()
        {
            operations.Following = follower.Active;
            bool pathBusy = pathView != null && pathView.IsBusy;
            bool manualBusy = busy || operations.ManualExecuting;
            motionGuard.Mapping = canvases.Settings; motionGuard.MappingReady = canvases.MappingReady;
            motionGuard.XMotor = motion.XAxis; motionGuard.YMotor = motion.YAxis; motionGuard.Simulation = connection.IsSimulation;
            connection.ShowState(connected, manualBusy || shuttingDown || pathBusy, connected ? session.DeviceName : String.Empty);
            motion.SetState(connected && !shuttingDown && !operations.FeedbackStale, manualBusy || follower.Active || pathBusy, joggingMotor.HasValue);
            motion.ApplyGuard(motionGuard, feedbackService.Latest, feedbackService.Fresh);
            programs.SetState(connected && !shuttingDown,
                manualBusy || !feedbackService.Fresh || operations.FeedbackStale || follower.Active || shuttingDown || pathBusy, !String.IsNullOrEmpty(filePath));
            editorWindow.Editor.ReadOnly = manualBusy || follower.Active || shuttingDown || pathBusy;
            canvases.SetState(connected && !shuttingDown && samplesReady && motion.XAxis != motion.YAxis,
                manualBusy || shuttingDown || pathBusy, follower.Active, operations.ManualStopping);
            axisStates.Display(feedbackService.Latest, feedbackService.Fresh && connected, motion.Motor, manualBusy || follower.Active || pathBusy || shuttingDown);
            axisStates.RepairOutputButton.Enabled = connected && !connection.IsSimulation && !manualBusy && !pathBusy && !follower.Active && !shuttingDown;
            AxisSample zeroX = feedbackService.Latest == null ? null : feedbackService.Latest.Find(motion.XAxis);
            AxisSample zeroY = feedbackService.Latest == null ? null : feedbackService.Latest.Find(motion.YAxis);
            axisStates.XYZeroButton.Enabled = connected && feedbackService.Fresh && !operations.FeedbackStale && !manualBusy && !pathBusy && !follower.Active && !shuttingDown && motion.XAxis != motion.YAxis &&
                motionGuard.Reason(zeroX, zeroX == null ? 0 : Math.Sign(-zeroX.Position), 0, motion.Speed.Value, true) == null &&
                motionGuard.Reason(zeroY, zeroY == null ? 0 : Math.Sign(-zeroY.Position), 0, motion.Speed.Value, true) == null;
            diagnostics.Simulation = connection.IsSimulation;
            diagnostics.Display(feedbackService.Latest, feedbackService.Fresh && connected, motion.Motor, manualBusy || follower.Active || pathBusy || shuttingDown);
            globalStop.Enabled = connected && !shuttingDown;
            globalRecover.Enabled = connected && manual.CanRecover;
            if (pathView != null)
            {
                pathView.UpdateContext(connection.IsSimulation, canvases.Settings, canvases.MappingReady, motion.XAxis, motion.YAxis, motion.Motor);
                pathView.ApplyMotionLimits(configuration);
                pathView.RefreshState();
            }
        }

        private void Run(string message, Action action, Action success)
        {
            if (busy || operations.ManualExecuting || shuttingDown || follower.Active || pathView.IsBusy) return;
            feedback.Text = message; feedback.ForeColor = UiTheme.Muted;
            manual.Execute(action, delegate(Exception error)
            {
                if (IsDisposed) return;
                if (error != null) Report(error.Message, true);
                else if (success != null) success();
                UpdateState();
            });
        }

        private void Report(string message, bool error)
        {
            if (IsDisposed || Disposing) return;
            feedback.Text = message; feedback.ForeColor = error ? UiTheme.Danger : UiTheme.Muted;
            log.Append(error ? "错误" : "信息", message);
        }

        private void StartJog(object sender, JogRequestEventArgs e)
        {
            decimal? step = motion.SingleStep.Checked ? (decimal?)motion.Step.Value : null;
            TryMotion(delegate { manual.StartJog(e.Motor, e.Direction, motion.Speed.Value, step, motion.Acceleration.Value, null); });
        }

        private void StopJog()
        {
            manual.StopJog(null);
        }

        private int[] MonitoredAxes()
        {
            List<int> motors = new List<int>(new int[] { motion.XAxis, motion.YAxis, motion.Motor });
            if (joggingMotor.HasValue) motors.Add(joggingMotor.Value);
            if (operations.MovingMotor.HasValue) motors.Add(operations.MovingMotor.Value);
            return motors.ToArray();
        }

        private void StopMonitoredAxes()
        {
            if (pathView.IsExecuting)
            {
                pathView.StopExecution();
                if (motion.Motor != motion.XAxis && motion.Motor != motion.YAxis)
                    session.Execute(delegate { session.Stop(new int[] { motion.Motor }); }, delegate(Exception error) { if (error != null) Report(error.Message, true); });
                return;
            }
            if (!connected || shuttingDown) return;
            CancelDrawing();
            int[] motors = MonitoredAxes(); joggingMotor = null;
            manual.Stop(motors);
            UpdateState();
        }

        private void RefreshData(object sender, EventArgs e)
        {
            if (IsDisposed || feedbackService == null) return;
            feedbackService.CheckTimeout(); manual.Tick();
            if (connected) readouts.ShowAge(feedbackService.AgeMs, feedbackService.Fresh);
            if (connected && !feedbackService.Fresh) { xPosition.Text = "X 未知"; yPosition.Text = "Y 未知"; UpdateState(); }
            if (IsDisposed || !connected || shuttingDown || feedbackService.Latest == null) return;
            AxisSample[] samples = feedbackService.Latest.Axes;
            if (samples[0].Motor != motion.XAxis || samples[1].Motor != motion.YAxis || samples[2].Motor != motion.Motor) return;
            samplesReady = true; currentSamples = samples; readouts.Display(samples);
            readouts.ShowAge(feedbackService.AgeMs, feedbackService.Fresh);
            operations.FeedbackStale = false;
            alarms.Set("communication", false, "通信", "", ""); alarms.Update(feedbackService.Latest);
            canvases.DisplayCurrent(samples[0].Position, samples[1].Position);
            if (!pathView.IsExecuting) canvases.Display(samples[0].Position, samples[1].Position);
            DisplayPosition(samples[0].Position, samples[1].Position);
            MotionPoint feedbackPoint = canvases.Settings.FromCounts(samples[0].Position, samples[1].Position);
            if (follower.RecordFeedback(feedbackPoint, feedbackService.Latest.TimeMs))
                log.Append("随动延时", DescribeFollowTiming(follower.Timing));
            if (follower.Active && !canvases.Settings.Contains(canvases.Settings.FromCounts(samples[0].Position, samples[1].Position)))
            {
                CancelDrawing(); canvases.Follow.Checked = false; Report("反馈位置超出已设置范围，跟随已停止。", true);
            }
            if (follower.Active && !feedbackService.Latest.Snapshot.Ready)
            { CancelDrawing(); Report("轴故障、限位或失能，鼠标跟随已停止。", true); }
            UpdateState();
        }

        private static string DescribeFollowTiming(FollowTiming timing)
        {
            return "鼠标输入→上位机发送 " + Milliseconds(timing.MouseToHostMs) +
                "；上位机发送→下位机接受 " + Milliseconds(timing.HostToControllerMs) +
                "；下位机接受→反馈到位 " + Milliseconds(timing.ControllerToArrivalMs) +
                "；总延时 " + Milliseconds(timing.TotalMs);
        }

        private static string Milliseconds(double? value)
        { return value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "未确认"; }

        private void Disconnect(bool closeWindow)
        {
            if (shuttingDown) return;
            if (!connected) { FinishDisconnect(closeWindow); return; }
            if (disconnectPending) { closePending = closePending || closeWindow; return; }
            disconnectPending = true; closePending = closeWindow;
            CancelDrawing(); manual.Stop(MonitoredAxes());
        }
        private void FinishDisconnect(bool closeWindow)
        {
            if (shuttingDown) return;
            CancelDrawing();
            pathView.CancelPendingWork();
            refreshTimer.Stop();
            int[] motors = MonitoredAxes();
            manual.Disconnect(motors, delegate(Exception stopError, Exception error)
            {
                if (IsDisposed) return;
                connected = false; ResetSamples();
                nxLink.Stop();
                if (stopError != null) Report("断开前停止失败：" + stopError.Message, true);
                if (error != null) Report("设备关闭异常：" + error.Message, true);
                else Report("设备连接已关闭。", false);
                UpdateState();
                if (closeWindow) { closeReady = true; Close(); } else refreshTimer.Start();
            });
        }

        private void SendCommand()
        {
            string command = programs.Command.Text.Trim();
            if (command.Length == 0) { Report("请输入要发送的命令。", true); programs.Command.Focus(); return; }
            CommandResult result = null;
            Run("正在发送命令…", delegate { result = session.Send(command); }, delegate
            {
                programs.Response.Text = (String.IsNullOrEmpty(result.Response) ? "命令已接受（空应答）" : result.Response.Trim()) +
                    Environment.NewLine + Environment.NewLine + "状态：0x" + unchecked((uint)result.Status).ToString("X8");
                Report("命令 " + command + " → " + (String.IsNullOrEmpty(result.Response) ? "空应答" : result.Response.Trim()), false);
            });
        }

        private bool ConfirmDiscard()
        {
            if (editorWindow.Editor.Text == savedText) return true;
            DialogResult answer = MessageBox.Show(this, "程序内容尚未保存。是否先另存为文件？",
                "保存程序", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return answer == DialogResult.No || (answer == DialogResult.Yes && SaveProgram());
        }

        private void OpenProgram()
        {
            if (!ConfirmDiscard()) return;
            OpenFileDialog dialog = programOpenDialog;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                ProgramDocument document = ProgramFileStore.Read(dialog.FileName);
                string content = document.Text;
                filePath = document.Path; fileEncoding = document.Encoding; savedText = content;
                editorWindow.Editor.Text = content;
                programs.FileLabel.Text = System.IO.Path.GetFileName(filePath) + "  ·  " + fileEncoding.WebName;
                Report("已打开程序：" + filePath, false); UpdateState();
            }
            catch (Exception error) { Report("打开文件失败：" + error.Message, true); }
        }

        private bool SaveProgram()
        {
            SaveFileDialog dialog = programSaveDialog;
            dialog.FileName = String.IsNullOrEmpty(filePath) ? "program.pmc" : System.IO.Path.GetFileName(filePath);
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            try
            {
                ProgramFileStore.Save(dialog.FileName, editorWindow.Editor.Text, fileEncoding);
                filePath = dialog.FileName; savedText = editorWindow.Editor.Text;
                programs.FileLabel.Text = System.IO.Path.GetFileName(filePath) + "  ·  " + fileEncoding.WebName;
                Report("程序已保存：" + filePath, false); UpdateState(); return true;
            }
            catch (Exception error) { Report("保存文件失败：" + error.Message, true); return false; }
        }

        private void DownloadProgram()
        {
            if (editorWindow.Editor.Text != savedText) { Report("程序已修改，请先另存为后再下载。", true); if (!SaveProgram()) return; }
            bool simulation = connection.IsSimulation;
            if (!simulation && MessageBox.Show(this, "将已保存的程序下载到当前 PMAC 设备：\n" + filePath + "\n\n确定下载？",
                "下载程序", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            string path = filePath;
            Run("正在下载程序…", delegate { session.Download(path); },
                delegate { Report(simulation ? "模拟下载完成：已验证文件，未执行程序。" : "程序下载成功。", false); });
        }

        private void CloseForm(object sender, FormClosingEventArgs e)
        {
            if (closeReady) return;
            if (pathView.IsExecuting)
            {
                e.Cancel = true; pathView.StopExecution(delegate(Exception error)
                { if (error == null) Close(); else Report("停止失败：" + error.Message, true); }); return;
            }
            e.Cancel = true; if (shuttingDown) return;
            StopJog(); CancelDrawing(); if (!ConfirmDiscard()) return; Disconnect(true);
        }
    }
}
