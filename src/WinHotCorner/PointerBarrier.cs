using System;
using System.Runtime.InteropServices;

namespace WinHotCorner
{
    /// <summary>
    /// Holds the pointer at a covered corner with ClipCursor, as GNOME's pointer barrier does (ExpandHotCornerArea,
    /// *Expand hot corner area* in the control panel, on by default).
    /// </summary>
    /// <remarks>
    /// While the pointer is within a covered corner's edges (see <see cref="CornerPressure.IsAtCorner"/>), it is
    /// confined to that corner's monitor, so it cannot slip on to the neighbouring monitor there; once it moves away,
    /// it is free again. The mouse hook still reports where the pointer would have gone, so pushes count as at a free
    /// corner.
    ///
    /// ClipCursor is shared with every other program, games in a window among them, so the clip is careful:
    /// - it is not set while another program confines the pointer, nor while a mouse button is held (dragging a window
    ///   to the other monitor), nor while an app is fullscreen on that monitor;
    /// - it is only released while it is still ours, so another program's clip is never removed;
    /// - Windows resets it when the foreground window changes; it is set again on the next movement at the corner.
    /// </remarks>
    internal sealed class PointerBarrier
    {
        [DllImport("user32.dll")]
        private static extern bool ClipCursor(ref RECT rect);

        [DllImport("user32.dll", EntryPoint = "ClipCursor")]
        private static extern bool ReleaseClip(IntPtr rect);

        [DllImport("user32.dll")]
        private static extern bool GetClipCursor(out RECT rect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        /// <summary>
        /// The clip this has set, while it holds the pointer
        /// </summary>
        private static RECT? _clip;

        /// <summary>
        /// The pointer is held, or may need to be released
        /// </summary>
        public bool IsHolding => _clip.HasValue;

        /// <summary>
        /// Holds the pointer on the monitor of the covered corner it is at, or releases it
        /// </summary>
        /// <param name="at">the covered corner the pointer is at, or null</param>
        /// <param name="buttonsDown">a mouse button is held</param>
        public void Update(CornerPressure at, bool buttonsDown)
        {
            if (at == null || buttonsDown)
            {
                Release();
                return;
            }

            RECT bounds = at.Corner.Bounds;
            bool ours = GetClipCursor(out RECT current) && _clip.HasValue && Same(current, _clip.Value);
            if (ours && Same(current, bounds))
                return;

            // Someone else confines the pointer (a game, for example): leave it to them
            if (!ours && !IsFree(current))
            {
                _clip = null;
                return;
            }

            if (!FullscreenCheck.ShouldTrigger(at.Corner.Monitor))
                return;

            if (ClipCursor(ref bounds))
                _clip = bounds;
        }

        /// <summary>
        /// Frees the pointer if this holds it, and the clip is still ours
        /// </summary>
        public static void Release()
        {
            if (!_clip.HasValue)
                return;
            if (GetClipCursor(out RECT current) && Same(current, _clip.Value))
                ReleaseClip(IntPtr.Zero);
            _clip = null;
        }

        /// <summary>
        /// Nothing confines the pointer: the clip is the whole virtual screen
        /// </summary>
        private static bool IsFree(RECT clip)
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN), y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            return clip.Left <= x && clip.Top <= y
                && clip.Right >= x + GetSystemMetrics(SM_CXVIRTUALSCREEN) && clip.Bottom >= y + GetSystemMetrics(SM_CYVIRTUALSCREEN);
        }

        private static bool Same(RECT a, RECT b) =>
            a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }
}
