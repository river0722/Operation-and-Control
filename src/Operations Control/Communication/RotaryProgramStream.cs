using System;
using System.Collections.Generic;
using System.Globalization;
using Operations_Control.Models;

namespace Operations_Control.Communication
{
    // Temporary backpressure: the caller retains the unsent segment and retries.
    public sealed class FollowBufferBusyException : InvalidOperationException
    {
        public FollowBufferBusyException() : base("随动缓冲正在消化轨迹，等待后继续发送。") { }
    }
    // Turbo SRM: PVT/SPLINE1 are PROG/ROT commands, not immediate commands.
    // Keep ROT open while appending: CLOSE can append RETURN and end the stream.
    // All calls run on the session's STA worker. Only unambiguous motor/coordinate
    // feedback queries may share the port while entry is open.
    public sealed class RotaryProgramStream
    {
        private const int BufferWords = 2048, MaximumQueuedLines = 48;
        private readonly Func<string, string> send;
        private readonly Func<double> now;
        private readonly HashSet<int> defined = new HashSet<int>();
        private int coordinateSystem;
        private bool active, started;
        private double blockedSince = Double.NaN;
        private int blockedRemaining;
        public bool EntryOpen { get; private set; }

        public RotaryProgramStream(Func<string, string> send) : this(send, delegate { return MonotonicClock.NowMs; }) { }
        public RotaryProgramStream(Func<string, string> send, Func<double> now) { this.send = send; this.now = now; }

        public void Begin(int cs, int x, int y, CanvasSettings mapping)
        {
            if (active) throw new InvalidOperationException("已有实时随动流，请先停止。");
            CloseEntry();
            CoordinateStatus status = CoordinateStatus.Parse(send("&" + cs + "??"));
            if (status.Running || status.Fault || !status.InPosition)
                throw new InvalidOperationException("随动预留坐标系必须空闲、无故障且已到位。");
            coordinateSystem = cs; started = false;
            blockedSince = Double.NaN;
            try
            {
                send("&" + cs);
                send("#" + x + "->" + N(mapping.XCountsPerUnit) + "X");
                send("#" + y + "->" + N(mapping.YCountsPerUnit) + "Y");
                send("I42=0"); send("I" + (5090 + cs * 100) + "=1000");
                if (!defined.Contains(cs))
                {
                    try { send("DEFINE ROT " + BufferWords); }
                    catch (Exception error)
                    {
                        throw new InvalidOperationException("无法为预留坐标系 " + cs + " 建立实时旋转缓冲区。请核对现有 ROT/LOOKAHEAD/GATHER 内存配置；未自动删除其他缓冲区。" + error.Message, error);
                    }
                    defined.Add(cs);
                }
                active = true;
                // B0 MUST be sent with entry closed, otherwise it means a B-axis move.
                send("&" + cs + "B0");
                OpenEntry(); send("CLEAR"); send("ABS");
            }
            catch
            {
                if (active) { try { Abort(); } catch { } }
                throw;
            }
        }

        public void Append(IList<string> lines)
        {
            if (!active) throw new InvalidOperationException("实时旋转缓冲区尚未启动。");
            try
            {
                if (started)
                {
                    CoordinateStatus status = CoordinateStatus.Parse(send("&" + coordinateSystem + "??"));
                    if (!status.Running || status.Fault)
                        throw new InvalidOperationException("随动坐标系已停止或故障，未继续追加旧轨迹；请检查状态后重新起笔。");
                }
                int remaining;
                if (!Int32.TryParse(send("&" + coordinateSystem + "PR").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out remaining) || remaining < 0)
                    throw new InvalidOperationException("实时旋转缓冲区 PR 反馈无效。");
                if (remaining + lines.Count > MaximumQueuedLines)
                {
                    if (Double.IsNaN(blockedSince) || remaining < blockedRemaining) blockedSince = now();
                    blockedRemaining = remaining;
                    if (now() - blockedSince >= 10000)
                        throw new InvalidOperationException("随动缓冲连续 10 秒未消化，已停止；请检查控制器运行状态、倍率及运动速度。");
                    throw new FollowBufferBusyException();
                }
                blockedSince = Double.NaN;
                foreach (string line in lines) send(line);
                if (!started)
                {
                    started = true;
                    send("&" + coordinateSystem + "R");
                }
            }
            catch (FollowBufferBusyException) { throw; }
            catch
            {
                try { Abort(); } catch { }
                throw;
            }
        }

        public void End()
        {
            Append(new string[] { "DWELL 0", "RETURN" });
            try { CloseEntry(); } catch { try { Abort(); } catch { } throw; }
            active = started = false;
        }

        public void Abort()
        {
            if (coordinateSystem == 0) return;
            active = started = false;
            Exception failure = null;
            try { CloseEntry(); } catch (Exception error) { failure = error; }
            bool aborted = false;
            try { send("&" + coordinateSystem + "A"); aborted = true; } catch (Exception error) { if (failure == null) failure = error; }
            if (aborted && defined.Contains(coordinateSystem))
            {
                try
                {
                    CloseEntry(); OpenEntry(); send("CLEAR");
                }
                catch (Exception error) { if (failure == null) failure = error; }
                try { CloseEntry(); } catch (Exception error) { if (failure == null) failure = error; }
            }
            if (failure != null) throw failure;
        }

        private void OpenEntry()
        {
            EntryOpen = true; // Also clean up an OPEN whose acknowledgement is lost.
            send("&" + coordinateSystem + " OPEN ROT");
        }

        private void CloseEntry() { send("CLOSE"); EntryOpen = false; }

        private static string N(double value) { return value.ToString("0.########", CultureInfo.InvariantCulture); }
    }
}
