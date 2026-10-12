using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Operations_Control.Models;

namespace Operations_Control.Services
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
            CommandResult.Validate(response, status);
            return new CommandResult(response, status);
        }

        public void Download(string filePath)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未连接。");
            if (!File.Exists(filePath)) throw new FileNotFoundException("程序文件不存在。", filePath);
            object[] arguments = { deviceNumber, filePath, false, false, true, true, false };
            Invoke("Download", arguments);
            if (!(bool)arguments[6]) throw new InvalidOperationException("程序下载失败，请查看 PcommServer 生成的下载日志。");
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
