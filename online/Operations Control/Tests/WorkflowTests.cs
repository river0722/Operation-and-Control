using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Operations_Control;
using Operations_Control.Models;
using Operations_Control.Services;
using Operations_Control.Views;

internal static class WorkflowTests
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Near(double a, double b, double tolerance, string message) { Check(Math.Abs(a - b) <= tolerance, message + ": " + a + " != " + b); }
    private static void Reject(Action action, string message) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (FormatException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
    private static void Pump(int milliseconds) { Stopwatch time = Stopwatch.StartNew(); while (time.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); } }
    private static void Until(Func<bool> condition, int timeout, string message) { Stopwatch time = Stopwatch.StartNew(); while (!condition() && time.ElapsedMilliseconds < timeout) Pump(10); Check(condition(), message); }
    private static string Dxf(string entities, int units) { return "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n" + units + "\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n" + entities + "0\nENDSEC\n0\nEOF\n"; }
    private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }
    private static void Call(ControllerSession session, Control owner, Action action)
    {
        bool done = false; Exception failure = null;
        session.Execute(owner, action, delegate(Exception error) { failure = error; done = true; });
        Until(delegate { return done; }, 5000, "Controller operation completes"); if (failure != null) throw failure;
    }
    private static void SaveImage(Form form, string directory, string name)
    {
        Pump(80); form.Refresh();
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(Path.Combine(directory, name)); }
    }
    private static void Geometry()
    {
        DrawingPath arc = PathGeometry.Arc(0, 0, 5, 0, 90, 0.01);
        Near(arc.Strokes[0][0].X, 5, 1e-9, "Arc starts at radius"); Near(arc.Strokes[0][arc.PointCount - 1].Y, 5, 1e-9, "Arc ends at 90 degrees");
        DrawingPath star = PathGeometry.Star(0, 0, 5); Check(star.PointCount == 11, "Star has ten edges");
        Near(PathGeometry.Distance(star.Strokes[0][0], star.Strokes[0][10]), 0, 1e-9, "Star closes");
        DrawingPath line = DxfReader.ReadText(Dxf("0\nLINE\n10\n0\n20\n0\n11\n1\n21\n2\n", 1), 0.01, 1);
        Near(line.Strokes[0][1].X, 25.4, 1e-8, "Inches convert to mm");
        line = DxfReader.ReadText(Dxf("0\nLINE\n10\n0\n20\n0\n11\n1\n21\n2\n", 0), 0.01, 10);
        Near(line.Strokes[0][1].Y, 20, 1e-8, "Unitless scale is explicit");
        DrawingPath bulge = DxfReader.ReadText(Dxf("0\nLWPOLYLINE\n90\n2\n70\n0\n10\n-1\n20\n0\n42\n1\n10\n1\n20\n0\n", 4), 0.001, 1);
        double min = 0; foreach (MotionPoint p in bulge.Strokes[0]) min = Math.Min(min, p.Y);
        Near(min, -1, 0.002, "Positive bulge traces counterclockwise semicircle");
        DrawingPath poly = DxfReader.ReadText(Dxf("0\nPOLYLINE\n70\n1\n0\nVERTEX\n10\n0\n20\n0\n0\nVERTEX\n10\n1\n20\n0\n0\nVERTEX\n10\n1\n20\n1\n0\nSEQEND\n", 4), 0.01, 1);
        Check(poly.PointCount == 4, "Closed POLYLINE adds final edge");
        string spline = "0\nSPLINE\n70\n0\n71\n2\n72\n6\n73\n3\n40\n0\n40\n0\n40\n0\n40\n1\n40\n1\n40\n1\n10\n0\n20\n0\n10\n1\n20\n2\n10\n2\n20\n0\n";
        DrawingPath curve = DxfReader.ReadText(Dxf(spline, 4), 0.001, 1);
        Near(curve.Strokes[0][0].X, 0, 1e-9, "Spline start"); Near(curve.Strokes[0][curve.PointCount - 1].X, 2, 1e-9, "Spline end");
        double max = 0; foreach (MotionPoint p in curve.Strokes[0]) max = Math.Max(max, p.Y); Near(max, 1, 0.001, "Quadratic spline midpoint");
        Reject(delegate { DxfReader.ReadText(Dxf("0\nINSERT\n2\nBLOCK1\n", 4), 0.01, 1); }, "Blocks must not silently disappear");
        Reject(delegate { DxfReader.ReadText(Dxf("0\nLINE\n10\n0\n20\n0\n30\n1\n11\n1\n21\n1\n", 4), 0.01, 1); }, "3D input must be rejected");
        Reject(delegate { DxfReader.ReadText("AutoCAD Binary DXF", 0.01, 1); }, "Binary DXF rejected");
        Reject(delegate { DxfReader.ReadText("0\nSECTION\n2", 0.01, 1); }, "Truncated pair rejected");
        Reject(delegate { DxfReader.ReadText(Dxf("0\nLINE\n10\nNaN\n20\n0\n11\n1\n21\n1\n", 4), 0.01, 1); }, "Nonfinite coordinate rejected");
        Reject(delegate { DxfReader.ReadText(Dxf("0\nARC\n10\n0\n20\n0\n40\n1\n51\n90\n", 4), 0.01, 1); }, "Missing start angle rejected");
        Reject(delegate { PathGeometry.Arc(0, 0, 1e100, 0, 360, 0.01); }, "Impossible chord tolerance rejected before point-count conversion");
        Reject(delegate { PathGeometry.Resample(PathGeometry.Line(0, 0, 1e100, 0).Strokes[0], 0.001); }, "Huge resampling length rejected before integer overflow");
        DrawingPath example = DxfReader.Read(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\examples\course-shapes.dxf"), 0.01, 1);
        Check(example.Strokes.Count == 5, "Delivered DXF imports all five entities");
        foreach (InterpolationMode mode in new InterpolationMode[] { InterpolationMode.PVT, InterpolationMode.Spline })
            Check(TrajectoryPlanner.Build(example, new PlanningOptions { Mode = mode }, new MotionPoint(0, 0)).DurationMs > 0, "Delivered DXF plans in " + mode);
    }
    private static MotionPlan MakePlan(InterpolationMode mode)
    {
        PlanningOptions options = new PlanningOptions { Mode = mode, Speed = 8, Acceleration = 40, Spacing = 0.5 };
        DrawingPath path = PathGeometry.Arc(0, 0, 3, 0, 270, 0.01); path.Strokes.Add(PathGeometry.Line(-2, -2, 2, 2).Strokes[0]);
        return TrajectoryPlanner.Build(path, options, new MotionPoint(0, 0));
    }
    private static void Planning()
    {
        foreach (InterpolationMode mode in new InterpolationMode[] { InterpolationMode.PVT, InterpolationMode.Spline })
        {
            MotionPlan plan = MakePlan(mode);
            Check(plan.Blocks.Count == 4, "Separate strokes include explicit transfer blocks");
            Near(PathGeometry.Distance(plan.At(0), new MotionPoint(0, 0)), 0, 1e-8, "Initial position preserved");
            Near(PathGeometry.Distance(plan.At(plan.DurationMs), new MotionPoint(2, 2)), 0, 1e-8, "Endpoint preserved");
            Check(plan.Program.Contains("I5190=1000") && plan.Program.Contains("#1->100X"), "Program declares units and mapping");
            Check(!plan.Program.Contains("UNDEFINE ALL") && !plan.Program.Contains("ENABLE PLC"), "Program does not rewrite unrelated axes or PLCs");
            Check(plan.Program.Contains(mode == InterpolationMode.PVT ? "PVT" : "SPLINE1"), "Requested interpolation emitted");
            double maxSpeed = 0, maxAcceleration = 0, boundary = 0;
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                MotionSegment s = plan.Segments[i];
                Check(s.DurationMs > 0 && s.DurationMs <= 4000, "Turbo PMAC segment time range");
                if (i > 0) { Near(PathGeometry.Distance(plan.Segments[i - 1].End, s.Start), 0, 1e-7, "Position continuity"); Near(PathGeometry.Distance(plan.Segments[i - 1].EndVelocity, s.StartVelocity), 0, 1e-7, "Velocity continuity"); }
                for (int j = 0; j <= 20; j++)
                {
                    double u = j / 20.0; MotionPoint p = s.At(u), v = s.VelocityAt(u); maxSpeed = Math.Max(maxSpeed, PathGeometry.Distance(v, new MotionPoint(0, 0)));
                    Check(plan.Options.Mapping.Contains(p), "Interpolated point remains in envelope");
                    if (j > 0) maxAcceleration = Math.Max(maxAcceleration, PathGeometry.Distance(v, s.VelocityAt((j - 1) / 20.0)) / (s.DurationMs / 20000));
                }
                boundary += s.DurationMs;
            }
            Check(maxSpeed <= plan.Options.Speed + 1e-6 && maxAcceleration <= plan.Options.Acceleration + 1e-6, "Velocity and acceleration bounds");
            Near(boundary, plan.DurationMs, 1e-6, "Durations total");
        }
        Reject(delegate { TrajectoryPlanner.Build(PathGeometry.Line(0, 0, 11, 0), new PlanningOptions(), new MotionPoint(0, 0)); }, "Out-of-travel path rejected");
        PlanningOptions invalid = new PlanningOptions { XMotor = 2, YMotor = 2 };
        Reject(delegate { TrajectoryPlanner.Build(PathGeometry.Line(0, 0, 1, 0), invalid, new MotionPoint(0, 0)); }, "Duplicate motor mapping rejected");
        // Independent constants from Turbo PMAC SRM 5-153, including the 9/10 bank boundary.
        int[] systems = { 1, 8, 9, 10, 16 }, variables = { 5190, 5890, 5990, 6090, 6690 };
        for (int i = 0; i < systems.Length; i++)
        {
            MotionPlan cs = TrajectoryPlanner.Build(PathGeometry.Line(0, 0, 1, 0), new PlanningOptions { CoordinateSystem = systems[i] }, new MotionPoint(0, 0));
            Check(cs.Program.Contains("I" + variables[i] + "=1000"), "Turbo coordinate-system feed units " + systems[i]);
        }
    }
    private static void Analysis(string output)
    {
        MotionPlan plan = MakePlan(InterpolationMode.PVT);
        TrajectoryRun delayed = new TrajectoryRun { Plan = plan, Simulation = true, Completed = true, Result = "完成", StartedUtc = DateTime.UtcNow };
        for (double t = 0; t <= plan.DurationMs + 100; t += 20) delayed.Samples.Add(new TrajectorySample(t, t, t, 2, plan.At(t - 80)));
        AnalysisResult result = TrajectoryAnalysis.Analyze(delayed);
        Check(result.EstimatedLagMs.HasValue, "Moving trace supports lag estimate"); Near(result.EstimatedLagMs.Value, 80, 10, "Known 80 ms lag recovered");
        Check(result.TrackingRms > 0.01 && result.QueryMean == 2, "Temporal error includes lag");
        MotionPlan straight = TrajectoryPlanner.Build(PathGeometry.Line(0, 0, 5, 0), new PlanningOptions(), new MotionPoint(0, 0));
        TrajectoryRun shifted = new TrajectoryRun { Plan = straight, Simulation = true, Result = "停止", StartedUtc = DateTime.UtcNow };
        for (double t = 0; t <= straight.DurationMs; t += 10) { MotionPoint p = straight.At(t); p.Y += 0.2; shifted.Samples.Add(new TrajectorySample(t, t, t, 1, p)); }
        AnalysisResult offset = TrajectoryAnalysis.Analyze(shifted);
        Near(offset.ContourRms, 0.2, 1e-8, "Known normal contour offset"); Near(offset.TrackingRms, 0.2, 1e-8, "Known temporal error");
        Check(offset.Text.Contains("未完整运行"), "Incomplete run is labeled");
        string exported = TrajectoryAnalysis.Export(delayed, output);
        Check(File.ReadAllLines(Path.Combine(exported, "actual.csv")).Length == delayed.Samples.Count + 1, "Full feedback exported");
        Check(File.Exists(Path.Combine(exported, "drawing.csv")) && File.Exists(Path.Combine(exported, "planned.csv")), "Input and reference exported");
        Reject(delegate { TrajectoryAnalysis.Analyze(new TrajectoryRun { Plan = straight }); }, "Empty samples cannot claim zero error");
        TrajectoryRun longRun = new TrajectoryRun { Plan = straight, Simulation = true, Result = "测试数据", StartedUtc = DateTime.UtcNow };
        for (int i = 0; i < 4101; i++) { double t = i * straight.DurationMs / 4100; longRun.Samples.Add(new TrajectorySample(t, t, t, 1, straight.At(t))); }
        Check(File.ReadAllLines(Path.Combine(TrajectoryAnalysis.Export(longRun, output), "actual.csv")).Length == 4102, "Analysis retains samples beyond canvas history limit");
    }
    private static void Simulator()
    {
        using (SimulatorTransport sim = new SimulatorTransport())
        {
            sim.Connect(0);
            Check(MotorStatus.Parse(sim.Send("#1?").Response).Ready, "Sim initial closed-loop state");
            sim.Send("#1HOMEZ"); Check(!MotorStatus.Parse(sim.Send("#1?").Response).HomeComplete, "HOMEZ does not prove mechanical homing");
            sim.Send("#1HM"); Check(MotorStatus.Parse(sim.Send("#1?").Response).Homing, "HM begins home search");
            Thread.Sleep(350); Check(MotorStatus.Parse(sim.Send("#1?").Response).HomeComplete, "HM completes home search");
            sim.Send("#1K"); Check(!MotorStatus.Parse(sim.Send("#1?").Response).Enabled, "Kill disables amplifier");
            Reject(delegate { sim.Send("#1J=100"); }, "Killed axis rejects queued movement");
            sim.Send("#1J/"); Check(MotorStatus.Parse(sim.Send("#1?").Response).Enabled, "J/ closes loop");
            MotionPlan plan = TrajectoryPlanner.Build(PathGeometry.Line(0, 0, 1, 2), new PlanningOptions { Speed = 100, Acceleration = 1000 }, new MotionPoint(0, 0));
            sim.StartPlan(plan, 0); Thread.Sleep(50);
            double x = Double.Parse(sim.Send("#1P").Response, CultureInfo.InvariantCulture), y = Double.Parse(sim.Send("#2P").Response, CultureInfo.InvariantCulture);
            Near(y, 2 * x, 5, "Both axes share path time");
            sim.StopPlan(); string p = sim.Send("#1P").Response; Thread.Sleep(100); Check(p == sim.Send("#1P").Response, "Stopping freezes planned trajectory");
            sim.Send("#1HOMEZ"); sim.Send("#2HOMEZ"); sim.StartPlan(plan, 0); sim.InjectFault(1);
            Check(!sim.PlanRunning && MotorStatus.Parse(sim.Send("#1?").Response).Fault, "Fault stops plan and exposes status");
        }
        MotorStatus status = MotorStatus.Parse("C02000004409"); Check(status.NegativeLimit && status.Fault && status.HomeComplete, "Known status mask decoding");
        Check(MotorStatus.Parse("882000000401").Ready, "Turbo enable in first word bit 19");
        Check(!MotorStatus.Parse("802000004401").Ready, "Reserved second word bit 14 cannot enable an axis");
        Check(MotorStatus.Parse("882000001001").Fault && MotorStatus.Parse("882000000801").Fault, "Latched desired/actual travel stop blocks motion");
        Check(CoordinateStatus.Parse("000001000000000000").Running, "Coordinate running bit");
        Check(CoordinateStatus.Parse("000000400000000000").Fault, "Coordinate runtime error bit");
        Check(CoordinateStatus.Parse("000000020000000000").InPosition, "Coordinate in-position bit");
        Reject(delegate { MotorStatus.Parse("0000"); }, "Malformed status rejected");
        Reject(delegate { CoordinateStatus.Parse("000000000000"); }, "Non-Turbo coordinate response rejected");
    }
    private static void Interface(string output)
    {
        using (Form1 main = new Form1())
        {
            main.Show(); Pump(200);
            ControllerSession session = Field<ControllerSession>(main, "session");
            ConnectionView connection = Field<ConnectionView>(main, "connection");
            PathView form = Field<PathView>(main, "pathView");
            Form owner = main;
            connection.ConnectButton.PerformClick();
            Until(delegate { return connection.DisconnectButton.Enabled; }, 5000, "Main simulation connects");
            Pump(200);
            int windows = 0;
            foreach (Form window in Application.OpenForms) if (window.TopLevel) windows++;
            Check(windows == 1, "All workflows share one top-level window");
            Check(main.Contains(form) && main.Contains(Field<ProgramEditorForm>(main, "editorWindow")) && main.Contains(Field<LogForm>(main, "logWindow")), "Path, editor and log are embedded in main window");
            {
                form.LoadDrawing(PathGeometry.Line(0, 0, 1, 1));
                form.GeneratePlan(); Until(delegate { return form.CurrentPlan != null; }, 5000, "Main view plans path");
                form.StartExecution();
                Check(!Field<NumericUpDown>(form, "coordinate").Enabled && !Field<NumericUpDown>(form, "lag").Enabled && !Field<NumericUpDown>(form, "scale").Enabled, "Execution locks advanced and transform parameters");
                Check(!connection.DisconnectButton.Enabled && !Field<MotionView>(main, "motion").MoveButton.Enabled, "Execution locks competing main actions");
                Until(delegate { return form.LatestSnapshot != null && form.CurrentRun.Samples.Count > 2; }, 5000, "Run sends live feedback to header");
                Check(Field<Label>(main, "xPosition").Text == "X " + (form.LatestSnapshot.XCounts / CanvasSettings.Default.XCountsPerUnit).ToString("0.000") + " mm", "Header displays planned execution feedback");
                Until(delegate { return form.CurrentRun != null && form.CurrentRun.Completed; }, 10000, "Main view runs and completes path");
                Check(form.CurrentRun.Samples.Count > 2, "Main view records actual feedback");
                Check(form.CurrentRun.Simulation, "Run identifies simulated data");
                form.Pages.SelectedIndex = 3;
                Field<Button>(form, "analyzeButton").PerformClick(); Until(delegate { return Field<TextBox>(form, "analysisText").Text.Contains("RMS"); }, 5000, "Analysis displayed");
                SaveImage(main, output, "workflow-analysis.png");
                Field<TabControl>(form, "tabs").SelectedIndex = 0;
                SaveImage(main, output, "workflow-path.png");
                main.Size = main.MinimumSize; Field<ComboBox>(form, "shape").SelectedIndex = 2; Pump(50); SaveImage(main, output, "workflow-minimum.png");
                FlowLayoutPanel geometry = Field<FlowLayoutPanel>(form, "geometry");
                foreach (Control control in geometry.Controls)
                    if (control.Visible) Check(geometry.ClientRectangle.Contains(control.Bounds), "Arc fields and mouse-follow control fit minimum window");
                Console.WriteLine("Workflow: restore window size");
                main.Size = new Size(1176, 830); Pump(100);
                Console.WriteLine("Workflow: select device page");
                form.Pages.SelectedTab = form.SettingsPage;
                Console.WriteLine("Workflow: home axes");
                form.StartHoming(); Until(delegate { return !form.IsExecuting && Field<Label>(form, "feedbackLabel").Text.Contains("机械回零完成"); }, 5000, "Dual home requires fresh transition and in-position");
                SaveImage(main, output, "workflow-device.png");
                // Complete a DXF -> SPLINE1 -> feedback -> export cycle through the real GUI/session.
                form.LoadDrawing(DxfReader.ReadText(Dxf("0\nLWPOLYLINE\n90\n4\n70\n1\n10\n-1\n20\n-1\n10\n1\n20\n-1\n10\n1\n20\n1\n10\n-1\n20\n1\n", 4), 0.01, 1));
                Field<ComboBox>(form, "interpolation").SelectedIndex = 1;
                form.GeneratePlan(); Until(delegate { return form.CurrentPlan != null; }, 5000, "Imported DXF spline plans");
                form.StartExecution(); Until(delegate { return form.CurrentRun != null && form.CurrentRun.Completed; }, 20000, "Imported DXF spline completes through GUI");
                string demo = TrajectoryAnalysis.Export(form.CurrentRun, output);
                Check(File.ReadAllText(Path.Combine(demo, "program.pmc")).Contains("SPLINE1"), "GUI export retains executed spline program");
                Field<TabControl>(form, "tabs").SelectedIndex = 0; SaveImage(main, output, "workflow-dxf-spline.png");
                form.StartHoming(); Until(delegate { return !form.IsExecuting && Field<Label>(form, "feedbackLabel").Text.Contains("机械回零完成"); }, 5000, "Repeated home requires new transition");
                form.LoadDrawing(PathGeometry.Arc(0, 0, 4, 0, 360, 0.01)); form.GeneratePlan();
                Until(delegate { return form.CurrentPlan != null; }, 5000, "Second path plans");
                form.StartExecution(); Pump(250); form.Pages.SelectedTab = form.ProgramPage;
                Field<Button>(main, "globalStop").PerformClick();
                Until(delegate { return !form.IsExecuting; }, 5000, "Stop completes");
                Check(!form.CurrentRun.Completed && form.CurrentRun.Result.Contains("停止"), "Stopped run never marked complete");
                ControllerSnapshot before = null, after = null;
                Call(session, owner, delegate { before = session.ReadSnapshot(1, 2); }); Pump(150); Call(session, owner, delegate { after = session.ReadSnapshot(1, 2); });
                Near(before.XCounts, after.XCounts, 0.001, "Stopped main path does not restart");
                form.StartHoming(); Pump(80); form.StopExecution(); Until(delegate { return !form.IsExecuting; }, 5000, "Home can be cancelled");
                Call(session, owner, delegate { before = session.ReadSnapshot(1, 2); }); Pump(400); Call(session, owner, delegate { after = session.ReadSnapshot(1, 2); });
                Near(before.XCounts, after.XCounts, 0.001, "Cancelled home does not complete later");
                Call(session, owner, delegate { session.Move(1, 500, 15); }); Pump(150);
                Call(session, owner, delegate { session.SetSpeed(1, 0.01M); }); Pump(100);
                Field<ComboBox>(form, "homeTarget").SelectedIndex = 0; Field<NumericUpDown>(form, "homeTimeout").Value = 1;
                form.StartHoming(); Until(delegate { return !form.IsExecuting && Field<Label>(form, "feedbackLabel").Text.Contains("回零超时"); }, 4000, "Homing timeout stops slow axis");
                form.GeneratePlan(); Until(delegate { return form.CurrentPlan != null; }, 5000, "Replan at current position");
                form.StartExecution(); main.Close(); Until(delegate { return main.IsDisposed; }, 5000, "Main close cancels pending start and stops");
            }
        }
        using (Form owner = new Form())
        using (ControllerSession session = new ControllerSession())
        using (CanvasView canvases = new CanvasView())
        using (PathView hardware = new PathView(session, false, CanvasSettings.Default, false, 1, 2, canvases))
        {
            owner.Controls.Add(hardware); owner.Show();
            Call(session, owner, delegate { session.Connect(true, owner.Handle.ToInt32()); });
            {
                Pump(150); Field<CheckBox>(hardware, "hardwareConfirmed").Checked = true;
                Check(!Field<Button>(hardware, "homeButton").Enabled && !Field<Button>(hardware, "enableButton").Enabled, "Missing real calibration keeps hardware controls disabled");
            }
            Call(session, owner, delegate { session.Disconnect(); }); owner.Close();
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string output = args.Length > 0 ? args[0] : Path.GetTempPath(); Directory.CreateDirectory(output);
            Geometry(); Planning(); Analysis(output); Simulator(); Interface(output);
            Console.WriteLine("PASS: " + checks + " workflow assertions; simulation only, no hardware accessed."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
