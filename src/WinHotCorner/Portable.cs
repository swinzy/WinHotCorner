using System;
using System.IO;

namespace WinHotCorner
{
    /// <summary>
    /// Portable mode: on while the settings file is next to the program
    /// </summary>
    /// <remarks>
    /// The portable download is the usual hot corner with a WinHotCorner.ini beside it. In portable mode everything
    /// stays in the program's folder: the settings are read from that file instead of HKCU (nothing is written to
    /// the registry) and the log is written next to it. Group Policy still applies. The mode is decided once at
    /// startup; deleting the file later only brings back the defaults.
    /// </remarks>
    internal static class Portable
    {
        /// <summary>
        /// The program's folder
        /// </summary>
        public static readonly string FOLDER = AppDomain.CurrentDomain.BaseDirectory;

        public static readonly string SETTINGS_FILE = Path.Combine(FOLDER, ConfigManager.SETTINGS_FILE_NAME);

        public static readonly bool IsOn = File.Exists(SETTINGS_FILE);
    }
}
