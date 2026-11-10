using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WinHotCorner
{
    /// <summary>
    /// Names shared by the hot corner and whatever controls it (the control panel)
    /// </summary>
    public static class HotCornerControl
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Enough to read the program file, also of an elevated process
        /// </summary>
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// Held by the running hot corner, so only one runs per session.
        /// The assembly GUID, unchanged since the first release, so older versions are recognised too
        /// </summary>
        public const string MUTEX_NAME = "16ba4ae2-691e-495c-affc-532a994857ed";

        /// <summary>
        /// Set this event to ask the running hot corner to exit
        /// </summary>
        public const string EXIT_EVENT_NAME = @"Local\WinHotCorner.Exit";

        /// <summary>
        /// The scheduled task the installer registers: starts the hot corner at logon with the user's highest privileges
        /// </summary>
        public const string TASK_NAME = "WinHotCorner";

        /// <summary>
        /// Checks if the hot corner is running in this session
        /// </summary>
        public static bool IsRunning()
        {
            try
            {
                using (Mutex.OpenExisting(MUTEX_NAME))
                    return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Exists, just not ours to open (the hot corner runs elevated)
                return true;
            }
        }

        /// <summary>
        /// Finds the program file of the hot corner running in this session, which tells an installed one from a
        /// portable one (with the settings file next to it)
        /// </summary>
        /// <returns>null if none is running, or its path cannot be read</returns>
        public static string GetRunningPath()
        {
            int session = Process.GetCurrentProcess().SessionId;
            foreach (Process process in Process.GetProcessesByName("WinHotCorner"))
            {
                using (process)
                {
                    if (process.SessionId != session)
                        continue;

                    IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
                    if (handle == IntPtr.Zero)
                        continue;
                    try
                    {
                        var name = new StringBuilder(32768);
                        int size = name.Capacity;
                        if (QueryFullProcessImageName(handle, 0, name, ref size))
                            return name.ToString();
                    }
                    finally
                    {
                        CloseHandle(handle);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Asks the running hot corner to exit
        /// </summary>
        /// <returns>false if no hot corner is running</returns>
        public static bool RequestExit()
        {
            try
            {
                using (EventWaitHandle exit = EventWaitHandle.OpenExisting(EXIT_EVENT_NAME))
                    exit.Set();
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }
    }
}
