using System.Drawing;

using System.Windows.Forms;

namespace Operations_Control.Controls
{
    public static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(244, 247, 251);
        public static readonly Color Surface = Color.White;
        public static readonly Color Text = Color.FromArgb(26, 39, 59);
        public static readonly Color Muted = Color.FromArgb(91, 105, 124);
        public static readonly Color Border = Color.FromArgb(215, 222, 232);
        public static readonly Color Primary = Color.FromArgb(39, 91, 204);
        public static readonly Color Danger = Color.FromArgb(180, 45, 54);
        public static readonly Color Success = Color.FromArgb(27, 112, 83);

        // Keep designer-chosen colors, while showing a distinct disabled state at runtime.
        public static void BindButtonStates(Control root)
        {
            foreach (Control child in root.Controls)
            {
                Button button = child as Button;
                if (button != null)
                {
                    Color background = button.BackColor;
                    Color foreground = button.ForeColor;
                    Color border = button.FlatAppearance.BorderColor;
                    System.EventHandler update = delegate
                    {
                        button.BackColor = button.Enabled ? background : Color.FromArgb(237, 240, 245);
                        button.ForeColor = button.Enabled ? foreground : Muted;
                        button.FlatAppearance.BorderColor = button.Enabled ? border : Border;
                    };
                    button.EnabledChanged += update;
                    update(button, System.EventArgs.Empty);
                }
                BindButtonStates(child);
            }
        }

    }
}
