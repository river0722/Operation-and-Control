namespace Operations_Control.Views
{
    partial class LogView
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        // Edit layout and control properties with the Visual Studio designer.
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.logGroup = new System.Windows.Forms.GroupBox();
            this.logLayout = new System.Windows.Forms.TableLayoutPanel();
            this.logHint = new System.Windows.Forms.Label();
            this.exportButton = new System.Windows.Forms.Button();
            this.clearButton = new System.Windows.Forms.Button();
            this.entries = new System.Windows.Forms.TextBox();
            this.logSaveDialog = new System.Windows.Forms.SaveFileDialog();
            this.components.Add(this.logSaveDialog);
            this.logGroup.SuspendLayout();
            this.logLayout.SuspendLayout();
            this.SuspendLayout();
            //
            // logGroup
            //
            this.logGroup.Name = "logGroup";
            this.logGroup.TabIndex = 0;
            this.logGroup.Text = "操作日志";
            this.logGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logGroup.Margin = new System.Windows.Forms.Padding(6);
            this.logGroup.Padding = new System.Windows.Forms.Padding(14, 8, 14, 10);
            this.logGroup.BackColor = System.Drawing.Color.FromArgb(255, 255, 255);
            this.logGroup.ForeColor = System.Drawing.Color.FromArgb(26, 39, 59);
            this.logGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.logGroup.TabStop = false;
            //
            // logLayout
            //
            this.logLayout.Name = "logLayout";
            this.logLayout.TabIndex = 1;
            this.logLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logLayout.Margin = new System.Windows.Forms.Padding(0);
            this.logLayout.Padding = new System.Windows.Forms.Padding(0);
            this.logLayout.ColumnCount = 3;
            this.logLayout.RowCount = 2;
            this.logLayout.BackColor = System.Drawing.Color.Transparent;
            //
            // logHint
            //
            this.logHint.Name = "logHint";
            this.logHint.TabIndex = 2;
            this.logHint.Text = "设备操作与状态记录";
            this.logHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.logHint.ForeColor = System.Drawing.Color.FromArgb(91, 105, 124);
            this.logHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logHint.Margin = new System.Windows.Forms.Padding(0);
            this.logHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // exportButton
            //
            this.exportButton.Name = "exportButton";
            this.exportButton.TabIndex = 3;
            this.exportButton.Text = "导出日志";
            this.exportButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.exportButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.exportButton.Margin = new System.Windows.Forms.Padding(4);
            this.exportButton.MinimumSize = new System.Drawing.Size(80, 34);
            this.exportButton.BackColor = System.Drawing.Color.FromArgb(255, 255, 255);
            this.exportButton.ForeColor = System.Drawing.Color.FromArgb(26, 39, 59);
            this.exportButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.exportButton.UseVisualStyleBackColor = false;
            this.exportButton.AccessibleName = "导出日志";
            this.exportButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(215, 222, 232);
            //
            // clearButton
            //
            this.clearButton.Name = "clearButton";
            this.clearButton.TabIndex = 4;
            this.clearButton.Text = "清空";
            this.clearButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.clearButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clearButton.Margin = new System.Windows.Forms.Padding(4);
            this.clearButton.MinimumSize = new System.Drawing.Size(80, 34);
            this.clearButton.BackColor = System.Drawing.Color.FromArgb(255, 255, 255);
            this.clearButton.ForeColor = System.Drawing.Color.FromArgb(26, 39, 59);
            this.clearButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearButton.UseVisualStyleBackColor = false;
            this.clearButton.AccessibleName = "清空";
            this.clearButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(215, 222, 232);
            //
            // entries
            //
            this.entries.Name = "entries";
            this.entries.TabIndex = 5;
            this.entries.Dock = System.Windows.Forms.DockStyle.Fill;
            this.entries.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.entries.Margin = new System.Windows.Forms.Padding(0);
            this.entries.Multiline = true;
            this.entries.AccessibleName = "操作日志";
            this.entries.ReadOnly = true;
            this.entries.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.entries.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.entries.BackColor = System.Drawing.Color.FromArgb(255, 255, 255);
            //
            // logSaveDialog
            //
            this.logSaveDialog.Filter = "文本日志 (*.txt)|*.txt";
            this.logLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 74F));
            this.logLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13F));
            this.logLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13F));
            this.logLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.logLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.logLayout.Controls.Add(this.logHint, 0, 0);
            this.logLayout.Controls.Add(this.exportButton, 1, 0);
            this.logLayout.Controls.Add(this.clearButton, 2, 0);
            this.logLayout.Controls.Add(this.entries, 0, 1);
            this.logLayout.SetColumnSpan(this.entries, 3);
            this.logGroup.Controls.Add(this.logLayout);
            this.Controls.Add(this.logGroup);
            this.exportButton.Click += new System.EventHandler(this.ExportLog);
            this.clearButton.Click += new System.EventHandler(this.ClearLog);
            //
            // LogView
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(244, 247, 251);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.Name = "LogView";
            this.Size = new System.Drawing.Size(1132, 136);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.logLayout.ResumeLayout(false);
            this.logLayout.PerformLayout();
            this.logGroup.ResumeLayout(false);
            this.logGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.GroupBox logGroup;
        private System.Windows.Forms.TableLayoutPanel logLayout;
        private System.Windows.Forms.Label logHint;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button clearButton;
        private System.Windows.Forms.TextBox entries;
        private System.Windows.Forms.SaveFileDialog logSaveDialog;
    }
}
