using System;
using System.Threading;
using Operations_Control.Application;
using Operations_Control.Models;

// Run with the bridge virtual environment installed; never connects to hardware.
internal static class NxLinkBridgeTests
{
    private static int Main()
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nx-python-" + Guid.NewGuid().ToString("N"));
        string badScripts = System.IO.Path.Combine(scratch, ".venv\\Scripts");
        System.IO.Directory.CreateDirectory(badScripts);
        string realPython = Environment.GetEnvironmentVariable("PMAC_NXLINK_PYTHON");
        try
        {
            if (!String.IsNullOrEmpty(realPython))
            {
                string broken = System.IO.Path.Combine(badScripts, "python.exe");
                System.IO.File.Copy(realPython, broken);
                System.IO.File.WriteAllText(System.IO.Path.Combine(scratch, ".venv\\pyvenv.cfg"), "home = Z:\\missing-python\r\nexecutable = Z:\\missing-python\\python.exe\r\n");
                Environment.SetEnvironmentVariable("PMAC_NXLINK_PYTHON", broken);
                string detected = NxLinkPython.Find(scratch, Console.Error.WriteLine, delegate { return false; });
                if (detected == broken) return 3;
                Console.Error.WriteLine("PASS: broken copied venv skipped; local Python 3.13 found: " + detected);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PMAC_NXLINK_PYTHON", realPython);
            System.IO.Directory.Delete(scratch, true);
        }
        using (NxLinkBridge bridge = new NxLinkBridge())
        using (AutoResetEvent ready = new AutoResetEvent(false))
        {
            bridge.Message += delegate(string text)
            {
                if (text.IndexOf("已就绪", StringComparison.Ordinal) >= 0) ready.Set();
                Console.Error.WriteLine(text);
            };
            bridge.Start();
            if (!ready.WaitOne(20000, false)) return 1;
            double now = MonotonicClock.NowMs;
            bridge.Publish(new ControllerFeedback(new ControllerSnapshot
            {
                XCounts = 1638.4, YCounts = -3276.8, XTimeMs = now, YTimeMs = now
            }, new AxisSample[] {
                new AxisSample(1, 1638.4, null, DateTime.Now),
                new AxisSample(2, -3276.8, null, DateTime.Now) }));
            Console.WriteLine("BRIDGE_TEST_FRAME");
            Console.Out.Flush();
            Console.ReadLine(); // External OPC UA reader verifies the copied values.
            bridge.Stop();
            bridge.Stop();
            // A second connection must get a fresh child and clean sequence/history.
            bridge.Start();
            if (!ready.WaitOne(20000, false)) return 2;
            bridge.Stop();
        }
        Console.WriteLine("PASS: C# child startup, copied feedback, EOF, repeated stop and restart.");
        return 0;
    }
}
