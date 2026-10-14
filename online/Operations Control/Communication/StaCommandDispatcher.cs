using System;
using System.Threading;
using System.Windows.Forms;

namespace Operations_Control.Communication
{
    // WinForms is used privately as an STA message pump for the COM driver.
    // Neither controller contracts nor application services expose a Control.
    internal sealed class StaCommandDispatcher
    {
        private readonly Control worker;
        private volatile bool disposed;

        public StaCommandDispatcher()
        {
            Control dispatcher = null;
            using (ManualResetEvent ready = new ManualResetEvent(false))
            {
                Thread thread = new Thread(delegate()
                {
                    dispatcher = new Control();
                    IntPtr handle = dispatcher.Handle;
                    ready.Set();
                    System.Windows.Forms.Application.Run();
                    dispatcher.Dispose();
                });
                thread.IsBackground = true;
                thread.Name = "PMAC communication (STA)";
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.WaitOne();
            }
            worker = dispatcher;
        }

        public void Execute(Action action, Action<Exception> completed, SynchronizationContext completionContext)
        {
            if (disposed) return;
            SynchronizationContext context = completionContext ?? SynchronizationContext.Current;
            worker.BeginInvoke((MethodInvoker)delegate
            {
                Exception failure = null;
                try { action(); }
                catch (Exception error) { failure = error; }
                if (completed == null || disposed) return;
                SendOrPostCallback finish = delegate { if (!disposed) completed(failure); };
                if (context == null) finish(null);
                else context.Post(finish, null);
            });
        }

        public void Shutdown(Action cleanup)
        {
            if (disposed) return;
            disposed = true;
            worker.BeginInvoke((MethodInvoker)delegate
            {
                try { cleanup(); }
                finally { System.Windows.Forms.Application.ExitThread(); }
            });
        }
    }
}
