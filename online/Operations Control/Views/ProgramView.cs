using System;
using System.Windows.Forms;

namespace Operations_Control.Views
{
    public partial class ProgramView : UserControl
    {
        public ProgramView() { InitializeComponent(); }

        public void SetState(bool connected, bool busy, bool hasFile)
        {
            SendButton.Enabled = connected && !busy;
            Command.Enabled = !busy;
            DownloadButton.Enabled = connected && !busy && hasFile;
            BrowseButton.Enabled = SaveButton.Enabled = EditButton.Enabled = !busy;
        }
    }
}
