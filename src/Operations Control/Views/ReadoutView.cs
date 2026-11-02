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
            ShowSelected(samples[2].Motor != samples[0].Motor && samples[2].Motor != samples[1].Motor);
            xReadout.Display("X 轴位置", samples[0]);
            yReadout.Display("Y 轴位置", samples[1]);
            selectedReadout.Display("所选电机", samples[2]);
        }

        public void Clear(int x, int y, int selected)
        {
            ShowSelected(selected != x && selected != y);
            xReadout.Clear("X 轴位置 / 电机 " + x);
            yReadout.Clear("Y 轴位置 / 电机 " + y);
            selectedReadout.Clear("所选电机 / 电机 " + selected);
        }
        private void ShowSelected(bool visible)
        {
            selectedReadout.Visible = visible;
            readoutsLayout.RowStyles[2].Height = visible ? 36F : 0F;
        }
        public void ShowAge(double age, bool fresh)
        {
            if (!fresh) { xReadout.Clear("X：数据过期"); yReadout.Clear("Y：数据过期"); selectedReadout.Clear("所选轴：数据过期"); }
        }
    }
}
