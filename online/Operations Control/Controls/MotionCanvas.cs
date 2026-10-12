using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Operations_Control.Models;

namespace Operations_Control.Controls
{
    [ToolboxItem(true)]
    public sealed class MotionCanvas : Control
    {
        private struct Vertex
        {
            public MotionPoint Point;
            public bool Break;
            public Vertex(MotionPoint point, bool start) { Point = point; Break = start; }
        }
        private readonly List<Vertex> points = new List<Vertex>();
        private CanvasSettings settings = CanvasSettings.Default;
        private bool drawing, newSegment = true;
        private Color pathColor = Color.FromArgb(39, 91, 204);
        public event EventHandler StrokeStarted;
        public event EventHandler<DrawPointEventArgs> PointDrawn;
        public event EventHandler StrokeEnded;

        public MotionCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9F);
            Size = new Size(450, 260);
            TabStop = true;
        }
        [DefaultValue(false)]
        public bool DrawingEnabled { get; set; }
        [Category("Appearance")]
        public Color PathColor { get { return pathColor; } set { pathColor = value; Invalidate(); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CanvasSettings Settings { get { return settings; } set { settings = value; Invalidate(); } }
        [Browsable(false)]
        public bool IsDrawing { get { return drawing; } }
        [Browsable(false)]
        public int PointCount { get { return points.Count; } }

        public void ClearPath() { points.Clear(); newSegment = true; Invalidate(); }
        public void BreakPath() { newSegment = true; }
        public void Append(MotionPoint point)
        {
            if (Double.IsNaN(point.X) || Double.IsNaN(point.Y) || Double.IsInfinity(point.X) || Double.IsInfinity(point.Y)) return;
            if (!newSegment && points.Count > 0)
            {
                MotionPoint last = points[points.Count - 1].Point;
                if (Math.Abs(last.X - point.X) < 0.0001 && Math.Abs(last.Y - point.Y) < 0.0001) return;
            }
            if (points.Count >= 4000)
            {
                points.RemoveRange(0, 500);
                Vertex first = points[0]; first.Break = true; points[0] = first;
            }
            points.Add(new Vertex(point, newSegment)); newSegment = false; Invalidate();
        }

        public void EndStroke()
        {
            if (!drawing) return;
            drawing = false; newSegment = true; Capture = false;
            if (StrokeEnded != null) StrokeEnded(this, EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!DrawingEnabled || e.Button != MouseButtons.Left || !settings.PlotArea(ClientRectangle).Contains(e.Location)) return;
            Focus(); drawing = true; newSegment = true; Capture = true;
            if (StrokeStarted != null) StrokeStarted(this, EventArgs.Empty);
            DrawAt(e.Location);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!drawing) return;
            if (!settings.PlotArea(ClientRectangle).Contains(e.Location)) { EndStroke(); return; }
            DrawAt(e.Location);
        }
        private void DrawAt(Point location)
        {
            MotionPoint point = settings.FromScreen(location, settings.PlotArea(ClientRectangle));
            Append(point);
            if (PointDrawn != null) PointDrawn(this, new DrawPointEventArgs(point));
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) EndStroke(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (drawing && !Capture) EndStroke(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); EndStroke(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) EndStroke(); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) { EndStroke(); e.Handled = true; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            RectangleF plot = settings.PlotArea(ClientRectangle);
            using (Pen grid = new Pen(Color.FromArgb(227, 232, 238)))
            using (Pen border = new Pen(Color.FromArgb(150, 161, 177)))
            using (Brush label = new SolidBrush(Color.FromArgb(91, 105, 124)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    float x = plot.Left + plot.Width * i / 4, y = plot.Bottom - plot.Height * i / 4;
                    g.DrawLine(grid, x, plot.Top, x, plot.Bottom); g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    string xValue = (settings.XMin + (settings.XMax - settings.XMin) * i / 4).ToString("0.##", CultureInfo.InvariantCulture);
                    string yValue = (settings.YMin + (settings.YMax - settings.YMin) * i / 4).ToString("0.##", CultureInfo.InvariantCulture);
                    SizeF xSize = g.MeasureString(xValue, Font), ySize = g.MeasureString(yValue, Font);
                    g.DrawString(xValue, Font, label, x - xSize.Width / 2, plot.Bottom + 3);
                    g.DrawString(yValue, Font, label, plot.Left - ySize.Width - 4, y - ySize.Height / 2);
                }
                g.DrawRectangle(border, plot.X, plot.Y, plot.Width, plot.Height);
                g.DrawString("X / " + settings.Unit, Font, label, plot.Right + 12, plot.Bottom + 2);
                g.DrawString("Y / " + settings.Unit, Font, label, plot.Left - 42, 2);
            }
            GraphicsState state = g.Save();
            g.SetClip(plot); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen path = new Pen(PathColor, 2F))
            using (Brush marker = new SolidBrush(PathColor))
            {
                for (int i = 1; i < points.Count; i++)
                    if (!points[i].Break) g.DrawLine(path, settings.ToScreen(points[i - 1].Point, plot), settings.ToScreen(points[i].Point, plot));
                if (points.Count > 0)
                {
                    PointF last = settings.ToScreen(points[points.Count - 1].Point, plot);
                    g.FillEllipse(marker, last.X - 4, last.Y - 4, 8, 8);
                }
            }
            g.Restore(state);
        }
    }
}
