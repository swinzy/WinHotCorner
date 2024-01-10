using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WinHotCorner
{
    internal class Program
    {
        private static string Guid = ((GuidAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value;
        private static Mutex mutex = new Mutex(true, Guid);
        private static readonly FileSystemWatcher watcher = new FileSystemWatcher(ConfigManager.CONF_PATH);
        private static readonly HotCornerService service = new HotCornerService();
        [STAThread]
        static void Main(string[] args)
        {   
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                SetupConfigWatcher();

                service.ReloadConfigAsync();
                service.Start();
                Application.Run(new ApplicationContext());
                watcher.Dispose();
                mutex.ReleaseMutex();
            }    
        }

        private static void SetupConfigWatcher()
        {
            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Error += OnError;

            watcher.Filter = "config.xml";
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine("Error");
        }
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            service.ShouldReloadConfig = true;
            Console.WriteLine("Configuration changed, marked as should reload.");
        }
    }
}
