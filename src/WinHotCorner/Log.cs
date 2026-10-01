using System;
using System.Diagnostics;
using System.IO;

namespace WinHotCorner
{
    /// <summary>
    /// Minimal logging for a process that has no window.
    /// Every message goes to the debug output (viewable with DebugView, also in Release) and,
    /// in Debug builds, to the console. Errors are also appended to a small log file.
    /// </summary>
    internal static class Log
    {
        /// <summary>
        /// The log folder, typically is "C:\Users\[USER]\Appdata\Local\WinHotCorner"
        /// </summary>
        public static readonly string LOG_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinHotCorner");

        /// <summary>
        /// The error log, typically is "C:\Users\[USER]\Appdata\Local\WinHotCorner\WinHotCorner.log"
        /// </summary>
        public static readonly string LOG_FILE = Path.Combine(LOG_DIR, "WinHotCorner.log");

        /// <summary>
        /// The log file is moved to "*.old" once it grows beyond this size
        /// </summary>
        private const long MAX_LOG_SIZE = 1024 * 1024;

        private static readonly object fileLock = new object();

        public static void Info(string message) => Write("INFO", message, false);

        public static void Error(string message) => Write("ERROR", message, true);

        private static void Write(string level, string message, bool toFile)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

            Trace.WriteLine(line, "WinHotCorner");
#if DEBUG
            Console.WriteLine(line);
#endif
            if (toFile)
                AppendToFile(line);
        }

        private static void AppendToFile(string line)
        {
            // Logging must never take the process down
            try
            {
                lock (fileLock)
                {
                    Directory.CreateDirectory(LOG_DIR);
                    var info = new FileInfo(LOG_FILE);
                    if (info.Exists && info.Length > MAX_LOG_SIZE)
                    {
                        string old = LOG_FILE + ".old";
                        File.Delete(old);
                        File.Move(LOG_FILE, old);
                    }
                    File.AppendAllText(LOG_FILE, line + Environment.NewLine);
                }
            }
            catch (Exception) { }
        }
    }
}
