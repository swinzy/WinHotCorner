using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
 
namespace WinHotCorner
{
    public class RegistryListener : IDisposable
    {
        private const string QUERY = @"SELECT * FROM RegistryTreeChangeEvent WHERE Hive = 'HKEY_USERS' AND RootPath='{0}\\{1}'";
        private ManagementEventWatcher watcher;
        private bool disposedValue;

        public RegistryListener()
        {
            var currentUser = WindowsIdentity.GetCurrent();
            Console.WriteLine(string.Format(QUERY, currentUser.User.Value, "Software"));
            watcher = new ManagementEventWatcher(string.Format(QUERY, currentUser.User.Value, "Software"));
            watcher.EventArrived +=
                new EventArrivedEventHandler(RegistryEventArrived);
            watcher.Start();
        }

        private void RegistryEventArrived(object sender, EventArrivedEventArgs e)
        {

            Console.WriteLine("Received an event:");
            try
            {
                Console.WriteLine(e.NewEvent.Properties.Count); 
                //Iterate over the properties received from the event and print them out.
                //foreach (var prop in e.NewEvent.Properties)
                //{
                //    Console.WriteLine($"{prop.Name}:{prop.Value.ToString()}");
                //}
            }
            catch { 
            }

            Console.WriteLine();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.watcher?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
