using System;
using System.Windows.Forms;

namespace Operations_Control.Views
{
    public partial class ProgramEditorForm : Form
    {
        public event EventHandler SaveRequested;
        public ProgramEditorForm() { InitializeComponent(); }
        private void RequestSave(object sender, EventArgs e) { if (SaveRequested != null) SaveRequested(this, EventArgs.Empty); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }
}
