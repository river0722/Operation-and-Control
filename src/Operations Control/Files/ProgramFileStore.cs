using System;
using System.IO;
using System.Text;

namespace Operations_Control.Files
{
    public sealed class ProgramDocument
    {
        public string Path, Text;
        public Encoding Encoding;
    }

    public static class ProgramFileStore
    {
        public static ProgramDocument Read(string path)
        {
            if (new FileInfo(path).Length > 2 * 1024 * 1024)
                throw new InvalidOperationException("程序文件超过 2 MB，请使用专用编辑器处理。");
            Encoding encoding = new UTF8Encoding(false, true); string text;
            try { text = File.ReadAllText(path, encoding); }
            catch (DecoderFallbackException) { encoding = Encoding.GetEncoding(936); text = File.ReadAllText(path, encoding); }
            return new ProgramDocument { Path = path, Text = text, Encoding = encoding };
        }
        public static void Save(string path, string text, Encoding encoding) { File.WriteAllText(path, text, encoding); }
        public static void SaveGenerated(string path, string text) { Save(path, text, Encoding.ASCII); }
        public static TemporaryProgram CreateTemporary(string text) { return new TemporaryProgram(text); }
    }

    public sealed class TemporaryProgram : IDisposable
    {
        public string Path { get; private set; }
        internal TemporaryProgram(string text)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pmac-trajectory-" + Guid.NewGuid().ToString("N") + ".pmc");
            try { ProgramFileStore.SaveGenerated(Path, text); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { if (File.Exists(Path)) File.Delete(Path); }
    }
}
