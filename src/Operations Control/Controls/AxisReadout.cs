using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Controls
{
    public partial class AxisReadout : UserControl
    {
        public AxisReadout() { InitializeComponent(); }

        [Category("Appearance")]
        [DefaultValue("轴位置")]
        public string Caption
        {
            get { return captionLabel.Text; }
            set { captionLabel.Text = value; }
        }

        public void Display(string caption, AxisSample sample)
        {
            Caption = caption + " / 电机 " + sample.Motor;
            position.Text = sample.Position.ToString("0.000", CultureInfo.InvariantCulture);
            velocity.Text = sample.Velocity.HasValue ? sample.Velocity.Value.ToString("0.000", CultureInfo.InvariantCulture) : "—";
        }

        public void Clear(string caption)
        {
            Caption = caption;
            position.Text = "—";
            velocity.Text = "—";
        }
    }
}
