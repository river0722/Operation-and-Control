using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using Operations_Control;
using Operations_Control.Files;
using Operations_Control.Controls;
using Operations_Control.Models;
using Operations_Control.Motion;
using Operations_Control.Views;

// Evidence runner for the application under development. No hardware connection is opened.
class Section53Evidence
{
    static string output;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static T Field<T>(object item, string name) { return (T)item.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item); }
    static void Pump(int ms) { DateTime end=DateTime.UtcNow.AddMilliseconds(ms); while (DateTime.UtcNow<end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static void Export(DrawingPath p, string name)
    {
        using (StreamWriter w = new StreamWriter(Path.Combine(Path.Combine(output,"data"),name), false, new UTF8Encoding(true)))
        {
            w.WriteLine("stroke,point,x_mm,y_mm");
            for (int i=0;i<p.Strokes.Count;i++) for (int j=0;j<p.Strokes[i].Count;j++)
                w.WriteLine((i+1)+","+(j+1)+","+p.Strokes[i][j].X.ToString("R",Inv)+","+p.Strokes[i][j].Y.ToString("R",Inv));
        }
    }
    static void Shot(Form f,string name)
    {
        Pump(100); f.Refresh();
        using(Bitmap b=new Bitmap(f.Width,f.Height)) { f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height)); b.Save(Path.Combine(Path.Combine(output,"figures"),name),System.Drawing.Imaging.ImageFormat.Png); }
    }
    static void Set(PathView view,string name,double value) { Field<NumericUpDown>(view,name).Value=(decimal)value; }
    static void Shape(PathView view,int kind,double x,double y,double r,double endX,double endY,double start,double sweep)
    {
        Field<ComboBox>(view,"shape").SelectedIndex=kind;
        Set(view,"centerX",x); Set(view,"centerY",y); Set(view,"radius",r); Set(view,"endX",endX); Set(view,"endY",endY); Set(view,"startAngle",start); Set(view,"sweepAngle",sweep);
        Field<Button>(view,"shapeButton").PerformClick(); Pump(40);
    }
    static void Mouse(MotionCanvas c,string method,double x,double y)
    {
        Point pt=Point.Round(c.Settings.ToScreen(new MotionPoint(x,y),c.Settings.PlotArea(c.ClientRectangle)));
        typeof(MotionCanvas).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{new MouseEventArgs(MouseButtons.Left,1,pt.X,pt.Y,0)});
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            output=Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.Combine(output,"data")); Directory.CreateDirectory(Path.Combine(output,"figures"));
            StringBuilder log=new StringBuilder("case\tresult\tstrokes\tpoints\tdetail\n");
            foreach(string path in Directory.GetFiles(Path.Combine(output,"samples"),"*.dxf"))
            {
                string name=Path.GetFileNameWithoutExtension(path); double scale=name=="unitless-line"?10:1;
                try {
                    DrawingPath p=DxfReader.Read(path,0.01,scale); Export(p,name+"-import.csv");
                    log.Append(name+"\tACCEPT\t"+p.Strokes.Count+"\t"+p.PointCount+"\t"+p.Source+"\n");
                } catch(Exception e) { log.Append(name+"\tREJECT\t0\t0\t"+e.GetType().Name+": "+e.Message.Replace('\t',' ')+"\n"); }
            }
            File.WriteAllText(Path.Combine(Path.Combine(output,"data"),"parser-results.tsv"),log.ToString(),new UTF8Encoding(true));
            DrawingPath main=DxfReader.Read(Path.Combine(Path.Combine(output,"samples"),"cad-six-types-mm.dxf"),0.01,1);
            DrawingPath transformed=PathGeometry.Transform(main,0.5,1,-1); Export(transformed,"cad-transformed.csv");
            DrawingPath resampled=new DrawingPath(); foreach(List<MotionPoint> stroke in main.Strokes) resampled.Strokes.Add(PathGeometry.Resample(stroke,0.5)); Export(resampled,"cad-resampled.csv");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using(Form1 f=new Form1())
            {
                // Instantiate and render this repository's UI in a separate off-screen test window.
                // Remain disconnected: CAD import and preview drawing do not require a controller.
                f.ShowInTaskbar=false; f.StartPosition=FormStartPosition.Manual; f.Location=new Point(-18000,-18000); f.Size=new Size(1440,1000); f.Show(); Pump(250);
                ConnectionView conn=Field<ConnectionView>(f,"connection");
                if(!conn.IsSimulation) throw new Exception("Expected simulation mode");
                PathView view=Field<PathView>(f,"pathView"); view.Pages.SelectedIndex=0;
                CanvasView canvases=Field<CanvasView>(f,"canvases"); canvases.Follow.Checked=false;
                view.LoadDrawing(new DrawingPath()); Shot(f,"cad-before-empty-ui.png");
                Shape(view,0,-8,-7,1,-3,-7,0,180);
                Shape(view,1,4,4,2,1,1,0,180);
                Shape(view,2,-5,-3,2,1,1,0,180);
                Shape(view,3,-5,4,2,1,1,0,180);
                MotionCanvas canvas=canvases.DrawingCanvas;
                Mouse(canvas,"OnMouseDown",0,-5);
                for(int i=1;i<=45;i++) { double t=(double)i/45; Mouse(canvas,"OnMouseMove",t*6,-5+1.25*Math.Sin(2*Math.PI*t)); }
                Mouse(canvas,"OnMouseUp",6,-5); Pump(80);
                if(view.CurrentDrawing.Strokes.Count!=5) throw new Exception("Expected four geometric shapes and one mouse-event freehand stroke; got "+view.CurrentDrawing.Strokes.Count);
                Export(view.CurrentDrawing,"drawing-demo.csv"); Shot(f,"drawing-typical-and-freehand-ui.png");
                // Same production parser and PathView data-loading path; file picker is bypassed.
                view.LoadDrawing(main); Shot(f,"cad-imported-ui.png");
                using(Bitmap b=new Bitmap(canvas.Width,canvas.Height)) { canvas.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height)); b.Save(Path.Combine(Path.Combine(output,"figures"),"cad-imported-canvas.png"),System.Drawing.Imaging.ImageFormat.Png); }
                if(view.CurrentDrawing.PointCount!=main.PointCount) throw new Exception("UI input point count mismatch");
                File.WriteAllText(Path.Combine(Path.Combine(output,"data"),"ui-evidence.txt"),
                    "Connection: none; simulation mode selected; mouse follow off.\r\n"+
                    "Drawing demo: 4 shape-button operations plus 1 freehand stroke through MotionCanvas mouse event handlers.\r\n"+
                    "CAD evidence: DxfReader.Read(file,0.01,1) -> PathView.LoadDrawing; native file picker bypassed.\r\n"+
                    "DrawingPath strokes="+main.Strokes.Count+"; points="+main.PointCount+"; displayed status="+view.StatusText+"\r\n"+
                    "Screenshots: actual WinForms DrawToBitmap rendering in an isolated off-screen test instance; no fabricated UI.\r\n"+
                    "Assembly="+typeof(DxfReader).Assembly.Location+"\r\n", new UTF8Encoding(true));
                view.CancelPendingWork(); f.Dispose();
            }
            Console.WriteLine(log.ToString()); Console.WriteLine("UI evidence complete; no hardware connection or motion commands."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
    }
}
