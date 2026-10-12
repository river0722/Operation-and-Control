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

        public int XAxis { get { return (int)XMotor.SelectedItem; } }
        public int YAxis { get { return (int)YMotor.SelectedItem; } }
        public int Motor { get { return (int)SelectedMotor.SelectedItem; } }

        public MotionView()
        {
            InitializeComponent();
            XMotor.SelectedIndex = 0;
            YMotor.SelectedIndex = 1;
            SelectedMotor.SelectedIndex = 0;
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
            // Keep the held button enabled so mouse capture survives until release.
            foreach (Button button in jogButtons) button.Enabled = connected && (!busy || jogging);
            XMotor.Enabled = YMotor.Enabled = SelectedMotor.Enabled = connected && !busy && !jogging;
            Speed.Enabled = SingleStep.Enabled = connected && !busy && !jogging;
            Step.Enabled = connected && !busy && !jogging && SingleStep.Checked;
            Target.Enabled = connected && !busy && !jogging;
            MoveButton.Enabled = ZeroButton.Enabled = ApplySpeedButton.Enabled = connected && !busy && !jogging;
            StopButton.Enabled = connected;
        }
    }
}
