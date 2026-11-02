using Application = System.Windows.Forms.Application;
using System;
using System.Collections.Generic;
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
using Operations_Control.Communication;
using Operations_Control.Motion;
using Operations_Control.Files;
using Operations_Control.Application;
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
        session.Execute(delegate { value = Double.Parse(session.Send("#" + motor + "P").Response, CultureInfo.InvariantCulture); },
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
        PointF top = settings.ToScreen(new MotionPoint(-20, -10), plot);
        Check(top.X == plot.Left && top.Y == plot.Top, "Y must increase downward");
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
            Throws(delegate { simulator.Send("I122=51"); }, "Speeds above the new 50 ct/ms cap must fail");
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

    // Model the firmware's text-entry rules, rather than the simulator's direct
    // trajectory API, so ERR005 and ambiguous online/buffered commands are covered.
    private sealed class RotaryFirmware : IControllerTransport
    {
        public readonly List<string> Commands = new List<string>();
        private readonly HashSet<int> buffers = new HashSet<int>();
        public bool Open, Running, RejectDefine, LoseRunReply;
        public int Runs, Aborts, Defines, Moves, Remaining, AppendedReturns;
        public string FailPrefix, BlockAt;
        public ManualResetEvent Entered, Release;
        private int cs;
        private bool pointed;
        public bool IsConnected { get { return true; } }
        public string DeviceName { get { return "rotary protocol test"; } }
        public bool Connect(int ownerWindow) { return true; }
        public void Disconnect() { }
        public void Dispose() { }
        public void Download(string path) { throw new Exception("Realtime follow must not download a fixed program"); }
        public CommandResult Send(string command)
        {
            Commands.Add(command);
            string text = command.Trim().ToUpperInvariant();
            if (text == BlockAt) { Entered.Set(); if (!Release.WaitOne(2000)) throw new Exception("Protocol test blocker timed out"); }
            if (FailPrefix != null && text.StartsWith(FailPrefix)) { FailPrefix = null; throw new InvalidOperationException("injected write failure"); }
            System.Text.RegularExpressions.Match address = System.Text.RegularExpressions.Regex.Match(text, @"^&(\d+)\s*(.*)$");
            if (address.Success) { cs = Int32.Parse(address.Groups[1].Value); text = address.Groups[2].Value; }
            string response = "";
            if (text == "CLOSE") { if (Open) { AppendedReturns++; Running = false; } Open = false; }
            else if (text == "??") response = Running ? "000001000000000000" : "000000020000000000";
            else if (text.StartsWith("DEFINE ROT"))
            {
                if (Open || RejectDefine || buffers.Contains(cs)) throw new InvalidOperationException("ERR003 buffer allocation conflict");
                buffers.Add(cs); Defines++;
            }
            else if (text == "OPEN ROT")
            {
                if (Open || !buffers.Contains(cs)) throw new InvalidOperationException("ERR007 invalid buffer state");
                Open = true;
            }
            else if (text == "CLEAR")
            {
                if (!Open || Running) throw new InvalidOperationException("Cannot clear closed or running rotary buffer");
                Moves = 0;
            }
            else if (text == "B0")
            {
                if (Open) throw new InvalidOperationException("B0 was interpreted as an unintended B-axis move");
                if (!buffers.Contains(cs)) throw new InvalidOperationException("ERR003 missing rotary buffer");
                pointed = true;
            }
            else if (text == "R")
            {
                if (!pointed || Moves == 0) throw new InvalidOperationException("Must start a prepared buffer with a complete move");
                Running = true; Runs++;
                if (LoseRunReply) { LoseRunReply = false; throw new InvalidOperationException("Lost run acknowledgement"); }
            }
            else if (text == "A") { Running = false; Aborts++; }
            else if (text == "PR")
            {
                if (!Open) throw new Exception("PR only has defined semantics with rotary entry open");
                response = Remaining.ToString(CultureInfo.InvariantCulture);
            }
            else if (text == "ABS" || text.StartsWith("PVT ") || text.StartsWith("SPLINE1 ") ||
                text.StartsWith("X") || text.StartsWith("DWELL") || text == "RETURN")
            {
                if (!Open) throw new InvalidOperationException("ERR005 Command not allowed unless buffer is open");
                if (text.StartsWith("X")) Moves++;
            }
            else if (text.StartsWith("I") || text.StartsWith("#"))
            {
                bool feedback = System.Text.RegularExpressions.Regex.IsMatch(text, @"^#[1-8](P|F|\?)$");
                if (Open && !feedback) throw new InvalidOperationException("Online query/setting leaked into rotary program");
                response = text.EndsWith("?") ? "882000000401" : "0";
            }
            else if (text.Length != 0) throw new Exception("Unexpected protocol command: " + command);
            return new CommandResult(response, 0);
        }
    }

    private static ControllerSession ProtocolSession(RotaryFirmware firmware)
    {
        ControllerSession session = new ControllerSession(null);
        typeof(ControllerSession).GetField("transport", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, firmware);
        return session;
    }

    private static void TestRotaryFollow()
    {
        RotaryFirmware firmware = new RotaryFirmware();
        Throws(delegate { firmware.Send("PVT 20"); }, "Firmware rejects the previous immediate PVT path with ERR005");
        using (ControllerSession session = ProtocolSession(firmware))
        {
            session.BeginPvtFollow(1, 1, 2, CanvasSettings.Default);
            Check(firmware.Open && firmware.Runs == 0 && firmware.AppendedReturns == 0, "Preparation opens rotary entry after B0 and does not start motion or insert RETURN");
            session.AppendPvtFollow(20, new MotionPoint(1, 1), new MotionPoint(0, 0));
            Check(firmware.Running && firmware.Runs == 1 && firmware.Open, "First complete PVT move starts the rotary program while keeping entry open");
            session.ReadFeedback(1, 2, 1, 1);
            Throws(delegate { session.Send("I122"); }, "Ambiguous parameter queries are rejected while rotary entry is open");
            Check(!firmware.Commands.Contains("I122"), "Rejected parameter query never becomes an unintended buffered axis command");
            session.AppendPvtFollow(20, new MotionPoint(2, 1), new MotionPoint(0, 0));
            Check(firmware.Runs == 1 && firmware.Moves == 2 && firmware.Open && firmware.AppendedReturns == 0,
                "Further PVT segments append without restarting, closing/adding RETURN, or blocking feedback queries");
            session.AbortPvtFollow(1, 1, 2);
            Check(!firmware.Running && !firmware.Open && firmware.Moves == 0, "Abort stops execution, clears stale moves and leaves entry closed");
            session.BeginSplineFollow(1, 1, 2, CanvasSettings.Default);
            session.AppendSplineFollow(30, new MotionPoint[] { new MotionPoint(2, 1), new MotionPoint(3, 2) });
            Check(firmware.Defines == 1 && firmware.Running && firmware.Moves == 2 && firmware.Open,
                "Spline1 reuses the session-owned buffer and executes through the same valid protocol");
            session.AbortSplineFollow(1, 1, 2);
            session.BeginPvtFollow(9, 1, 2, CanvasSettings.Default);
            Check(firmware.Commands.Contains("I5990=1000") && firmware.Commands.Contains("&9B0"), "Rotary setup addresses the selected coordinate system and its time base");
            firmware.Remaining = 48;
            Throws(delegate { session.AppendPvtFollow(20, new MotionPoint(1, 1), new MotionPoint(0, 0)); }, "Full rotary entry returns temporary backpressure");
            Check(!firmware.Running && firmware.Open && firmware.Moves == 0, "Temporary backpressure leaves the stream intact without writing a partial move");
            firmware.Remaining = 0;
            session.AppendPvtFollow(20, new MotionPoint(1, 1), new MotionPoint(0, 0));
            Check(firmware.Running && firmware.Moves == 1, "Capacity returning resumes the same stream");
            session.AbortPvtFollow(9, 1, 2);
        }
        firmware = new RotaryFirmware();
        double flowTime = 0;
        RotaryProgramStream flow = new RotaryProgramStream(delegate(string command) { return firmware.Send(command).Response; }, delegate { return flowTime; });
        flow.Begin(1, 1, 2, CanvasSettings.Default);
        flow.Append(new string[] { "PVT 60", "X1:0 Y1:0" });
        firmware.Remaining = 48;
        Throws(delegate { flow.Append(new string[] { "PVT 60", "X2:0 Y1:0" }); }, "A running full buffer is temporarily throttled");
        flowTime = 9000; firmware.Remaining = 47;
        Throws(delegate { flow.Append(new string[] { "PVT 60", "X2:0 Y1:0" }); }, "A draining buffer may wait without losing the stream");
        flowTime = 18000;
        Throws(delegate { flow.Append(new string[] { "PVT 60", "X2:0 Y1:0" }); }, "Progress resets the ten-second no-drain deadline");
        Check(firmware.Running && firmware.Aborts == 0 && firmware.Moves == 1, "Waiting neither aborts nor duplicates the pending motion");
        flowTime = 19001;
        Throws(delegate { flow.Append(new string[] { "PVT 60", "X2:0 Y1:0" }); }, "Ten seconds without consumption finally stops the controller stream");
        Check(!firmware.Running && !firmware.Open && firmware.Moves == 0 && firmware.Aborts > 0, "Persistent stall closes and clears the rotary buffer");
        firmware = new RotaryFirmware { RejectDefine = true };
        using (ControllerSession session = ProtocolSession(firmware))
        {
            Throws(delegate { session.BeginPvtFollow(1, 1, 2, CanvasSettings.Default); }, "Existing buffer allocation conflicts are reported");
            Check(!firmware.Open && firmware.Runs == 0 && !firmware.Commands.Exists(delegate(string c) { return c.StartsWith("DELETE"); }),
                "Allocation failure neither starts motion nor deletes unrelated controller buffers");
        }
        foreach (string failure in new string[] { "PVT ", "X ", "lost-run-reply" })
        {
            firmware = new RotaryFirmware();
            using (ControllerSession session = ProtocolSession(firmware))
            {
                session.BeginPvtFollow(1, 1, 2, CanvasSettings.Default);
                if (failure == "lost-run-reply") firmware.LoseRunReply = true; else firmware.FailPrefix = failure;
                Throws(delegate { session.AppendPvtFollow(20, new MotionPoint(1, 1), new MotionPoint(0, 0)); }, "Injected " + failure + " failure reaches caller");
                Check(!firmware.Open && !firmware.Running && firmware.Moves == 0, "Failure at " + failure + " closes entry and stops/clears incomplete execution");
            }
        }
        firmware = new RotaryFirmware();
        using (ControllerSession session = ProtocolSession(firmware))
        {
            session.BeginPvtFollow(1, 1, 2, CanvasSettings.Default);
            session.AppendPvtFollow(20, new MotionPoint(1, 1), new MotionPoint(0, 0));
            firmware.FailPrefix = "CLOSE";
            Throws(delegate { session.AbortPvtFollow(1, 1, 2); }, "An uncertain close acknowledgement is reported");
            Check(!firmware.Open && !firmware.Running && firmware.Moves == 0 && firmware.Aborts > 0,
                "A failed close still attempts abort and retries closing/clearing the rotary buffer");
        }
        firmware = new RotaryFirmware();
        using (ControllerSession session = ProtocolSession(firmware))
        using (ManualResetEvent entered = new ManualResetEvent(false))
        using (ManualResetEvent release = new ManualResetEvent(false))
        using (ManualResetEvent done = new ManualResetEvent(false))
        {
            firmware.BlockAt = "DEFINE ROT 2048"; firmware.Entered = entered; firmware.Release = release;
            CanvasFollower follower = new CanvasFollower(session);
            follower.Begin(1, 2, 5, CanvasSettings.Default, 1, 0, new MotionPoint(0, 0));
            follower.SubmitMm(new MotionPoint(1, 1)); follower.Pump();
            Check(entered.WaitOne(2000), "Initialization is in flight on the communication worker");
            follower.Cancel(); release.Set();
            session.Execute(delegate { }, delegate(Exception error) { done.Set(); });
            Until(delegate { return done.WaitOne(0); }, "Queued rotary cancellation completes on the application context");
            Check(firmware.Runs == 0 && firmware.Aborts > 0 && !firmware.Open,
                "Releasing while rotary initialization is in flight still aborts it and never starts the cancelled target");
        }
    }

    private static void TestDispatch()
    {
        using (Form owner = new Form())
        using (ControllerSession session = new ControllerSession(new WindowsFormsSynchronizationContext()))
        {
            owner.Show();
            int caller = Thread.CurrentThread.ManagedThreadId, worker = 0, callback = 0;
            ApartmentState apartment = ApartmentState.Unknown;
            bool done = false;
            session.Execute(delegate { worker = Thread.CurrentThread.ManagedThreadId; apartment = Thread.CurrentThread.GetApartmentState(); },
                delegate(Exception error) { callback = Thread.CurrentThread.ManagedThreadId; done = true; });
            Until(delegate { return done; }, "Controller dispatch completes without an owner parameter");
            Check(worker != caller && apartment == ApartmentState.STA, "Device access stays on its dedicated STA thread");
            Check(callback == caller, "Completion returns to the caller's UI synchronization context");
            done = false; callback = 0;
            SynchronizationContext previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                session.Execute(delegate { }, delegate(Exception error) { callback = Thread.CurrentThread.ManagedThreadId; done = true; });
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            Until(delegate { return done; }, "Explicit application context handles calls without an ambient context");
            Check(callback == caller, "A missing ambient context cannot move UI callbacks onto the worker");
            done = false; Exception failure = null;
            session.Execute(delegate { throw new InvalidOperationException("expected dispatch failure"); }, delegate(Exception error) { failure = error; done = true; });
            Until(delegate { return done; }, "Failed controller work still invokes completion");
            Check(failure is InvalidOperationException, "Worker errors reach the caller without killing the dispatch thread");
            done = false; session.Execute(delegate { }, delegate(Exception error) { done = true; });
            Until(delegate { return done; }, "Dispatch continues accepting work after an error");
            owner.Close();
        }
    }

    private static void TestInterface(string output)
    {
        using (Form1 form = new Form1())
        {
            form.Show(); Pump(100);
            ConnectionView connection = Find<ConnectionView>(form);
            MotionView motion = Find<MotionView>(form);
            DiagnosticsView diagnostics = Find<DiagnosticsView>(form);
            ProgramView programs = Find<ProgramView>(form);
            CanvasView canvases = Find<CanvasView>(form);
            PathView path = Find<PathView>(form);
            ControllerSession session = (ControllerSession)typeof(Form1).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Check(connection.IsSimulation, "Simulation must be the default");
            Check(connection.ConnectButton.Parent.ClientRectangle.Contains(connection.ConnectButton.Bounds) &&
                connection.DisconnectButton.Parent.ClientRectangle.Contains(connection.DisconnectButton.Bounds), "Connection buttons fit the compact header");
            Check(!motion.MoveButton.Enabled && !programs.SendButton.Enabled, "Disconnected operations must be disabled");
            Check(motion.Speed.Maximum == 50M && diagnostics.MaximumSpeed.Maximum == 50M,
                "Manual jog and per-axis maximum speed fields both allow 50 ct/ms");
            Check(connection.Visible && !path.Contains(connection), "Connection controls remain global outside the page tools");
            Label alarm = (Label)typeof(Form1).GetField("alarmSummary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Check(alarm.ForeColor == UiTheme.Muted, "No active alarms must not be presented as a fault");
            DrawMouse(canvases.DrawingCanvas, "OnMouseDown", 1, 1);
            DrawMouse(canvases.DrawingCanvas, "OnMouseMove", 2, 2);
            DrawMouse(canvases.DrawingCanvas, "OnMouseUp", 2, 2);
            Check(canvases.DrawingCanvas.PointCount == 2 && !canvases.Follow.Enabled, "Offline drawing must work without enabling motion");
            Check(motion.XAxis == 1 && motion.YAxis == 2, "X/Y must default to motors 1 and 2");
            connection.ConnectButton.PerformClick();
            Until(delegate { return connection.DisconnectButton.Enabled; }, "Simulation connection must update UI");
            Pump(550);
            Check(canvases.DrawingCanvas.HasCurrentPosition && Math.Abs(canvases.DrawingCanvas.CurrentPosition.X) < 0.01 &&
                Math.Abs(canvases.DrawingCanvas.CurrentPosition.Y) < 0.01, "Drawing canvas must show the current controller position");
            Screenshot(form, Path.Combine(output, "motion-control.png"));
            path.Pages.SelectedTab = (TabPage)motion.Parent.Parent;
            AxisStatusView axisStates = Find<AxisStatusView>(form);
            ReadoutView readouts = Find<ReadoutView>(form);
            Control selectedReadout = readouts.Controls.Find("selectedReadout", true)[0];
            Label[] stateRows = (Label[])typeof(AxisStatusView).GetField("states", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(axisStates);
            Check(!selectedReadout.Visible && !stateRows[2].Visible, "Selected X axis must not repeat its readout or status");
            motion.SelectedMotor.SelectedItem = 2; Pump(250);
            Check(!selectedReadout.Visible && !stateRows[2].Visible, "Selected Y axis must not repeat its readout or status");
            motion.SelectedMotor.SelectedItem = 3; Pump(250);
            Check(selectedReadout.Visible && stateRows[2].Visible, "Independent third motor retains its readout and status");
            motion.SelectedMotor.SelectedItem = 1; Pump(250);
            Check(FindButton(form, "停止").Visible && FindButton(motion, "停止运动") == null && FindButton(programs, "打开/编辑") == null,
                "Global stop remains available while duplicate stop and editor entry are removed");
            Screenshot(form, Path.Combine(output, "manual-deduplicated.png"));
            Check(axisStates.EnableButton.Visible && axisStates.DisableButton.Visible && axisStates.EnableButton != axisStates.DisableButton,
                "Manual control retains separate visible enable and disable buttons");
            Check(!FindButton(path, "导入 DXF").Visible && !FindButton(path, "生成规划").Visible && !FindButton(path, "运行").Visible,
                "Drawing and planning tools must not occupy the manual page");
            Check(axisStates.EnableButton.FlatStyle == FlatStyle.Standard && axisStates.DisableButton.FlatStyle == FlatStyle.Standard,
                "Axis enable controls retain standard WinForms button styling");
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
            Check(!FindButton(path, "生成规划").Visible && connection.Visible,
                "Program page hides drawing tools while keeping global connection available");
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
            Button plan = FindButton(path, "生成规划");
            Check(plan.Visible && form.RectangleToScreen(form.ClientRectangle).Contains(plan.RectangleToScreen(plan.ClientRectangle)),
                "Planning action fits the drawing page at minimum window size");
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
        PathView followPath = Find<PathView>(form);
        NumericUpDown offlineSpeed = (NumericUpDown)typeof(PathView).GetField("speed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(followPath);
        offlineSpeed.Value = 0.001M;
        motion.Speed.Value = 5; Pump(150);
        double before = Position(session, form);
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", -1, -1); Pump(160);
        DrawMouse(canvases.DrawingCanvas, "OnMouseUp", -1, -1); Pump(120);
        Check(Position(session, form) == before, "Connected preview drawing must issue no movement when follow is unchecked");
        motion.YMotor.SelectedIndex = 0; Pump(150);
        Check(!canvases.Follow.Enabled, "Same motor mapping must disable dual-axis following");
        motion.YMotor.SelectedIndex = 1; Pump(180);
        Check(canvases.Follow.Enabled, "Feedback-ready simulation must allow following");
        double rejectedX = Position(session, form), rejectedY = Position(session, form, 2);
        double farStartX = rejectedX > 0 ? -8 : 8, farStartY = rejectedY > 0 ? -8 : 8;
        canvases.Follow.Checked = true;
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", farStartX, farStartY); Pump(120);
        DrawMouse(canvases.DrawingCanvas, "OnMouseUp", farStartX, farStartY); Pump(120);
        Check(!canvases.Follow.Checked && Position(session, form) == rejectedX && Position(session, form, 2) == rejectedY,
            "Mouse follow must reject a start point away from the controller position");
        canvases.Follow.Checked = true;
        // Mouse follow must start at the current controller position.
        double followStartX = Position(session, form) / canvases.Settings.XCountsPerUnit;
        double followStartY = Position(session, form, 2) / canvases.Settings.YCountsPerUnit;
        DrawMouse(canvases.DrawingCanvas, "OnMouseDown", followStartX, followStartY); Pump(350);
        DrawMouse(canvases.DrawingCanvas, "OnMouseMove", 4, 3); Pump(150);
        Check(!motion.MoveButton.Enabled && !programs.SendButton.Enabled && FindButton(form, "停止").Enabled,
            "Drawing follow must lock competing commands while preserving stop");
        Check(!offlineSpeed.Enabled, "Offline speed remains locked during automatic following");
        Until(delegate { return Math.Abs(Position(session, form) - 400) < 10 && Math.Abs(Position(session, form, 2) - 300) < 10; },
            "Automatic following reaches mouse target independently of the tiny offline planning speed");
        double firstFollowX = Position(session, form), firstFollowY = Position(session, form, 2);
        Check(Math.Abs(firstFollowX - 400) < 10 && Math.Abs(firstFollowY - 300) < 10,
            "Mouse mm target must move both simulated axes through their conversion scales; actual=" + firstFollowX + "," + firstFollowY +
            ", pointer=" + canvases.DrawingCanvas.PointerPoint.X + "," + canvases.DrawingCanvas.PointerPoint.Y);
        Check(canvases.ActualCanvas.PointCount > 3, "Actual trajectory must accumulate controller feedback samples");
        DrawMouse(canvases.DrawingCanvas, "OnMouseMove", -8, -6); Pump(110);
        DrawMouse(canvases.DrawingCanvas, "OnMouseUp", -8, -6); Pump(120);
        double stoppedX = Position(session, form), stoppedY = Position(session, form, 2); Pump(200);
        Check(Position(session, form) == stoppedX && Position(session, form, 2) == stoppedY,
            "Mouse release must stop both axes before distant target completes");
        Check(motion.MoveButton.Enabled && !canvases.DrawingCanvas.IsDrawing, "Release must restore normal controls");

        // Re-grab the live position after the previous stroke, without rearming or aiming within 0.5 mm.
        MotionCanvas handleCanvas = canvases.DrawingCanvas;
        MotionPoint handleStart = handleCanvas.CurrentPosition;
        RectangleF handlePlot = handleCanvas.Settings.PlotArea(handleCanvas.ClientRectangle);
        Point handlePress = Point.Round(handleCanvas.Settings.ToScreen(handleStart, handlePlot));
        handlePress.Offset(12, 0);
        MotionPoint rawPress = handleCanvas.Settings.FromScreen(handlePress, handlePlot);
        Cursor.Position = handleCanvas.PointToScreen(handlePress); Pump(30);
        Check(PathGeometry.Distance(rawPress, handleStart) > CanvasFollower.StartToleranceMm,
            "Handle test grabs outside the old physical start tolerance");
        typeof(MotionCanvas).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(handleCanvas, new object[] { new MouseEventArgs(MouseButtons.None, 0, handlePress.X, handlePress.Y, 0) });
        Check(handleCanvas.Cursor == Cursors.Hand, "Hovering the enlarged position handle shows a hand cursor; armed=" + canvases.Follow.Checked +
            ", handle=" + handleCanvas.FollowHandleEnabled + ", drawing=" + handleCanvas.IsDrawing + ", enabled=" + handleCanvas.DrawingEnabled + ", cursor=" + handleCanvas.Cursor + ", start=" + handleStart.X + "," + handleStart.Y);
        typeof(MotionCanvas).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(handleCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, handlePress.X, handlePress.Y, 0) });
        CanvasFollower handleFollower = (CanvasFollower)typeof(Form1).GetField("follower", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        Check(handleCanvas.IsDraggingPosition && handleFollower.Active && canvases.Follow.Checked,
            "Grabbing the current handle rearms following at the new actual position");
        Check(PathGeometry.Distance(handleCanvas.PointerPoint, handleStart) < 1e-8,
            "Handle press snaps exactly to actual position without a target jump");
        Point handleMove = new Point(handlePress.X + 8, handlePress.Y + 5);
        MotionPoint rawMove = handleCanvas.Settings.FromScreen(handleMove, handlePlot);
        MotionPoint expectedDrag = new MotionPoint(handleStart.X + rawMove.X - rawPress.X, handleStart.Y + rawMove.Y - rawPress.Y);
        Cursor.Position = handleCanvas.PointToScreen(handleMove);
        typeof(MotionCanvas).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(handleCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 0, handleMove.X, handleMove.Y, 0) });
        Check(PathGeometry.Distance(handleCanvas.PointerPoint, expectedDrag) < 1e-8,
            "Dragging preserves the grab offset instead of jumping to the pointer");
        Screenshot(form, Path.Combine(output, "follow-handle.png")); Pump(350);
        Check(Math.Abs(Position(session, form) - expectedDrag.X * canvases.Settings.XCountsPerUnit) < 10 &&
            Math.Abs(Position(session, form, 2) - expectedDrag.Y * canvases.Settings.YCountsPerUnit) < 10,
            "Dragging the position handle moves both simulated axes to the requested displacement");
        typeof(MotionCanvas).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(handleCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, handleMove.X, handleMove.Y, 0) });
        Pump(150); stoppedX = Position(session, form); stoppedY = Position(session, form, 2); Pump(150);
        Check(!handleCanvas.IsDraggingPosition && !handleFollower.Active && Position(session, form) == stoppedX && Position(session, form, 2) == stoppedY,
            "Releasing a position handle stops motion and clears drag state");

        // Deliberately hold the communication worker so a queued target can be cancelled before execution.
        using (ManualResetEvent started = new ManualResetEvent(false))
        using (ManualResetEvent release = new ManualResetEvent(false))
        {
            session.Execute(delegate { started.Set(); release.WaitOne(2000); }, delegate(Exception error) { });
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
        Pump(100);
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
            TestDesigner(); TestCoordinates(); TestTransport(); TestRotaryFollow(); TestDispatch(); TestInterface(output);
            Console.WriteLine("PASS: " + assertions + " assertions; simulation only, no hardware accessed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
