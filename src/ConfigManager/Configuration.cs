namespace WinHotCorner
{
    /// <summary>
    /// Which screens have a hot corner. A screen's top-left corner is free when no other screen is directly to its
    /// left or above it; a covered corner only works where Windows itself holds the pointer (its sticky corners).
    /// </summary>
    public enum HotCornerScreens
    {
        /// <summary>
        /// The primary screen, and every screen whose corner is free (GNOME's choice)
        /// </summary>
        PrimaryAndFree = 0,
        Primary = 1,
        Free = 2,
        All = 3,
    }

    /// <summary>
    /// The settings of the hot corner. Every property starts at its default.
    /// </summary>
    public class Configuration
    {
        public const int MIN_PRESSURE_THRESHOLD = 10;
        public const int MAX_PRESSURE_THRESHOLD = 1000;

        /// <summary>
        /// When false, the hot corner exits right after starting, and exits if it is turned off while running
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How hard the pointer has to push against a hot corner to trigger it: pressure in logical pixels,
        /// collected over up to one second. 100 is GNOME's HOT_CORNER_PRESSURE_THRESHOLD.
        /// </summary>
        public int PressureThreshold { get; set; } = 100;

        public HotCornerScreens Screens { get; set; } = HotCornerScreens.PrimaryAndFree;

        public bool DisableWhenFullscreen { get; set; } = true;
        public bool DisableWhenMouseDown { get; set; } = true;

        /// <summary>
        /// Output the properties for easy debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Enabled: {Enabled}, PressureThreshold: {PressureThreshold}, Screens: {Screens}, DisableWhenFullscreen: {DisableWhenFullscreen}, DisableWhenMouseDown: {DisableWhenMouseDown}";
        }
    }
}
