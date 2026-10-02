using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinHotCorner
{
    /// <summary>
    /// A top-left corner that can trigger, in physical pixels
    /// </summary>
    internal struct HotCorner : IEquatable<HotCorner>
    {
        public IntPtr Monitor;
        public int X;
        public int Y;
        /// <summary>
        /// Display scale of the monitor (1.0 = 100%), for converting GNOME's logical pixels
        /// </summary>
        public double Scale;

        public bool Equals(HotCorner other) =>
            Monitor == other.Monitor && X == other.X && Y == other.Y && Scale == other.Scale;

        public override string ToString() => $"({X}, {Y}) at {Scale:P0}";
    }

    /// <summary>
    /// Finds the hot corners of the current monitor layout
    /// </summary>
    internal static class DisplayLayout
    {
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        private const uint MONITOR_DEFAULTTONULL = 0;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const double DEFAULT_DPI = 96;

        /// <summary>
        /// Every monitor whose top-left corner is exposed, i.e. no other monitor directly to its left or above it.
        /// </summary>
        /// <remarks>
        /// Same test as GNOME Shell's LayoutManager._updateHotCorners (js/ui/layout.js): look at the pixel just left
        /// of the corner and the pixel just above it. GNOME always keeps the primary monitor's corner and stops the
        /// pointer there with a barrier; Windows has no barriers, so a covered corner cannot be hit on any monitor.
        /// </remarks>
        public static List<HotCorner> GetHotCorners()
        {
            var monitors = new List<KeyValuePair<IntPtr, RECT>>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdc, rect, data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(hMonitor, ref info))
                    monitors.Add(new KeyValuePair<IntPtr, RECT>(hMonitor, info.rcMonitor));
                return true;
            }, IntPtr.Zero);

            var corners = new List<HotCorner>();
            foreach (var monitor in monitors)
            {
                RECT bounds = monitor.Value;
                bool covered = false;
                foreach (var other in monitors)
                {
                    if (other.Key == monitor.Key)
                        continue;
                    if (Contains(other.Value, bounds.Left - 1, bounds.Top) || Contains(other.Value, bounds.Left, bounds.Top - 1))
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                {
                    corners.Add(new HotCorner
                    {
                        Monitor = monitor.Key,
                        X = bounds.Left,
                        Y = bounds.Top,
                        Scale = GetScale(monitor.Key),
                    });
                }
            }
            return corners;
        }

        /// <summary>
        /// Checks if a point is on any monitor (false means the pointer cannot go there)
        /// </summary>
        public static bool IsOnAnyMonitor(POINT pt) => MonitorFromPoint(pt, MONITOR_DEFAULTTONULL) != IntPtr.Zero;

        private static bool Contains(RECT rect, int x, int y) =>
            x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;

        private static double GetScale(IntPtr hMonitor)
        {
            if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 && dpiX > 0)
                return dpiX / DEFAULT_DPI;
            return 1.0;
        }
    }
}
