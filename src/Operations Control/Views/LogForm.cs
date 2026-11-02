using System.Windows.Forms;

namespace Operations_Control.Views
{
    public partial class LogForm : Form
    {
        public LogForm() { InitializeComponent(); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }
}
