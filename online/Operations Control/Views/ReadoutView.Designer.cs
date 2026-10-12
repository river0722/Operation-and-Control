namespace Operations_Control.Views
{
    partial class ReadoutView
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
            this.readoutGroup = new System.Windows.Forms.GroupBox();
            this.readoutsLayout = new System.Windows.Forms.TableLayoutPanel();
            this.xReadout = new Operations_Control.Controls.AxisReadout();
            this.yReadout = new Operations_Control.Controls.AxisReadout();
            this.selectedReadout = new Operations_Control.Controls.AxisReadout();
            this.freshness = new System.Windows.Forms.Label();
            this.readoutGroup.SuspendLayout();
            this.readoutsLayout.SuspendLayout();
            this.SuspendLayout();
            // readoutGroup
            this.readoutGroup.Name = "readoutGroup";
            this.readoutGroup.TabIndex = 0;
            this.readoutGroup.Text = "当前位置与速度";
            this.readoutGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.readoutGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.readoutGroup.Margin = new System.Windows.Forms.Padding(3);
            this.readoutGroup.Padding = new System.Windows.Forms.Padding(10, 6, 10, 8);
            this.readoutGroup.TabStop = false;
            // readoutsLayout
            this.readoutsLayout.Name = "readoutsLayout";
            this.readoutsLayout.TabIndex = 1;
            this.readoutsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.readoutsLayout.Margin = new System.Windows.Forms.Padding(0);
            this.readoutsLayout.Padding = new System.Windows.Forms.Padding(0);
            this.readoutsLayout.ColumnCount = 1;
            this.readoutsLayout.RowCount = 4;
            // xReadout
            this.xReadout.Name = "xReadout";
            this.xReadout.TabIndex = 2;
            this.xReadout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.xReadout.Margin = new System.Windows.Forms.Padding(0);
            this.xReadout.Caption = "X 坐标 / 1";
            // yReadout
            this.yReadout.Name = "yReadout";
            this.yReadout.TabIndex = 3;
            this.yReadout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.yReadout.Margin = new System.Windows.Forms.Padding(0);
            this.yReadout.Caption = "Y 坐标 / 2";
            // selectedReadout
            this.selectedReadout.Name = "selectedReadout";
            this.selectedReadout.TabIndex = 4;
            this.selectedReadout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selectedReadout.Margin = new System.Windows.Forms.Padding(0);
            this.selectedReadout.Caption = "所选电机 / 1";
            // freshness
            this.freshness.Name = "freshness";
            this.freshness.TabIndex = 5;
            this.freshness.Text = "反馈单位 ct；速度单位 ct/ms";
            this.freshness.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.freshness.Dock = System.Windows.Forms.DockStyle.Fill;
            this.freshness.Margin = new System.Windows.Forms.Padding(2);
            this.freshness.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.readoutsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.readoutsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.readoutsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.readoutsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.readoutsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.readoutsLayout.Controls.Add(this.xReadout, 0, 0);
            this.readoutsLayout.Controls.Add(this.yReadout, 0, 1);
            this.readoutsLayout.Controls.Add(this.selectedReadout, 0, 2);
            this.readoutsLayout.Controls.Add(this.freshness, 0, 3);
            this.readoutGroup.Controls.Add(this.readoutsLayout);
            this.Controls.Add(this.readoutGroup);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "ReadoutView";
            this.Size = new System.Drawing.Size(710, 185);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.readoutsLayout.ResumeLayout(false);
            this.readoutsLayout.PerformLayout();
            this.readoutGroup.ResumeLayout(false);
            this.readoutGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.GroupBox readoutGroup;
        private System.Windows.Forms.TableLayoutPanel readoutsLayout;
        private Operations_Control.Controls.AxisReadout xReadout;
        private Operations_Control.Controls.AxisReadout yReadout;
        private Operations_Control.Controls.AxisReadout selectedReadout;
        private System.Windows.Forms.Label freshness;
    }
}
