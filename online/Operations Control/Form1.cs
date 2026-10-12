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
            WireEvents();
            canvases.ConfigureMode(connection.IsSimulation);
            readouts.Clear(motion.XAxis, motion.YAxis, motion.Motor);
            refreshTimer.Start();
            drawTimer.Start();
            UpdateState();
            log.Append("信息", "请选择连接模式并连接设备。模拟模式不会访问实际控制卡。");
        }

        private void ShowHelp(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                "1. 选择模拟模式或 PMAC 实机，点击连接设备。\n" +
                "2. 选择电机、速度和步长。连续点动需按住按钮，松开即停止；键盘可使用空格。\n" +
                "3. 单轴定位使用绝对位置；当前位置置零发送 HOMEZ，不执行机械回零。\n" +
                "4. 程序控制区可发送命令；程序编辑与操作日志可从菜单打开。\n" +
                "5. 左画布显示反馈轨迹，右画布按住鼠标左键绘图。勾选跟随后，松开或移出画布即停止。\n" +
                "6. 模拟画布默认 ±10 mm、100 ct/mm。实机需填写实际 X/Y ct/mm 与行程，再应用坐标。\n\n" +
                "画布以电机零点为零点，单位为 mm；手动控制为 ct / ct/ms。\n" +
                "鼠标跟随为独立双轴目标更新，不保证按绘制路径进行直线插补。\n" +
                "软件停止作用于 X、Y 和所选电机，不能替代硬件急停。\n" +
                "实机模式需要已安装并配置的 32 位 PcommServer / PMAC 驱动。",
                "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowEditor(object sender, EventArgs e) { editorWindow.Show(this); editorWindow.Activate(); }
        private void ShowLog(object sender, EventArgs e) { logWindow.Show(this); logWindow.Activate(); }
        private void ShowAbout(object sender, EventArgs e)
        {
            MessageBox.Show(this, "PMAC 上位机控制程序\n原生控件 · 四分区布局 · 反馈轨迹与鼠标绘图\nX 默认电机 1，Y 默认电机 2。", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        }

        private void BeginDrawing(object sender, EventArgs e)
        {
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
            connection.ShowState(connected, busy || shuttingDown, connected ? session.DeviceName : String.Empty);
            motion.SetState(connected && !shuttingDown, busy || follower.Active, joggingMotor.HasValue);
            programs.SetState(connected && !shuttingDown,
                busy || joggingMotor.HasValue || follower.Active || shuttingDown, !String.IsNullOrEmpty(filePath));
            editorWindow.Editor.ReadOnly = busy || follower.Active || shuttingDown;
            canvases.SetState(connected && !shuttingDown && samplesReady && motion.XAxis != motion.YAxis,
                busy || joggingMotor.HasValue || shuttingDown, follower.Active);
            modeHint.Text = connection.IsSimulation ? "模拟模式" : "PMAC 实机";
        }

        private void Run(string message, Action action, Action success)
        {
            if (busy || shuttingDown || follower.Active) return;
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
            if (!connected || busy || shuttingDown || joggingMotor.HasValue || follower.Active) return;
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
            dateText.Text = DateTime.Now.ToString("yyyy-MM-dd"); timeText.Text = DateTime.Now.ToString("HH:mm:ss");
            if (!connected || busy || pollPending || shuttingDown) return;
            int[] motors = new int[] { motion.XAxis, motion.YAxis, motion.Motor };
            AxisSample[] samples = null; pollPending = true;
            session.Execute(this, delegate { samples = session.ReadAxes(motors); }, delegate(Exception error)
            {
                pollPending = false; if (!connected || shuttingDown) return;
                if (error != null) { Report("位置刷新失败：" + error.Message, true); ResetSamples(); Disconnect(false); }
                else if (motors[0] == motion.XAxis && motors[1] == motion.YAxis && motors[2] == motion.Motor)
                {
                    samplesReady = true; currentSamples = samples; readouts.Display(samples);
                    canvases.Display(samples[0].Position, samples[1].Position);
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
            e.Cancel = true; if (shuttingDown) return;
            StopJog(); CancelDrawing(); if (!ConfirmDiscard()) return; Disconnect(true);
        }
    }
}
