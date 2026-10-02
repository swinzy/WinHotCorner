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
    ///
    /// If the key is deleted (for example to reset the settings), the watcher opens it again and keeps watching.
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
        private const int ERROR_KEY_DELETED = 1018;

        public event Action Changed;

        private readonly Func<RegistryKey> _open;
        private readonly bool _subtree;
        private readonly AutoResetEvent _event = new AutoResetEvent(false);
        private readonly object _lock = new object();
        private RegistryKey _key;
        private RegisteredWaitHandle _wait;

        /// <param name="open">opens (or creates) the key to watch, with notify access</param>
        /// <param name="subtree">also watch subkeys (and their creation)</param>
        public RegistryWatcher(Func<RegistryKey> open, bool subtree)
        {
            _open = open;
            _subtree = subtree;
            _key = open();
        }

        /// <summary>
        /// Starts watching
        /// </summary>
        public void Arm()
        {
            lock (_lock)
            {
                int result = Notify();
                if (result == ERROR_KEY_DELETED)
                {
                    // The key was deleted, which is the change being reported: watch the new one
                    _key.Dispose();
                    _key = _open();
                    result = Notify();
                }
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
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _wait?.Unregister(null);
                _event.Dispose();
                _key.Dispose();
            }
        }

        private int Notify() => RegNotifyChangeKeyValue(_key.Handle, _subtree,
            REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_THREAD_AGNOSTIC, _event.SafeWaitHandle, true);
    }
}
