using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Graphics;

namespace WinHotCorner.ControlPanel;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYOUTRTL = 0x00400000;

    /// <summary>
    /// Initial window size, in logical pixels
    /// </summary>
    private const int WINDOW_WIDTH = 760;
    private const int WINDOW_HEIGHT = 640;

    /// <summary>
    /// The installer puts the control panel in Program Files\WinHotCorner\ControlPanel, next to the hot corner
    /// </summary>
    private static readonly string HotCornerExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "WinHotCorner.exe"));

    /// <summary>
    /// The settings file of the portable hot corner whose settings are shown, or null for the installed one, whose
    /// settings are in the registry (see <see cref="FindHotCorner"/>)
    /// </summary>
    private string? _portableSettings;

    /// <summary>
    /// The last change could not be written to <see cref="_portableSettings"/>
    /// </summary>
    private bool _saveFailed;

    /// <summary>
    /// The hot corner whose settings are shown
    /// </summary>
    private string ShownHotCornerExe =>
        _portableSettings is null ? HotCornerExe : Path.Combine(Path.GetDirectoryName(_portableSettings)!, "WinHotCorner.exe");

    /// <summary>
    /// The choices in <see cref="ScreensBox"/>, in their order there
    /// </summary>
    private static readonly HotCornerScreens[] ScreenChoices =
        [HotCornerScreens.Primary, HotCornerScreens.Free, HotCornerScreens.PrimaryAndFree, HotCornerScreens.All];

    /// <summary>
    /// The text in the user's language (Strings\&lt;language&gt;\Resources.resw)
    /// </summary>
    private static readonly ResourceLoader Strings = new();

    private readonly DispatcherQueueTimer _statusTimer;
    private readonly bool _rightToLeft;
    private Configuration _config = new();
    private ISet<string> _managed = new HashSet<string>();

    /// <summary>
    /// Set while the controls are filled in from the configuration, so that is not written back
    /// </summary>
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        Title = Strings.GetString("WindowTitle");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "WHC.ico"));
        ResizeAndCenter();

        ShowState(EnabledSwitch, EnabledState);
        ShowState(FullscreenSwitch, FullscreenState);
        ShowState(MouseDownSwitch, MouseDownState);
        ShowState(RightToLeftSwitch, RightToLeftState);
        ShowState(HoldSwitch, HoldState);
        ShowState(RippleSwitch, RippleState);

        // Right to left, the hot corner can be the top-right one: the choice shows only then
        _rightToLeft = InterfaceDirection.IsRightToLeft();
        RightToLeftCard.Visibility = _rightToLeft ? Visibility.Visible : Visibility.Collapsed;
        if (_rightToLeft)
            MirrorWindow();

        ThresholdBox.Minimum = Configuration.MIN_PRESSURE_THRESHOLD;
        ThresholdBox.Maximum = Configuration.MAX_PRESSURE_THRESHOLD;
        LoadSettings();

        // Settings can also change elsewhere (Group Policy, the registry): refresh when the window is used again
        Activated += (sender, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                LoadSettings();
        };

        _statusTimer = DispatcherQueue.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(1);
        _statusTimer.Tick += (sender, e) => UpdateStatus();
        _statusTimer.Start();
        Closed += (sender, e) => _statusTimer.Stop();
    }

    /// <summary>
    /// Keeps the On/Off text next to a switch up to date
    /// </summary>
    private static void ShowState(ToggleSwitch toggle, TextBlock state)
    {
        state.Text = Strings.GetString(toggle.IsOn ? "On" : "Off");
        toggle.Toggled += (sender, e) => state.Text = Strings.GetString(toggle.IsOn ? "On" : "Off");
    }

    /// <summary>
    /// Lays the window out right to left, as Windows' own apps are in a right-to-left display language: the title bar
    /// (its buttons on the left) and the content. WinUI does not do this by itself
    /// </summary>
    private void MirrorWindow()
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_LAYOUTRTL));
        ((FrameworkElement)Content).FlowDirection = FlowDirection.RightToLeft;
    }

    private void ResizeAndCenter()
    {
        double scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        int width = (int)(WINDOW_WIDTH * scale);
        int height = (int)(WINDOW_HEIGHT * scale);
        RectInt32 area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
    }

    private void LoadSettings()
    {
        _loading = true;
        try
        {
            // Values the hot corner ignores are reported in its own log, the defaults show here
            FindHotCorner();
            _config = _portableSettings is null ? ConfigManager.Load(new List<string>()) : LoadPortableSettings(_portableSettings);
            _managed = ConfigManager.GetPolicySettings();
            bool installed = File.Exists(ShownHotCornerExe);

            EnabledSwitch.IsOn = _config.Enabled;
            EnabledSwitch.IsEnabled = installed && !_managed.Contains(nameof(Configuration.Enabled));
            ShowScreenLabels();
            ScreensBox.SelectedIndex = Array.IndexOf(ScreenChoices, _config.Screens);
            ScreensBox.IsEnabled = !_managed.Contains(nameof(Configuration.Screens));
            HoldSwitch.IsOn = _config.ExpandHotCornerArea;
            HoldSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.ExpandHotCornerArea));
            RippleSwitch.IsOn = _config.ShowRipple;
            RippleSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.ShowRipple));
            RightToLeftSwitch.IsOn = _config.MirrorForRightToLeft;
            RightToLeftSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.MirrorForRightToLeft));
            FullscreenSwitch.IsOn = _config.DisableWhenFullscreen;
            FullscreenSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.DisableWhenFullscreen));
            MouseDownSwitch.IsOn = _config.DisableWhenMouseDown;
            MouseDownSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.DisableWhenMouseDown));
            ThresholdBox.Value = _config.PressureThreshold;
            ThresholdBox.IsEnabled = !_managed.Contains(nameof(Configuration.PressureThreshold));
            ResetButton.IsEnabled = ScreensBox.IsEnabled || HoldSwitch.IsEnabled || RippleSwitch.IsEnabled || RightToLeftSwitch.IsEnabled || FullscreenSwitch.IsEnabled || MouseDownSwitch.IsEnabled || ThresholdBox.IsEnabled;

            NotInstalledInfo.IsOpen = !installed;
            PolicyInfo.IsOpen = _managed.Count > 0;
            ShowPortableInfo();
        }
        finally
        {
            _loading = false;
        }
        UpdateStatus();
    }

    /// <summary>
    /// Follows the hot corner that is running: a portable one when the settings file is next to it. While none is
    /// running, the window stays with the last one it found, so a portable hot corner that was turned off here can be
    /// turned on again
    /// </summary>
    private void FindHotCorner()
    {
        string? running = HotCornerControl.GetRunningPath();
        if (running is null)
            return;

        string settings = Path.Combine(Path.GetDirectoryName(running)!, ConfigManager.SETTINGS_FILE_NAME);
        string? portable = File.Exists(settings) ? settings : null;
        if (portable != _portableSettings)
        {
            _portableSettings = portable;
            _saveFailed = false;
        }
    }

    /// <returns>the settings in the file, or the ones shown while it cannot be read (it may be being saved)</returns>
    private Configuration LoadPortableSettings(string path)
    {
        try
        {
            // Values the hot corner ignores are reported in its own log, the defaults show here
            return ConfigManager.LoadFromFile(path, new List<string>());
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return _config;
        }
    }

    /// <summary>
    /// Says where the settings of a portable hot corner are, or that they could not be saved there
    /// </summary>
    private void ShowPortableInfo()
    {
        PortableInfo.IsOpen = _portableSettings is not null;
        if (_portableSettings is null)
            return;
        PortableInfo.Severity = _saveFailed ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
        PortableInfo.Message = string.Format(Strings.GetString(_saveFailed ? "PortableNotSaved" : "PortableSaved"), _portableSettings);
    }

    /// <summary>
    /// Writes one setting where the hot corner shown reads it: the registry, or the settings file of a portable one
    /// </summary>
    private void SetValue(string name, int value)
    {
        if (_portableSettings is null)
        {
            ConfigManager.SetUserValue(name, value);
            return;
        }
        SavePortable(() => ConfigManager.SetFileValue(_portableSettings, name, value));
    }

    /// <summary>
    /// Sets settings back to their defaults, where the hot corner shown reads them
    /// </summary>
    private void ClearValues(params string[] names)
    {
        if (_portableSettings is null)
        {
            foreach (string name in names)
                ConfigManager.ClearUserValue(name);
            return;
        }
        SavePortable(() => ConfigManager.ClearFileValues(_portableSettings, names));
    }

    private void SavePortable(Action save)
    {
        try
        {
            save();
            _saveFailed = false;
            ShowPortableInfo();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // A read-only folder, for example: show what is still in effect
            _saveFailed = true;
            LoadSettings();
        }
    }

    /// <summary>
    /// Shows a line under "Hot corner" only when something is unusual; On/Off is already next to the switch
    /// </summary>
    private void UpdateStatus()
    {
        bool notRunning = false;
        if (!File.Exists(ShownHotCornerExe))
            StatusText.Text = Strings.GetString("StatusNotInstalled");
        else if (!_config.Enabled)
            StatusText.Text = _managed.Contains(nameof(Configuration.Enabled)) ? Strings.GetString("StatusTurnedOff") : "";
        else
        {
            notRunning = !HotCornerControl.IsRunning();
            StatusText.Text = notRunning ? Strings.GetString("StatusNotRunning") : "";
        }

        StatusText.Visibility = StatusText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RunNowButton.Visibility = notRunning ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        _config.Enabled = EnabledSwitch.IsOn;
        SetValue(nameof(Configuration.Enabled), _config.Enabled ? 1 : 0);

        // Turning off needs nothing more: the hot corner sees the change and exits by itself
        if (_config.Enabled && !HotCornerControl.IsRunning())
        {
            string exe = ShownHotCornerExe;
            bool portable = _portableSettings is not null;
            _ = Task.Run(() => StartHotCorner(exe, portable));
        }
        UpdateStatus();
    }

    private void ScreensBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ScreensBox.SelectedIndex < 0)
            return;

        // The selection made while loading can be reported only later, so compare instead of relying on _loading
        HotCornerScreens screens = ScreenChoices[ScreensBox.SelectedIndex];
        if (screens == _config.Screens)
            return;
        _config.Screens = screens;
        SetValue(nameof(Configuration.Screens), (int)screens);
    }

    private async void RunNowButton_Click(object sender, RoutedEventArgs e)
    {
        RunNowButton.IsEnabled = false;
        string exe = ShownHotCornerExe;
        bool portable = _portableSettings is not null;
        await Task.Run(() => StartHotCorner(exe, portable));
        RunNowButton.IsEnabled = true;
        UpdateStatus();
    }

    private void HoldSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            SetValue(nameof(Configuration.ExpandHotCornerArea), HoldSwitch.IsOn ? 1 : 0);
    }

    private void RippleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            SetValue(nameof(Configuration.ShowRipple), RippleSwitch.IsOn ? 1 : 0);
    }

    private void RightToLeftSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        _config.MirrorForRightToLeft = RightToLeftSwitch.IsOn;
        SetValue(nameof(Configuration.MirrorForRightToLeft), _config.MirrorForRightToLeft ? 1 : 0);
        ShowScreenLabels();
    }

    /// <summary>
    /// Fills in the screen choices, saying top right instead of top left when the hot corner is the top-right one
    /// </summary>
    private void ShowScreenLabels()
    {
        string side = _rightToLeft && _config.MirrorForRightToLeft ? "TopRight" : "";
        string[] labels =
        [
            Strings.GetString("ScreensPrimary"),
            Strings.GetString("ScreensFree" + side),
            Strings.GetString("ScreensPrimaryAndFree" + side),
            Strings.GetString("ScreensAll"),
        ];

        // Replacing the selected item clears the selection: put it back, without writing it
        bool loading = _loading;
        _loading = true;
        int selected = ScreensBox.SelectedIndex;
        for (int i = 0; i < labels.Length; i++)
        {
            if (i >= ScreensBox.Items.Count)
                ScreensBox.Items.Add(labels[i]);
            else if ((string)ScreensBox.Items[i] != labels[i])
                ScreensBox.Items[i] = labels[i];
        }
        ScreensBox.SelectedIndex = selected;
        _loading = loading;
    }

    private void FullscreenSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            SetValue(nameof(Configuration.DisableWhenFullscreen), FullscreenSwitch.IsOn ? 1 : 0);
    }

    private void MouseDownSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            SetValue(nameof(Configuration.DisableWhenMouseDown), MouseDownSwitch.IsOn ? 1 : 0);
    }

    private void ThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading)
            return;

        // Emptied box: show the current value again
        if (double.IsNaN(args.NewValue))
        {
            _loading = true;
            sender.Value = _config.PressureThreshold;
            _loading = false;
            return;
        }

        int value = (int)Math.Round(Math.Clamp(args.NewValue, Configuration.MIN_PRESSURE_THRESHOLD, Configuration.MAX_PRESSURE_THRESHOLD));
        if (value == _config.PressureThreshold)
            return;
        _config.PressureThreshold = value;
        SetValue(nameof(Configuration.PressureThreshold), value);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ClearValues(nameof(Configuration.Screens), nameof(Configuration.ExpandHotCornerArea), nameof(Configuration.ShowRipple),
            nameof(Configuration.MirrorForRightToLeft), nameof(Configuration.DisableWhenFullscreen),
            nameof(Configuration.DisableWhenMouseDown), nameof(Configuration.PressureThreshold));
        LoadSettings();
    }

    /// <summary>
    /// Starts the hot corner the way it starts at logon: through its scheduled task, with the user's highest privileges,
    /// or, where it was installed signed for uiAccess (no task), as the Run key starts it. A portable one is just run
    /// </summary>
    private static void StartHotCorner(string exe, bool portable)
    {
        if (portable)
        {
            try
            {
                if (File.Exists(exe))
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // The status line keeps saying that it is not running
            }
            return;
        }

        try
        {
            var info = new ProcessStartInfo("schtasks.exe", $"/Run /TN \"{HotCornerControl.TASK_NAME}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            using Process? schtasks = Process.Start(info);
            if (schtasks != null && schtasks.WaitForExit(5000) && schtasks.ExitCode == 0)
                return;
        }
        catch (Exception)
        {
            // Fall back to starting it directly
        }

        // No usable task: start it directly, through ShellExecute, which grants the signed version its uiAccess.
        // The usual version then runs without elevation until the next logon, so it does not trigger while an
        // elevated window is in the foreground
        try
        {
            if (File.Exists(exe))
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // The status line keeps saying that it is not running
        }
    }
}
