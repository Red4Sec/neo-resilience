using Microsoft.Extensions.Configuration;

namespace NeoStatsPlugin
{
    public class Settings
    {
        /// <summary>
        /// Path
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Default settings
        /// </summary>
        public static Settings Default { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="section">Section</param>
        private Settings(IConfigurationSection section)
        {
            Path = section.GetSection("Path").Value;

            if (string.IsNullOrEmpty(Path))
            {
                Path = "stats.json";
            }
        }

        /// <summary>
        /// Load config
        /// </summary>
        /// <param name="section">Section</param>
        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
