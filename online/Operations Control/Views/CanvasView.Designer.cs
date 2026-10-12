namespace Operations_Control.Views
{
    partial class CanvasView
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
            this.canvasLayout = new System.Windows.Forms.TableLayoutPanel();
            this.settingsLayout = new System.Windows.Forms.TableLayoutPanel();
            this.xRangeLabel = new System.Windows.Forms.Label();
            this.XMin = new System.Windows.Forms.NumericUpDown();
            this.xSeparator = new System.Windows.Forms.Label();
            this.XMax = new System.Windows.Forms.NumericUpDown();
            this.yRangeLabel = new System.Windows.Forms.Label();
            this.YMin = new System.Windows.Forms.NumericUpDown();
            this.ySeparator = new System.Windows.Forms.Label();
            this.YMax = new System.Windows.Forms.NumericUpDown();
            this.xScaleLabel = new System.Windows.Forms.Label();
            this.XScale = new System.Windows.Forms.NumericUpDown();
            this.yScaleLabel = new System.Windows.Forms.Label();
            this.YScale = new System.Windows.Forms.NumericUpDown();
            this.applyButton = new System.Windows.Forms.Button();
            this.plotsLayout = new System.Windows.Forms.TableLayoutPanel();
            this.traceGroup = new System.Windows.Forms.GroupBox();
            this.drawGroup = new System.Windows.Forms.GroupBox();
            this.traceLayout = new System.Windows.Forms.TableLayoutPanel();
            this.ActualCanvas = new Operations_Control.Controls.MotionCanvas();
            this.traceFooter = new System.Windows.Forms.TableLayoutPanel();
            this.traceInfo = new System.Windows.Forms.Label();
            this.clearTraceButton = new System.Windows.Forms.Button();
            this.drawLayout = new System.Windows.Forms.TableLayoutPanel();
            this.DrawingCanvas = new Operations_Control.Controls.MotionCanvas();
            this.drawFooter = new System.Windows.Forms.TableLayoutPanel();
            this.Follow = new System.Windows.Forms.CheckBox();
            this.drawInfo = new System.Windows.Forms.Label();
            this.clearDrawButton = new System.Windows.Forms.Button();
            this.canvasLayout.SuspendLayout();
            this.settingsLayout.SuspendLayout();
            this.plotsLayout.SuspendLayout();
            this.traceGroup.SuspendLayout();
            this.drawGroup.SuspendLayout();
            this.traceLayout.SuspendLayout();
            this.traceFooter.SuspendLayout();
            this.drawLayout.SuspendLayout();
            this.drawFooter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.XMin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.XMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.YMin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.YMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.XScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.YScale)).BeginInit();
            this.SuspendLayout();
            // canvasLayout
            this.canvasLayout.Name = "canvasLayout";
            this.canvasLayout.TabIndex = 0;
            this.canvasLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvasLayout.Margin = new System.Windows.Forms.Padding(0);
            this.canvasLayout.Padding = new System.Windows.Forms.Padding(0);
            this.canvasLayout.ColumnCount = 1;
            this.canvasLayout.RowCount = 2;
            // settingsLayout
            this.settingsLayout.Name = "settingsLayout";
            this.settingsLayout.TabIndex = 1;
            this.settingsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.settingsLayout.Margin = new System.Windows.Forms.Padding(0);
            this.settingsLayout.Padding = new System.Windows.Forms.Padding(0);
            this.settingsLayout.ColumnCount = 13;
            this.settingsLayout.RowCount = 1;
            // xRangeLabel
            this.xRangeLabel.Name = "xRangeLabel";
            this.xRangeLabel.TabIndex = 2;
            this.xRangeLabel.Text = "X mm";
            this.xRangeLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.xRangeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.xRangeLabel.Margin = new System.Windows.Forms.Padding(2);
            this.xRangeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // XMin
            this.XMin.Name = "XMin";
            this.XMin.TabIndex = 3;
            this.XMin.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.XMin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.XMin.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.XMin.Minimum = -1000000M;
            this.XMin.Maximum = 1000000M;
            this.XMin.Value = -10M;
            this.XMin.DecimalPlaces = 3;
            this.XMin.Increment = 0.1M;
            // xSeparator
            this.xSeparator.Name = "xSeparator";
            this.xSeparator.TabIndex = 4;
            this.xSeparator.Text = "～";
            this.xSeparator.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.xSeparator.Dock = System.Windows.Forms.DockStyle.Fill;
            this.xSeparator.Margin = new System.Windows.Forms.Padding(2);
            this.xSeparator.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // XMax
            this.XMax.Name = "XMax";
            this.XMax.TabIndex = 5;
            this.XMax.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.XMax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.XMax.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.XMax.Minimum = -1000000M;
            this.XMax.Maximum = 1000000M;
            this.XMax.Value = 10M;
            this.XMax.DecimalPlaces = 3;
            this.XMax.Increment = 0.1M;
            // yRangeLabel
            this.yRangeLabel.Name = "yRangeLabel";
            this.yRangeLabel.TabIndex = 6;
            this.yRangeLabel.Text = "Y mm";
            this.yRangeLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.yRangeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.yRangeLabel.Margin = new System.Windows.Forms.Padding(2);
            this.yRangeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // YMin
            this.YMin.Name = "YMin";
            this.YMin.TabIndex = 7;
            this.YMin.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.YMin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.YMin.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.YMin.Minimum = -1000000M;
            this.YMin.Maximum = 1000000M;
            this.YMin.Value = -10M;
            this.YMin.DecimalPlaces = 3;
            this.YMin.Increment = 0.1M;
            // ySeparator
            this.ySeparator.Name = "ySeparator";
            this.ySeparator.TabIndex = 8;
            this.ySeparator.Text = "～";
            this.ySeparator.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.ySeparator.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ySeparator.Margin = new System.Windows.Forms.Padding(2);
            this.ySeparator.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // YMax
            this.YMax.Name = "YMax";
            this.YMax.TabIndex = 9;
            this.YMax.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.YMax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.YMax.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.YMax.Minimum = -1000000M;
            this.YMax.Maximum = 1000000M;
            this.YMax.Value = 10M;
            this.YMax.DecimalPlaces = 3;
            this.YMax.Increment = 0.1M;
            // xScaleLabel
            this.xScaleLabel.Name = "xScaleLabel";
            this.xScaleLabel.TabIndex = 10;
            this.xScaleLabel.Text = "X ct/mm";
            this.xScaleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.xScaleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.xScaleLabel.Margin = new System.Windows.Forms.Padding(2);
            this.xScaleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // XScale
            this.XScale.Name = "XScale";
            this.XScale.TabIndex = 11;
            this.XScale.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.XScale.Dock = System.Windows.Forms.DockStyle.Fill;
            this.XScale.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.XScale.Minimum = 0M;
            this.XScale.Maximum = 1000000M;
            this.XScale.Value = 100M;
            this.XScale.DecimalPlaces = 3;
            this.XScale.Increment = 0.1M;
            // yScaleLabel
            this.yScaleLabel.Name = "yScaleLabel";
            this.yScaleLabel.TabIndex = 12;
            this.yScaleLabel.Text = "Y ct/mm";
            this.yScaleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.yScaleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.yScaleLabel.Margin = new System.Windows.Forms.Padding(2);
            this.yScaleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // YScale
            this.YScale.Name = "YScale";
            this.YScale.TabIndex = 13;
            this.YScale.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.YScale.Dock = System.Windows.Forms.DockStyle.Fill;
            this.YScale.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.YScale.Minimum = 0M;
            this.YScale.Maximum = 1000000M;
            this.YScale.Value = 100M;
            this.YScale.DecimalPlaces = 3;
            this.YScale.Increment = 0.1M;
            // applyButton
            this.applyButton.Name = "applyButton";
            this.applyButton.TabIndex = 14;
            this.applyButton.Text = "应用坐标";
            this.applyButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.applyButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.applyButton.Margin = new System.Windows.Forms.Padding(3);
            this.applyButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.applyButton.UseVisualStyleBackColor = true;
            this.applyButton.AccessibleName = "应用坐标";
            // plotsLayout
            this.plotsLayout.Name = "plotsLayout";
            this.plotsLayout.TabIndex = 15;
            this.plotsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.plotsLayout.Margin = new System.Windows.Forms.Padding(0);
            this.plotsLayout.Padding = new System.Windows.Forms.Padding(0);
            this.plotsLayout.ColumnCount = 2;
            this.plotsLayout.RowCount = 1;
            // traceGroup
            this.traceGroup.Name = "traceGroup";
            this.traceGroup.TabIndex = 16;
            this.traceGroup.Text = "平台实时运动轨迹 / mm";
            this.traceGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.traceGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.traceGroup.Margin = new System.Windows.Forms.Padding(3);
            this.traceGroup.Padding = new System.Windows.Forms.Padding(10, 6, 10, 8);
            this.traceGroup.TabStop = false;
            // drawGroup
            this.drawGroup.Name = "drawGroup";
            this.drawGroup.TabIndex = 17;
            this.drawGroup.Text = "鼠标绘图与实时跟随 / mm";
            this.drawGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.drawGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.drawGroup.Margin = new System.Windows.Forms.Padding(3);
            this.drawGroup.Padding = new System.Windows.Forms.Padding(10, 6, 10, 8);
            this.drawGroup.TabStop = false;
            // traceLayout
            this.traceLayout.Name = "traceLayout";
            this.traceLayout.TabIndex = 18;
            this.traceLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.traceLayout.Margin = new System.Windows.Forms.Padding(0);
            this.traceLayout.Padding = new System.Windows.Forms.Padding(0);
            this.traceLayout.ColumnCount = 1;
            this.traceLayout.RowCount = 2;
            // ActualCanvas
            this.ActualCanvas.Name = "ActualCanvas";
            this.ActualCanvas.TabIndex = 19;
            this.ActualCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ActualCanvas.Margin = new System.Windows.Forms.Padding(0);
            this.ActualCanvas.AccessibleName = "实际反馈轨迹画布";
            // traceFooter
            this.traceFooter.Name = "traceFooter";
            this.traceFooter.TabIndex = 20;
            this.traceFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.traceFooter.Margin = new System.Windows.Forms.Padding(0);
            this.traceFooter.Padding = new System.Windows.Forms.Padding(0);
            this.traceFooter.ColumnCount = 2;
            this.traceFooter.RowCount = 1;
            // traceInfo
            this.traceInfo.Name = "traceInfo";
            this.traceInfo.TabIndex = 21;
            this.traceInfo.Text = "模拟比例 100 ct/mm";
            this.traceInfo.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.traceInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.traceInfo.Margin = new System.Windows.Forms.Padding(2);
            this.traceInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // clearTraceButton
            this.clearTraceButton.Name = "clearTraceButton";
            this.clearTraceButton.TabIndex = 22;
            this.clearTraceButton.Text = "清除轨迹";
            this.clearTraceButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clearTraceButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.clearTraceButton.Margin = new System.Windows.Forms.Padding(3);
            this.clearTraceButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.clearTraceButton.UseVisualStyleBackColor = true;
            this.clearTraceButton.AccessibleName = "清除轨迹";
            // drawLayout
            this.drawLayout.Name = "drawLayout";
            this.drawLayout.TabIndex = 23;
            this.drawLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.drawLayout.Margin = new System.Windows.Forms.Padding(0);
            this.drawLayout.Padding = new System.Windows.Forms.Padding(0);
            this.drawLayout.ColumnCount = 1;
            this.drawLayout.RowCount = 2;
            // DrawingCanvas
            this.DrawingCanvas.Name = "DrawingCanvas";
            this.DrawingCanvas.TabIndex = 24;
            this.DrawingCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DrawingCanvas.Margin = new System.Windows.Forms.Padding(0);
            this.DrawingCanvas.DrawingEnabled = true;
            this.DrawingCanvas.PathColor = System.Drawing.Color.FromArgb(37, 140, 114);
            this.DrawingCanvas.AccessibleName = "鼠标绘图画布";
            // drawFooter
            this.drawFooter.Name = "drawFooter";
            this.drawFooter.TabIndex = 25;
            this.drawFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.drawFooter.Margin = new System.Windows.Forms.Padding(0);
            this.drawFooter.Padding = new System.Windows.Forms.Padding(0);
            this.drawFooter.ColumnCount = 3;
            this.drawFooter.RowCount = 1;
            // Follow
            this.Follow.Name = "Follow";
            this.Follow.TabIndex = 26;
            this.Follow.Text = "平台跟随鼠标";
            this.Follow.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Follow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Follow.Margin = new System.Windows.Forms.Padding(3);
            this.Follow.Enabled = false;
            // drawInfo
            this.drawInfo.Name = "drawInfo";
            this.drawInfo.TabIndex = 27;
            this.drawInfo.Text = "按住左键绘图，松开即停";
            this.drawInfo.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.drawInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.drawInfo.Margin = new System.Windows.Forms.Padding(2);
            this.drawInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // clearDrawButton
            this.clearDrawButton.Name = "clearDrawButton";
            this.clearDrawButton.TabIndex = 28;
            this.clearDrawButton.Text = "清除绘图";
            this.clearDrawButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clearDrawButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.clearDrawButton.Margin = new System.Windows.Forms.Padding(3);
            this.clearDrawButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.clearDrawButton.UseVisualStyleBackColor = true;
            this.clearDrawButton.AccessibleName = "清除绘图";
            this.canvasLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.canvasLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.canvasLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 5F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 8F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 2F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 8F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 5F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 8F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 2F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 8F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 9F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 9F));
            this.settingsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16F));
            this.settingsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.settingsLayout.Controls.Add(this.xRangeLabel, 0, 0);
            this.settingsLayout.Controls.Add(this.XMin, 1, 0);
            this.settingsLayout.Controls.Add(this.xSeparator, 2, 0);
            this.settingsLayout.Controls.Add(this.XMax, 3, 0);
            this.settingsLayout.Controls.Add(this.yRangeLabel, 4, 0);
            this.settingsLayout.Controls.Add(this.YMin, 5, 0);
            this.settingsLayout.Controls.Add(this.ySeparator, 6, 0);
            this.settingsLayout.Controls.Add(this.YMax, 7, 0);
            this.settingsLayout.Controls.Add(this.xScaleLabel, 8, 0);
            this.settingsLayout.Controls.Add(this.XScale, 9, 0);
            this.settingsLayout.Controls.Add(this.yScaleLabel, 10, 0);
            this.settingsLayout.Controls.Add(this.YScale, 11, 0);
            this.settingsLayout.Controls.Add(this.applyButton, 12, 0);
            this.canvasLayout.Controls.Add(this.settingsLayout, 0, 0);
            this.plotsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.plotsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.plotsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.traceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.traceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.traceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.traceFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 78F));
            this.traceFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 22F));
            this.traceFooter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.traceFooter.Controls.Add(this.traceInfo, 0, 0);
            this.traceFooter.Controls.Add(this.clearTraceButton, 1, 0);
            this.traceLayout.Controls.Add(this.ActualCanvas, 0, 0);
            this.traceLayout.Controls.Add(this.traceFooter, 0, 1);
            this.traceGroup.Controls.Add(this.traceLayout);
            this.plotsLayout.Controls.Add(this.traceGroup, 0, 0);
            this.drawLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.drawLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.drawLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.drawFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.drawFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 48F));
            this.drawFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 22F));
            this.drawFooter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.drawFooter.Controls.Add(this.Follow, 0, 0);
            this.drawFooter.Controls.Add(this.drawInfo, 1, 0);
            this.drawFooter.Controls.Add(this.clearDrawButton, 2, 0);
            this.drawLayout.Controls.Add(this.DrawingCanvas, 0, 0);
            this.drawLayout.Controls.Add(this.drawFooter, 0, 1);
            this.drawGroup.Controls.Add(this.drawLayout);
            this.plotsLayout.Controls.Add(this.drawGroup, 1, 0);
            this.canvasLayout.Controls.Add(this.plotsLayout, 0, 1);
            this.Controls.Add(this.canvasLayout);
            this.applyButton.Click += new System.EventHandler(this.ApplySettings);
            this.clearTraceButton.Click += new System.EventHandler(this.ClearTrace);
            this.clearDrawButton.Click += new System.EventHandler(this.ClearDrawing);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "CanvasView";
            this.Size = new System.Drawing.Size(1140, 350);
            this.Margin = new System.Windows.Forms.Padding(0);
            ((System.ComponentModel.ISupportInitialize)(this.XMin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.XMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.YMin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.YMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.XScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.YScale)).EndInit();
            this.drawFooter.ResumeLayout(false);
            this.drawFooter.PerformLayout();
            this.drawLayout.ResumeLayout(false);
            this.drawLayout.PerformLayout();
            this.traceFooter.ResumeLayout(false);
            this.traceFooter.PerformLayout();
            this.traceLayout.ResumeLayout(false);
            this.traceLayout.PerformLayout();
            this.drawGroup.ResumeLayout(false);
            this.drawGroup.PerformLayout();
            this.traceGroup.ResumeLayout(false);
            this.traceGroup.PerformLayout();
            this.plotsLayout.ResumeLayout(false);
            this.plotsLayout.PerformLayout();
            this.settingsLayout.ResumeLayout(false);
            this.settingsLayout.PerformLayout();
            this.canvasLayout.ResumeLayout(false);
            this.canvasLayout.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.TableLayoutPanel canvasLayout;
        private System.Windows.Forms.TableLayoutPanel settingsLayout;
        private System.Windows.Forms.Label xRangeLabel;
        public System.Windows.Forms.NumericUpDown XMin;
        private System.Windows.Forms.Label xSeparator;
        public System.Windows.Forms.NumericUpDown XMax;
        private System.Windows.Forms.Label yRangeLabel;
        public System.Windows.Forms.NumericUpDown YMin;
        private System.Windows.Forms.Label ySeparator;
        public System.Windows.Forms.NumericUpDown YMax;
        private System.Windows.Forms.Label xScaleLabel;
        public System.Windows.Forms.NumericUpDown XScale;
        private System.Windows.Forms.Label yScaleLabel;
        public System.Windows.Forms.NumericUpDown YScale;
        private System.Windows.Forms.Button applyButton;
        private System.Windows.Forms.TableLayoutPanel plotsLayout;
        private System.Windows.Forms.GroupBox traceGroup;
        private System.Windows.Forms.GroupBox drawGroup;
        private System.Windows.Forms.TableLayoutPanel traceLayout;
        public Operations_Control.Controls.MotionCanvas ActualCanvas;
        private System.Windows.Forms.TableLayoutPanel traceFooter;
        private System.Windows.Forms.Label traceInfo;
        private System.Windows.Forms.Button clearTraceButton;
        private System.Windows.Forms.TableLayoutPanel drawLayout;
        public Operations_Control.Controls.MotionCanvas DrawingCanvas;
        private System.Windows.Forms.TableLayoutPanel drawFooter;
        public System.Windows.Forms.CheckBox Follow;
        private System.Windows.Forms.Label drawInfo;
        private System.Windows.Forms.Button clearDrawButton;
    }
}
