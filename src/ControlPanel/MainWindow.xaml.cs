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
using Windows.Graphics;

namespace WinHotCorner.ControlPanel;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

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
    /// The choices in <see cref="ScreensBox"/>, in their order there
    /// </summary>
    private static readonly HotCornerScreens[] ScreenChoices =
        [HotCornerScreens.Primary, HotCornerScreens.Free, HotCornerScreens.PrimaryAndFree, HotCornerScreens.All];

    private readonly DispatcherQueueTimer _statusTimer;
    private Configuration _config = new();
    private ISet<string> _managed = new HashSet<string>();

    /// <summary>
    /// Set while the controls are filled in from the configuration, so that is not written back
    /// </summary>
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "WHC.ico"));
        ResizeAndCenter();

        ShowState(EnabledSwitch, EnabledState);
        ShowState(FullscreenSwitch, FullscreenState);
        ShowState(MouseDownSwitch, MouseDownState);

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
        state.Text = toggle.IsOn ? "On" : "Off";
        toggle.Toggled += (sender, e) => state.Text = toggle.IsOn ? "On" : "Off";
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
            _config = ConfigManager.Load(new List<string>());
            _managed = ConfigManager.GetPolicySettings();
            bool installed = File.Exists(HotCornerExe);

            EnabledSwitch.IsOn = _config.Enabled;
            EnabledSwitch.IsEnabled = installed && !_managed.Contains(nameof(Configuration.Enabled));
            ScreensBox.SelectedIndex = Array.IndexOf(ScreenChoices, _config.Screens);
            ScreensBox.IsEnabled = !_managed.Contains(nameof(Configuration.Screens));
            FullscreenSwitch.IsOn = _config.DisableWhenFullscreen;
            FullscreenSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.DisableWhenFullscreen));
            MouseDownSwitch.IsOn = _config.DisableWhenMouseDown;
            MouseDownSwitch.IsEnabled = !_managed.Contains(nameof(Configuration.DisableWhenMouseDown));
            ThresholdBox.Value = _config.PressureThreshold;
            ThresholdBox.IsEnabled = !_managed.Contains(nameof(Configuration.PressureThreshold));
            ResetButton.IsEnabled = ScreensBox.IsEnabled || FullscreenSwitch.IsEnabled || MouseDownSwitch.IsEnabled || ThresholdBox.IsEnabled;

            NotInstalledInfo.IsOpen = !installed;
            PolicyInfo.IsOpen = _managed.Count > 0;
        }
        finally
        {
            _loading = false;
        }
        UpdateStatus();
    }

    /// <summary>
    /// Shows a line under "Hot corner" only when something is unusual; On/Off is already next to the switch
    /// </summary>
    private void UpdateStatus()
    {
        if (!File.Exists(HotCornerExe))
            StatusText.Text = "Not installed";
        else if (!_config.Enabled)
            StatusText.Text = _managed.Contains(nameof(Configuration.Enabled)) ? "Turned off by your organization" : "";
        else
            StatusText.Text = HotCornerControl.IsRunning() ? "" : "Not running";

        StatusText.Visibility = StatusText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        _config.Enabled = EnabledSwitch.IsOn;
        ConfigManager.SetUserValue(nameof(Configuration.Enabled), _config.Enabled ? 1 : 0);

        // Turning off needs nothing more: the hot corner sees the change and exits by itself
        if (_config.Enabled && !HotCornerControl.IsRunning())
            _ = Task.Run(StartHotCorner);
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
        ConfigManager.SetUserValue(nameof(Configuration.Screens), (int)screens);
    }

    private void FullscreenSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            ConfigManager.SetUserValue(nameof(Configuration.DisableWhenFullscreen), FullscreenSwitch.IsOn ? 1 : 0);
    }

    private void MouseDownSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            ConfigManager.SetUserValue(nameof(Configuration.DisableWhenMouseDown), MouseDownSwitch.IsOn ? 1 : 0);
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
        ConfigManager.SetUserValue(nameof(Configuration.PressureThreshold), value);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigManager.ClearUserValue(nameof(Configuration.Screens));
        ConfigManager.ClearUserValue(nameof(Configuration.DisableWhenFullscreen));
        ConfigManager.ClearUserValue(nameof(Configuration.DisableWhenMouseDown));
        ConfigManager.ClearUserValue(nameof(Configuration.PressureThreshold));
        LoadSettings();
    }

    /// <summary>
    /// Starts the hot corner the way it starts at logon: through its scheduled task, with the user's highest privileges
    /// </summary>
    private static void StartHotCorner()
    {
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

        // No usable task: start it directly. It then runs without elevation until the next logon,
        // so it does not trigger while an elevated window is in the foreground
        if (File.Exists(HotCornerExe))
            Process.Start(new ProcessStartInfo(HotCornerExe) { UseShellExecute = true });
    }
}
