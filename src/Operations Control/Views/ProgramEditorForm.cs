using System;
using System.Windows.Forms;

namespace Operations_Control.Views
{
    public partial class ProgramEditorForm : Form
    {
        public event EventHandler SaveRequested;
        public ProgramEditorForm() { InitializeComponent(); }
        public void HideSaveToolbar() { saveButton.Visible = false; editorLayout.RowStyles[0].Height = 0; Padding = new Padding(0); }
        private void RequestSave(object sender, EventArgs e) { if (SaveRequested != null) SaveRequested(this, EventArgs.Empty); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }
}
