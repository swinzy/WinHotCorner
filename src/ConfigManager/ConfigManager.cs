using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace WinHotCorner
{
    /// <summary>
    /// Saves and loads the configuration in the registry, or in the settings file in portable mode
    /// </summary>
    /// <remarks>
    /// Every setting is a DWORD with the name of its <see cref="Configuration"/> property. A missing key or value
    /// means the default, so the hot corner works out of the box without anything written.
    ///
    /// The hot corner usually runs elevated, while anything running as the user can write HKCU. So values are
    /// read strictly: anything that is not a DWORD in range is ignored (the default is used) and reported.
    ///
    /// In portable mode the user's values come from a settings file instead (name=value lines, see
    /// <see cref="LoadFromFile"/>), read just as strictly. Group Policy still overrides them.
    /// </remarks>
    public static class ConfigManager
    {
        /// <summary>
        /// The user's settings, written by the control panel: HKEY_CURRENT_USER\Software\WinHotCorner
        /// </summary>
        public const string KEY_PATH = @"Software\WinHotCorner";

        /// <summary>
        /// Group Policy: a value here overrides the user's: HKEY_LOCAL_MACHINE\Software\Policies\WinHotCorner
        /// </summary>
        public const string POLICY_KEY_PATH = @"Software\Policies\WinHotCorner";

        /// <summary>
        /// The settings file of portable mode, next to the hot corner
        /// </summary>
        public const string SETTINGS_FILE_NAME = "WinHotCorner.ini";

        private const int MAX_SCREENS = (int)HotCornerScreens.All;

        /// <summary>
        /// Reads one of the user's values, from wherever they are kept
        /// </summary>
        /// <returns>true if there is a valid value of that name</returns>
        private delegate bool UserValueReader(string name, int min, int max, ICollection<string> problems, out int value);

        /// <summary>
        /// Saves the given configuration to the user's key
        /// </summary>
        /// <param name="cfg"></param>
        public static void Save(Configuration cfg)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KEY_PATH))
            {
                key.SetValue(nameof(Configuration.Enabled), cfg.Enabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.PressureThreshold), cfg.PressureThreshold, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.Screens), (int)cfg.Screens, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.MirrorForRightToLeft), cfg.MirrorForRightToLeft ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.ExpandHotCornerArea), cfg.ExpandHotCornerArea ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.ShowRipple), cfg.ShowRipple ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.DisableWhenFullscreen), cfg.DisableWhenFullscreen ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.DisableWhenMouseDown), cfg.DisableWhenMouseDown ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Sets one of the user's values (the name of a <see cref="Configuration"/> property)
        /// </summary>
        public static void SetUserValue(string name, int value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KEY_PATH))
                key.SetValue(name, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Removes one of the user's values, so the default applies again
        /// </summary>
        public static void ClearUserValue(string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KEY_PATH, true))
                key?.DeleteValue(name, false);
        }

        /// <summary>
        /// The settings that Group Policy sets, which the user cannot change
        /// </summary>
        /// <returns>names of <see cref="Configuration"/> properties</returns>
        public static ISet<string> GetPolicySettings()
        {
            var names = new HashSet<string>();
            var ignored = new List<string>();
            try
            {
                using (RegistryKey policy = Registry.LocalMachine.OpenSubKey(POLICY_KEY_PATH))
                {
                    foreach (string name in new[] { nameof(Configuration.Enabled), nameof(Configuration.MirrorForRightToLeft),
                        nameof(Configuration.ExpandHotCornerArea), nameof(Configuration.ShowRipple), nameof(Configuration.DisableWhenFullscreen), nameof(Configuration.DisableWhenMouseDown) })
                    {
                        if (TryRead(policy, "policy", name, 0, 1, ignored, out _))
                            names.Add(name);
                    }
                    if (TryRead(policy, "policy", nameof(Configuration.PressureThreshold),
                            Configuration.MIN_PRESSURE_THRESHOLD, Configuration.MAX_PRESSURE_THRESHOLD, ignored, out _))
                        names.Add(nameof(Configuration.PressureThreshold));
                    if (TryRead(policy, "policy", nameof(Configuration.Screens), 0, MAX_SCREENS, ignored, out _))
                        names.Add(nameof(Configuration.Screens));
                }
            }
            catch (Exception)
            {
                // Cannot read the policy key: nothing is shown as managed, the hot corner reports the problem
            }
            return names;
        }

        /// <summary>
        /// Loads the configuration: policy value if set, otherwise the user's value, otherwise the default
        /// </summary>
        /// <param name="problems">receives a description of every value that was ignored or could not be read</param>
        /// <returns>The configuration, never null</returns>
        public static Configuration Load(ICollection<string> problems)
        {
            RegistryKey user;
            try
            {
                user = Registry.CurrentUser.OpenSubKey(KEY_PATH);
            }
            catch (Exception ex)
            {
                // Cannot even open the key: run with the defaults
                problems.Add($"Cannot read configuration: {ex.Message}");
                return new Configuration();
            }

            using (user)
            {
                return Load((string name, int min, int max, ICollection<string> found, out int value) =>
                    TryRead(user, "user", name, min, max, found, out value), problems);
            }
        }

        /// <summary>
        /// Loads the configuration of portable mode: policy value if set, otherwise the value in the settings file,
        /// otherwise the default. A missing file means the defaults.
        /// </summary>
        /// <remarks>
        /// Each line is name=value, with the name of a <see cref="Configuration"/> property and a whole number, as in
        /// the registry. Empty lines, comments (starting with ; or #) and section headers ([...]) are skipped.
        /// </remarks>
        /// <param name="problems">receives a description of every line or value that was ignored</param>
        /// <returns>The configuration, never null</returns>
        /// <exception cref="IOException">The file exists but cannot be read, for example while it is being saved</exception>
        /// <exception cref="UnauthorizedAccessException">The file exists but cannot be read</exception>
        public static Configuration LoadFromFile(string path, ICollection<string> problems)
        {
            string file = Path.GetFileName(path);
            Dictionary<string, string> values = ReadSettingsFile(path, file, problems);
            return Load((string name, int min, int max, ICollection<string> found, out int value) =>
                TryRead(values, file, name, min, max, found, out value), problems);
        }

        /// <summary>
        /// Sets one of the values in the settings file of portable mode (the name of a <see cref="Configuration"/>
        /// property), keeping the rest of the file, comments included, as it is
        /// </summary>
        /// <exception cref="IOException">The file cannot be read or written</exception>
        /// <exception cref="UnauthorizedAccessException">The file cannot be written</exception>
        public static void SetFileValue(string path, string name, int value)
        {
            SetFileValues(path, new Dictionary<string, int> { { name, value } });
        }

        /// <summary>
        /// Sets values in the settings file of portable mode back to their defaults. They are written out rather than
        /// removed, so the file keeps listing every setting
        /// </summary>
        /// <exception cref="IOException">The file cannot be read or written</exception>
        /// <exception cref="UnauthorizedAccessException">The file cannot be written</exception>
        public static void ClearFileValues(string path, IEnumerable<string> names)
        {
            var defaults = new Configuration();
            var values = new Dictionary<string, int>();
            foreach (string name in names)
            {
                object value = typeof(Configuration).GetProperty(name).GetValue(defaults, null);
                values[name] = value is bool on ? (on ? 1 : 0) : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            SetFileValues(path, values);
        }

        /// <summary>
        /// Replaces the lines of these settings, or adds them at the end
        /// </summary>
        private static void SetFileValues(string path, Dictionary<string, int> values)
        {
            var lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                    continue;

                int equals = line.IndexOf('=');
                string name = equals > 0 ? line.Substring(0, equals).Trim() : "";
                foreach (KeyValuePair<string, int> setting in values)
                {
                    if (string.Equals(setting.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = FileLine(setting);
                        written.Add(setting.Key);
                    }
                }
            }
            foreach (KeyValuePair<string, int> setting in values)
            {
                if (!written.Contains(setting.Key))
                    lines.Add(FileLine(setting));
            }
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
        }

        private static string FileLine(KeyValuePair<string, int> setting) =>
            setting.Key + "=" + setting.Value.ToString(CultureInfo.InvariantCulture);

        private static Configuration Load(UserValueReader user, ICollection<string> problems)
        {
            var cfg = new Configuration();
            try
            {
                using (RegistryKey policy = Registry.LocalMachine.OpenSubKey(POLICY_KEY_PATH))
                {
                    cfg.Enabled = ReadBool(policy, user, nameof(Configuration.Enabled), cfg.Enabled, problems);
                    cfg.PressureThreshold = ReadInt(policy, user, nameof(Configuration.PressureThreshold), cfg.PressureThreshold,
                        Configuration.MIN_PRESSURE_THRESHOLD, Configuration.MAX_PRESSURE_THRESHOLD, problems);
                    cfg.Screens = (HotCornerScreens)ReadInt(policy, user, nameof(Configuration.Screens), (int)cfg.Screens, 0, MAX_SCREENS, problems);
                    cfg.MirrorForRightToLeft = ReadBool(policy, user, nameof(Configuration.MirrorForRightToLeft), cfg.MirrorForRightToLeft, problems);
                    cfg.ExpandHotCornerArea = ReadBool(policy, user, nameof(Configuration.ExpandHotCornerArea), cfg.ExpandHotCornerArea, problems);
                    cfg.ShowRipple = ReadBool(policy, user, nameof(Configuration.ShowRipple), cfg.ShowRipple, problems);
                    cfg.DisableWhenFullscreen = ReadBool(policy, user, nameof(Configuration.DisableWhenFullscreen), cfg.DisableWhenFullscreen, problems);
                    cfg.DisableWhenMouseDown = ReadBool(policy, user, nameof(Configuration.DisableWhenMouseDown), cfg.DisableWhenMouseDown, problems);
                }
            }
            catch (Exception ex)
            {
                // Cannot even open the policy key: run with the defaults
                problems.Add($"Cannot read configuration: {ex.Message}");
                return new Configuration();
            }
            return cfg;
        }

        private static bool ReadBool(RegistryKey policy, UserValueReader user, string name, bool fallback, ICollection<string> problems)
        {
            return ReadInt(policy, user, name, fallback ? 1 : 0, 0, 1, problems) != 0;
        }

        private static int ReadInt(RegistryKey policy, UserValueReader user, string name, int fallback, int min, int max, ICollection<string> problems)
        {
            if (TryRead(policy, "policy", name, min, max, problems, out int value))
                return value;
            if (user(name, min, max, problems, out value))
                return value;
            return fallback;
        }

        /// <summary>
        /// Reads the name=value lines of the settings file
        /// </summary>
        /// <returns>the values by name (any case), empty if there is no file</returns>
        private static Dictionary<string, string> ReadSettingsFile(string path, string file, ICollection<string> problems)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (FileNotFoundException)
            {
                return values;
            }
            catch (DirectoryNotFoundException)
            {
                return values;
            }

            var settings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in typeof(Configuration).GetProperties())
                settings.Add(property.Name);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                    continue;

                int equals = line.IndexOf('=');
                string name = equals > 0 ? line.Substring(0, equals).Trim() : "";
                if (!settings.Contains(name))
                {
                    problems.Add($"Ignored line {i + 1} of {file}: not a setting");
                    continue;
                }
                values[name] = line.Substring(equals + 1).Trim();
            }
            return values;
        }

        /// <returns>true if the settings file has a valid value of that name</returns>
        private static bool TryRead(Dictionary<string, string> values, string file, string name, int min, int max, ICollection<string> problems, out int value)
        {
            value = 0;
            if (!values.TryGetValue(name, out string text))
                return false;

            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
            {
                problems.Add($"Ignored {name} in {file}: \"{text}\" is not a whole number");
                return false;
            }
            if (value < min || value > max)
            {
                problems.Add($"Ignored {name} in {file}: {value} is not between {min} and {max}");
                return false;
            }
            return true;
        }

        /// <returns>true if the key has a valid value of that name</returns>
        private static bool TryRead(RegistryKey key, string source, string name, int min, int max, ICollection<string> problems, out int value)
        {
            value = 0;
            if (key is null)
                return false;

            try
            {
                object data = key.GetValue(name);
                if (data is null)
                    return false;

                if (key.GetValueKind(name) != RegistryValueKind.DWord)
                {
                    problems.Add($"Ignored {source} value {name}: not a DWORD");
                    return false;
                }

                value = (int)data;
                if (value < min || value > max)
                {
                    problems.Add($"Ignored {source} value {name}: {value} is not between {min} and {max}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                problems.Add($"Cannot read {source} value {name}: {ex.Message}");
                return false;
            }
        }
    }
}
