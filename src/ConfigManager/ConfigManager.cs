using System;
using System.IO;
using System.Xml.Serialization;

namespace WinHotCorner
{
    /// <summary>
    /// A class for saving/loading configurations from file
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        /// The directory where configuration is stored, typically is "C:\Users\[USER]\Appdata\Local\WinHotCorner"
        /// </summary>
        public static readonly string CONF_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinHotCorner");
        /// <summary>
        /// The configuration file, typically is "C:\Users\[USER]\Appdata\Local\WinHotCorner\config.xml"
        /// </summary>
        public static readonly string CONF_FILE = Path.Combine(CONF_PATH, "config.xml");
        static ConfigManager() 
        {
            // Create configuration directory if it does not exist
            Directory.CreateDirectory(CONF_PATH);
        }

        /// <summary>
        /// Saves the given configuration to file
        /// </summary>
        /// <param name="cfg"></param>
        public static void Save(Configuration cfg)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Configuration));
            using (TextWriter writter = new StreamWriter(CONF_FILE))
            {
                xs.Serialize(writter, cfg);
            }
        }

        /// <summary>
        /// Loads the configuration from file
        /// </summary>
        /// <returns>The configuration (found), default configuration (not found) or null (error)</returns>
        public static Configuration Load()
        {
            try
            {
                // If config file does not exist, return default config
                if (!File.Exists(CONF_FILE))
                {
                    Console.WriteLine("Configuration not found, default config is being loaded.");
                    return new Configuration();
                }

                // Otherwise deserialise from XML
                XmlSerializer xs = new XmlSerializer(typeof(Configuration));
                using (TextReader reader = new StreamReader(CONF_FILE))
                {
                    return xs.Deserialize(reader) as Configuration;
                }             
            }
            catch (Exception ex)
            {
                // In case anything went wrong, output log and return null
                // This is for giving the client a chance to retry
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
