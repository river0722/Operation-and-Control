using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Operations_Control;
using Operations_Control.Controls;
using Operations_Control.Models;
using Operations_Control.Services;
using Operations_Control.Views;

internal static class SmokeTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }

    private static void Throws(Action action, string message)
    {
        bool thrown = false;
        try { action(); } catch (InvalidOperationException) { thrown = true; }
        Check(thrown, message);
    }

    private static void Pump(int milliseconds)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }

    private static void Until(Func<bool> condition, string message)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!condition() && timer.ElapsedMilliseconds < 5000) Pump(20);
        Check(condition(), message);
    }

    private static T Find<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T) return (T)child;
            T nested = Find<T>(child); if (nested != null) return nested;
        }
        return null;
    }

    private static Button FindButton(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button && child.Text == text) return (Button)child;
            Button nested = FindButton(child, text); if (nested != null) return nested;
        }
        return null;
    }

    private static double Position(ControllerSession session, Form form)
    { return Position(session, form, 1); }

    private static double Position(ControllerSession session, Form form, int motor)
    {
        bool done = false; double value = 0; Exception failure = null;
        session.Execute(form, delegate { value = Double.Parse(session.Send("#" + motor + "P").Response, CultureInfo.InvariantCulture); },
            delegate(Exception error) { failure = error; done = true; });
        Until(delegate { return done; }, "Query must complete");
        if (failure != null) throw failure;
        return value;
    }

    private static void Mouse(Button button, bool down)
    {
        if (down) button.Focus();
        typeof(Control).GetMethod(down ? "OnMouseDown" : "OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
    }

    private static void Screenshot(Form form, string path)
    {
        Pump(100);
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static void DrawMouse(MotionCanvas canvas, string method, double x, double y)
    {
        Point point = Point.Round(canvas.Settings.ToScreen(new MotionPoint(x, y), canvas.Settings.PlotArea(canvas.ClientRectangle)));
        Cursor.Position = canvas.PointToScreen(point);
        typeof(MotionCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) });
    }

    private static void RejectArgument(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }

    private static void TestCoordinates()
    {
        CanvasSettings settings = new CanvasSettings(-20, 20, -10, 10, 200, 500, "mm");
        MotionPoint counts = settings.ToCounts(new MotionPoint(3, -4));
        Check(counts.X == 600 && counts.Y == -2000, "Each axis must use its own ct/mm scale");
        MotionPoint mm = settings.FromCounts(counts.X, counts.Y);
        Check(mm.X == 3 && mm.Y == -4, "Feedback conversion must roundtrip");
        RectangleF plot = settings.PlotArea(new Rectangle(0, 0, 600, 400));
        Check(Math.Abs(plot.Width / plot.Height - 2) < 0.001, "Canvas must preserve physical aspect ratio");
        PointF top = settings.ToScreen(new MotionPoint(-20, 10), plot);
        Check(top.X == plot.Left && top.Y == plot.Top, "Y must increase upward");
        MotionPoint screenPoint = settings.FromScreen(Point.Round(settings.ToScreen(new MotionPoint(3, -4), plot)), plot);
        Check(Math.Abs(screenPoint.X - 3) < 0.1 && Math.Abs(screenPoint.Y + 4) < 0.1, "Mouse mapping must roundtrip");
        RejectArgument(delegate { new CanvasSettings(0, 0, 0, 10, 100, 100, "mm"); }, "Zero width must be rejected");
        RejectArgument(delegate { new CanvasSettings(-10, 10, -10, 10, 0, 100, "mm"); }, "Unknown scale must be rejected");
        RejectArgument(delegate { new CanvasSettings(-10, 10, -10, 10, Double.NaN, 100, "mm"); }, "Nonfinite scale must be rejected");
        RejectArgument(delegate { settings.ToCounts(new MotionPoint(21, 0)); }, "Targets beyond applied range must be rejected");
        using (MotionCanvas canvas = new MotionCanvas())
        {
            for (int i = 0; i < 5000; i++) canvas.Append(new MotionPoint(i, i));
            Check(canvas.PointCount <= 4000, "Trajectory memory must remain bounded");
        }
        using (CanvasView canvases = new CanvasView())
        {
            canvases.ConfigureMode(false); canvases.SetState(true, false, false);
            Check(!canvases.MappingReady && !canvases.Follow.Enabled && canvases.XScale.Value == 0,
                "Real mode must not inherit demo calibration");
            canvases.XMin.Value = -5; canvases.XMax.Value = 5;
            canvases.YMin.Value = -7; canvases.YMax.Value = 7;
            canvases.XScale.Value = 200; canvases.YScale.Value = 300;
            typeof(CanvasView).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvases, new object[] { null, EventArgs.Empty });
            canvases.SetState(true, false, false);
            Check(canvases.HardwareCalibrated && canvases.Follow.Enabled, "Explicitly applied valid calibration must enable following");
            canvases.AxisMappingChanged(); canvases.SetState(true, false, false);
            Check(!canvases.HardwareCalibrated && !canvases.Follow.Enabled, "Changing real motor mapping must invalidate previous calibration");
            canvases.XScale.Value = 400; canvases.SetState(true, false, false);
            Check(!canvases.MappingReady && !canvases.Follow.Enabled, "Unapplied scale edits must disable following");
            canvases.ConfigureMode(true);
            Check(canvases.MappingReady && !canvases.HardwareCalibrated && canvases.Settings.Unit == "mm", "Demo mode must be clearly separate from real calibration");
            canvases.Display(10, 10); int count = canvases.ActualCanvas.PointCount;
            canvases.Display(1e100, 1e100);
            Check(canvases.ActualCanvas.PointCount == count, "Out-of-range feedback must not generate unbounded drawing coordinates");
        }
    }

    private static void TestTransport()
    {
        using (SimulatorTransport simulator = new SimulatorTransport())
        {
            Throws(delegate { simulator.Send("#1P"); }, "Disconnected commands must fail");
            Check(simulator.Connect(0), "Simulation must connect");
            simulator.Send("I122=1"); simulator.Send("#1J^100"); Thread.Sleep(120);
            Check(simulator.Send("#1P").Response == "100", "Relative move must reach target");
            simulator.Send("#1J=-20"); Thread.Sleep(140);
            Check(simulator.Send("#1P").Response == "-20", "Absolute move must support negative target");
            simulator.Send("#1J+"); Thread.Sleep(40);
            double moving = Double.Parse(simulator.Send("#1P").Response, CultureInfo.InvariantCulture);
            Check(moving > -20, "Continuous jogging must move");
            simulator.Send("#1J/"); string stopped = simulator.Send("#1P").Response; Thread.Sleep(40);
            Check(simulator.Send("#1P").Response == stopped, "Stop must hold position");
            simulator.Send("#1HOMEZ"); Check(simulator.Send("#1P").Response == "0", "HOMEZ must reset origin");
            simulator.Send("I822=2"); Check(simulator.Send("I822").Response == "2", "Motor 8 speed must be addressable");
            Throws(delegate { simulator.Send("#9J+"); }, "Invalid axes must fail");
            Throws(delegate { simulator.Send("I122=-1"); }, "Negative speed must fail");
            Throws(delegate { simulator.Send("I122=16"); }, "Excessive speed must fail");
            Throws(delegate { simulator.Send("UNDEFINE ALL"); }, "Unsupported commands must not silently succeed");
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "#1J+"); simulator.Download(file); Thread.Sleep(40);
                Check(simulator.Send("#1P").Response == "0", "Simulation download must not execute script");
            }
            finally { File.Delete(file); }
            simulator.Disconnect(); Check(!simulator.IsConnected, "Disconnect must clear state");
        }
        CommandResult.Validate("", unchecked((int)0x80000000));
        CommandResult.Validate("123", 3);
        Throws(delegate { CommandResult.Validate("", unchecked((int)0xC0000000)); }, "Timeout status must fail");
        Throws(delegate { CommandResult.Validate("", unchecked((int)0x10000000)); }, "Unsolicited response must not be treated as matched reply");
        Throws(delegate { CommandResult.Validate("ERR003", unchecked((int)0x80000006)); }, "Controller errors must fail even with EOT");
    }

    private static void TestInterface(string output)
    {
        using (Form1 form = new Form1())
        {
            form.Show(); Pump(100);
            ConnectionView connection = Find<ConnectionView>(form);
            MotionView motion = Find<MotionView>(form);
            ProgramView programs = Find<ProgramView>(form);
            CanvasView canvases = Find<CanvasView>(form);
            PathView path = Find<PathView>(form);
            ControllerSession session = (ControllerSession)typeof(Form1).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Check(connection.IsSimulation, "Simulation must be the default");
            Check(connection.ConnectButton.Parent.ClientRectangle.Contains(connection.ConnectButton.Bounds) &&
                connection.DisconnectButton.Parent.ClientRectangle.Contains(connection.DisconnectButton.Bounds), "Connection buttons fit the compact header");
            Check(!motion.MoveButton.Enabled && !programs.SendButton.Enabled, "Disconnected operations must be disabled");
            DrawMouse(canvases.DrawingCanvas, "OnMouseDown", 1, 1);
            DrawMouse(canvases.DrawingCanvas, "OnMouseMove", 2, 2);
            DrawMouse(canvases.DrawingCanvas, "OnMouseUp", 2, 2);
            Check(canvases.DrawingCanvas.PointCount == 2 && !canvases.Follow.Enabled, "Offline drawing must work without enabling motion");
            Check(motion.XAxis == 1 && motion.YAxis == 2, "X/Y must default to motors 1 and 2");
            connection.ConnectButton.PerformClick();
            Until(delegate { return connection.DisconnectButton.Enabled; }, "Simulation connection must update UI");
            Pump(550);
            Screenshot(form, Path.Combine(output, "motion-control.png"));
            path.Pages.SelectedTab = (TabPage)motion.Parent.Parent;
            motion.Target.Value = 100;
            motion.MoveButton.PerformClick();
            Pump(600);
            Screenshot(form, Path.Combine(output, "manual-control.png"));
            Check(Position(session, form) == 100, "UI absolute move must reach target");
            Button plus = FindButton(motion, "X +");
            Mouse(plus, true); Mouse(plus, false); Pump(300);
            Check(Position(session, form) == 200, "UI single-step must move exactly one step");
            motion.SingleStep.Checked = false;
            Mouse(plus, true); Pump(150);
            Check(plus.Enabled, "Held jog button must remain enabled to retain capture");
            double held = Position(session, form);
            Mouse(plus, false); Pump(100); double released = Position(session, form);
            Pump(120); Check(Position(session, form) == released && released > held, "Release must stop continuous jogging");
            Mouse(plus, true); Mouse(plus, false); Pump(100);
            double fast = Position(session, form); Pump(100);
            Check(Position(session, form) == fast, "Fast press/release must preserve start-stop ordering");
            Mouse(plus, true); Pump(80);
            typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { EventArgs.Empty });
            Pump(100); double deactivated = Position(session, form); Pump(80);
            Check(Position(session, form) == deactivated, "Window deactivation must stop continuous jogging");
            Mouse(plus, true); Pump(60); plus.Capture = true; plus.Capture = false;
            Pump(80); double captureLost = Position(session, form); Pump(80);
            Check(Position(session, form) == captureLost, "Lost mouse capture must stop continuous jogging");
            path.Pages.SelectedTab = path.ProgramPage;
            programs.Command.Text = "VERSION"; programs.SendButton.PerformClick();
            Until(delegate { return programs.Response.Text.IndexOf("simulator") >= 0; }, "Command response must render");
            Screenshot(form, Path.Combine(output, "commands-and-programs.png"));
            programs.Command.Text = "BAD COMMAND"; programs.SendButton.PerformClick();
            Pump(120); Check(programs.SendButton.Enabled, "Invalid command must leave UI usable");
            path.Pages.SelectedIndex = 0;
            TestDrawing(form, connection, motion, programs, canvases, session, output);
            form.Size = form.MinimumSize;
            Screenshot(form, Path.Combine(output, "minimum-window.png"));
            Button stop = FindButton(form, "停止");
            Check(stop.Visible && !path.Contains(stop), "Global stop must stay visible outside all tabs");
            Check(form.RectangleToScreen(form.ClientRectangle).Contains(stop.RectangleToScreen(stop.ClientRectangle)), "Stop must fit at minimum window size");
            Check(!motion.AutoScroll && canvases.DrawingCanvas.Settings.PlotArea(canvases.DrawingCanvas.ClientRectangle).Height > 120,
                "Usable canvas plots must remain visible at minimum size");
            path.Pages.SelectedTab = path.ProgramPage; Pump(100);
            Check(form.RectangleToScreen(form.ClientRectangle).Contains(programs.SendButton.RectangleToScreen(programs.SendButton.ClientRectangle)) &&
                form.RectangleToScreen(form.ClientRectangle).Contains(programs.DownloadButton.RectangleToScreen(programs.DownloadButton.ClientRectangle)),
                "Program commands and download fit the minimum main window");
            Screenshot(form, Path.Combine(output, "minimum-program.png"));
            path.Pages.SelectedIndex = 0;
            connection.DisconnectButton.PerformClick();
            Until(delegate { return connection.ConnectButton.Enabled; }, "Disconnect must clear UI state");
            Check(!motion.MoveButton.Enabled, "Disconnected movement must be disabled again");
            form.Close(); Until(delegate { return form.IsDisposed; }, "Form must close without worker hang");
        }
    }

    private static void TestDrawing(Form1 form, ConnectionView connection, MotionView motion, ProgramView programs,
        CanvasView canvases, ControllerSession session, string output)
    {
        motion.Speed.Value = 5; Pump(150);
        double before = Position(session, form);
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", -1, -1); Pump(160);
        DrawMouse(canvases.DrawingCanvas, "OnMouseUp", -1, -1); Pump(120);
        Check(Position(session, form) == before, "Connected preview drawing must issue no movement when follow is unchecked");
        motion.YMotor.SelectedIndex = 0; Pump(150);
        Check(!canvases.Follow.Enabled, "Same motor mapping must disable dual-axis following");
        motion.YMotor.SelectedIndex = 1; Pump(180);
        Check(canvases.Follow.Enabled, "Feedback-ready simulation must allow following");
        canvases.Follow.Checked = true;
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", -4, -3); Pump(350);
        DrawMouse(canvases.DrawingCanvas, "OnMouseMove", 4, 3); Pump(350);
        Check(!motion.MoveButton.Enabled && !programs.SendButton.Enabled && motion.StopButton.Enabled,
            "Drawing follow must lock competing commands while preserving stop");
        Check(Math.Abs(Position(session, form) - 400) < 10 && Math.Abs(Position(session, form, 2) - 300) < 10,
            "Mouse mm target must move both simulated axes through their conversion scales");
        Check(canvases.ActualCanvas.PointCount > 3, "Actual trajectory must accumulate controller feedback samples");
        DrawMouse(canvases.DrawingCanvas, "OnMouseMove", -8, -6); Pump(110);
        DrawMouse(canvases.DrawingCanvas, "OnMouseUp", -8, -6); Pump(120);
        double stoppedX = Position(session, form), stoppedY = Position(session, form, 2); Pump(200);
        Check(Position(session, form) == stoppedX && Position(session, form, 2) == stoppedY,
            "Mouse release must stop both axes before distant target completes");
        Check(motion.MoveButton.Enabled && !canvases.DrawingCanvas.IsDrawing, "Release must restore normal controls");

        // Deliberately hold the communication worker so a queued target can be cancelled before execution.
        using (ManualResetEvent started = new ManualResetEvent(false))
        using (ManualResetEvent release = new ManualResetEvent(false))
        {
            session.Execute(form, delegate { started.Set(); release.WaitOne(2000); }, delegate(Exception error) { });
            Check(started.WaitOne(2000), "Worker blocker must start");
            DrawMouse(canvases.DrawingCanvas, "OnMouseDown", 7, 7);
            CanvasFollower follower = (CanvasFollower)typeof(Form1).GetField("follower", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            follower.Pump();
            DrawMouse(canvases.DrawingCanvas, "OnMouseUp", 7, 7);
            release.Set(); Pump(200);
            Check(Position(session, form) == stoppedX && Position(session, form, 2) == stoppedY,
                "Cancelled queued target must never restart motion");
            RejectArgument(delegate { follower.Begin(1, 1, 1); }, "Follower must reject duplicate axes even outside UI");
        }

        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", 8, 8); Pump(110);
        canvases.DrawingCanvas.Capture = false; Pump(150);
        double lostCapture = Position(session, form); Pump(150);
        Check(!canvases.DrawingCanvas.IsDrawing && Position(session, form) == lostCapture, "Lost canvas capture must stop following");
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", -8, -8); Pump(110);
        typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { EventArgs.Empty });
        Pump(150); double deactivated = Position(session, form); Pump(150);
        Check(Position(session, form) == deactivated && !canvases.DrawingCanvas.IsDrawing, "Window deactivation must cancel drawing follow");
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", 8, 8); Pump(110);
        FindButton(form, "停止").PerformClick(); Pump(150);
        double buttonStopped = Position(session, form); Pump(150);
        Check(Position(session, form) == buttonStopped && !canvases.DrawingCanvas.IsDrawing, "Stop button must cancel drawing before stopping axes");

        FindButton(form, "清空轨迹").PerformClick(); FindButton(form, "清空绘图").PerformClick();
        Check(canvases.DrawingCanvas.PointCount == 0 && Position(session, form) == buttonStopped, "Clearing canvases must not move motors");
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", 6, 0); Pump(300);
        for (int i = 1; i <= 24; i++)
        {
            double angle = i * Math.PI / 12;
            DrawMouse(canvases.DrawingCanvas, "OnMouseMove", 6 * Math.Cos(angle), 6 * Math.Sin(angle)); Pump(110);
        }
        DrawMouse(canvases.DrawingCanvas, "OnMouseUp", 6, 0); Pump(150);
        programs.Command.Clear();
        Screenshot(form, Path.Combine(output, "dual-canvases.png"));
        PathView path = Find<PathView>(form); path.Pages.SelectedTab = path.ProgramPage;
        programs.EditButton.PerformClick(); Pump(100);
        ProgramEditorForm editor = (ProgramEditorForm)typeof(Form1).GetField("editorWindow", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        editor.Editor.Text = "OPEN PROG 1\r\n; 在此编辑 PMAC 程序\r\nCLOSE";
        Screenshot(form, Path.Combine(output, "program-editor.png")); editor.Editor.Clear();
        Check(!editor.TopLevel && editor.Visible && form.Contains(editor), "Editor must stay embedded in the main program page");
        path.Pages.SelectedIndex = 0;

        canvases.Follow.Checked = true; DrawMouse(canvases.DrawingCanvas, "OnMouseDown", -8, -8); Pump(110);
        connection.DisconnectButton.PerformClick();
        Until(delegate { return connection.ConnectButton.Enabled; }, "Disconnect during drawing must complete");
        Check(!canvases.DrawingCanvas.IsDrawing && !canvases.Follow.Checked, "Disconnect must cancel and disarm following");
        connection.ConnectButton.PerformClick();
        Until(delegate { return canvases.Follow.Enabled; }, "Reconnect must receive new feedback before allowing following");
        Pump(150);
        Check(Position(session, form) == 0 && Position(session, form, 2) == 0, "Reconnect must not replay stale drawing targets");
    }

    private static void TestDesigner()
    {
        Type[] views = new Type[] { typeof(Form1), typeof(ConnectionView), typeof(MotionView), typeof(ProgramView), typeof(LogView),
            typeof(AxisReadout), typeof(ReadoutView), typeof(CanvasView), typeof(PathView), typeof(ProgramEditorForm), typeof(LogForm) };
        foreach (Type type in views)
        {
            using (DesignSurface surface = new DesignSurface(type))
            {
                foreach (object error in surface.LoadErrors)
                    throw new Exception(type.Name + " designer failed: " + error);
                IDesignerHost host = (IDesignerHost)surface.GetService(typeof(IDesignerHost));
                Control control = host.RootComponent as Control;
                Check(control != null && control.Controls.Count > 0, type.Name + " must load its controls in design mode");
                Check(host.GetDesigner(control) != null, type.Name + " must expose a Windows Forms designer");
                if (control is Form1)
                {
                    object session = typeof(Form1).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
                    Check(session == null, "Opening the form designer must not start controller services");
                    Check(Find<ReadoutView>(control) != null && Find<CanvasView>(control) != null, "Four-quadrant and canvas modules must load in designer");
                }
                if (control is AxisReadout)
                {
                    PropertyDescriptor caption = TypeDescriptor.GetProperties(control)["Caption"];
                    caption.SetValue(control, "设计器测试");
                    Check(((AxisReadout)control).Caption == "设计器测试", "Readout caption must be editable through the property grid");
                }
            }
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string output = args.Length > 0 ? args[0] : "."; Directory.CreateDirectory(output);
            TestDesigner(); TestCoordinates(); TestTransport(); TestInterface(output);
            Console.WriteLine("PASS: " + assertions + " assertions; simulation only, no hardware accessed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
