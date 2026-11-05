using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Operations_Control.Models;

namespace Operations_Control.Application
{
    // A single pending copy keeps Python/OPC UA off the UI and controller threads.
    public sealed class NxLinkBridge : IDisposable
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private Thread worker;
        private Process process;
        private volatile bool stopping, ready;
        private string pending;
        private long sequence;
        private double lastX = Double.NaN, lastY = Double.NaN;
        private int lastXMotor, lastYMotor;
        public event Action<string> Message;

        public void Start()
        {
            Stop();
            string script = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NxLink"), "nxlink.py");
            if (!File.Exists(script)) { Notify("NX 数据桥未启动，找不到脚本：" + script); return; }
            stopping = ready = false;
            pending = null; sequence = 0; lastX = lastY = Double.NaN;
            worker = new Thread(delegate() { Run(script); }) { IsBackground = true, Name = "NX feedback bridge" };
            worker.Start();
        }

        private void Output(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == "NXLINK_READY")
            {
                ready = true; wake.Set();
                Notify("NX 数据桥已就绪，正在复制实机反馈；NX 地址见 NxLink/config.json。");
            }
            else if (!String.IsNullOrEmpty(e.Data)) Notify(e.Data);
        }

        private void Run(string script)
        {
            Process child = null;
            try
            {
                string directory = Path.GetDirectoryName(script);
                string python = NxLinkPython.Find(directory, Notify, delegate { return stopping; });
                if (stopping) return;
                ProcessStartInfo info = new ProcessStartInfo(python, "-u \"" + Path.Combine(directory, "bootstrap.py") + "\"");
                info.WorkingDirectory = directory;
                info.UseShellExecute = false; info.CreateNoWindow = true;
                info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
                info.StandardOutputEncoding = info.StandardErrorEncoding = System.Text.Encoding.UTF8;
                info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                process = child = new Process { StartInfo = info };
                child.OutputDataReceived += Output; child.ErrorDataReceived += Output;
                child.Start();
                child.BeginOutputReadLine(); child.BeginErrorReadLine();
                Stopwatch startup = Stopwatch.StartNew();
                while (!stopping)
                {
                    if (child.HasExited) throw new InvalidOperationException("脚本已退出，退出码 " + child.ExitCode + "；请检查 Python 和 asyncua 依赖及 NX 端口占用。");
                    if (!ready && startup.ElapsedMilliseconds > 180000) throw new TimeoutException("脚本环境准备或启动超过 180 秒未就绪，请检查依赖下载网络。");
                    string line = null;
                    if (ready) lock (gate) { line = pending; pending = null; }
                    if (line != null)
                    {
                        child.StandardInput.WriteLine(line);
                        child.StandardInput.Flush();
                    }
                    else wake.WaitOne(100, false);
                }
            }
            catch (Exception error)
            {
                if (!stopping) Notify("NX 数据桥停止：" + error.Message + "；实机控制连接继续保留。");
            }
            finally
            {
                ready = false;
                try
                {
                    if (child != null)
                    {
                        child.StandardInput.Close(); // EOF lets Python zero speed and close OPC UA.
                        if (!child.WaitForExit(1000)) child.Kill();
                    }
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }

        public void Publish(ControllerFeedback feedback)
        {
            if (worker == null || !worker.IsAlive || stopping || feedback == null || feedback.Snapshot == null || feedback.Axes.Length < 2) return;
            ControllerSnapshot s = feedback.Snapshot;
            int x = feedback.Axes[0].Motor, y = feedback.Axes[1].Motor;
            if (x == lastXMotor && y == lastYMotor && s.XTimeMs == lastX && s.YTimeMs == lastY) return;
            double age = MonotonicClock.NowMs - Math.Max(s.XTimeMs, s.YTimeMs);
            if (age > 1000 || !Finite(s.XCounts) || !Finite(s.YCounts) || !Finite(s.XTimeMs) || !Finite(s.YTimeMs)) return;
            double unix = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds - Math.Max(0, age) / 1000;
            string line = String.Format(CultureInfo.InvariantCulture,
                "{{\"x_counts\":{0:R},\"y_counts\":{1:R},\"x_time_s\":{2:R},\"y_time_s\":{3:R},\"x_motor\":{4},\"y_motor\":{5},\"seq\":{6},\"t\":{7:R}",
                s.XCounts, s.YCounts, s.XTimeMs / 1000, s.YTimeMs / 1000, x, y, sequence++, unix) + "}";
            lastX = s.XTimeMs; lastY = s.YTimeMs; lastXMotor = x; lastYMotor = y;
            lock (gate) pending = line;
            wake.Set();
        }

        private static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
        private void Notify(string text) { if (Message != null) Message(text); }

        public void Stop()
        {
            stopping = true; wake.Set();
            Thread thread = worker;
            if (thread != null && !thread.Join(1500))
            {
                try { if (process != null && !process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                thread.Join(1000);
            }
            worker = null;
            if (process != null) { process.Dispose(); process = null; }
            lock (gate) pending = null;
        }

        public void Dispose() { Stop(); wake.Close(); }
    }
}
