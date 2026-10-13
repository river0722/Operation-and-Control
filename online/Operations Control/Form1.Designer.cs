namespace Operations_Control
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.TableLayoutPanel rootLayout, header, manualLayout;
        private System.Windows.Forms.Panel contentPanel;
        private System.Windows.Forms.TabControl workspaceTabs;
        private System.Windows.Forms.TabPage drawingPage, manualPage, filePage, logPage;
        private Operations_Control.Views.ConnectionView connection;
        private Operations_Control.Views.ReadoutView readouts;
        private Operations_Control.Views.ProgramView programs;
        private Operations_Control.Views.MotionView motion;
        private Operations_Control.Views.CanvasView canvases;
        private System.Windows.Forms.Label feedback, xPosition, yPosition;
        private System.Windows.Forms.Button globalStop;
        private System.Windows.Forms.Timer refreshTimer, drawTimer;
        private System.Windows.Forms.OpenFileDialog programOpenDialog;
        private System.Windows.Forms.SaveFileDialog programSaveDialog;
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            programOpenDialog = new System.Windows.Forms.OpenFileDialog { Filter = "PMAC 程序 (*.txt;*.pmc)|*.txt;*.pmc|所有文件 (*.*)|*.*" };
            programSaveDialog = new System.Windows.Forms.SaveFileDialog { FileName = "program.pmc", Filter = "PMAC 程序 (*.pmc)|*.pmc|文本文件 (*.txt)|*.txt" };
            rootLayout = new System.Windows.Forms.TableLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new System.Windows.Forms.Padding(0) };
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 82));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28));
            header = new System.Windows.Forms.TableLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new System.Windows.Forms.Padding(0) };
            header.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 66));
            header.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17));
            header.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17));
            header.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100));
            connection = new Operations_Control.Views.ConnectionView { Dock = System.Windows.Forms.DockStyle.Fill };
            xPosition = new System.Windows.Forms.Label { Text = "X —", Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Font = new System.Drawing.Font("Consolas", 11) };
            yPosition = new System.Windows.Forms.Label { Text = "Y —", Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Font = new System.Drawing.Font("Consolas", 11) };
            globalStop = new System.Windows.Forms.Button { Text = "停止", Dock = System.Windows.Forms.DockStyle.Fill, Margin = new System.Windows.Forms.Padding(6, 22, 6, 18), ForeColor = System.Drawing.Color.Firebrick, Font = new System.Drawing.Font("Microsoft YaHei UI", 11, System.Drawing.FontStyle.Bold) };
            header.Controls.Add(connection, 0, 0); header.Controls.Add(xPosition, 1, 0); header.Controls.Add(yPosition, 2, 0); header.Controls.Add(globalStop, 3, 0);
            contentPanel = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill, Margin = new System.Windows.Forms.Padding(0) };
            workspaceTabs = new System.Windows.Forms.TabControl { Dock = System.Windows.Forms.DockStyle.Fill };
            drawingPage = new System.Windows.Forms.TabPage("绘图与运行"); manualPage = new System.Windows.Forms.TabPage("手动控制"); filePage = new System.Windows.Forms.TabPage("程序");
            canvases = new Operations_Control.Views.CanvasView { Dock = System.Windows.Forms.DockStyle.Fill };
            readouts = new Operations_Control.Views.ReadoutView { Dock = System.Windows.Forms.DockStyle.Fill };
            motion = new Operations_Control.Views.MotionView { Dock = System.Windows.Forms.DockStyle.Fill };
            programs = new Operations_Control.Views.ProgramView { Dock = System.Windows.Forms.DockStyle.Fill };
            drawingPage.Controls.Add(canvases);
            manualLayout = new System.Windows.Forms.TableLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            manualLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 180));
            manualLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 260));
            manualLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            manualLayout.Controls.Add(readouts, 0, 0); manualLayout.Controls.Add(motion, 0, 1); manualPage.Controls.Add(manualLayout);
            filePage.Controls.Add(programs); workspaceTabs.TabPages.AddRange(new System.Windows.Forms.TabPage[] { drawingPage, manualPage, filePage }); contentPanel.Controls.Add(workspaceTabs);
            feedback = new System.Windows.Forms.Label { Dock = System.Windows.Forms.DockStyle.Fill, AutoEllipsis = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Text = "未连接", Margin = new System.Windows.Forms.Padding(3) };
            refreshTimer = new System.Windows.Forms.Timer(components); refreshTimer.Interval = 100; refreshTimer.Tick += RefreshData;
            drawTimer = new System.Windows.Forms.Timer(components); drawTimer.Interval = 100; drawTimer.Tick += PumpDrawing;
            rootLayout.Controls.Add(header, 0, 0); rootLayout.Controls.Add(contentPanel, 0, 1); rootLayout.Controls.Add(feedback, 0, 2);
            Controls.Add(rootLayout); AutoScaleDimensions = new System.Drawing.SizeF(96, 96); AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10); Padding = new System.Windows.Forms.Padding(6);
            BackColor = System.Drawing.SystemColors.Control; ClientSize = new System.Drawing.Size(1180, 820); MinimumSize = new System.Drawing.Size(1040, 720);
            Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon"))); KeyPreview = true; StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Name = "Form1"; Text = "PMAC 上位机控制程序";
        }
    }
}
