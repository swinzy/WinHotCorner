using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinHotCorner
{
    internal class HotCornerService
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Defines how many times it should retry upon failure to load configuration
        /// </summary>
        private const int CFG_RETRY = 3;

        /// <summary>
        /// How often the watchdog checks the mouse hook, the monitor layout and the configuration, in milliseconds
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

        public bool ShouldReloadConfig { get; set; } = false;
        private Configuration Configuration { get; set; } = new Configuration();

        private readonly MouseHook _hook = new MouseHook();
        private readonly Timer _watchdog = new Timer { Interval = WATCHDOG_INTERVAL };
        private MessageWindow _window;
        private List<PressureBarrier> _barriers = new List<PressureBarrier>();

        /// <summary>
        /// Mouse buttons currently held, one bit per button
        /// </summary>
        private int _buttonsDown = 0;

        private long _lastEventCount = 0;
        private POINT _lastCursorPos;

        public HotCornerService()
        {
            _hook.MouseEvent += OnMouseEvent;
            _watchdog.Tick += (sender, e) => Watchdog();
        }

        /// <summary>
        /// Starts detecting. Must be called on the thread that runs the message loop.
        /// </summary>
        public void Start()
        {
            _window = new MessageWindow(this);
            UpdateBarriers();
            _hook.Install();
            GetCursorPos(out _lastCursorPos);
            _watchdog.Start();
            Log.Info("Started");
        }

        public void Stop()
        {
            _watchdog.Stop();
            _hook.Dispose();
            _window?.DestroyHandle();
        }

        /// <summary>
        /// Reloads configuration from file (contains retry mechanism)
        /// </summary>
        public async void ReloadConfigAsync()
        {
            Log.Info("Reloading configuration...");

            // Immediately set flag to false to prevent multiple reloading process at the same time
            ShouldReloadConfig = false;

            // Start trying to load config and retry upon failure
            var newCfg = ConfigManager.Load();
            for (int i = 0; i < CFG_RETRY; i++)
            {
                if (newCfg is null)
                {
                    Log.Info($"Failed to load configuration, retrying in 1 second ({i+1}/{CFG_RETRY}).");
                    await Task.Delay(1000).ContinueWith(_ => newCfg = ConfigManager.Load());
                }
                else
                {
                    break;
                }
            }

            // Failure after a series of retrials: keep the current configuration until the file changes again
            if (newCfg is null)
            {
                Log.Error("Cannot load configuration, aborted.");
                return;
            }

            // Success
            Log.Info("Configuration loaded.");
            Configuration = newCfg;
            Log.Info(Configuration.ToString());
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
            bool involved = false;
            for (int i = 0; i < _barriers.Count; i++)
            {
                if (_barriers[i].IsHit || _barriers[i].IsNear(pt))
                {
                    involved = true;
                    break;
                }
            }
            if (!involved)
                return;

            // Inside the hook the pointer has not moved yet, so this is where it is coming from
            GetCursorPos(out POINT prev);

            double threshold = Configuration.Force;
            for (int i = 0; i < _barriers.Count; i++)
            {
                // Trigger later from the message loop, so the hook returns right away
                if (_barriers[i].OnMove(prev, pt, time, threshold))
                    PostMessage(_window.Handle, WM_TRIGGER, _barriers[i].Corner.Monitor, IntPtr.Zero);
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

            // Don't mix our Win+Tab into a shortcut the user is pressing
            if (TaskView.IsModifierDown())
            {
                Log.Info("Not triggered: Modifier key held");
                return;
            }

            TaskView.Open();
        }

        /// <summary>
        /// Rebuilds the corners if the monitor layout changed
        /// </summary>
        private void UpdateBarriers()
        {
            List<HotCorner> corners = DisplayLayout.GetHotCorners();

            bool same = corners.Count == _barriers.Count;
            for (int i = 0; same && i < corners.Count; i++)
                same = corners[i].Equals(_barriers[i].Corner);
            if (same)
                return;

            _barriers = corners.ConvertAll(corner => new PressureBarrier(corner));
            Log.Info($"Hot corners: {string.Join(", ", corners)}");
        }

        /// <summary>
        /// Windows removes a mouse hook without notice when it is too slow once, so check that it still works.
        /// Also catches monitor changes that sent no message, and applies a changed configuration.
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

            UpdateBarriers();

            if (ShouldReloadConfig)
                ReloadConfigAsync();
        }

        /// <summary>
        /// Hidden top-level window: receives monitor change broadcasts and runs triggers posted by the hook
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
                    case WM_DISPLAYCHANGE:
                    case WM_DPICHANGED:
                    case WM_SETTINGCHANGE:
                        _service.UpdateBarriers();
                        break;
                }
                base.WndProc(ref m);
            }
        }
    }
}
