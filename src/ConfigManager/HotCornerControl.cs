using System;
using System.Threading;

namespace WinHotCorner
{
    /// <summary>
    /// Names shared by the hot corner and whatever controls it (the control panel)
    /// </summary>
    public static class HotCornerControl
    {
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
