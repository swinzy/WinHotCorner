using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace WinHotCorner
{
    /// <summary>
    /// GNOME's ripple: three quarter circles that grow out of the corner and fade away when it triggers.
    /// </summary>
    /// <remarks>
    /// Port of GNOME Shell's js/ui/ripples.js, drawn in the .ripple-box style of its theme: white at 20%, a 2 px
    /// spread and 2 px blur shadow of the same colour, 52 px, scaled with the corner as the pivot. At a top-right
    /// corner (right to left) it is mirrored, as GNOME's .ripple-box:rtl.
    ///
    /// The ripple is a click-through layered window that never takes the focus. Task View covers ordinary topmost
    /// windows; only a window of a program with uiAccess stays above it. Without uiAccess the ripple plays all the
    /// same, under Task View, so nothing here needs to know which it is.
    /// </remarks>
    internal sealed class Ripple : NativeWindow, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits,
            IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr ho);

        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
        private const byte AC_SRC_OVER = 0;
        private const byte AC_SRC_ALPHA = 1;
        private const int ULW_ALPHA = 2;

        /// <summary>
        /// Size of the ripple box at scale 1, in logical pixels (.ripple-box width, height and border radius)
        /// </summary>
        private const double BOX_SIZE = 52;

        /// <summary>
        /// How far the shadow reaches past the box, in logical pixels (2 px spread plus 2 px blur)
        /// </summary>
        private const double SHADOW = 4;

        /// <summary>
        /// Opacity of the box and of its shadow (both rgba(255, 255, 255, 0.2))
        /// </summary>
        private const double ALPHA = 0.2;

        /// <summary>
        /// Time between frames, in milliseconds. Window timers don't go much below 15 ms anyway
        /// </summary>
        private const int FRAME_INTERVAL = 10;

        private struct Wave
        {
            public double Delay;
            public double Duration;
            public double StartScale;
            public double StartOpacity;
            public double FinalScale;
        }

        /// <summary>
        /// The three ripples, as in ripples.js (found by trial and error there, so don't look for them to make sense)
        /// </summary>
        private static readonly Wave[] WAVES =
        {
            //                   delay   time  scale  opacity => scale
            new Wave { Delay = 0, Duration = 830, StartScale = 0.25, StartOpacity = 1.0, FinalScale = 1.5 },
            new Wave { Delay = 50, Duration = 1000, StartScale = 0.0, StartOpacity = 0.7, FinalScale = 1.25 },
            new Wave { Delay = 350, Duration = 1000, StartScale = 0.0, StartOpacity = 0.3, FinalScale = 1 },
        };

        private static readonly double LAST_FRAME = Math.Max(Math.Max(WAVES[0].Delay + WAVES[0].Duration,
            WAVES[1].Delay + WAVES[1].Duration), WAVES[2].Delay + WAVES[2].Duration);

        private readonly Timer _timer = new Timer { Interval = FRAME_INTERVAL };
        private readonly Stopwatch _clock = new Stopwatch();
        private HotCorner _corner;

        // The frame: a premultiplied 32-bit DIB of _size x _size pixels, selected into _dc
        private IntPtr _dc;
        private IntPtr _bitmap;
        private IntPtr _oldBitmap;
        private IntPtr _bits;
        private int[] _pixels;
        private int _size;

        public Ripple()
        {
            CreateHandle(new CreateParams
            {
                Caption = "WinHotCorner ripple",
                Style = WS_POPUP,
                ExStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            });
            _timer.Tick += (sender, e) => NextFrame();
        }

        /// <summary>
        /// Plays the ripple at a corner, from the start if it is already playing.
        /// Does nothing when animations are turned off in Windows (Settings, Accessibility, Visual effects).
        /// </summary>
        public void Play(HotCorner corner)
        {
            if (SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out bool animate, 0) && !animate)
                return;

            _corner = corner;
            int size = (int)Math.Ceiling((BOX_SIZE + SHADOW) * WAVES[0].FinalScale * corner.Scale) + 1;
            if (size != _size)
                CreateFrame(size);

            _clock.Restart();
            if (!Draw(0))
                return;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            _timer.Start();
        }

        private void NextFrame()
        {
            double elapsed = _clock.Elapsed.TotalMilliseconds;
            if (elapsed >= LAST_FRAME || !Draw(elapsed))
                Hide();
        }

        private void Hide()
        {
            _timer.Stop();
            _clock.Reset();
            ShowWindow(Handle, SW_HIDE);
        }

        /// <summary>
        /// Draws the ripples as they are <paramref name="elapsed"/> milliseconds in, and shows the frame
        /// </summary>
        /// <returns>false if the window could not be updated</returns>
        private bool Draw(double elapsed)
        {
            // Radius, shadow width and opacity of each ripple in this frame, in physical pixels
            var radius = new double[WAVES.Length];
            var shadow = new double[WAVES.Length];
            var opacity = new double[WAVES.Length];
            for (int i = 0; i < WAVES.Length; i++)
            {
                Wave wave = WAVES[i];
                double t = Math.Min(1, Math.Max(0, (elapsed - wave.Delay) / wave.Duration));
                // Opacity eases in (quad) from the square root of the start opacity to 0, the scale eases out (quad)
                opacity[i] = Math.Sqrt(wave.StartOpacity) * (1 - t * t);
                double scale = (wave.StartScale + (wave.FinalScale - wave.StartScale) * t * (2 - t)) * _corner.Scale;
                radius[i] = BOX_SIZE * scale;
                shadow[i] = SHADOW * scale;
            }

            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    // The box is a quarter circle around the corner (border-radius 0 0 52px 0)
                    double d = Math.Sqrt((x + 0.5) * (x + 0.5) + (y + 0.5) * (y + 0.5));
                    double a = 0;
                    for (int i = 0; i < WAVES.Length; i++)
                    {
                        if (opacity[i] <= 0 || d >= radius[i] + shadow[i])
                            continue;
                        // How much of the pixel the box covers, and the shadow fading out past its edge
                        double box = Clamp(radius[i] - d + 0.5);
                        double glow = Clamp((radius[i] + shadow[i] - d) / shadow[i]);
                        double ripple = opacity[i] * ALPHA * (box + glow * (1 - box));
                        a = ripple + a * (1 - ripple);
                    }
                    // White, premultiplied
                    int v = (int)Math.Round(a * 255);
                    int column = _corner.RightToLeft ? _size - 1 - x : x;
                    _pixels[y * _size + column] = v << 24 | v << 16 | v << 8 | v;
                }
            }
            Marshal.Copy(_pixels, 0, _bits, _pixels.Length);

            // The frame's corner pixel on the corner's pixel
            var position = new POINT { X = _corner.RightToLeft ? _corner.X - _size + 1 : _corner.X, Y = _corner.Y };
            var size = new SIZE { cx = _size, cy = _size };
            var source = new POINT();
            var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            if (UpdateLayeredWindow(Handle, IntPtr.Zero, ref position, ref size, _dc, ref source, 0, ref blend, ULW_ALPHA))
                return true;

            Log.Error($"Cannot draw the ripple (error {Marshal.GetLastWin32Error()})");
            return false;
        }

        private static double Clamp(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        /// <summary>
        /// Makes the frame big enough for the largest ripple on the corner's monitor
        /// </summary>
        private void CreateFrame(int size)
        {
            DeleteFrame();

            var header = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = size,
                biHeight = -size, // top-down
                biPlanes = 1,
                biBitCount = 32,
            };
            _dc = CreateCompatibleDC(IntPtr.Zero);
            _bitmap = CreateDIBSection(_dc, ref header, 0, out _bits, IntPtr.Zero, 0);
            _oldBitmap = SelectObject(_dc, _bitmap);
            _pixels = new int[size * size];
            _size = size;
        }

        private void DeleteFrame()
        {
            if (_dc == IntPtr.Zero)
                return;
            SelectObject(_dc, _oldBitmap);
            DeleteObject(_bitmap);
            DeleteDC(_dc);
            _dc = _bitmap = _oldBitmap = _bits = IntPtr.Zero;
            _size = 0;
        }

        public void Dispose()
        {
            _timer.Dispose();
            DeleteFrame();
            DestroyHandle();
        }
    }
}
