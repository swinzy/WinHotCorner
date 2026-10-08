using System.Runtime.InteropServices;
using System.Text;

namespace WinHotCorner
{
    /// <summary>
    /// Which way Windows' interface is written, shared by the hot corner and the control panel
    /// </summary>
    public static class InterfaceDirection
    {
        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int LCIDToLocaleName(uint locale, StringBuilder name, int size, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetLocaleInfoEx(string localeName, uint type, out int data, int size);

        private const uint LOCALE_IREADINGLAYOUT = 0x70;
        private const uint LOCALE_RETURN_NUMBER = 0x20000000;
        private const int LOCALE_NAME_MAX_LENGTH = 85;

        /// <summary>
        /// Whether Windows' display language is written right to left (Arabic, Hebrew and others). GNOME puts the
        /// hot corner in the top-right corner then, going by its interface language
        /// </summary>
        public static bool IsRightToLeft()
        {
            var name = new StringBuilder(LOCALE_NAME_MAX_LENGTH);
            if (LCIDToLocaleName(GetUserDefaultUILanguage(), name, name.Capacity, 0) == 0)
                return false;
            // 1: right to left (0 left to right, 2 and 3 vertical)
            return GetLocaleInfoEx(name.ToString(), LOCALE_IREADINGLAYOUT | LOCALE_RETURN_NUMBER, out int layout, sizeof(int) / sizeof(char)) != 0
                && layout == 1;
        }
    }
}
