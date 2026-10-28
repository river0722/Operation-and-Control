using System;
using System.Windows.Forms;
using Operations_Control.Controls;
using Operations_Control.Models;

namespace Operations_Control.Views
{
    public partial class MotionView : UserControl
    {
        public event EventHandler<JogRequestEventArgs> JogStarted;
        public event EventHandler JogReleased;
        private readonly Button[] jogButtons;
        private bool holdingJog;
        private readonly ToolTip reasons = new ToolTip { ShowAlways = true };

        public int XAxis { get { return (int)XMotor.SelectedItem; } }
        public int YAxis { get { return (int)YMotor.SelectedItem; } }
        public int Motor { get { return (int)SelectedMotor.SelectedItem; } }

        public MotionView()
        {
            InitializeComponent();
            XMotor.SelectedIndex = 0;
            YMotor.SelectedIndex = 1;
            SelectedMotor.SelectedIndex = 0;
            reasons.SetToolTip(Acceleration, "定位加速度上限，单位 ct/ms²；控制器按速度与加速度计算总加速时间，并设置 S 曲线时间。");
            jogButtons = new Button[] { jogUpButton, jogLeftButton, jogRightButton, jogDownButton };
            WireJog(jogUpButton, delegate { return YAxis; }, 1);
            WireJog(jogLeftButton, delegate { return XAxis; }, -1);
            WireJog(jogRightButton, delegate { return XAxis; }, 1);
            WireJog(jogDownButton, delegate { return YAxis; }, -1);
        }

        private void SingleStepChanged(object sender, EventArgs e) { Step.Enabled = SingleStep.Checked; }

        private void WireJog(Button button, Func<int> motor, int direction)
        {
            bool keyHeld = false;
            button.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) RaiseJog(motor(), direction);
            };
            button.MouseUp += delegate { ReleaseJog(); };
            button.MouseCaptureChanged += delegate { if (!button.Capture) ReleaseJog(); };
            button.LostFocus += delegate { keyHeld = false; ReleaseJog(); };
            button.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Space && e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!keyHeld) { keyHeld = true; RaiseJog(motor(), direction); }
            };
            button.KeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { keyHeld = false; ReleaseJog(); e.SuppressKeyPress = true; }
            };
        }

        private void RaiseJog(int motor, int direction) { if (JogStarted != null) JogStarted(this, new JogRequestEventArgs(motor, direction)); }
        private void ReleaseJog() { if (JogReleased != null) JogReleased(this, EventArgs.Empty); }

        public void SetState(bool connected, bool busy, bool jogging)
        {
            holdingJog = jogging;
            // Keep the held button enabled so mouse capture survives until release.
            foreach (Button button in jogButtons) button.Enabled = connected && (!busy || jogging);
            XMotor.Enabled = YMotor.Enabled = SelectedMotor.Enabled = connected && !busy && !jogging;
            Speed.Enabled = Acceleration.Enabled = SingleStep.Enabled = connected && !busy && !jogging;
            Step.Enabled = connected && !busy && !jogging && SingleStep.Checked;
            Target.Enabled = connected && !busy && !jogging;
            MoveButton.Enabled = ZeroButton.Enabled = ApplySpeedButton.Enabled = connected && !busy && !jogging;
        }
        public void ApplyGuard(Operations_Control.Application.MotionGuard guard, ControllerFeedback feedback, bool fresh)
        {
            if (!fresh) { foreach (Button button in jogButtons) button.Enabled = false; MoveButton.Enabled = ZeroButton.Enabled = ApplySpeedButton.Enabled = false; stopHint.Text = "反馈未知／过期，等待有效数据"; return; }
            if (holdingJog) return;
            GuardButton(jogUpButton, guard, feedback, YAxis, 1);
            GuardButton(jogDownButton, guard, feedback, YAxis, -1);
            GuardButton(jogLeftButton, guard, feedback, XAxis, -1);
            GuardButton(jogRightButton, guard, feedback, XAxis, 1);
            AxisSample sample = feedback == null ? null : feedback.Find(Motor);
            string reason = guard.Reason(sample, sample == null ? 0 : Math.Sign((double)Target.Value - sample.Position), (double)Target.Value, Speed.Value, true);
            MoveButton.Enabled = MoveButton.Enabled && reason == null;
            stopHint.Text = reason == null ? String.Empty : "定位不可用：" + reason;
            stopHint.ForeColor = reason == null ? System.Drawing.SystemColors.ControlText : System.Drawing.Color.Firebrick;
            reasons.SetToolTip(MoveButton, reason ?? "绝对定位；等待实际到位确认");
            ZeroButton.Enabled = ZeroButton.Enabled && sample != null && sample.Status != null && sample.Status.VelocityZero && sample.Status.InPosition;
        }
        private void GuardButton(Button button, Operations_Control.Application.MotionGuard guard, ControllerFeedback feedback, int motor, int direction)
        {
            AxisSample sample = feedback == null ? null : feedback.Find(motor);
            double? target = SingleStep.Checked && sample != null ? (double?)(sample.Position + (double)Step.Value * direction) : null;
            string reason = guard.Reason(sample, direction, target, Speed.Value, true);
            button.Enabled = button.Enabled && reason == null; reasons.SetToolTip(button, reason ?? "按住点动，松开请求停止");
        }
    }
}
