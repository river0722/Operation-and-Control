namespace Operations_Control.Views
{
    partial class ConnectionView
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
            this.connectionGroup = new System.Windows.Forms.GroupBox();
            this.connectionLayout = new System.Windows.Forms.TableLayoutPanel();
            this.modeLayout = new System.Windows.Forms.TableLayoutPanel();
            this.modeLabel = new System.Windows.Forms.Label();
            this.Mode = new System.Windows.Forms.ComboBox();
            this.stateLayout = new System.Windows.Forms.TableLayoutPanel();
            this.stateLabel = new System.Windows.Forms.Label();
            this.status = new System.Windows.Forms.TextBox();
            this.ConnectButton = new System.Windows.Forms.Button();
            this.DisconnectButton = new System.Windows.Forms.Button();
            this.device = new System.Windows.Forms.Label();
            this.connectionGroup.SuspendLayout();
            this.connectionLayout.SuspendLayout();
            this.modeLayout.SuspendLayout();
            this.stateLayout.SuspendLayout();
            this.SuspendLayout();
            // connectionGroup
            this.connectionGroup.Name = "connectionGroup";
            this.connectionGroup.TabIndex = 0;
            this.connectionGroup.Text = "通讯";
            this.connectionGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.connectionGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.connectionGroup.Margin = new System.Windows.Forms.Padding(3);
            this.connectionGroup.Padding = new System.Windows.Forms.Padding(10, 6, 10, 8);
            this.connectionGroup.TabStop = false;
            // connectionLayout
            this.connectionLayout.Name = "connectionLayout";
            this.connectionLayout.TabIndex = 1;
            this.connectionLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.connectionLayout.Margin = new System.Windows.Forms.Padding(0);
            this.connectionLayout.Padding = new System.Windows.Forms.Padding(0);
            this.connectionLayout.ColumnCount = 2;
            this.connectionLayout.RowCount = 3;
            // modeLayout
            this.modeLayout.Name = "modeLayout";
            this.modeLayout.TabIndex = 2;
            this.modeLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modeLayout.Margin = new System.Windows.Forms.Padding(0);
            this.modeLayout.Padding = new System.Windows.Forms.Padding(0);
            this.modeLayout.ColumnCount = 2;
            this.modeLayout.RowCount = 1;
            // modeLabel
            this.modeLabel.Name = "modeLabel";
            this.modeLabel.TabIndex = 3;
            this.modeLabel.Text = "模式";
            this.modeLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.modeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modeLabel.Margin = new System.Windows.Forms.Padding(2);
            this.modeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // Mode
            this.Mode.Name = "Mode";
            this.Mode.TabIndex = 4;
            this.Mode.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Mode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Mode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.Mode.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            // stateLayout
            this.stateLayout.Name = "stateLayout";
            this.stateLayout.TabIndex = 5;
            this.stateLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stateLayout.Margin = new System.Windows.Forms.Padding(0);
            this.stateLayout.Padding = new System.Windows.Forms.Padding(0);
            this.stateLayout.ColumnCount = 2;
            this.stateLayout.RowCount = 1;
            // stateLabel
            this.stateLabel.Name = "stateLabel";
            this.stateLabel.TabIndex = 6;
            this.stateLabel.Text = "状态";
            this.stateLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.stateLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stateLabel.Margin = new System.Windows.Forms.Padding(2);
            this.stateLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // status
            this.status.Name = "status";
            this.status.TabIndex = 7;
            this.status.Text = "未连接";
            this.status.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.status.Dock = System.Windows.Forms.DockStyle.Fill;
            this.status.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.status.Multiline = false;
            this.status.ReadOnly = true;
            this.status.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // ConnectButton
            this.ConnectButton.Name = "ConnectButton";
            this.ConnectButton.TabIndex = 8;
            this.ConnectButton.Text = "连接设备";
            this.ConnectButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ConnectButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.ConnectButton.Margin = new System.Windows.Forms.Padding(3);
            this.ConnectButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.ConnectButton.UseVisualStyleBackColor = true;
            this.ConnectButton.AccessibleName = "连接设备";
            // DisconnectButton
            this.DisconnectButton.Name = "DisconnectButton";
            this.DisconnectButton.TabIndex = 9;
            this.DisconnectButton.Text = "断开连接";
            this.DisconnectButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DisconnectButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.DisconnectButton.Margin = new System.Windows.Forms.Padding(3);
            this.DisconnectButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.DisconnectButton.UseVisualStyleBackColor = true;
            this.DisconnectButton.AccessibleName = "断开连接";
            this.DisconnectButton.Enabled = false;
            // device
            this.device.Name = "device";
            this.device.TabIndex = 10;
            this.device.Text = "请选择连接模式";
            this.device.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular);
            this.device.Dock = System.Windows.Forms.DockStyle.Fill;
            this.device.Margin = new System.Windows.Forms.Padding(2);
            this.device.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.connectionLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.connectionLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.connectionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.connectionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.connectionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.modeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.modeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 67F));
            this.modeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.Mode.Items.AddRange(new object[] { "模拟模式", "PMAC 实机" });
            this.modeLayout.Controls.Add(this.modeLabel, 0, 0);
            this.modeLayout.Controls.Add(this.Mode, 1, 0);
            this.stateLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.stateLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 67F));
            this.stateLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.stateLayout.Controls.Add(this.stateLabel, 0, 0);
            this.stateLayout.Controls.Add(this.status, 1, 0);
            this.connectionLayout.Controls.Add(this.modeLayout, 0, 0);
            this.connectionLayout.Controls.Add(this.ConnectButton, 1, 0);
            this.connectionLayout.Controls.Add(this.stateLayout, 0, 1);
            this.connectionLayout.Controls.Add(this.DisconnectButton, 1, 1);
            this.connectionLayout.Controls.Add(this.device, 0, 2);
            this.connectionLayout.SetColumnSpan(this.device, 2);
            this.connectionGroup.Controls.Add(this.connectionLayout);
            this.Controls.Add(this.connectionGroup);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "ConnectionView";
            this.Size = new System.Drawing.Size(400, 185);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.stateLayout.ResumeLayout(false);
            this.stateLayout.PerformLayout();
            this.modeLayout.ResumeLayout(false);
            this.modeLayout.PerformLayout();
            this.connectionLayout.ResumeLayout(false);
            this.connectionLayout.PerformLayout();
            this.connectionGroup.ResumeLayout(false);
            this.connectionGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.GroupBox connectionGroup;
        private System.Windows.Forms.TableLayoutPanel connectionLayout;
        private System.Windows.Forms.TableLayoutPanel modeLayout;
        private System.Windows.Forms.Label modeLabel;
        public System.Windows.Forms.ComboBox Mode;
        private System.Windows.Forms.TableLayoutPanel stateLayout;
        private System.Windows.Forms.Label stateLabel;
        private System.Windows.Forms.TextBox status;
        public System.Windows.Forms.Button ConnectButton;
        public System.Windows.Forms.Button DisconnectButton;
        private System.Windows.Forms.Label device;
    }
}
