namespace Operations_Control.Views
{
    partial class ProgramView
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.programGroup = new System.Windows.Forms.GroupBox();
            this.programLayout = new System.Windows.Forms.TableLayoutPanel();
            this.commandLabel = new System.Windows.Forms.Label();
            this.Command = new System.Windows.Forms.TextBox();
            this.SendButton = new System.Windows.Forms.Button();
            this.fileLabel = new System.Windows.Forms.Label();
            this.FileLabel = new System.Windows.Forms.TextBox();
            this.BrowseButton = new System.Windows.Forms.Button();
            this.fileButtons = new System.Windows.Forms.TableLayoutPanel();
            this.EditButton = new System.Windows.Forms.Button();
            this.SaveButton = new System.Windows.Forms.Button();
            this.DownloadButton = new System.Windows.Forms.Button();
            this.responseLabel = new System.Windows.Forms.Label();
            this.Response = new System.Windows.Forms.TextBox();
            this.programGroup.SuspendLayout();
            this.programLayout.SuspendLayout();
            this.fileButtons.SuspendLayout();
            this.SuspendLayout();
            // programGroup
            this.programGroup.Name = "programGroup";
            this.programGroup.TabIndex = 0;
            this.programGroup.Text = "程序控制";
            this.programGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.programGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.programGroup.Margin = new System.Windows.Forms.Padding(3);
            this.programGroup.Padding = new System.Windows.Forms.Padding(10, 6, 10, 8);
            this.programGroup.TabStop = false;
            // programLayout
            this.programLayout.Name = "programLayout";
            this.programLayout.TabIndex = 1;
            this.programLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.programLayout.Margin = new System.Windows.Forms.Padding(0);
            this.programLayout.Padding = new System.Windows.Forms.Padding(0);
            this.programLayout.ColumnCount = 3;
            this.programLayout.RowCount = 5;
            // commandLabel
            this.commandLabel.Name = "commandLabel";
            this.commandLabel.TabIndex = 2;
            this.commandLabel.Text = "命令";
            this.commandLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.commandLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.commandLabel.Margin = new System.Windows.Forms.Padding(2);
            this.commandLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // Command
            this.Command.Name = "Command";
            this.Command.TabIndex = 3;
            this.Command.Text = "";
            this.Command.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Regular);
            this.Command.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Command.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.Command.Multiline = false;
            this.Command.MaxLength = 1024;
            // SendButton
            this.SendButton.Name = "SendButton";
            this.SendButton.TabIndex = 4;
            this.SendButton.Text = "发送";
            this.SendButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SendButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.SendButton.Margin = new System.Windows.Forms.Padding(3);
            this.SendButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.SendButton.UseVisualStyleBackColor = true;
            this.SendButton.AccessibleName = "发送";
            this.SendButton.Enabled = false;
            // fileLabel
            this.fileLabel.Name = "fileLabel";
            this.fileLabel.TabIndex = 5;
            this.fileLabel.Text = "程序文件";
            this.fileLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.fileLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileLabel.Margin = new System.Windows.Forms.Padding(2);
            this.fileLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // FileLabel
            this.FileLabel.Name = "FileLabel";
            this.FileLabel.TabIndex = 6;
            this.FileLabel.Text = "尚未选择文件";
            this.FileLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.FileLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FileLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.FileLabel.Multiline = false;
            this.FileLabel.ReadOnly = true;
            // BrowseButton
            this.BrowseButton.Name = "BrowseButton";
            this.BrowseButton.TabIndex = 7;
            this.BrowseButton.Text = "选择";
            this.BrowseButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.BrowseButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.BrowseButton.Margin = new System.Windows.Forms.Padding(3);
            this.BrowseButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.BrowseButton.UseVisualStyleBackColor = true;
            this.BrowseButton.AccessibleName = "选择";
            // fileButtons
            this.fileButtons.Name = "fileButtons";
            this.fileButtons.TabIndex = 8;
            this.fileButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileButtons.Margin = new System.Windows.Forms.Padding(0);
            this.fileButtons.Padding = new System.Windows.Forms.Padding(0);
            this.fileButtons.ColumnCount = 3;
            this.fileButtons.RowCount = 1;
            // EditButton
            this.EditButton.Name = "EditButton";
            this.EditButton.TabIndex = 9;
            this.EditButton.Text = "打开/编辑";
            this.EditButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.EditButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.EditButton.Margin = new System.Windows.Forms.Padding(3);
            this.EditButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.EditButton.UseVisualStyleBackColor = true;
            this.EditButton.AccessibleName = "打开/编辑";
            // SaveButton
            this.SaveButton.Name = "SaveButton";
            this.SaveButton.TabIndex = 10;
            this.SaveButton.Text = "另存为";
            this.SaveButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SaveButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.SaveButton.Margin = new System.Windows.Forms.Padding(3);
            this.SaveButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.SaveButton.UseVisualStyleBackColor = true;
            this.SaveButton.AccessibleName = "另存为";
            // DownloadButton
            this.DownloadButton.Name = "DownloadButton";
            this.DownloadButton.TabIndex = 11;
            this.DownloadButton.Text = "文件下载";
            this.DownloadButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DownloadButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.DownloadButton.Margin = new System.Windows.Forms.Padding(3);
            this.DownloadButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.DownloadButton.UseVisualStyleBackColor = true;
            this.DownloadButton.AccessibleName = "文件下载";
            this.DownloadButton.Enabled = false;
            // responseLabel
            this.responseLabel.Name = "responseLabel";
            this.responseLabel.TabIndex = 12;
            this.responseLabel.Text = "控制器应答";
            this.responseLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.responseLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.responseLabel.Margin = new System.Windows.Forms.Padding(2);
            this.responseLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // Response
            this.Response.Name = "Response";
            this.Response.TabIndex = 13;
            this.Response.Text = "连接后显示命令应答";
            this.Response.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular);
            this.Response.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Response.Margin = new System.Windows.Forms.Padding(3);
            this.Response.Multiline = true;
            this.Response.ReadOnly = true;
            this.Response.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.Response.WordWrap = false;
            this.programLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 22F));
            this.programLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 54F));
            this.programLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.programLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.programLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.programLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.programLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.programLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.programLayout.Controls.Add(this.commandLabel, 0, 0);
            this.programLayout.Controls.Add(this.Command, 1, 0);
            this.programLayout.Controls.Add(this.SendButton, 2, 0);
            this.programLayout.Controls.Add(this.fileLabel, 0, 1);
            this.programLayout.Controls.Add(this.FileLabel, 1, 1);
            this.programLayout.Controls.Add(this.BrowseButton, 2, 1);
            this.fileButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 34F));
            this.fileButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.fileButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.fileButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.fileButtons.Controls.Add(this.EditButton, 0, 0);
            this.fileButtons.Controls.Add(this.SaveButton, 1, 0);
            this.fileButtons.Controls.Add(this.DownloadButton, 2, 0);
            this.programLayout.Controls.Add(this.fileButtons, 0, 2);
            this.programLayout.SetColumnSpan(this.fileButtons, 3);
            this.programLayout.Controls.Add(this.responseLabel, 0, 3);
            this.programLayout.SetColumnSpan(this.responseLabel, 3);
            this.programLayout.Controls.Add(this.Response, 0, 4);
            this.programLayout.SetColumnSpan(this.Response, 3);
            this.programGroup.Controls.Add(this.programLayout);
            this.Controls.Add(this.programGroup);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "ProgramView";
            this.Size = new System.Drawing.Size(400, 255);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.fileButtons.ResumeLayout(false);
            this.fileButtons.PerformLayout();
            this.programLayout.ResumeLayout(false);
            this.programLayout.PerformLayout();
            this.programGroup.ResumeLayout(false);
            this.programGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.GroupBox programGroup;
        private System.Windows.Forms.TableLayoutPanel programLayout;
        private System.Windows.Forms.Label commandLabel;
        public System.Windows.Forms.TextBox Command;
        public System.Windows.Forms.Button SendButton;
        private System.Windows.Forms.Label fileLabel;
        public System.Windows.Forms.TextBox FileLabel;
        public System.Windows.Forms.Button BrowseButton;
        private System.Windows.Forms.TableLayoutPanel fileButtons;
        public System.Windows.Forms.Button EditButton;
        public System.Windows.Forms.Button SaveButton;
        public System.Windows.Forms.Button DownloadButton;
        private System.Windows.Forms.Label responseLabel;
        public System.Windows.Forms.TextBox Response;
    }
}
