using System;
using System.Drawing;
using Operations_Control.Models;

namespace Operations_Control.Controls
{
    public static class CanvasProjection
    {
        // Preserve the physical aspect ratio and use a downward-positive Y axis.
        public static RectangleF PlotArea(this CanvasSettings settings, Rectangle bounds)
        {
            float availableWidth = Math.Max(1, bounds.Width - 112);
            float availableHeight = Math.Max(1, bounds.Height - 60);
            double scale = Math.Min(availableWidth / (settings.XMax - settings.XMin), availableHeight / (settings.YMax - settings.YMin));
            float width = (float)((settings.XMax - settings.XMin) * scale), height = (float)((settings.YMax - settings.YMin) * scale);
            return new RectangleF(bounds.Left + 50 + (availableWidth - width) / 2,
                bounds.Top + 28 + (availableHeight - height) / 2, width, height);
        }
        public static PointF ToScreen(this CanvasSettings settings, MotionPoint point, RectangleF plot)
        {
            return new PointF(plot.Left + (float)((point.X - settings.XMin) / (settings.XMax - settings.XMin)) * plot.Width,
                plot.Top + (float)((point.Y - settings.YMin) / (settings.YMax - settings.YMin)) * plot.Height);
        }
        public static MotionPoint FromScreen(this CanvasSettings settings, Point point, RectangleF plot)
        {
            return new MotionPoint(settings.XMin + (point.X - plot.Left) / plot.Width * (settings.XMax - settings.XMin),
                settings.YMin + (point.Y - plot.Top) / plot.Height * (settings.YMax - settings.YMin));
        }
    }
}
