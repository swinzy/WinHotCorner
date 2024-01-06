using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WinHotCornerService
{
    /*
     * The following code is based on this blog:
     * https://holstcoding.blogspot.com/2011/02/wpf-c-how-to-detect-if-another.html
     * Original post: https://www.richard-banks.org/2007/09/how-to-detect-if-another-application-is.html
     * 
     * It provides a wrapped class for detecting if any application is running in fullscreen mode
     * with custom blacklist hWnd and classname
     */

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Checks if an app is running in fullscreen mode
    /// </summary>
    internal class FullscreenCheck
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private static List<IntPtr> exemptedHWnds = new List<IntPtr>();
        private static List<string> exemptedClassNames = new List<string>();

        private const string SWCHR_CNAME = "XamlExplorerHostIslandWindow";
        private const string WPPR_CNAME = "WorkerW";

        static FullscreenCheck()
        {
            // Add desktop hWnd
            exemptedHWnds.Add(GetDesktopWindow());

            // Add shell hWnd
            exemptedHWnds.Add(GetShellWindow());

            // Add switcher className (Alt+Tab Win+Tab etc.)
            exemptedClassNames.Add(SWCHR_CNAME);

            // Add wallpaper className
            exemptedClassNames.Add(WPPR_CNAME);
        }

        /// <summary>
        /// Gets the class name of given hWnd
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private static string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder className = new StringBuilder(256);
            if (GetClassName(hWnd, className, className.Capacity) > 0)
                return className.ToString();
            else
                return string.Empty;
        }

        /// <summary>
        /// Checks if the given hWnd is exempted for fullscreen detection
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private static bool IsExempted(IntPtr hWnd)
        {
            return exemptedHWnds.Contains(hWnd) || exemptedClassNames.Contains(GetWindowClassName(hWnd));
        }

        /// <summary>
        /// Checks if a given hWnd is running in fullscreen mode (window bounds matches whole screen)
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        public static bool IsFullScreen(IntPtr hWnd)
        {
            if (hWnd != null && !hWnd.Equals(IntPtr.Zero))
            {
                GetWindowRect(hWnd, out RECT appBounds);
                System.Drawing.Rectangle screenBounds = System.Windows.Forms.Screen.FromHandle(hWnd).Bounds;
                if ((appBounds.Bottom - appBounds.Top) == screenBounds.Height
                    && (appBounds.Right - appBounds.Left) == screenBounds.Width)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Hot corner should only be triggered when no fullscreen app is running (or app is exempted)
        /// </summary>
        /// <returns></returns>
        public static bool ShouldTrigger()
        {
            IntPtr currentAppHWnd = GetForegroundWindow();
            return IsExempted(currentAppHWnd) || !IsFullScreen(currentAppHWnd);
        }

      
    }
}
