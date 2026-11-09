using System;
using System.IO;
using System.Threading;

namespace WinHotCorner
{
    /// <summary>
    /// Reports changes to the settings file of portable mode
    /// </summary>
    /// <remarks>
    /// Editors save in several steps (truncate and write, or write a temporary file and rename it), each of which
    /// raises an event, so changes are reported once things have been quiet for a moment. <see cref="Changed"/>
    /// runs on a thread pool thread.
    /// </remarks>
    internal sealed class SettingsFileWatcher : IDisposable
    {
        /// <summary>
        /// How long to wait after the last event before reporting, in milliseconds
        /// </summary>
        private const int SETTLE_TIME = 300;

        public event Action Changed;

        private readonly FileSystemWatcher _watcher;
        private readonly Timer _settle;

        public SettingsFileWatcher(string path)
        {
            _settle = new Timer(state => Changed?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            _watcher.Changed += OnEvent;
            _watcher.Created += OnEvent;
            _watcher.Deleted += OnEvent;
            _watcher.Renamed += OnEvent;
            // Too many changes at once: read the file again anyway
            _watcher.Error += (sender, e) => OnEvent(sender, null);
        }

        /// <summary>
        /// Starts watching
        /// </summary>
        public void Arm() => _watcher.EnableRaisingEvents = true;

        private void OnEvent(object sender, EventArgs e) => _settle.Change(SETTLE_TIME, Timeout.Infinite);

        public void Dispose()
        {
            _watcher.Dispose();
            _settle.Dispose();
        }
    }
}
