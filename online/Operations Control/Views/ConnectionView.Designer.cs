namespace Operations_Control.Views
{
    partial class ConnectionView
    {
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.GroupBox connectionGroup;
        private System.Windows.Forms.TableLayoutPanel connectionLayout;
        public System.Windows.Forms.ComboBox Mode;
        public System.Windows.Forms.Button ConnectButton, DisconnectButton;
        private System.Windows.Forms.TextBox status;
        private System.Windows.Forms.Label device;
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            connectionGroup = new System.Windows.Forms.GroupBox { Text = "通讯", Dock = System.Windows.Forms.DockStyle.Fill, Padding = new System.Windows.Forms.Padding(8), Font = new System.Drawing.Font("Microsoft YaHei UI", 11, System.Drawing.FontStyle.Bold) };
            connectionLayout = new System.Windows.Forms.TableLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            connectionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            foreach (int width in new int[] { 25, 16, 16, 18, 25 }) connectionLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, width));
            Mode = new System.Windows.Forms.ComboBox { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList, Dock = System.Windows.Forms.DockStyle.Fill, Margin = new System.Windows.Forms.Padding(3, 5, 3, 3), Font = new System.Drawing.Font("Microsoft YaHei UI", 10) };
            Mode.Items.AddRange(new object[] { "模拟模式", "PMAC 实机" });
            ConnectButton = new System.Windows.Forms.Button { Text = "连接", Dock = System.Windows.Forms.DockStyle.Fill, Font = new System.Drawing.Font("Microsoft YaHei UI", 10) };
            DisconnectButton = new System.Windows.Forms.Button { Text = "断开", Dock = System.Windows.Forms.DockStyle.Fill, Font = new System.Drawing.Font("Microsoft YaHei UI", 10) };
            status = new System.Windows.Forms.TextBox { Text = "未连接", ReadOnly = true, TextAlign = System.Windows.Forms.HorizontalAlignment.Center, Dock = System.Windows.Forms.DockStyle.Fill, Margin = new System.Windows.Forms.Padding(3, 5, 3, 3), Font = new System.Drawing.Font("Microsoft YaHei UI", 10) };
            device = new System.Windows.Forms.Label { Dock = System.Windows.Forms.DockStyle.Fill, AutoEllipsis = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F) };
            connectionLayout.Controls.Add(Mode, 0, 0); connectionLayout.Controls.Add(ConnectButton, 1, 0); connectionLayout.Controls.Add(DisconnectButton, 2, 0); connectionLayout.Controls.Add(status, 3, 0); connectionLayout.Controls.Add(device, 4, 0);
            connectionGroup.Controls.Add(connectionLayout); Controls.Add(connectionGroup);
            AutoScaleDimensions = new System.Drawing.SizeF(96, 96); AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10); BackColor = System.Drawing.SystemColors.Control; Size = new System.Drawing.Size(700, 80); Margin = new System.Windows.Forms.Padding(0);
        }
    }
}
