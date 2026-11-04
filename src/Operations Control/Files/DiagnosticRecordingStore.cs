using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Operations_Control.Models;

namespace Operations_Control.Files
{
    // Persist the original position/error/status samples. Replay derives velocity
    // from their actual timestamps, using the same calculation as live charts.
    public static class DiagnosticRecordingStore
    {
        private const string Header = "motor,time_ms,actual_position_ct,following_error_ct,status_first_hex,status_second_hex,gap_before";
        public const int MaximumSamples = 1000000;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static string DirectoryPath { get { return Path.Combine(DeviceConfigurationStore.DirectoryPath, "recordings"); } }
        public static StreamWriter Create(string path, string source, DateTime startedUtc)
        {
            if (source != "hardware" && source != "simulation") throw new ArgumentException("数据来源无效。");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            StreamWriter writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
            try
            {
                writer.WriteLine("pmac_diagnostics_v1," + source + "," + startedUtc.ToUniversalTime().ToString("o", Invariant));
                writer.WriteLine(Header); writer.Flush(); return writer;
            }
            catch { writer.Dispose(); throw; }
        }
        public static void Append(StreamWriter writer, DiagnosticSample sample)
        {
            writer.WriteLine(sample.Motor.ToString(Invariant) + "," + sample.TimeMs.ToString("R", Invariant) + "," + sample.Position.ToString("R", Invariant) + "," +
                (sample.FollowingError.HasValue ? sample.FollowingError.Value.ToString("R", Invariant) : "") + "," +
                (sample.Status == null ? "," : sample.Status.FirstWord.ToString("X6", Invariant) + "," + sample.Status.SecondWord.ToString("X6", Invariant)) + "," + (sample.GapBefore ? "1" : "0"));
        }
        public static DiagnosticRecording Load(string path)
        {
            DiagnosticRecording recording = new DiagnosticRecording();
            using (StreamReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8, true))
            {
                string[] metadata = (reader.ReadLine() ?? "").Split(',');
                DateTime started;
                if (metadata.Length != 3 || metadata[0] != "pmac_diagnostics_v1" ||
                    (metadata[1] != "hardware" && metadata[1] != "simulation") ||
                    !DateTime.TryParseExact(metadata[2], "o", Invariant, DateTimeStyles.RoundtripKind, out started) || reader.ReadLine() != Header)
                    throw new FormatException("请选择诊断录制功能保存的 CSV（pmac_diagnostics_v1）。");
                recording.Source = metadata[1]; recording.StartedUtc = started.ToUniversalTime();
                Dictionary<int, double> lastTimes = new Dictionary<int, double>();
                string line; int lineNumber = 2;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (recording.Samples.Count >= MaximumSamples) throw new FormatException("记录超过 1000000 个轴样本，请拆分文件。");
                    try
                    {
                        string[] columns = line.Split(','); int motor;
                        if (columns.Length != 7 || !Int32.TryParse(columns[0], NumberStyles.None, Invariant, out motor) || motor < 1 || motor > 8) throw new FormatException();
                        DiagnosticSample sample = new DiagnosticSample { Motor = motor, TimeMs = Number(columns[1]), Position = Number(columns[2]),
                            FollowingError = columns[3].Length == 0 ? (double?)null : Number(columns[3]), GapBefore = columns[6] == "1" };
                        if (sample.TimeMs < 0 || (lastTimes.ContainsKey(motor) && sample.TimeMs <= lastTimes[motor]) || (columns[6] != "0" && columns[6] != "1")) throw new FormatException();
                        if (columns[4].Length != 0 || columns[5].Length != 0) sample.Status = MotorStatus.Parse(columns[4] + columns[5]);
                        lastTimes[motor] = sample.TimeMs; recording.Samples.Add(sample);
                    }
                    catch (FormatException) { throw new FormatException("诊断 CSV 第 " + lineNumber + " 行无效；需要有限数值、有效状态字和各轴递增的时间。"); }
                }
            }
            if (recording.Samples.Count == 0) throw new FormatException("记录没有轴采样数据。");
            return recording;
        }
        public static void CopySnapshot(string source, string destination)
        {
            if (String.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) throw new IOException("快照目标不能覆盖源文件。");
            using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long remaining = input.Length; byte[] buffer = new byte[65536];
                while (remaining > 0)
                {
                    int read = input.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
                    if (read == 0) throw new IOException("复制诊断数据时源文件意外结束。");
                    output.Write(buffer, 0, read); remaining -= read;
                }
            }
        }
        public static void Save(string path, DiagnosticRecording recording)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (StreamWriter writer = Create(temporary, recording.Source, recording.StartedUtc))
                    foreach (DiagnosticSample sample in recording.Samples) Append(writer, sample);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static double Number(string text)
        {
            double value;
            if (!Double.TryParse(text, NumberStyles.Float, Invariant, out value) || Double.IsNaN(value) || Double.IsInfinity(value)) throw new FormatException();
            return value;
        }
    }
}
