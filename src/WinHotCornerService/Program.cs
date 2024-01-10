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
        // For ensuring singleton instance
        private static string Guid = ((GuidAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value;
        private static Mutex mutex = new Mutex(true, Guid);

        /// <summary>
        /// Configuration file watcher
        /// </summary>
        private static readonly FileSystemWatcher watcher = new FileSystemWatcher(ConfigManager.CONF_PATH);

        /// <summary>
        /// Main service
        /// </summary>
        private static readonly HotCornerService service = new HotCornerService();

        [STAThread]
        static void Main(string[] args)
        {   
            // Check singleton
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                SetupConfigWatcher();

                // Load the config for initialisation
                service.ReloadConfigAsync();
                service.Start();

                // Start a message loop for event handling
                Application.Run(new ApplicationContext());

                // Release resources
                watcher.Dispose();
                mutex.ReleaseMutex();
            }    
        }

        /// <summary>
        /// Watches for changes of configuration files
        /// </summary>
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
            Console.WriteLine(e.GetException().Message);
        }
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            // If config file changed, notify the service (do not load config immediately to improve performance)
            service.ShouldReloadConfig = true;
            Console.WriteLine("Configuration changed, marked as should reload.");
        }
    }
}
