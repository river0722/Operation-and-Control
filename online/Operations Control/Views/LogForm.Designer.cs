namespace Operations_Control.Views
{
    partial class LogForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LogForm));
            this.Log = new Operations_Control.Views.LogView();
            this.SuspendLayout();
            // Log
            this.Log.Name = "Log";
            this.Log.TabIndex = 0;
            this.Log.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Log.Margin = new System.Windows.Forms.Padding(0);
            this.Controls.Add(this.Log);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "LogForm";
            this.ClientSize = new System.Drawing.Size(850, 450);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Text = "操作日志";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.MinimumSize = new System.Drawing.Size(600, 300);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        public Operations_Control.Views.LogView Log;
    }
}
