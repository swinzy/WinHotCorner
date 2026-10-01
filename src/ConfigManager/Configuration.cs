using System.Xml.Serialization;

namespace WinHotCorner
{
    /// <summary>
    /// A model for (de)serialising configuration
    /// </summary>
    [XmlRoot(ElementName = "WinHotCornerConfig")]
    public class Configuration
    {
        /// <summary>
        /// How hard the pointer has to push against a hot corner to trigger it: pressure in logical pixels,
        /// collected over up to one second. 100 is GNOME's HOT_CORNER_PRESSURE_THRESHOLD.
        /// </summary>
        public int Force { get; set; } = 100;
        public bool DisableWhenFullscreen { get; set; } = true;
        public bool DisableWhenMouseDown { get; set; } = true;

        /// <summary>
        /// Output the properties for easy debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Force: {Force}\nDisableWhenFullscreen: {DisableWhenFullscreen}\nDisableWhenMouseDown: {DisableWhenMouseDown}\n";
        }
    }
}
