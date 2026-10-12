using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Operations_Control.Views
{
    public partial class LogView : UserControl
    {
        private int count;
        public LogView() { InitializeComponent(); }

        private void ClearLog(object sender, EventArgs e) { entries.Clear(); count = 0; }

        private void ExportLog(object sender, EventArgs e)
        {
            logSaveDialog.FileName = "PMAC-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            if (logSaveDialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            try { File.WriteAllText(logSaveDialog.FileName, entries.Text, Encoding.UTF8); Append("信息", "日志已导出。"); }
            catch (Exception error) { Append("错误", "导出失败：" + error.Message); }
        }

        public void Append(string level, string message)
        {
            if (count >= 500) { entries.Clear(); count = 0; }
            entries.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  [" + level + "]  " + message.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine);
            count++;
        }
    }
}
