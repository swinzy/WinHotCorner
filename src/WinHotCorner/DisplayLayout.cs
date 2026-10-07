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
        /// <summary>
        /// Another monitor is directly to the left of the corner or above it
        /// </summary>
        public bool Covered;

        public bool Equals(HotCorner other) =>
            Monitor == other.Monitor && X == other.X && Y == other.Y && Scale == other.Scale && Covered == other.Covered;

        public override string ToString() => $"({X}, {Y}) at {Scale:P0}{(Covered ? ", covered" : "")}";
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
        private const uint MONITORINFOF_PRIMARY = 1;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const double DEFAULT_DPI = 96;

        /// <summary>
        /// The top-left corners of the monitors that <paramref name="screens"/> picks
        /// </summary>
        /// <remarks>
        /// A corner is covered when another monitor is directly to its left or above it: the same test as GNOME
        /// Shell's LayoutManager._updateHotCorners (js/ui/layout.js), which looks at the pixel just left of the corner
        /// and the pixel just above it. GNOME keeps the primary monitor's corner and the free ones, and stops the
        /// pointer at a covered primary corner with a barrier. Windows has no barriers, so a covered corner only works
        /// where Windows holds the pointer by itself (see <see cref="CornerPressure"/>).
        /// </remarks>
        public static List<HotCorner> GetHotCorners(HotCornerScreens screens)
        {
            var monitors = new List<KeyValuePair<IntPtr, MONITORINFO>>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdc, rect, data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(hMonitor, ref info))
                    monitors.Add(new KeyValuePair<IntPtr, MONITORINFO>(hMonitor, info));
                return true;
            }, IntPtr.Zero);

            var corners = new List<HotCorner>();
            foreach (var monitor in monitors)
            {
                RECT bounds = monitor.Value.rcMonitor;
                bool covered = false;
                foreach (var other in monitors)
                {
                    if (other.Key == monitor.Key)
                        continue;
                    RECT otherBounds = other.Value.rcMonitor;
                    if (Contains(otherBounds, bounds.Left - 1, bounds.Top) || Contains(otherBounds, bounds.Left, bounds.Top - 1))
                    {
                        covered = true;
                        break;
                    }
                }

                bool primary = (monitor.Value.dwFlags & MONITORINFOF_PRIMARY) != 0;
                bool wanted;
                switch (screens)
                {
                    case HotCornerScreens.Primary: wanted = primary; break;
                    case HotCornerScreens.Free: wanted = !covered; break;
                    case HotCornerScreens.All: wanted = true; break;
                    default: wanted = primary || !covered; break;
                }

                if (wanted)
                {
                    corners.Add(new HotCorner
                    {
                        Monitor = monitor.Key,
                        X = bounds.Left,
                        Y = bounds.Top,
                        Scale = GetScale(monitor.Key),
                        Covered = covered,
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
