using System;
using System.Windows.Forms;
using Operations_Control.Controls;

namespace Operations_Control.Views
{
    public partial class ConnectionView : UserControl
    {
        public bool IsSimulation { get { return Mode.SelectedIndex == 0; } }

        public ConnectionView()
        {
            InitializeComponent();
            Mode.SelectedIndex = 0;
        }

        public void ShowState(bool connected, bool busy, string deviceName)
        {
            Mode.Enabled = !connected && !busy;
            ConnectButton.Enabled = !connected && !busy;
            DisconnectButton.Enabled = connected && !busy;
            status.Text = busy ? "正在处理…" : connected ? "已连接" : "未连接";
            status.ForeColor = connected ? UiTheme.Success : UiTheme.Muted;
            device.Text = connected ? deviceName : String.Empty;
        }
    }
}
