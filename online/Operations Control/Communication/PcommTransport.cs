using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Operations_Control.Models;

namespace Operations_Control.Communication
{
    // Late binding keeps simulation/build independent of an installed COM interop assembly.
    // This object is created, used and released exclusively on ControllerSession's STA thread.
    public sealed class PcommTransport : IControllerTransport
    {
        private object device;
        private int deviceNumber;
        public bool IsConnected { get; private set; }
        public string DeviceName { get { return "PMAC · 设备 " + deviceNumber; } }

        private void Invoke(string method, object[] arguments)
        {
            try
            {
                ParameterModifier outputs = new ParameterModifier(arguments.Length);
                if (method == "SelectDevice") { outputs[1] = true; outputs[2] = true; }
                else if (method == "Open") outputs[1] = true;
                else if (method == "GetResponseEx") { outputs[3] = true; outputs[4] = true; }
                else if (method == "Download") outputs[6] = true;
                device.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, device, arguments,
                    new ParameterModifier[] { outputs }, CultureInfo.InvariantCulture, null);
            }
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException("PcommServer " + method + " 失败：" +
                    (error.InnerException ?? error).Message, error.InnerException ?? error);
            }
        }

        public bool Connect(int ownerWindow)
        {
            Type type = Type.GetTypeFromProgID("PCommServer.PmacDevice");
            if (type == null) throw new InvalidOperationException("未找到 PcommServer。请安装 PewinPro2 / PMAC 驱动及对应的 32 位 COM 组件。");
            device = Activator.CreateInstance(type);
            object[] selection = { ownerWindow, 0, false };
            Invoke("SelectDevice", selection);
            if (!(bool)selection[2]) { Dispose(); return false; }
            deviceNumber = (int)selection[1];
            object[] opening = { deviceNumber, false };
            Invoke("Open", opening);
            IsConnected = (bool)opening[1];
            if (!IsConnected) throw new InvalidOperationException("无法打开 PMAC 设备 " + deviceNumber + "，请检查驱动和设备配置。");
            return true;
        }

        public CommandResult Send(string command)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未连接。");
            object[] arguments = { deviceNumber, command, true, String.Empty, 0 };
            Invoke("GetResponseEx", arguments);
            string response = (string)arguments[3];
            int status = (int)arguments[4];
            try { CommandResult.Validate(response, status); }
            catch (Exception error)
            {
                throw new InvalidOperationException("PMAC 命令 [" + command + "] 失败：" + error.Message, error);
            }
            return new CommandResult(response, status);
        }

        public void Download(string filePath)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未连接。");
            if (!File.Exists(filePath)) throw new FileNotFoundException("程序文件不存在。", filePath);
            string logPath = Path.ChangeExtension(filePath, ".LOG");
            DateTime startedAt = DateTime.UtcNow;
            object[] arguments = { deviceNumber, filePath, false, false, true, true, false };
            Invoke("Download", arguments);
            if (!(bool)arguments[6]) throw new InvalidOperationException("程序下载失败，请查看 PcommServer 生成的下载日志。");

            // PcommServer can return true when the transfer operation completed even
            // though PMAC rejected individual lines. The generated .LOG is the
            // authoritative result for a compiled download.
            DownloadLogFailure failure = ReadDownloadLog(logPath, startedAt);
            if (failure != null)
                throw new InvalidOperationException("PMAC 程序下载失败：" + failure.Message + "；日志：" + logPath);
        }

        private sealed class DownloadLogFailure
        {
            public string Message;
        }

        private static DownloadLogFailure ReadDownloadLog(string logPath, DateTime startedAt)
        {
            if (!File.Exists(logPath)) return null;
            FileInfo info = new FileInfo(logPath);
            if (info.LastWriteTimeUtc < startedAt.AddSeconds(-1)) return null;
            string[] lines = File.ReadAllLines(logPath);
            foreach (string line in lines)
            {
                if (line.IndexOf("**PMAC Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Download aborted", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new DownloadLogFailure { Message = line.Trim() };
            }
            foreach (string line in lines)
            {
                if (line.IndexOf("Total Errors:", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !line.TrimEnd().EndsWith("0", StringComparison.Ordinal))
                    return new DownloadLogFailure { Message = line.Trim() };
            }
            return null;
        }

        public void Disconnect()
        {
            try { if (IsConnected) Invoke("Close", new object[] { deviceNumber }); }
            finally { IsConnected = false; }
        }

        public void Dispose()
        {
            try { Disconnect(); }
            finally
            {
                if (device != null && Marshal.IsComObject(device)) Marshal.FinalReleaseComObject(device);
                device = null;
            }
        }
    }
}
