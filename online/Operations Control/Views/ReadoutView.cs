using System;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Views
{
    public partial class ReadoutView : UserControl
    {
        public ReadoutView() { InitializeComponent(); Clear(1, 2, 1); }

        public void Display(AxisSample[] samples)
        {
            xReadout.Display("X 轴位置", samples[0]);
            yReadout.Display("Y 轴位置", samples[1]);
            selectedReadout.Display("所选电机", samples[2]);
            freshness.Text = "更新 " + samples[0].Timestamp.ToString("HH:mm:ss.fff") + " · 100 ms 轮询 · 速度由位置差估算";
        }

        public void Clear(int x, int y, int selected)
        {
            xReadout.Clear("X 轴位置 / 电机 " + x);
            yReadout.Clear("Y 轴位置 / 电机 " + y);
            selectedReadout.Clear("所选电机 / 电机 " + selected);
            freshness.Text = "数据未就绪 · 100 ms 轮询 · 速度由位置差估算";
        }
    }
}
