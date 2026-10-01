namespace WinHotCorner
{
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

        public bool DisableWhenFullscreen { get; set; } = true;
        public bool DisableWhenMouseDown { get; set; } = true;

        /// <summary>
        /// Output the properties for easy debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Enabled: {Enabled}, PressureThreshold: {PressureThreshold}, DisableWhenFullscreen: {DisableWhenFullscreen}, DisableWhenMouseDown: {DisableWhenMouseDown}";
        }
    }
}
