namespace Operations_Control.Views
{
    partial class ProgramEditorForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProgramEditorForm));
            this.editorLayout = new System.Windows.Forms.TableLayoutPanel();
            this.saveButton = new System.Windows.Forms.Button();
            this.Editor = new System.Windows.Forms.TextBox();
            this.editorLayout.SuspendLayout();
            this.SuspendLayout();
            // editorLayout
            this.editorLayout.Name = "editorLayout";
            this.editorLayout.TabIndex = 0;
            this.editorLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editorLayout.Margin = new System.Windows.Forms.Padding(0);
            this.editorLayout.Padding = new System.Windows.Forms.Padding(0);
            this.editorLayout.ColumnCount = 1;
            this.editorLayout.RowCount = 2;
            // saveButton
            this.saveButton.Name = "saveButton";
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "另存为…";
            this.saveButton.Dock = System.Windows.Forms.DockStyle.Left;
            this.saveButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.saveButton.Margin = new System.Windows.Forms.Padding(3);
            this.saveButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.AccessibleName = "另存为…";
            this.saveButton.Size = new System.Drawing.Size(120, 32);
            // Editor
            this.Editor.Name = "Editor";
            this.Editor.TabIndex = 2;
            this.Editor.Text = "";
            this.Editor.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Regular);
            this.Editor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Editor.Margin = new System.Windows.Forms.Padding(3);
            this.Editor.Multiline = true;
            this.Editor.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.Editor.WordWrap = false;
            this.Editor.AcceptsTab = true;
            this.Editor.AcceptsReturn = true;
            this.Editor.MaxLength = 0;
            this.editorLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.editorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.editorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.editorLayout.Controls.Add(this.saveButton, 0, 0);
            this.editorLayout.Controls.Add(this.Editor, 0, 1);
            this.Controls.Add(this.editorLayout);
            this.saveButton.Click += new System.EventHandler(this.RequestSave);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular);
            this.Name = "ProgramEditorForm";
            this.ClientSize = new System.Drawing.Size(850, 580);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Text = "程序编辑";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Padding = new System.Windows.Forms.Padding(10);
            this.editorLayout.ResumeLayout(false);
            this.editorLayout.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.TableLayoutPanel editorLayout;
        private System.Windows.Forms.Button saveButton;
        public System.Windows.Forms.TextBox Editor;
    }
}
