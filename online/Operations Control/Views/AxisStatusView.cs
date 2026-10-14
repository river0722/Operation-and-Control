using System;
using System.Drawing;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Views
{
    public sealed class AxisStatusView : UserControl
    {
        private readonly Label[] states = new Label[3];
        private readonly Label active = new Label { AutoSize = true, Margin = new Padding(5, 8, 8, 3) };
        public readonly Button EnableButton = new Button { Text = "所选轴使能", AutoSize = true };
        public readonly Button DisableButton = new Button { Text = "所选轴去使能", AutoSize = true };
        public readonly Button ResetButton = new Button { Text = "所选轴故障复位", AutoSize = true };
        public AxisStatusView()
        {
            TableLayoutPanel rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8, 2, 8, 2) };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++)
            {
                rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
                states[i] = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "轴状态未知", TextAlign = ContentAlignment.MiddleLeft };
                rows.Controls.Add(states[i], 0, i);
            }
            rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            actions.Controls.AddRange(new Control[] { active, EnableButton, DisableButton, ResetButton }); rows.Controls.Add(actions, 0, 3);
            Controls.Add(rows); Height = 118;
        }
        public void Display(ControllerFeedback feedback, bool fresh, int selected, bool busy)
        {
            active.Text = "操作对象：电机 " + selected;
            for (int i = 0; i < 3; i++)
            {
                AxisSample sample = feedback == null ? null : feedback.Axes[i];
                states[i].Text = (i == 0 ? "X" : i == 1 ? "Y" : "所选轴") + (sample == null ? "：状态未知" : " / 电机 " + sample.Motor + "：" +
                    (!fresh || sample.Status == null ? "反馈过期／未知" : sample.Status.ToString()) +
                    (fresh && sample.FollowingError.HasValue ? "；跟随误差 " + sample.FollowingError.Value.ToString("0.###") + " ct" : ""));
                states[i].ForeColor = !fresh || (sample != null && sample.Status != null && sample.Status.Fault) ? Color.Firebrick : SystemColors.ControlText;
            }
            AxisSample current = feedback == null ? null : feedback.Find(selected);
            bool stopped = current != null && current.Status != null && (!current.Status.Enabled || (current.Status.VelocityZero && current.Status.InPosition));
            EnableButton.Enabled = DisableButton.Enabled = ResetButton.Enabled = fresh && !busy && stopped;
        }
    }
}
