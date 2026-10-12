namespace Operations_Control
{
    partial class Form1
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.programOpenDialog = new System.Windows.Forms.OpenFileDialog();
            this.programSaveDialog = new System.Windows.Forms.SaveFileDialog();
            this.rootLayout = new System.Windows.Forms.TableLayoutPanel();
            this.topBar = new System.Windows.Forms.TableLayoutPanel();
            this.menu = new System.Windows.Forms.MenuStrip();
            this.helpMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.editorMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.logMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.clockLayout = new System.Windows.Forms.TableLayoutPanel();
            this.dateLabel = new System.Windows.Forms.Label();
            this.dateText = new System.Windows.Forms.TextBox();
            this.timeLabel = new System.Windows.Forms.Label();
            this.timeText = new System.Windows.Forms.TextBox();
            this.modeHint = new System.Windows.Forms.Label();
            this.quadrants = new System.Windows.Forms.TableLayoutPanel();
            this.connection = new Operations_Control.Views.ConnectionView();
            this.readouts = new Operations_Control.Views.ReadoutView();
            this.programs = new Operations_Control.Views.ProgramView();
            this.motion = new Operations_Control.Views.MotionView();
            this.canvases = new Operations_Control.Views.CanvasView();
            this.feedback = new System.Windows.Forms.Label();
            this.refreshTimer = new System.Windows.Forms.Timer(this.components);
            this.drawTimer = new System.Windows.Forms.Timer(this.components);
            this.rootLayout.SuspendLayout();
            this.topBar.SuspendLayout();
            this.menu.SuspendLayout();
            this.clockLayout.SuspendLayout();
            this.quadrants.SuspendLayout();
            this.SuspendLayout();
            // 
            // programOpenDialog
            // 
            this.programOpenDialog.Filter = "PMAC 程序 (*.txt;*.pmc)|*.txt;*.pmc|所有文件 (*.*)|*.*";
            // 
            // programSaveDialog
            // 
            this.programSaveDialog.FileName = "program.pmc";
            this.programSaveDialog.Filter = "PMAC 程序 (*.pmc)|*.pmc|文本文件 (*.txt)|*.txt";
            // 
            // rootLayout
            // 
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootLayout.Controls.Add(this.topBar, 0, 0);
            this.rootLayout.Controls.Add(this.quadrants, 0, 1);
            this.rootLayout.Controls.Add(this.canvases, 0, 2);
            this.rootLayout.Controls.Add(this.feedback, 0, 3);
            this.rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootLayout.Location = new System.Drawing.Point(6, 6);
            this.rootLayout.Margin = new System.Windows.Forms.Padding(0);
            this.rootLayout.Name = "rootLayout";
            this.rootLayout.RowCount = 4;
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 440F));
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.rootLayout.Size = new System.Drawing.Size(1168, 888);
            this.rootLayout.TabIndex = 0;
            // 
            // topBar
            // 
            this.topBar.ColumnCount = 2;
            this.topBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 44F));
            this.topBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56F));
            this.topBar.Controls.Add(this.menu, 0, 0);
            this.topBar.Controls.Add(this.clockLayout, 1, 0);
            this.topBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.topBar.Location = new System.Drawing.Point(0, 0);
            this.topBar.Margin = new System.Windows.Forms.Padding(0);
            this.topBar.Name = "topBar";
            this.topBar.RowCount = 1;
            this.topBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.topBar.Size = new System.Drawing.Size(1168, 32);
            this.topBar.TabIndex = 1;
            // 
            // menu
            // 
            this.menu.AutoSize = false;
            this.menu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.menu.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.helpMenu,
            this.editorMenu,
            this.logMenu,
            this.aboutMenu});
            this.menu.Location = new System.Drawing.Point(0, 0);
            this.menu.Name = "menu";
            this.menu.Padding = new System.Windows.Forms.Padding(0);
            this.menu.Size = new System.Drawing.Size(513, 32);
            this.menu.TabIndex = 2;
            // 
            // helpMenu
            // 
            this.helpMenu.Name = "helpMenu";
            this.helpMenu.Size = new System.Drawing.Size(49, 32);
            this.helpMenu.Text = "帮助";
            this.helpMenu.Click += new System.EventHandler(this.ShowHelp);
            // 
            // editorMenu
            // 
            this.editorMenu.Name = "editorMenu";
            this.editorMenu.Size = new System.Drawing.Size(77, 32);
            this.editorMenu.Text = "程序编辑";
            this.editorMenu.Click += new System.EventHandler(this.ShowEditor);
            // 
            // logMenu
            // 
            this.logMenu.Name = "logMenu";
            this.logMenu.Size = new System.Drawing.Size(77, 32);
            this.logMenu.Text = "操作日志";
            this.logMenu.Click += new System.EventHandler(this.ShowLog);
            // 
            // aboutMenu
            // 
            this.aboutMenu.Name = "aboutMenu";
            this.aboutMenu.Size = new System.Drawing.Size(49, 32);
            this.aboutMenu.Text = "关于";
            this.aboutMenu.Click += new System.EventHandler(this.ShowAbout);
            // 
            // clockLayout
            // 
            this.clockLayout.ColumnCount = 5;
            this.clockLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.clockLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.clockLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.clockLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.clockLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18F));
            this.clockLayout.Controls.Add(this.dateLabel, 0, 0);
            this.clockLayout.Controls.Add(this.dateText, 1, 0);
            this.clockLayout.Controls.Add(this.timeLabel, 2, 0);
            this.clockLayout.Controls.Add(this.timeText, 3, 0);
            this.clockLayout.Controls.Add(this.modeHint, 4, 0);
            this.clockLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clockLayout.Location = new System.Drawing.Point(513, 0);
            this.clockLayout.Margin = new System.Windows.Forms.Padding(0);
            this.clockLayout.Name = "clockLayout";
            this.clockLayout.RowCount = 1;
            this.clockLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.clockLayout.Size = new System.Drawing.Size(655, 32);
            this.clockLayout.TabIndex = 7;
            // 
            // dateLabel
            // 
            this.dateLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dateLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.dateLabel.Location = new System.Drawing.Point(2, 2);
            this.dateLabel.Margin = new System.Windows.Forms.Padding(2);
            this.dateLabel.Name = "dateLabel";
            this.dateLabel.Size = new System.Drawing.Size(74, 28);
            this.dateLabel.TabIndex = 8;
            this.dateLabel.Text = "日期";
            this.dateLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // dateText
            // 
            this.dateText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dateText.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.dateText.Location = new System.Drawing.Point(81, 6);
            this.dateText.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.dateText.Name = "dateText";
            this.dateText.ReadOnly = true;
            this.dateText.Size = new System.Drawing.Size(210, 24);
            this.dateText.TabIndex = 9;
            this.dateText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // timeLabel
            // 
            this.timeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.timeLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.timeLabel.Location = new System.Drawing.Point(296, 2);
            this.timeLabel.Margin = new System.Windows.Forms.Padding(2);
            this.timeLabel.Name = "timeLabel";
            this.timeLabel.Size = new System.Drawing.Size(74, 28);
            this.timeLabel.TabIndex = 10;
            this.timeLabel.Text = "时间";
            this.timeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // timeText
            // 
            this.timeText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.timeText.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.timeText.Location = new System.Drawing.Point(375, 6);
            this.timeText.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.timeText.Name = "timeText";
            this.timeText.ReadOnly = true;
            this.timeText.Size = new System.Drawing.Size(157, 24);
            this.timeText.TabIndex = 11;
            this.timeText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // modeHint
            // 
            this.modeHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modeHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.modeHint.Location = new System.Drawing.Point(537, 2);
            this.modeHint.Margin = new System.Windows.Forms.Padding(2);
            this.modeHint.Name = "modeHint";
            this.modeHint.Size = new System.Drawing.Size(116, 28);
            this.modeHint.TabIndex = 12;
            this.modeHint.Text = "模拟模式";
            this.modeHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // quadrants
            // 
            this.quadrants.ColumnCount = 2;
            this.quadrants.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36F));
            this.quadrants.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 64F));
            this.quadrants.Controls.Add(this.connection, 0, 0);
            this.quadrants.Controls.Add(this.readouts, 1, 0);
            this.quadrants.Controls.Add(this.programs, 0, 1);
            this.quadrants.Controls.Add(this.motion, 1, 1);
            this.quadrants.Dock = System.Windows.Forms.DockStyle.Fill;
            this.quadrants.Location = new System.Drawing.Point(0, 32);
            this.quadrants.Margin = new System.Windows.Forms.Padding(0);
            this.quadrants.Name = "quadrants";
            this.quadrants.RowCount = 2;
            this.quadrants.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.quadrants.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 58F));
            this.quadrants.Size = new System.Drawing.Size(1168, 440);
            this.quadrants.TabIndex = 13;
            // 
            // connection
            // 
            this.connection.BackColor = System.Drawing.SystemColors.Control;
            this.connection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.connection.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.connection.Location = new System.Drawing.Point(0, 0);
            this.connection.Margin = new System.Windows.Forms.Padding(0);
            this.connection.Name = "connection";
            this.connection.Size = new System.Drawing.Size(420, 184);
            this.connection.TabIndex = 14;
            // 
            // readouts
            // 
            this.readouts.BackColor = System.Drawing.SystemColors.Control;
            this.readouts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.readouts.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.readouts.Location = new System.Drawing.Point(420, 0);
            this.readouts.Margin = new System.Windows.Forms.Padding(0);
            this.readouts.Name = "readouts";
            this.readouts.Size = new System.Drawing.Size(748, 184);
            this.readouts.TabIndex = 15;
            // 
            // programs
            // 
            this.programs.BackColor = System.Drawing.SystemColors.Control;
            this.programs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.programs.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.programs.Location = new System.Drawing.Point(0, 184);
            this.programs.Margin = new System.Windows.Forms.Padding(0);
            this.programs.Name = "programs";
            this.programs.Size = new System.Drawing.Size(420, 256);
            this.programs.TabIndex = 16;
            // 
            // motion
            // 
            this.motion.BackColor = System.Drawing.SystemColors.Control;
            this.motion.Dock = System.Windows.Forms.DockStyle.Fill;
            this.motion.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.motion.Location = new System.Drawing.Point(420, 184);
            this.motion.Margin = new System.Windows.Forms.Padding(0);
            this.motion.Name = "motion";
            this.motion.Size = new System.Drawing.Size(748, 256);
            this.motion.TabIndex = 17;
            // 
            // canvases
            // 
            this.canvases.BackColor = System.Drawing.SystemColors.Control;
            this.canvases.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvases.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.canvases.Location = new System.Drawing.Point(0, 472);
            this.canvases.Margin = new System.Windows.Forms.Padding(0);
            this.canvases.Name = "canvases";
            this.canvases.Size = new System.Drawing.Size(1168, 388);
            this.canvases.TabIndex = 18;
            // 
            // feedback
            // 
            this.feedback.AutoEllipsis = true;
            this.feedback.Dock = System.Windows.Forms.DockStyle.Fill;
            this.feedback.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.feedback.Location = new System.Drawing.Point(2, 862);
            this.feedback.Margin = new System.Windows.Forms.Padding(2);
            this.feedback.Name = "feedback";
            this.feedback.Size = new System.Drawing.Size(1164, 24);
            this.feedback.TabIndex = 19;
            this.feedback.Text = "就绪 · 请选择连接模式";
            this.feedback.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // refreshTimer
            // 
            this.refreshTimer.Tick += new System.EventHandler(this.RefreshData);
            // 
            // drawTimer
            // 
            this.drawTimer.Tick += new System.EventHandler(this.PumpDrawing);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(1180, 900);
            this.Controls.Add(this.rootLayout);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MainMenuStrip = this.menu;
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MinimumSize = new System.Drawing.Size(1040, 850);
            this.Name = "Form1";
            this.Padding = new System.Windows.Forms.Padding(6);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PMAC 上位机控制程序";
            this.rootLayout.ResumeLayout(false);
            this.topBar.ResumeLayout(false);
            this.menu.ResumeLayout(false);
            this.menu.PerformLayout();
            this.clockLayout.ResumeLayout(false);
            this.clockLayout.PerformLayout();
            this.quadrants.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.TableLayoutPanel topBar;
        private System.Windows.Forms.MenuStrip menu;
        private System.Windows.Forms.ToolStripMenuItem helpMenu;
        private System.Windows.Forms.ToolStripMenuItem editorMenu;
        private System.Windows.Forms.ToolStripMenuItem logMenu;
        private System.Windows.Forms.ToolStripMenuItem aboutMenu;
        private System.Windows.Forms.TableLayoutPanel clockLayout;
        private System.Windows.Forms.Label dateLabel;
        private System.Windows.Forms.TextBox dateText;
        private System.Windows.Forms.Label timeLabel;
        private System.Windows.Forms.TextBox timeText;
        private System.Windows.Forms.Label modeHint;
        private System.Windows.Forms.TableLayoutPanel quadrants;
        private Operations_Control.Views.ConnectionView connection;
        private Operations_Control.Views.ReadoutView readouts;
        private Operations_Control.Views.ProgramView programs;
        private Operations_Control.Views.MotionView motion;
        private Operations_Control.Views.CanvasView canvases;
        private System.Windows.Forms.Label feedback;
        private System.Windows.Forms.Timer refreshTimer;
        private System.Windows.Forms.Timer drawTimer;
        private System.Windows.Forms.OpenFileDialog programOpenDialog;
        private System.Windows.Forms.SaveFileDialog programSaveDialog;
    }
}
