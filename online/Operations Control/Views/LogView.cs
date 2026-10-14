using System;
using Operations_Control.Files;
using System.Text;
using System.Windows.Forms;

namespace Operations_Control.Views
{
    public partial class LogView : UserControl
    {
        private int count;
        public string LogDirectory { get; set; }
        public string PersistenceError { get; private set; }
        public LogView() { InitializeComponent(); LogDirectory = System.IO.Path.Combine(DeviceConfigurationStore.DirectoryPath, "logs"); }

        private void ClearLog(object sender, EventArgs e) { entries.Clear(); count = 0; }

        private void ExportLog(object sender, EventArgs e)
        {
            logSaveDialog.FileName = "PMAC-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            if (logSaveDialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            try { ProgramFileStore.Save(logSaveDialog.FileName, entries.Text, Encoding.UTF8); Append("信息", "日志已导出。"); }
            catch (Exception error) { Append("错误", "导出失败：" + error.Message); }
        }

        public void Append(string level, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  [" + level + "]  " + message.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine;
            try
            {
                System.IO.Directory.CreateDirectory(LogDirectory);
                System.IO.File.AppendAllText(System.IO.Path.Combine(LogDirectory, DateTime.Now.ToString("yyyyMMdd") + ".log"), line, new UTF8Encoding(false));
                PersistenceError = null;
            }
            catch (Exception error) { PersistenceError = "日志写入失败：" + error.Message; }
            if (count >= 500) { int end = entries.Text.IndexOf(Environment.NewLine); if (end >= 0) entries.Text = entries.Text.Substring(end + Environment.NewLine.Length); count--; }
            entries.AppendText(line);
            if (PersistenceError != null) entries.AppendText(PersistenceError + Environment.NewLine);
            count++;
        }
    }
}
