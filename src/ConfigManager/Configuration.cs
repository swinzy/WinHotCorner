using System.Xml.Serialization;

namespace WinHotCorner
{
    [XmlRoot(ElementName = "WinHotCornerConfig")]
    public class Configuration
    {
        public int Force { get; set; } = 5;
        public bool DisableWhenFullscreen { get; set; } = true;
        public bool DisableWhenMouseDown { get; set; } = true;

        public override string ToString()
        {
            return $"Force: {Force}\nDisableWhenFullscreen: {DisableWhenFullscreen}\nDisableWhenMouseDown: {DisableWhenMouseDown}\n";
        }
    }
}
