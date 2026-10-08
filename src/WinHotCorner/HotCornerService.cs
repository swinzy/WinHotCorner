using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace WinHotCorner
{
    internal class HotCornerService
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int tokenInformationClass, out int information, int length, out int returnLength);

        private const int TokenElevation = 20;
        private const int TokenUIAccess = 26;

        /// <summary>
        /// How often the watchdog checks the mouse hook and the monitor layout, in milliseconds
        /// </summary>
        private const int WATCHDOG_INTERVAL = 2000;

        private const int WM_SETTINGCHANGE = 0x001A;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_DPICHANGED = 0x02E0;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        /// <summary>
        /// Posted by the mouse hook to trigger outside of the hook, wParam is the corner's monitor
        /// </summary>
        private const int WM_TRIGGER = 0x8000 + 1;
        /// <summary>
        /// Posted when the configuration in the registry changed
        /// </summary>
        private const int WM_CONFIG_CHANGED = 0x8000 + 2;
        /// <summary>
        /// Posted when someone (the control panel) set the exit event
        /// </summary>
        private const int WM_EXIT = 0x8000 + 3;

        private Configuration Configuration { get; set; } = new Configuration();

        private readonly MouseHook _hook = new MouseHook();
        private readonly Timer _watchdog = new Timer { Interval = WATCHDOG_INTERVAL };
        private MessageWindow _window;
        private Ripple _ripple;
        private readonly PointerBarrier _barrier = new PointerBarrier();
        private List<CornerPressure> _corners = new List<CornerPressure>();

        /// <summary>
        /// Mouse buttons currently held, one bit per button
        /// </summary>
        private int _buttonsDown = 0;

        private long _lastEventCount = 0;
        private POINT _lastCursorPos;

        private RegistryWatcher _userWatcher;
        private RegistryWatcher _policyWatcher;
        private EventWaitHandle _exitEvent;
        private RegisteredWaitHandle _exitWait;

        public HotCornerService()
        {
            _hook.MouseEvent += OnMouseEvent;
            _watchdog.Tick += (sender, e) => Watchdog();
        }

        /// <summary>
        /// Starts detecting. Must be called on the thread that runs the message loop.
        /// </summary>
        /// <returns>false if the hot corner is turned off in the configuration</returns>
        public bool Start()
        {
            _window = new MessageWindow(this);
            _ripple = new Ripple();

            // Watch before loading, so a change made in between is not missed
            WatchConfiguration();
            LoadConfiguration();
            if (!Configuration.Enabled)
            {
                Log.Info("Turned off in the configuration, exiting.");
                return false;
            }

            ListenForExit();
            UpdateCorners();
            _hook.Install();
            GetCursorPos(out _lastCursorPos);
            _watchdog.Start();
            Log.Info($"Started (elevated: {HasToken(TokenElevation)}, uiAccess: {HasToken(TokenUIAccess)})");
            return true;
        }

        /// <summary>
        /// Reads a yes/no property of this process's token, for the log
        /// </summary>
        private static string HasToken(int informationClass)
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                if (!GetTokenInformation(identity.Token, informationClass, out int value, sizeof(int), out _))
                    return "unknown";
                return value != 0 ? "yes" : "no";
            }
        }

        public void Stop()
        {
            _watchdog.Stop();
            _hook.Dispose();
            _exitWait?.Unregister(null);
            _exitEvent?.Dispose();
            _userWatcher?.Dispose();
            _policyWatcher?.Dispose();
            PointerBarrier.Release();
            _ripple?.Dispose();
            _window?.DestroyHandle();
        }

        private void LoadConfiguration()
        {
            var problems = new List<string>();
            Configuration = ConfigManager.Load(problems);
            foreach (string problem in problems)
                Log.Error(problem);
            Log.Info($"Configuration: {Configuration}");
        }

        private void WatchConfiguration()
        {
            // The user's key is created if it is missing, so that it can be watched.
            // The policy key may not exist, so watch its parent, which always does
            _userWatcher = new RegistryWatcher(() => Registry.CurrentUser.CreateSubKey(ConfigManager.KEY_PATH), false);
            _policyWatcher = new RegistryWatcher(() => Registry.LocalMachine.OpenSubKey(@"Software\Policies"), true);

            foreach (RegistryWatcher watcher in new[] { _userWatcher, _policyWatcher })
            {
                watcher.Changed += () => PostMessage(_window.Handle, WM_CONFIG_CHANGED, IntPtr.Zero, IntPtr.Zero);
                watcher.Arm();
            }
        }

        private void OnConfigurationChanged()
        {
            LoadConfiguration();
            if (!Configuration.Enabled)
            {
                Log.Info("Turned off in the configuration, exiting.");
                Application.ExitThread();
                return;
            }
            UpdateCorners();
        }

        /// <summary>
        /// Creates the event that asks this process to exit (see <see cref="HotCornerControl.RequestExit"/>)
        /// </summary>
        private void ListenForExit()
        {
            // The hot corner usually runs elevated, and then by default only elevated processes could open the event.
            // Let the user's own unelevated processes, such as the control panel, set it
            var security = new EventWaitHandleSecurity();
            security.AddAccessRule(new EventWaitHandleAccessRule(WindowsIdentity.GetCurrent().User,
                EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify, AccessControlType.Allow));
            security.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                EventWaitHandleRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                EventWaitHandleRights.FullControl, AccessControlType.Allow));

            _exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, HotCornerControl.EXIT_EVENT_NAME, out bool createdNew, security);
            _exitWait = ThreadPool.RegisterWaitForSingleObject(_exitEvent,
                (state, timedOut) => PostMessage(_window.Handle, WM_EXIT, IntPtr.Zero, IntPtr.Zero), null, Timeout.Infinite, true);
        }

        /// <summary>
        /// Runs inside the mouse hook for every mouse event: keep it cheap
        /// </summary>
        private void OnMouseEvent(int message, POINT pt, uint time)
        {
            switch (message)
            {
                case WM_MOUSEMOVE:
                    OnMouseMove(pt, time);
                    break;
                case WM_LBUTTONDOWN: _buttonsDown |= 1; break;
                case WM_LBUTTONUP: _buttonsDown &= ~1; break;
                case WM_RBUTTONDOWN: _buttonsDown |= 2; break;
                case WM_RBUTTONUP: _buttonsDown &= ~2; break;
                case WM_MBUTTONDOWN: _buttonsDown |= 4; break;
                case WM_MBUTTONUP: _buttonsDown &= ~4; break;
                case WM_XBUTTONDOWN: _buttonsDown |= 8; break;
                case WM_XBUTTONUP: _buttonsDown &= ~8; break;
            }
        }

        private void OnMouseMove(POINT pt, uint time)
        {
            // Most movements are nowhere near a corner: skip them without any system call
            bool hold = Configuration.ExpandHotCornerArea;
            bool involved = _barrier.IsHolding;
            for (int i = 0; !involved && i < _corners.Count; i++)
            {
                if (_corners[i].IsWatching || _corners[i].IsNear(pt) || (hold && _corners[i].Corner.Covered && _corners[i].IsAtCorner(pt)))
                    involved = true;
            }
            if (!involved)
                return;

            // Inside the hook the pointer has not moved yet, so this is where it is coming from
            GetCursorPos(out POINT prev);

            // Before the pointer moves: a clip set now already applies to this movement
            if (hold || _barrier.IsHolding)
            {
                CornerPressure at = null;
                for (int i = 0; hold && at == null && i < _corners.Count; i++)
                {
                    if (_corners[i].Corner.Covered && (_corners[i].IsAtCorner(prev) || _corners[i].IsAtCorner(pt)))
                        at = _corners[i];
                }
                _barrier.Update(at, _buttonsDown != 0);
            }

            double threshold = Configuration.PressureThreshold;
            for (int i = 0; i < _corners.Count; i++)
            {
                // Trigger later from the message loop, so the hook returns right away
                if (_corners[i].OnMove(prev, pt, time, threshold))
                    PostMessage(_window.Handle, WM_TRIGGER, _corners[i].Corner.Monitor, IntPtr.Zero);
            }
        }

        private void Trigger(IntPtr monitor)
        {
            // Check fullscreen
            if (Configuration.DisableWhenFullscreen && !FullscreenCheck.ShouldTrigger(monitor))
            {
                Log.Info("Not triggered: App running in fullscreen");
                return;
            }

            // Check guesture
            if (Configuration.DisableWhenMouseDown && _buttonsDown != 0)
            {
                Log.Info("Not triggered: Mouse down");
                return;
            }

            if (!TaskView.Open())
                return;

            foreach (CornerPressure corner in _corners)
            {
                if (corner.Corner.Monitor == monitor)
                {
                    _ripple.Play(corner.Corner);
                    break;
                }
            }
        }

        /// <summary>
        /// Rebuilds the corners if the monitor layout changed
        /// </summary>
        private void UpdateCorners()
        {
            // The monitors may have moved: let go, the next movement at a corner holds the pointer again
            PointerBarrier.Release();

            bool rightToLeft = Configuration.MirrorForRightToLeft && InterfaceDirection.IsRightToLeft();
            List<HotCorner> corners = DisplayLayout.GetHotCorners(Configuration.Screens, rightToLeft);

            bool same = corners.Count == _corners.Count;
            for (int i = 0; same && i < corners.Count; i++)
                same = corners[i].Equals(_corners[i].Corner);
            if (same)
                return;

            _corners = corners.ConvertAll(corner => new CornerPressure(corner));
            Log.Info($"Hot corners: {string.Join(", ", corners)}");

            // Covered corners rely on Windows holding the pointer; for the log only, it may not be what is in effect
            if (corners.Exists(corner => corner.Covered))
            {
                object clip = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "MouseCornerClipLength", null);
                Log.Info($"MouseCornerClipLength: {clip ?? "not set (6)"}");
            }
        }

        /// <summary>
        /// Windows removes a mouse hook without notice when it is too slow once, so check that it still works.
        /// Also catches monitor changes that sent no message.
        /// </summary>
        private void Watchdog()
        {
            GetCursorPos(out POINT pos);
            bool cursorMoved = pos.X != _lastCursorPos.X || pos.Y != _lastCursorPos.Y;
            if (cursorMoved && _hook.EventCount == _lastEventCount)
            {
                // Can also happen when a program moves the pointer itself, reinstalling is harmless then
                Log.Info("Pointer moved but the mouse hook saw nothing, reinstalling it.");
                _hook.Reinstall();
            }
            _lastEventCount = _hook.EventCount;
            _lastCursorPos = pos;

            UpdateCorners();
        }

        /// <summary>
        /// Hidden top-level window: receives monitor change broadcasts and runs what other threads post to it
        /// </summary>
        private class MessageWindow : NativeWindow
        {
            private readonly HotCornerService _service;

            public MessageWindow(HotCornerService service)
            {
                _service = service;
                CreateHandle(new CreateParams { Caption = "WinHotCorner" });
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_TRIGGER:
                        _service.Trigger(m.WParam);
                        return;
                    case WM_CONFIG_CHANGED:
                        _service.OnConfigurationChanged();
                        return;
                    case WM_EXIT:
                        Log.Info("Asked to exit.");
                        Application.ExitThread();
                        return;
                    case WM_DISPLAYCHANGE:
                    case WM_DPICHANGED:
                    case WM_SETTINGCHANGE:
                        _service.UpdateCorners();
                        break;
                }
                base.WndProc(ref m);
            }
        }
    }
}
