using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace WinHotCorner
{
    /// <summary>
    /// Reports changes to a registry key (RegNotifyChangeKeyValue).
    /// </summary>
    /// <remarks>
    /// Windows notifies once per request, so the watcher asks again before reporting each change: a change made
    /// while the key is being read is not missed. <see cref="Changed"/> runs on a thread pool thread.
    /// </remarks>
    internal sealed class RegistryWatcher : IDisposable
    {
        [DllImport("advapi32.dll")]
        private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle hKey, bool bWatchSubtree, uint dwNotifyFilter, SafeWaitHandle hEvent, bool fAsynchronous);

        private const uint REG_NOTIFY_CHANGE_NAME = 0x00000001;
        private const uint REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;
        /// <summary>
        /// Keep watching even if the thread that armed the watch exits (Windows 8 and later)
        /// </summary>
        private const uint REG_NOTIFY_THREAD_AGNOSTIC = 0x10000000;

        public event Action Changed;

        private readonly RegistryKey _key;
        private readonly bool _subtree;
        private readonly AutoResetEvent _event = new AutoResetEvent(false);
        private RegisteredWaitHandle _wait;

        /// <param name="key">the key to watch, opened with notify access; disposed with the watcher</param>
        /// <param name="subtree">also watch subkeys (and their creation)</param>
        public RegistryWatcher(RegistryKey key, bool subtree)
        {
            _key = key;
            _subtree = subtree;
        }

        /// <summary>
        /// Starts watching
        /// </summary>
        public void Arm()
        {
            int result = RegNotifyChangeKeyValue(_key.Handle, _subtree,
                REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_THREAD_AGNOSTIC,
                _event.SafeWaitHandle, true);
            if (result != 0)
            {
                Log.Error($"Cannot watch registry key {_key.Name} (error {result})");
                return;
            }

            _wait?.Unregister(null);
            _wait = ThreadPool.RegisterWaitForSingleObject(_event, (state, timedOut) =>
            {
                Arm();
                Changed?.Invoke();
            }, null, Timeout.Infinite, true);
        }

        public void Dispose()
        {
            _wait?.Unregister(null);
            _event.Dispose();
            _key.Dispose();
        }
    }
}
