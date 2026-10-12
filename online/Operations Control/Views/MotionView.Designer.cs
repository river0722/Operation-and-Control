namespace Operations_Control.Views
{
    partial class MotionView
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
            this.motionGroup = new System.Windows.Forms.GroupBox();
            this.controlLayout = new System.Windows.Forms.TableLayoutPanel();
            this.jogLayout = new System.Windows.Forms.TableLayoutPanel();
            this.stepLabel = new System.Windows.Forms.Label();
            this.Step = new System.Windows.Forms.NumericUpDown();
            this.stepHint = new System.Windows.Forms.Label();
            this.speedLabel = new System.Windows.Forms.Label();
            this.Speed = new System.Windows.Forms.NumericUpDown();
            this.ApplySpeedButton = new System.Windows.Forms.Button();
            this.SingleStep = new System.Windows.Forms.CheckBox();
            this.axesLayout = new System.Windows.Forms.TableLayoutPanel();
            this.xLabel = new System.Windows.Forms.Label();
            this.yLabel = new System.Windows.Forms.Label();
            this.XMotor = new System.Windows.Forms.ComboBox();
            this.YMotor = new System.Windows.Forms.ComboBox();
            this.jogPad = new System.Windows.Forms.TableLayoutPanel();
            this.jogUpButton = new System.Windows.Forms.Button();
            this.jogDownButton = new System.Windows.Forms.Button();
            this.jogLeftButton = new System.Windows.Forms.Button();
            this.jogRightButton = new System.Windows.Forms.Button();
            this.positionLayout = new System.Windows.Forms.TableLayoutPanel();
            this.motorLabel = new System.Windows.Forms.Label();
            this.SelectedMotor = new System.Windows.Forms.ComboBox();
            this.MoveButton = new System.Windows.Forms.Button();
            this.Target = new System.Windows.Forms.NumericUpDown();
            this.ZeroButton = new System.Windows.Forms.Button();
            this.StopButton = new System.Windows.Forms.Button();
            this.stopHint = new System.Windows.Forms.Label();
            this.motionGroup.SuspendLayout();
            this.controlLayout.SuspendLayout();
            this.jogLayout.SuspendLayout();
            this.axesLayout.SuspendLayout();
            this.jogPad.SuspendLayout();
            this.positionLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Step)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Speed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Target)).BeginInit();
            this.SuspendLayout();
            // motionGroup
            this.motionGroup.Name = "motionGroup";
            this.motionGroup.TabIndex = 0;
            this.motionGroup.Text = "点动、定位控制";
            this.motionGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.motionGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.motionGroup.Margin = new System.Windows.Forms.Padding(3);
            this.motionGroup.Padding = new System.Windows.Forms.Padding(10, 6, 10, 8);
            this.motionGroup.TabStop = false;
            // controlLayout
            this.controlLayout.Name = "controlLayout";
            this.controlLayout.TabIndex = 1;
            this.controlLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.controlLayout.Margin = new System.Windows.Forms.Padding(0);
            this.controlLayout.Padding = new System.Windows.Forms.Padding(0);
            this.controlLayout.ColumnCount = 2;
            this.controlLayout.RowCount = 1;
            // jogLayout
            this.jogLayout.Name = "jogLayout";
            this.jogLayout.TabIndex = 2;
            this.jogLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.jogLayout.Margin = new System.Windows.Forms.Padding(0);
            this.jogLayout.Padding = new System.Windows.Forms.Padding(0);
            this.jogLayout.ColumnCount = 3;
            this.jogLayout.RowCount = 5;
            // stepLabel
            this.stepLabel.Name = "stepLabel";
            this.stepLabel.TabIndex = 3;
            this.stepLabel.Text = "步长 ct";
            this.stepLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.stepLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stepLabel.Margin = new System.Windows.Forms.Padding(2);
            this.stepLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // Step
            this.Step.Name = "Step";
            this.Step.TabIndex = 4;
            this.Step.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Step.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Step.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.Step.Minimum = 1M;
            this.Step.Maximum = 1000000M;
            this.Step.Value = 100M;
            this.Step.DecimalPlaces = 0;
            this.Step.Increment = 1M;
            // stepHint
            this.stepHint.Name = "stepHint";
            this.stepHint.TabIndex = 5;
            this.stepHint.Text = "单步距离";
            this.stepHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.stepHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stepHint.Margin = new System.Windows.Forms.Padding(2);
            this.stepHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // speedLabel
            this.speedLabel.Name = "speedLabel";
            this.speedLabel.TabIndex = 6;
            this.speedLabel.Text = "速度 ct/ms";
            this.speedLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.speedLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.speedLabel.Margin = new System.Windows.Forms.Padding(2);
            this.speedLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // Speed
            this.Speed.Name = "Speed";
            this.Speed.TabIndex = 7;
            this.Speed.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Speed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Speed.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.Speed.Minimum = 0.001M;
            this.Speed.Maximum = 15M;
            this.Speed.Value = 1M;
            this.Speed.DecimalPlaces = 3;
            this.Speed.Increment = 0.1M;
            // ApplySpeedButton
            this.ApplySpeedButton.Name = "ApplySpeedButton";
            this.ApplySpeedButton.TabIndex = 8;
            this.ApplySpeedButton.Text = "设置速度";
            this.ApplySpeedButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ApplySpeedButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.ApplySpeedButton.Margin = new System.Windows.Forms.Padding(3);
            this.ApplySpeedButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.ApplySpeedButton.UseVisualStyleBackColor = true;
            this.ApplySpeedButton.AccessibleName = "设置速度";
            this.ApplySpeedButton.Enabled = false;
            // SingleStep
            this.SingleStep.Name = "SingleStep";
            this.SingleStep.TabIndex = 9;
            this.SingleStep.Text = "点动一步";
            this.SingleStep.Checked = true;
            this.SingleStep.CheckState = System.Windows.Forms.CheckState.Checked;
            this.SingleStep.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.SingleStep.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SingleStep.Margin = new System.Windows.Forms.Padding(3);
            // axesLayout
            this.axesLayout.Name = "axesLayout";
            this.axesLayout.TabIndex = 10;
            this.axesLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.axesLayout.Margin = new System.Windows.Forms.Padding(0);
            this.axesLayout.Padding = new System.Windows.Forms.Padding(0);
            this.axesLayout.ColumnCount = 4;
            this.axesLayout.RowCount = 1;
            // xLabel
            this.xLabel.Name = "xLabel";
            this.xLabel.TabIndex = 11;
            this.xLabel.Text = "X轴";
            this.xLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.xLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.xLabel.Margin = new System.Windows.Forms.Padding(2);
            this.xLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // yLabel
            this.yLabel.Name = "yLabel";
            this.yLabel.TabIndex = 12;
            this.yLabel.Text = "Y轴";
            this.yLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.yLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.yLabel.Margin = new System.Windows.Forms.Padding(2);
            this.yLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // XMotor
            this.XMotor.Name = "XMotor";
            this.XMotor.TabIndex = 13;
            this.XMotor.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.XMotor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.XMotor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.XMotor.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.XMotor.FormattingEnabled = true;
            // YMotor
            this.YMotor.Name = "YMotor";
            this.YMotor.TabIndex = 14;
            this.YMotor.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.YMotor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.YMotor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.YMotor.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.YMotor.FormattingEnabled = true;
            // jogPad
            this.jogPad.Name = "jogPad";
            this.jogPad.TabIndex = 15;
            this.jogPad.Dock = System.Windows.Forms.DockStyle.Fill;
            this.jogPad.Margin = new System.Windows.Forms.Padding(0);
            this.jogPad.Padding = new System.Windows.Forms.Padding(0);
            this.jogPad.ColumnCount = 3;
            this.jogPad.RowCount = 3;
            // jogUpButton
            this.jogUpButton.Name = "jogUpButton";
            this.jogUpButton.TabIndex = 16;
            this.jogUpButton.Text = "Y +";
            this.jogUpButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.jogUpButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.jogUpButton.Margin = new System.Windows.Forms.Padding(3);
            this.jogUpButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.jogUpButton.UseVisualStyleBackColor = true;
            this.jogUpButton.AccessibleName = "Y +";
            this.jogUpButton.Enabled = false;
            // jogDownButton
            this.jogDownButton.Name = "jogDownButton";
            this.jogDownButton.TabIndex = 17;
            this.jogDownButton.Text = "Y −";
            this.jogDownButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.jogDownButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.jogDownButton.Margin = new System.Windows.Forms.Padding(3);
            this.jogDownButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.jogDownButton.UseVisualStyleBackColor = true;
            this.jogDownButton.AccessibleName = "Y −";
            this.jogDownButton.Enabled = false;
            // jogLeftButton
            this.jogLeftButton.Name = "jogLeftButton";
            this.jogLeftButton.TabIndex = 18;
            this.jogLeftButton.Text = "X −";
            this.jogLeftButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.jogLeftButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.jogLeftButton.Margin = new System.Windows.Forms.Padding(3);
            this.jogLeftButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.jogLeftButton.UseVisualStyleBackColor = true;
            this.jogLeftButton.AccessibleName = "X −";
            this.jogLeftButton.Enabled = false;
            // jogRightButton
            this.jogRightButton.Name = "jogRightButton";
            this.jogRightButton.TabIndex = 19;
            this.jogRightButton.Text = "X +";
            this.jogRightButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.jogRightButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.jogRightButton.Margin = new System.Windows.Forms.Padding(3);
            this.jogRightButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.jogRightButton.UseVisualStyleBackColor = true;
            this.jogRightButton.AccessibleName = "X +";
            this.jogRightButton.Enabled = false;
            // positionLayout
            this.positionLayout.Name = "positionLayout";
            this.positionLayout.TabIndex = 20;
            this.positionLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.positionLayout.Margin = new System.Windows.Forms.Padding(0);
            this.positionLayout.Padding = new System.Windows.Forms.Padding(0);
            this.positionLayout.ColumnCount = 2;
            this.positionLayout.RowCount = 5;
            // motorLabel
            this.motorLabel.Name = "motorLabel";
            this.motorLabel.TabIndex = 21;
            this.motorLabel.Text = "电机";
            this.motorLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.motorLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.motorLabel.Margin = new System.Windows.Forms.Padding(2);
            this.motorLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // SelectedMotor
            this.SelectedMotor.Name = "SelectedMotor";
            this.SelectedMotor.TabIndex = 22;
            this.SelectedMotor.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.SelectedMotor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SelectedMotor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.SelectedMotor.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.SelectedMotor.FormattingEnabled = true;
            // MoveButton
            this.MoveButton.Name = "MoveButton";
            this.MoveButton.TabIndex = 23;
            this.MoveButton.Text = "到位置";
            this.MoveButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MoveButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.MoveButton.Margin = new System.Windows.Forms.Padding(3);
            this.MoveButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.MoveButton.UseVisualStyleBackColor = true;
            this.MoveButton.AccessibleName = "到位置";
            this.MoveButton.Enabled = false;
            // Target
            this.Target.Name = "Target";
            this.Target.TabIndex = 24;
            this.Target.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Target.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Target.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.Target.Minimum = -100000000M;
            this.Target.Maximum = 100000000M;
            this.Target.Value = 0M;
            this.Target.DecimalPlaces = 0;
            this.Target.Increment = 1M;
            // ZeroButton
            this.ZeroButton.Name = "ZeroButton";
            this.ZeroButton.TabIndex = 25;
            this.ZeroButton.Text = "当前位置置零 HOMEZ";
            this.ZeroButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ZeroButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.ZeroButton.Margin = new System.Windows.Forms.Padding(3);
            this.ZeroButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.ZeroButton.UseVisualStyleBackColor = true;
            this.ZeroButton.AccessibleName = "当前位置置零 HOMEZ";
            this.ZeroButton.Enabled = false;
            // StopButton
            this.StopButton.Name = "StopButton";
            this.StopButton.TabIndex = 26;
            this.StopButton.Text = "停止运动";
            this.StopButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StopButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.StopButton.Margin = new System.Windows.Forms.Padding(3);
            this.StopButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.StopButton.UseVisualStyleBackColor = true;
            this.StopButton.AccessibleName = "停止运动";
            this.StopButton.Enabled = false;
            this.StopButton.ForeColor = System.Drawing.Color.FromArgb(180, 45, 54);
            // stopHint
            this.stopHint.Name = "stopHint";
            this.stopHint.TabIndex = 27;
            this.stopHint.Text = "作用于 X / Y 和所选电机";
            this.stopHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.stopHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stopHint.Margin = new System.Windows.Forms.Padding(2);
            this.stopHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.controlLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56F));
            this.controlLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 44F));
            this.controlLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.jogLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.jogLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.jogLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.jogLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.jogLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.jogLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.jogLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.jogLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.jogLayout.Controls.Add(this.stepLabel, 0, 0);
            this.jogLayout.Controls.Add(this.Step, 1, 0);
            this.jogLayout.Controls.Add(this.stepHint, 2, 0);
            this.jogLayout.Controls.Add(this.speedLabel, 0, 1);
            this.jogLayout.Controls.Add(this.Speed, 1, 1);
            this.jogLayout.Controls.Add(this.ApplySpeedButton, 2, 1);
            this.jogLayout.Controls.Add(this.SingleStep, 0, 2);
            this.jogLayout.SetColumnSpan(this.SingleStep, 3);
            this.axesLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.axesLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.axesLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.axesLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.axesLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.XMotor.Items.AddRange(new object[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            this.YMotor.Items.AddRange(new object[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            this.axesLayout.Controls.Add(this.xLabel, 0, 0);
            this.axesLayout.Controls.Add(this.XMotor, 1, 0);
            this.axesLayout.Controls.Add(this.yLabel, 2, 0);
            this.axesLayout.Controls.Add(this.YMotor, 3, 0);
            this.jogLayout.Controls.Add(this.axesLayout, 0, 3);
            this.jogLayout.SetColumnSpan(this.axesLayout, 3);
            this.jogPad.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.jogPad.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.jogPad.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.jogPad.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.jogPad.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.34F));
            this.jogPad.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.jogPad.Controls.Add(this.jogUpButton, 1, 0);
            this.jogPad.Controls.Add(this.jogDownButton, 1, 2);
            this.jogPad.Controls.Add(this.jogLeftButton, 0, 1);
            this.jogPad.Controls.Add(this.jogRightButton, 2, 1);
            this.jogLayout.Controls.Add(this.jogPad, 0, 4);
            this.jogLayout.SetColumnSpan(this.jogPad, 3);
            this.positionLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 34F));
            this.positionLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 66F));
            this.positionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.positionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.positionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.positionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            this.positionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.SelectedMotor.Items.AddRange(new object[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            this.positionLayout.Controls.Add(this.motorLabel, 0, 0);
            this.positionLayout.Controls.Add(this.SelectedMotor, 1, 0);
            this.positionLayout.Controls.Add(this.MoveButton, 0, 1);
            this.positionLayout.Controls.Add(this.Target, 1, 1);
            this.positionLayout.Controls.Add(this.ZeroButton, 0, 2);
            this.positionLayout.SetColumnSpan(this.ZeroButton, 2);
            this.positionLayout.Controls.Add(this.StopButton, 0, 3);
            this.positionLayout.SetColumnSpan(this.StopButton, 2);
            this.positionLayout.Controls.Add(this.stopHint, 0, 4);
            this.positionLayout.SetColumnSpan(this.stopHint, 2);
            this.controlLayout.Controls.Add(this.jogLayout, 0, 0);
            this.controlLayout.Controls.Add(this.positionLayout, 1, 0);
            this.motionGroup.Controls.Add(this.controlLayout);
            this.Controls.Add(this.motionGroup);
            this.SingleStep.CheckedChanged += new System.EventHandler(this.SingleStepChanged);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "MotionView";
            this.Size = new System.Drawing.Size(710, 255);
            this.Margin = new System.Windows.Forms.Padding(0);
            ((System.ComponentModel.ISupportInitialize)(this.Step)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Speed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Target)).EndInit();
            this.positionLayout.ResumeLayout(false);
            this.positionLayout.PerformLayout();
            this.jogPad.ResumeLayout(false);
            this.jogPad.PerformLayout();
            this.axesLayout.ResumeLayout(false);
            this.axesLayout.PerformLayout();
            this.jogLayout.ResumeLayout(false);
            this.jogLayout.PerformLayout();
            this.controlLayout.ResumeLayout(false);
            this.controlLayout.PerformLayout();
            this.motionGroup.ResumeLayout(false);
            this.motionGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.GroupBox motionGroup;
        private System.Windows.Forms.TableLayoutPanel controlLayout;
        private System.Windows.Forms.TableLayoutPanel jogLayout;
        private System.Windows.Forms.Label stepLabel;
        public System.Windows.Forms.NumericUpDown Step;
        private System.Windows.Forms.Label stepHint;
        private System.Windows.Forms.Label speedLabel;
        public System.Windows.Forms.NumericUpDown Speed;
        public System.Windows.Forms.Button ApplySpeedButton;
        public System.Windows.Forms.CheckBox SingleStep;
        private System.Windows.Forms.TableLayoutPanel axesLayout;
        private System.Windows.Forms.Label xLabel;
        private System.Windows.Forms.Label yLabel;
        public System.Windows.Forms.ComboBox XMotor;
        public System.Windows.Forms.ComboBox YMotor;
        private System.Windows.Forms.TableLayoutPanel jogPad;
        private System.Windows.Forms.Button jogUpButton;
        private System.Windows.Forms.Button jogDownButton;
        private System.Windows.Forms.Button jogLeftButton;
        private System.Windows.Forms.Button jogRightButton;
        private System.Windows.Forms.TableLayoutPanel positionLayout;
        private System.Windows.Forms.Label motorLabel;
        public System.Windows.Forms.ComboBox SelectedMotor;
        public System.Windows.Forms.Button MoveButton;
        public System.Windows.Forms.NumericUpDown Target;
        public System.Windows.Forms.Button ZeroButton;
        public System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.Label stopHint;
    }
}
