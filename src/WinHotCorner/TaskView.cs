using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinHotCorner
{
    /// <summary>
    /// Opens Task View by sending Win+Tab.
    /// </summary>
    /// <remarks>
    /// TODO: Simulating a keystroke is a workaround, not a proper solution. Ideally Windows would provide
    /// a function that opens Task View directly. There is no public one at the moment, so we fall back to
    /// the keyboard shortcut. Replace this if such an API ever becomes available.
    /// </remarks>
    internal static class TaskView
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// MOUSEINPUT is the largest member and must be here even though only keys are sent,
        /// otherwise INPUT has the wrong size and SendInput rejects every call
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const ushort VK_TAB = 0x09;
        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12;
        private const ushort VK_LWIN = 0x5B;
        private const ushort VK_RWIN = 0x5C;
        /// <summary>
        /// An unassigned key. Tapping it while Win is held keeps Win's release from opening the Start menu
        /// </summary>
        private const ushort VK_MASK = 0xE8;

        private static readonly ushort[] MODIFIERS = { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN };

        /// <summary>
        /// Checks if the user is holding Shift, Ctrl, Alt or Win
        /// </summary>
        public static bool IsModifierDown()
        {
            foreach (ushort vk in MODIFIERS)
            {
                if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Sends Win+Tab as one uninterrupted sequence
        /// </summary>
        /// <returns>false if Windows did not accept all the key events</returns>
        public static bool Open()
        {
            INPUT[] keys =
            {
                Key(VK_LWIN, false),
                Key(VK_TAB, false),
                Key(VK_TAB, true),
                Key(VK_LWIN, true),
            };

            // Note: when the foreground window has a higher integrity level (UIPI), the keys are dropped
            // without SendInput reporting any failure
            uint sent = Send(keys);
            if (sent == keys.Length)
                return true;

            Log.Error($"SendInput accepted {sent} of {keys.Length} key events (error {Marshal.GetLastWin32Error()})");

            // Release whatever went down but never came up, so no key is left stuck
            if (sent > 0)
            {
                var release = new List<INPUT> { Key(VK_MASK, false), Key(VK_MASK, true) };
                if (sent == 2)
                    release.Add(Key(VK_TAB, true));
                release.Add(Key(VK_LWIN, true));
                Send(release.ToArray());
            }
            return false;
        }

        private static uint Send(INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

        private static INPUT Key(ushort vk, bool up)
        {
            uint flags = up ? KEYEVENTF_KEYUP : 0;
            if (vk == VK_LWIN)
                flags |= KEYEVENTF_EXTENDEDKEY;

            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } },
            };
        }
    }
}
