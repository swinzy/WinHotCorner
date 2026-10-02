using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WinHotCorner
{
    /// <summary>
    /// Saves and loads the configuration in the registry
    /// </summary>
    /// <remarks>
    /// Every setting is a DWORD with the name of its <see cref="Configuration"/> property. A missing key or value
    /// means the default, so the hot corner works out of the box without anything written.
    ///
    /// The hot corner usually runs elevated, while anything running as the user can write HKCU. So values are
    /// read strictly: anything that is not a DWORD in range is ignored (the default is used) and reported.
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
        /// Saves the given configuration to the user's key
        /// </summary>
        /// <param name="cfg"></param>
        public static void Save(Configuration cfg)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KEY_PATH))
            {
                key.SetValue(nameof(Configuration.Enabled), cfg.Enabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(Configuration.PressureThreshold), cfg.PressureThreshold, RegistryValueKind.DWord);
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
                    foreach (string name in new[] { nameof(Configuration.Enabled), nameof(Configuration.DisableWhenFullscreen), nameof(Configuration.DisableWhenMouseDown) })
                    {
                        if (TryRead(policy, "policy", name, 0, 1, ignored, out _))
                            names.Add(name);
                    }
                    if (TryRead(policy, "policy", nameof(Configuration.PressureThreshold),
                            Configuration.MIN_PRESSURE_THRESHOLD, Configuration.MAX_PRESSURE_THRESHOLD, ignored, out _))
                        names.Add(nameof(Configuration.PressureThreshold));
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
            var cfg = new Configuration();
            try
            {
                using (RegistryKey user = Registry.CurrentUser.OpenSubKey(KEY_PATH))
                using (RegistryKey policy = Registry.LocalMachine.OpenSubKey(POLICY_KEY_PATH))
                {
                    cfg.Enabled = ReadBool(policy, user, nameof(Configuration.Enabled), cfg.Enabled, problems);
                    cfg.PressureThreshold = ReadInt(policy, user, nameof(Configuration.PressureThreshold), cfg.PressureThreshold,
                        Configuration.MIN_PRESSURE_THRESHOLD, Configuration.MAX_PRESSURE_THRESHOLD, problems);
                    cfg.DisableWhenFullscreen = ReadBool(policy, user, nameof(Configuration.DisableWhenFullscreen), cfg.DisableWhenFullscreen, problems);
                    cfg.DisableWhenMouseDown = ReadBool(policy, user, nameof(Configuration.DisableWhenMouseDown), cfg.DisableWhenMouseDown, problems);
                }
            }
            catch (Exception ex)
            {
                // Cannot even open the keys: run with the defaults
                problems.Add($"Cannot read configuration: {ex.Message}");
                return new Configuration();
            }
            return cfg;
        }

        private static bool ReadBool(RegistryKey policy, RegistryKey user, string name, bool fallback, ICollection<string> problems)
        {
            return ReadInt(policy, user, name, fallback ? 1 : 0, 0, 1, problems) != 0;
        }

        private static int ReadInt(RegistryKey policy, RegistryKey user, string name, int fallback, int min, int max, ICollection<string> problems)
        {
            if (TryRead(policy, "policy", name, min, max, problems, out int value))
                return value;
            if (TryRead(user, "user", name, min, max, problems, out value))
                return value;
            return fallback;
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
