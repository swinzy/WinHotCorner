using System;
using System.Threading;
using System.Windows.Forms;

namespace WinHotCorner
{
    internal class Program
    {
        // For ensuring singleton instance
        private static Mutex mutex = new Mutex(true, HotCornerControl.MUTEX_NAME);

        /// <summary>
        /// Main service
        /// </summary>
        private static readonly HotCornerService service = new HotCornerService();

        [STAThread]
        static void Main(string[] args)
        {   
            // Nothing is shown to the user, so at least leave a trace of crashes
            // and never leave the pointer confined
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                PointerBarrier.Release();
                Log.Error($"Unhandled exception: {e.ExceptionObject}");
            };
            Application.ThreadException += (sender, e) => Log.Error($"Unhandled exception: {e.Exception}");

            // Check singleton
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                // Does not start when turned off in the configuration
                if (service.Start())
                {
                    // Start a message loop for event handling
                    Application.Run(new ApplicationContext());
                }

                // Release resources
                service.Stop();
                mutex.ReleaseMutex();
            }    
        }
    }
}
