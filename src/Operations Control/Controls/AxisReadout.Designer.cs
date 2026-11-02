namespace Operations_Control.Controls
{
    partial class AxisReadout
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
            this.rowLayout = new System.Windows.Forms.TableLayoutPanel();
            this.captionLabel = new System.Windows.Forms.Label();
            this.position = new System.Windows.Forms.TextBox();
            this.positionUnit = new System.Windows.Forms.Label();
            this.velocityLabel = new System.Windows.Forms.Label();
            this.velocity = new System.Windows.Forms.TextBox();
            this.velocityUnit = new System.Windows.Forms.Label();
            this.rowLayout.SuspendLayout();
            this.SuspendLayout();
            // rowLayout
            this.rowLayout.Name = "rowLayout";
            this.rowLayout.TabIndex = 0;
            this.rowLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rowLayout.Margin = new System.Windows.Forms.Padding(0);
            this.rowLayout.Padding = new System.Windows.Forms.Padding(0);
            this.rowLayout.ColumnCount = 6;
            this.rowLayout.RowCount = 1;
            // captionLabel
            this.captionLabel.Name = "captionLabel";
            this.captionLabel.TabIndex = 1;
            this.captionLabel.Text = "轴位置";
            this.captionLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.captionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.captionLabel.Margin = new System.Windows.Forms.Padding(2);
            this.captionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // position
            this.position.Name = "position";
            this.position.TabIndex = 2;
            this.position.Text = "—";
            this.position.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Bold);
            this.position.Dock = System.Windows.Forms.DockStyle.Fill;
            this.position.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.position.Multiline = false;
            this.position.ReadOnly = true;
            this.position.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.position.BackColor = System.Drawing.SystemColors.Window;
            // positionUnit
            this.positionUnit.Name = "positionUnit";
            this.positionUnit.TabIndex = 3;
            this.positionUnit.Text = "ct";
            this.positionUnit.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.positionUnit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.positionUnit.Margin = new System.Windows.Forms.Padding(2);
            this.positionUnit.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // velocityLabel
            this.velocityLabel.Name = "velocityLabel";
            this.velocityLabel.TabIndex = 4;
            this.velocityLabel.Text = "速度";
            this.velocityLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.velocityLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.velocityLabel.Margin = new System.Windows.Forms.Padding(2);
            this.velocityLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // velocity
            this.velocity.Name = "velocity";
            this.velocity.TabIndex = 5;
            this.velocity.Text = "—";
            this.velocity.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Bold);
            this.velocity.Dock = System.Windows.Forms.DockStyle.Fill;
            this.velocity.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.velocity.Multiline = false;
            this.velocity.ReadOnly = true;
            this.velocity.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.velocity.BackColor = System.Drawing.SystemColors.Window;
            // velocityUnit
            this.velocityUnit.Name = "velocityUnit";
            this.velocityUnit.TabIndex = 6;
            this.velocityUnit.Text = "ct/ms";
            this.velocityUnit.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.velocityUnit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.velocityUnit.Margin = new System.Windows.Forms.Padding(2);
            this.velocityUnit.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.rowLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.rowLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 23F));
            this.rowLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 7F));
            this.rowLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.rowLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 23F));
            this.rowLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.rowLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rowLayout.Controls.Add(this.captionLabel, 0, 0);
            this.rowLayout.Controls.Add(this.position, 1, 0);
            this.rowLayout.Controls.Add(this.positionUnit, 2, 0);
            this.rowLayout.Controls.Add(this.velocityLabel, 3, 0);
            this.rowLayout.Controls.Add(this.velocity, 4, 0);
            this.rowLayout.Controls.Add(this.velocityUnit, 5, 0);
            this.Controls.Add(this.rowLayout);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "AxisReadout";
            this.Size = new System.Drawing.Size(710, 36);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.rowLayout.ResumeLayout(false);
            this.rowLayout.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.TableLayoutPanel rowLayout;
        private System.Windows.Forms.Label captionLabel;
        private System.Windows.Forms.TextBox position;
        private System.Windows.Forms.Label positionUnit;
        private System.Windows.Forms.Label velocityLabel;
        private System.Windows.Forms.TextBox velocity;
        private System.Windows.Forms.Label velocityUnit;
    }
}
