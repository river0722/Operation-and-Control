using System;
using Operations_Control.Models;

namespace Operations_Control.Services
{
    public interface IControllerTransport : IDisposable
    {
        bool IsConnected { get; }
        string DeviceName { get; }
        bool Connect(int ownerWindow);
        void Disconnect();
        CommandResult Send(string command);
        void Download(string filePath);
    }
}
