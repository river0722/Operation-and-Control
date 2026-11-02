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
        private bool hasCurrentPosition;
        private MotionPoint currentPosition;
        private bool followHandleEnabled, draggingPosition, handleHovered;
        private MotionPoint grabOffset;
        private float deviceDpiScale = 1;
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
        [DefaultValue(false)]
        public bool FollowHandleEnabled
        {
            get { return followHandleEnabled; }
            set { if (followHandleEnabled == value) return; followHandleEnabled = value; handleHovered = false; Cursor = Cursors.Default; Invalidate(); }
        }
        [Browsable(false)]
        public bool IsDraggingPosition { get { return draggingPosition; } }
        [Category("Appearance")]
        public Color PathColor { get { return pathColor; } set { pathColor = value; Invalidate(); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CanvasSettings Settings { get { return settings; } set { settings = value; Invalidate(); } }
        [Browsable(false)]
        public bool IsDrawing { get { return drawing; } }
        [Browsable(false)]
        public bool HasPointerPoint { get; private set; }
        [Browsable(false)]
        public MotionPoint PointerPoint { get; private set; }
        [Browsable(false)]
        public bool HasCurrentPosition { get { return hasCurrentPosition; } }
        [Browsable(false)]
        public MotionPoint CurrentPosition { get { return currentPosition; } }
        [Browsable(false)]
        public int PointCount { get { return points.Count; } }

        public void ClearPath() { points.Clear(); newSegment = true; Invalidate(); }
        public void SetCurrentPosition(MotionPoint point)
        {
            if (Double.IsNaN(point.X) || Double.IsInfinity(point.X) || Double.IsNaN(point.Y) || Double.IsInfinity(point.Y) || !settings.Contains(point))
                hasCurrentPosition = false;
            else { currentPosition = point; hasCurrentPosition = true; }
            Invalidate();
        }
        public void ClearCurrentPosition() { hasCurrentPosition = false; Invalidate(); }
        public void BreakPath() { newSegment = true; }
        public DrawingPath GetPath()
        {
            DrawingPath result = new DrawingPath(); List<MotionPoint> stroke = null;
            foreach (Vertex vertex in points)
            {
                if (stroke == null || vertex.Break) { stroke = new List<MotionPoint>(); result.Strokes.Add(stroke); }
                stroke.Add(vertex.Point);
            }
            result.Strokes.RemoveAll(delegate(List<MotionPoint> item) { return item.Count < 2; }); return result;
        }
        public void LoadPath(DrawingPath path)
        {
            points.Clear();
            if (path != null) foreach (List<MotionPoint> stroke in path.Strokes)
                for (int i = 0; i < stroke.Count; i++) points.Add(new Vertex(stroke[i], i == 0));
            newSegment = true; Invalidate();
        }
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
            drawing = false; draggingPosition = false; newSegment = true; Capture = false;
            Cursor = Cursors.Default; Invalidate();
            if (StrokeEnded != null) StrokeEnded(this, EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!DrawingEnabled || e.Button != MouseButtons.Left || !settings.PlotArea(ClientRectangle).Contains(e.Location)) return;
            PointerPoint = settings.FromScreen(e.Location, settings.PlotArea(ClientRectangle)); HasPointerPoint = true;
            Focus();
            draggingPosition = HitPositionHandle(e.Location);
            grabOffset = draggingPosition ? new MotionPoint(PointerPoint.X - currentPosition.X, PointerPoint.Y - currentPosition.Y) : new MotionPoint(0, 0);
            if (draggingPosition) PointerPoint = currentPosition;
            drawing = true; newSegment = true; Capture = true;
            Cursor = draggingPosition ? Cursors.SizeAll : Cursors.Default;
            if (StrokeStarted != null) StrokeStarted(this, EventArgs.Empty);
            if (drawing) DrawAt(e.Location);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!drawing)
            {
                bool hovered = DrawingEnabled && HitPositionHandle(e.Location);
                Cursor = hovered ? Cursors.Hand : Cursors.Default;
                if (hovered != handleHovered) { handleHovered = hovered; Invalidate(); }
                return;
            }
            if (!settings.PlotArea(ClientRectangle).Contains(e.Location)) { EndStroke(); return; }
            DrawAt(e.Location);
        }
        private void DrawAt(Point location)
        {
            MotionPoint point = settings.FromScreen(location, settings.PlotArea(ClientRectangle));
            if (draggingPosition) point = new MotionPoint(point.X - grabOffset.X, point.Y - grabOffset.Y);
            if (!settings.Contains(point)) { EndStroke(); return; }
            PointerPoint = point; HasPointerPoint = true;
            Append(point);
            if (PointDrawn != null) PointDrawn(this, new DrawPointEventArgs(point));
        }
        private bool HitPositionHandle(Point location)
        {
            if (!followHandleEnabled || !hasCurrentPosition) return false;
            PointF center = settings.ToScreen(currentPosition, settings.PlotArea(ClientRectangle));
            double dx = location.X - center.X, dy = location.Y - center.Y;
            double radius = 18 * deviceDpiScale;
            return dx * dx + dy * dy <= radius * radius;
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e); handleHovered = false;
            if (!drawing) Cursor = Cursors.Default;
            Invalidate();
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
            deviceDpiScale = g.DpiX / 96F;
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
                    string yValue = (settings.YMax - (settings.YMax - settings.YMin) * i / 4).ToString("0.##", CultureInfo.InvariantCulture);
                    SizeF xSize = g.MeasureString(xValue, Font), ySize = g.MeasureString(yValue, Font);
                    g.DrawString(xValue, Font, label, x - xSize.Width / 2, plot.Top - xSize.Height - 3);
                    g.DrawString(yValue, Font, label, plot.Left - ySize.Width - 4, y - ySize.Height / 2);
                }
                g.DrawRectangle(border, plot.X, plot.Y, plot.Width, plot.Height);
                string xTitle = "X / " + settings.Unit;
                SizeF xTitleSize = g.MeasureString(xTitle, Font);
                g.DrawString(xTitle, Font, label, plot.Right + 12, plot.Top - xTitleSize.Height - 3);
                g.DrawString("Y / " + settings.Unit, Font, label, plot.Left - 60, 2);
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
            if (hasCurrentPosition)
            {
                PointF current = settings.ToScreen(currentPosition, plot);
                if (followHandleEnabled)
                {
                    float radius = 18 * deviceDpiScale;
                    using (Brush halo = new SolidBrush(Color.FromArgb(handleHovered || draggingPosition ? 48 : 24, 224, 90, 32)))
                        g.FillEllipse(halo, current.X - radius, current.Y - radius, radius * 2, radius * 2);
                    using (Brush label = new SolidBrush(Color.FromArgb(160, 64, 23)))
                    {
                        string hint = draggingPosition ? "拖动中 · 松开停止" : "按住十字拖动";
                        SizeF size = g.MeasureString(hint, Font);
                        g.DrawString(hint, Font, label, Math.Max(plot.Left, Math.Min(current.X + 22, plot.Right - size.Width)),
                            Math.Max(plot.Top, Math.Min(current.Y + 12, plot.Bottom - size.Height)));
                    }
                }
                if (draggingPosition)
                {
                    PointF target = settings.ToScreen(PointerPoint, plot);
                    using (Pen targetPen = new Pen(Color.FromArgb(224, 90, 32), 1.5F))
                    {
                        targetPen.DashStyle = DashStyle.Dash;
                        g.DrawLine(targetPen, current, target);
                        g.DrawEllipse(targetPen, target.X - 9, target.Y - 9, 18, 18);
                        g.DrawLine(targetPen, target.X - 12, target.Y, target.X + 12, target.Y);
                        g.DrawLine(targetPen, target.X, target.Y - 12, target.X, target.Y + 12);
                    }
                }
                using (Pen currentPen = new Pen(Color.FromArgb(224, 90, 32), 2F))
                {
                    g.DrawEllipse(currentPen, current.X - 6, current.Y - 6, 12, 12);
                    g.DrawLine(currentPen, current.X - 9, current.Y, current.X + 9, current.Y);
                    g.DrawLine(currentPen, current.X, current.Y - 9, current.X, current.Y + 9);
                }
            }
            g.Restore(state);
        }
    }
}
