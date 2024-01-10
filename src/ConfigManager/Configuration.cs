using System.Xml.Serialization;

namespace WinHotCorner
{
    /// <summary>
    /// A model for (de)serialising configuration
    /// </summary>
    [XmlRoot(ElementName = "WinHotCornerConfig")]
    public class Configuration
    {
        public int Force { get; set; } = 5;
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
