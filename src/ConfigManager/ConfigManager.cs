using System;
using System.IO;
using System.Xml.Serialization;

namespace WinHotCorner
{
    public static class ConfigManager
    {
        public static readonly string CONF_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinHotCorner");
        public static readonly string CONF_FILE = Path.Combine(CONF_PATH, "config.xml");
        static ConfigManager() 
        {
            // Create configuration directory if it does not exist
            Directory.CreateDirectory(CONF_PATH);
        }


        public static void Save(Configuration cfg)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Configuration));
            using (TextWriter writter = new StreamWriter(CONF_FILE))
            {
                xs.Serialize(writter, cfg);
            }
        }

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

                XmlSerializer xs = new XmlSerializer(typeof(Configuration));
                using (TextReader reader = new StreamReader(CONF_FILE))
                {
                    return xs.Deserialize(reader) as Configuration;
                }             
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
