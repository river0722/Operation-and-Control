using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Operations_Control.Controls;
using Operations_Control.Models;
using Operations_Control.Services;
using Operations_Control.Views;

namespace Operations_Control
{
    // Coordinates views. PMAC protocol and transport are contained in Services.
    public partial class Form1 : Form
    {
        private ControllerSession session;
        private CanvasFollower follower;
        private ProgramEditorForm editorWindow;
        private LogForm logWindow;
        private LogView log;
        private AxisSample[] currentSamples;
        private bool samplesReady;
        private bool connected, busy, pollPending, shuttingDown, closeReady;
        private int? joggingMotor;
        private string filePath;
        private string savedText = String.Empty;
        private Encoding fileEncoding = new UTF8Encoding(false);
        private PathView pathView;

        public Form1()
        {
            InitializeComponent();
            // The designer must load controls without starting controller services.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
            Rectangle workspace = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, workspace.Width - 24), Math.Min(Height, workspace.Height - 24));
            session = new ControllerSession();
            follower = new CanvasFollower(session, this);
            editorWindow = new ProgramEditorForm(); logWindow = new LogForm(); log = logWindow.Log;
            editorWindow.Icon = Icon; logWindow.Icon = Icon;
            components.Add(editorWindow); components.Add(logWindow);
            canvases.ConfigureMode(connection.IsSimulation);
            IntegrateViews();
            WireEvents();
            readouts.Clear(motion.XAxis, motion.YAxis, motion.Motor);
            refreshTimer.Start();
            drawTimer.Start();
            UpdateState();
            log.Append("信息", "请选择连接模式并连接设备。模拟模式不会访问实际控制卡。");
        }

        private void ShowEditor(object sender, EventArgs e) { pathView.Pages.SelectedTab = pathView.ProgramPage; editorWindow.Editor.Focus(); }

        private void IntegrateViews()
        {
            pathView = new PathView(session, connection.IsSimulation, canvases.Settings, canvases.MappingReady,
                motion.XAxis, motion.YAxis, canvases) { Dock = DockStyle.Fill };
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
            contentPanel.Controls.Remove(workspaceTabs); workspaceTabs.Dispose(); contentPanel.Controls.Add(pathView);
            pathView.ActivityChanged += delegate { UpdateState(); };
            pathView.StatusChanged += delegate { Report(pathView.StatusText, pathView.StatusError); };
            pathView.SnapshotChanged += delegate
            {
                ControllerSnapshot sample = pathView.LatestSnapshot;
                DisplayPosition(sample.XCounts, sample.YCounts);
            };
            globalStop.Click += delegate { StopMonitoredAxes(); };
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
                    samplesReady = false; currentSamples = null; canvases.BreakTrace();
                    Report(opened ? "已连接 " + session.DeviceName : "已取消设备选择。", false);
                    UpdateState(); if (connected) RefreshData(null, EventArgs.Empty);
                });
            };
            connection.DisconnectButton.Click += delegate { Disconnect(false); };
            connection.Mode.SelectedIndexChanged += delegate
            {
                if (!connected && !busy) { canvases.ConfigureMode(connection.IsSimulation); UpdateState(); }
            };
            motion.JogStarted += StartJog;
            motion.JogReleased += delegate { StopJog(); };
            motion.MoveButton.Click += delegate
            {
                int motor = motion.Motor; decimal target = motion.Target.Value; decimal speed = motion.Speed.Value;
                Run("正在发送定位命令…", delegate { session.Move(motor, target, speed); },
                    delegate { Report("电机 " + motor + " 定位命令已接受，目标 " + target + " ct。", false); });
            };
            motion.ZeroButton.Click += delegate
            {
                int motor = motion.Motor;
                if (MessageBox.Show(this, "将电机 " + motor + " 的当前位置设为零点（HOMEZ）？",
                    "当前位置置零", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                Run("正在设置当前位置零点…",
                    delegate { session.Send("#" + motor + "HOMEZ"); session.ClearSamples(); },
                    delegate { ResetSamples(); Report("电机 " + motor + " 当前位置置零命令已接受。", false); });
            };
            motion.ApplySpeedButton.Click += delegate
            {
                int x = motion.XAxis; int y = motion.YAxis; decimal speed = motion.Speed.Value;
                Run("正在设置速度…", delegate { session.SetSpeed(x, speed); if (x != y) session.SetSpeed(y, speed); },
                    delegate { Report("X / Y 轴点动速度已设置为 " + speed + " ct/ms。", false); });
            };
            motion.StopButton.Click += delegate { StopMonitoredAxes(); };
            EventHandler axesChanged = delegate { CancelDrawing(); canvases.AxisMappingChanged(); ResetSamples(); UpdateState(); };
            motion.XMotor.SelectedIndexChanged += axesChanged;
            motion.YMotor.SelectedIndexChanged += axesChanged;
            motion.SelectedMotor.SelectedIndexChanged += delegate { CancelDrawing(); ResetSamples(); UpdateState(); };
            programs.SendButton.Click += delegate { SendCommand(); };
            programs.Command.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (programs.SendButton.Enabled) SendCommand(); }
            };
            programs.BrowseButton.Click += delegate { OpenProgram(); };
            programs.SaveButton.Click += delegate { SaveProgram(); };
            programs.EditButton.Click += ShowEditor;
            editorWindow.SaveRequested += delegate { if (programs.SaveButton.Enabled) SaveProgram(); };
            programs.DownloadButton.Click += delegate { DownloadProgram(); };
            canvases.DrawingCanvas.StrokeStarted += BeginDrawing;
            canvases.DrawingCanvas.PointDrawn += delegate(object sender, DrawPointEventArgs e)
            {
                if (follower.Active) follower.Submit(canvases.Settings.ToCounts(e.Point));
            };
            canvases.DrawingCanvas.StrokeEnded += delegate
            {
                if (follower.Active) Report("绘图跟随结束，已发送双轴停止请求。", false);
                follower.Cancel(); UpdateState();
            };
            canvases.Follow.CheckedChanged += delegate { if (!canvases.Follow.Checked) CancelDrawing(); };
            canvases.SettingsApplied += delegate { CancelDrawing(); UpdateState(); };
            canvases.MappingChanged += delegate { CancelDrawing(); UpdateState(); };
            follower.Failed += delegate { CancelDrawing(); Report(follower.Error, true); };
            Deactivate += delegate { StopJog(); CancelDrawing(); }; FormClosing += CloseForm;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { StopMonitoredAxes(); e.SuppressKeyPress = true; }
            };
            FormClosed += delegate { refreshTimer.Stop(); drawTimer.Stop(); session.Dispose(); };
        }

        private void ResetSamples()
        {
            samplesReady = false; currentSamples = null;
            readouts.Clear(motion.XAxis, motion.YAxis, motion.Motor); canvases.BreakTrace();
            xPosition.Text = "X —"; yPosition.Text = "Y —";
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
                !canvases.MappingReady || motion.XAxis == motion.YAxis) return;
            MotionPoint position = canvases.Settings.FromCounts(currentSamples[0].Position, currentSamples[1].Position);
            if (!canvases.Settings.Contains(position) || (DateTime.Now - currentSamples[0].Timestamp).TotalSeconds > 1)
            {
                canvases.Follow.Checked = false;
                Report("反馈超出画布范围或已过期，请确认坐标参数与当前位置。", true); return;
            }
            follower.Begin(motion.XAxis, motion.YAxis, motion.Speed.Value);
            Report((connection.IsSimulation ? "模拟" : "实机") + "绘图跟随中，松开鼠标停止。", false);
            UpdateState();
        }

        private void CancelDrawing()
        {
            canvases.DrawingCanvas.EndStroke(); follower.Cancel(); UpdateState();
        }

        private void PumpDrawing(object sender, EventArgs e) { if (!shuttingDown) follower.Pump(); }

        private void UpdateState()
        {
            bool pathBusy = pathView != null && pathView.IsBusy;
            connection.ShowState(connected, busy || shuttingDown || pathBusy, connected ? session.DeviceName : String.Empty);
            motion.SetState(connected && !shuttingDown, busy || follower.Active || pathBusy, joggingMotor.HasValue);
            programs.SetState(connected && !shuttingDown,
                busy || joggingMotor.HasValue || follower.Active || shuttingDown || pathBusy, !String.IsNullOrEmpty(filePath));
            editorWindow.Editor.ReadOnly = busy || follower.Active || shuttingDown || pathBusy;
            canvases.SetState(connected && !shuttingDown && samplesReady && motion.XAxis != motion.YAxis,
                busy || joggingMotor.HasValue || shuttingDown || pathBusy, follower.Active);
            globalStop.Enabled = connected && !shuttingDown;
            if (pathView != null)
            {
                pathView.UpdateContext(connection.IsSimulation, canvases.Settings, canvases.MappingReady, motion.XAxis, motion.YAxis);
                pathView.SetExternalState(busy || joggingMotor.HasValue || shuttingDown || follower.Active, follower.Active);
            }
        }

        private void Run(string message, Action action, Action success)
        {
            if (busy || shuttingDown || follower.Active || pathView.IsBusy) return;
            busy = true; feedback.Text = message; feedback.ForeColor = UiTheme.Muted; UpdateState();
            session.Execute(this, action, delegate(Exception error)
            {
                busy = false;
                if (error != null) Report(error.Message, true);
                else if (success != null) success();
                UpdateState();
            });
        }

        private void Report(string message, bool error)
        {
            feedback.Text = message; feedback.ForeColor = error ? UiTheme.Danger : UiTheme.Muted;
            log.Append(error ? "错误" : "信息", message);
        }

        private void StartJog(object sender, JogRequestEventArgs e)
        {
            if (!connected || busy || shuttingDown || joggingMotor.HasValue || follower.Active || pathView.IsBusy) return;
            decimal speed = motion.Speed.Value;
            decimal? step = motion.SingleStep.Checked ? (decimal?)motion.Step.Value : null;
            if (!step.HasValue) joggingMotor = e.Motor;
            Run("正在发送点动命令…", delegate { session.Jog(e.Motor, e.Direction, speed, step); }, delegate
            {
                Report("电机 " + e.Motor + (step.HasValue ? " 单步点动命令已接受。" : " 连续点动命令已接受。"), false);
            });
        }

        private void StopJog()
        {
            if (!joggingMotor.HasValue) return;
            int motor = joggingMotor.Value; joggingMotor = null;
            // Always queue release, even when start is pending. Same STA preserves order.
            session.Execute(this, delegate { session.Stop(new int[] { motor }); }, delegate(Exception error)
            {
                Report(error == null ? "电机 " + motor + " 停止命令已接受。" : "停止失败：" + error.Message, error != null);
                UpdateState();
            });
            UpdateState();
        }

        private int[] MonitoredAxes()
        {
            List<int> motors = new List<int>(new int[] { motion.XAxis, motion.YAxis, motion.Motor });
            if (joggingMotor.HasValue) motors.Add(joggingMotor.Value);
            return motors.ToArray();
        }

        private void StopMonitoredAxes()
        {
            if (pathView.IsExecuting)
            {
                pathView.StopExecution();
                if (motion.Motor != motion.XAxis && motion.Motor != motion.YAxis)
                    session.Execute(this, delegate { session.Stop(new int[] { motion.Motor }); }, delegate(Exception error) { if (error != null) Report(error.Message, true); });
                return;
            }
            if (!connected || shuttingDown) return;
            CancelDrawing();
            int[] motors = MonitoredAxes(); joggingMotor = null;
            session.Execute(this, delegate { session.Stop(motors); }, delegate(Exception error)
            {
                Report(error == null ? "X / Y 和所选电机停止命令已接受。" : "停止失败：" + error.Message, error != null);
                UpdateState();
            });
            UpdateState();
        }

        private void RefreshData(object sender, EventArgs e)
        {
            if (!connected || busy || pollPending || shuttingDown || pathView.IsExecuting) return;
            int[] motors = new int[] { motion.XAxis, motion.YAxis, motion.Motor };
            AxisSample[] samples = null; pollPending = true;
            session.Execute(this, delegate { samples = session.ReadAxes(motors); }, delegate(Exception error)
            {
                pollPending = false; if (!connected || shuttingDown || pathView.IsExecuting) return;
                if (error != null) { Report("位置刷新失败：" + error.Message, true); ResetSamples(); Disconnect(false); }
                else if (motors[0] == motion.XAxis && motors[1] == motion.YAxis && motors[2] == motion.Motor)
                {
                    samplesReady = true; currentSamples = samples; readouts.Display(samples);
                    canvases.Display(samples[0].Position, samples[1].Position);
                    DisplayPosition(samples[0].Position, samples[1].Position);
                    if (follower.Active && !canvases.Settings.Contains(canvases.Settings.FromCounts(samples[0].Position, samples[1].Position)))
                    {
                        CancelDrawing(); canvases.Follow.Checked = false; Report("反馈位置超出已设置范围，跟随已停止。", true);
                    }
                    UpdateState();
                }
            });
        }

        private void Disconnect(bool closeWindow)
        {
            if (shuttingDown) return;
            CancelDrawing();
            shuttingDown = true; refreshTimer.Stop();
            int[] motors = MonitoredAxes(); joggingMotor = null; UpdateState();
            Exception stopError = null;
            session.Execute(this, delegate
            {
                try { if (session.IsConnected) session.Stop(motors); }
                catch (Exception error) { stopError = error; }
                finally { session.Disconnect(); }
            }, delegate(Exception error)
            {
                connected = false; busy = false; shuttingDown = false; ResetSamples();
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
                if (new FileInfo(dialog.FileName).Length > 2 * 1024 * 1024)
                    throw new InvalidOperationException("程序文件超过 2 MB，请使用专用编辑器处理。");
                string content; Encoding encoding = new UTF8Encoding(false, true);
                try { content = File.ReadAllText(dialog.FileName, encoding); }
                catch (DecoderFallbackException) { encoding = Encoding.GetEncoding(936); content = File.ReadAllText(dialog.FileName, encoding); }
                filePath = dialog.FileName; fileEncoding = encoding; savedText = content;
                editorWindow.Editor.Text = content;
                programs.FileLabel.Text = Path.GetFileName(filePath) + "  ·  " + fileEncoding.WebName;
                Report("已打开程序：" + filePath, false); UpdateState();
            }
            catch (Exception error) { Report("打开文件失败：" + error.Message, true); }
        }

        private bool SaveProgram()
        {
            SaveFileDialog dialog = programSaveDialog;
            dialog.FileName = String.IsNullOrEmpty(filePath) ? "program.pmc" : Path.GetFileName(filePath);
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            try
            {
                File.WriteAllText(dialog.FileName, editorWindow.Editor.Text, fileEncoding);
                filePath = dialog.FileName; savedText = editorWindow.Editor.Text;
                programs.FileLabel.Text = Path.GetFileName(filePath) + "  ·  " + fileEncoding.WebName;
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
            StopJog(); CancelDrawing(); if (!ConfirmDiscard()) return; pathView.CancelPendingWork(); Disconnect(true);
        }
    }
}
