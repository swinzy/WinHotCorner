using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WinHotCornerService
{
    internal class Program
    {
        private static string Guid = ((GuidAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value;
        private static Mutex mutex = new Mutex(true, Guid);
        [STAThread]
        static void Main(string[] args)
        {   
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                var service = new HotCornerService();
                service.Start();
                Application.Run(new ApplicationContext());
                mutex.ReleaseMutex();
            }    
        }
    }
}
