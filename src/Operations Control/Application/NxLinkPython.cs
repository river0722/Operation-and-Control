using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Operations_Control.Application
{
    public static class NxLinkPython
    {
        // Probe each candidate: copied venv launchers can exist but be unusable.
        public static string Find(string directory, Action<string> log, Func<bool> cancelled)
        {
            List<string> candidates = new List<string>();
            Add(candidates, Environment.GetEnvironmentVariable("PMAC_NXLINK_PYTHON"));
            Add(candidates, Path.Combine(directory, ".venv\\Scripts\\python.exe"));
            string launcher = Probe("py.exe", "-3.13 ", cancelled);
            Add(candidates, launcher);
            foreach (RegistryKey hive in new RegistryKey[] { Registry.CurrentUser, Registry.LocalMachine })
                foreach (string root in new string[] { "Software\\Python", "Software\\WOW6432Node\\Python" })
                    ReadRegistry(candidates, hive, root);
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                string path = folder.Trim().Trim('"');
                if (path.Length == 0) continue;
                foreach (string name in new string[] { "python.exe", "python3.13.exe", "python3.exe" })
                    try { Add(candidates, Path.Combine(path, name)); } catch (ArgumentException) { }
            }
            foreach (string root in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetEnvironmentVariable("ProgramW6432"),
                Path.GetPathRoot(Environment.SystemDirectory) })
            {
                if (String.IsNullOrEmpty(root)) continue;
                Add(candidates, Path.Combine(root, "Programs\\Python\\Python313\\python.exe"));
                Add(candidates, Path.Combine(root, "Python313\\python.exe"));
                Add(candidates, Path.Combine(root, "Python313-32\\python.exe"));
            }
            foreach (string candidate in candidates)
            {
                if (cancelled()) throw new OperationCanceledException();
                if (!File.Exists(candidate)) continue;
                string found = Probe(candidate, "", cancelled);
                if (found != null) { log("已找到本机 Python 3.13：" + found); return found; }
                log("跳过失效或版本不匹配的 Python：" + candidate);
            }
            throw new InvalidOperationException("未找到可运行的 Python 3.13。请在本机安装 Python 3.13（含 py 启动器或注册安装路径），再重新连接。");
        }

        private static void Add(List<string> paths, string path)
        { if (!String.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path); }

        private static void ReadRegistry(List<string> paths, RegistryKey hive, string root)
        {
            try
            {
                using (RegistryKey companies = hive.OpenSubKey(root))
                {
                    if (companies == null) return;
                    foreach (string company in companies.GetSubKeyNames())
                        using (RegistryKey versions = companies.OpenSubKey(company))
                        {
                            if (versions == null) continue;
                            foreach (string version in versions.GetSubKeyNames())
                                using (RegistryKey install = versions.OpenSubKey(version + "\\InstallPath"))
                                {
                                    if (install == null) continue;
                                    Add(paths, install.GetValue("ExecutablePath") as string);
                                    string folder = install.GetValue(null) as string;
                                    if (!String.IsNullOrEmpty(folder)) Add(paths, Path.Combine(folder, "python.exe"));
                                }
                        }
                }
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        private static string Probe(string executable, string prefix, Func<bool> cancelled)
        {
            ProcessStartInfo info = new ProcessStartInfo(executable, prefix +
                "-I -c \"import sys; sys.stdout.reconfigure(encoding='utf-8'); print(sys.executable if sys.version_info[:2] == (3,13) else '')\"");
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = info.RedirectStandardError = true;
            info.StandardOutputEncoding = info.StandardErrorEncoding = System.Text.Encoding.UTF8;
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            try
            {
                using (Process child = Process.Start(info))
                {
                    string result = null;
                    child.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    { if (!String.IsNullOrEmpty(e.Data)) result = e.Data.Trim(); };
                    child.ErrorDataReceived += delegate { };
                    child.BeginOutputReadLine(); child.BeginErrorReadLine();
                    Stopwatch timer = Stopwatch.StartNew();
                    while (!child.WaitForExit(100))
                        if (cancelled() || timer.ElapsedMilliseconds > 4000) { child.Kill(); return null; }
                    child.WaitForExit();
                    return child.ExitCode == 0 && File.Exists(result) ? result : null;
                }
            }
            catch (System.ComponentModel.Win32Exception) { return null; }
            catch (InvalidOperationException) { return null; }
        }
    }
}
