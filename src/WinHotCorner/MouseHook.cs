using System;
using System.Runtime.InteropServices;

namespace WinHotCorner
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Global low-level mouse hook (WH_MOUSE_LL).
    /// </summary>
    /// <remarks>
    /// The hook sees where the pointer is about to go, after pointer speed and acceleration but before it is
    /// stopped by the edge of the screen. That overshoot is exactly the "pressure" GNOME measures at its barriers.
    ///
    /// The hook sits in the path of every mouse event in the system, so the handler must return quickly.
    /// Windows silently removes a hook whose handler is too slow; the owner should check <see cref="EventCount"/>
    /// and call <see cref="Reinstall"/> if events stop arriving.
    /// </remarks>
    internal sealed class MouseHook : IDisposable
    {
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_MOUSE_LL = 14;

        // Field offsets in MSLLHOOKSTRUCT: POINT pt (0, 4), DWORD mouseData (8), DWORD flags (12), DWORD time (16)
        private const int OFFSET_X = 0;
        private const int OFFSET_Y = 4;
        private const int OFFSET_TIME = 16;

        /// <summary>
        /// Called for every mouse event with the message (WM_MOUSEMOVE, WM_LBUTTONDOWN, ...),
        /// the position the pointer is about to move to and the event time in milliseconds
        /// </summary>
        public event Action<int, POINT, uint> MouseEvent;

        /// <summary>
        /// Number of events seen so far, for detecting a hook that Windows has removed
        /// </summary>
        public long EventCount { get; private set; }

        // Kept in a field so the garbage collector does not free the callback while Windows still calls it
        private readonly LowLevelMouseProc _proc;
        private IntPtr _hook = IntPtr.Zero;

        public MouseHook()
        {
            _proc = HookProc;
        }

        public void Install()
        {
            if (_hook != IntPtr.Zero)
                return;

            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
                Log.Error($"Cannot install mouse hook (error {Marshal.GetLastWin32Error()})");
        }

        public void Uninstall()
        {
            if (_hook == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        public void Reinstall()
        {
            Uninstall();
            Install();
        }

        public void Dispose() => Uninstall();

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                EventCount++;

                // Read the fields directly instead of marshalling the whole struct: no allocation per event
                var pt = new POINT
                {
                    X = Marshal.ReadInt32(lParam, OFFSET_X),
                    Y = Marshal.ReadInt32(lParam, OFFSET_Y),
                };
                uint time = (uint)Marshal.ReadInt32(lParam, OFFSET_TIME);

                // An exception must never escape into Windows' input processing
                try
                {
                    MouseEvent?.Invoke(wParam.ToInt32(), pt, time);
                }
                catch (Exception ex)
                {
                    Log.Error($"Mouse hook handler failed: {ex}");
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
    }
}
