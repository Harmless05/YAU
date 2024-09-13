using System.IO;
using System.Text.Json;

namespace YAU.Config
{
    public static class ConfigManager
    {
        internal static readonly string AppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YAU");
        private static readonly string ConfigFilePath = Path.Combine(AppPath, "YAUconfig.json");

        public static AppConfig LoadConfig()
        {
            //Create the directory if it does not exist
            if (!Directory.Exists(AppPath))
            {
                Directory.CreateDirectory(AppPath);
            }
            //Check if the config file exists
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? throw new Exception("Failed to deserialize the config file.");
            }
            else
            {
                // Return default configuration if the file does not exist
                var defaultConfig = new AppConfig
                {
                    AutoStartGTAV = false,
                    PlatSteam = false,
                    PlatEpic = false,
                    PlatRockstar = false,
                    AutoCloseYAU = false,
                    SelectProcess = false,
                    CustomDLL = false,
                    CheckYimUpdates = true,
                    CheckYAUUpdates = true,
                    Debug = false,
                    InjectionDelay = 3000,
                    DarkTheme = true,
                    LightTheme = false,
                    YimDLLPath = Path.Combine(AppPath, "YimMenu.dll"),
                    CustomDLLPath = ""
                };
                SaveConfig(defaultConfig); // Save the default configuration
                return defaultConfig;
            }
        }

        public static void SaveConfig(AppConfig config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }

        public static AppConfig ReadConfig()
        {
            return LoadConfig();
        }

        public static object GetConfigValue(string key)
        {
            var config = LoadConfig();
            var property = config.GetType().GetProperty(key);
            if (property != null)
            {
                return property.GetValue(config);
            }
            throw new ArgumentException($"Key '{key}' not found in configuration.");
        }

        public class AppConfig
        {
            public bool AutoStartGTAV { get; set; }
            public bool PlatSteam { get; set; }
            public bool PlatEpic { get; set; }
            public bool PlatRockstar { get; set; }
            public bool AutoCloseYAU { get; set; }
            public bool SelectProcess { get; set; }
            public bool CustomDLL { get; set; }
            public bool CheckYimUpdates { get; set; }
            public bool CheckYAUUpdates { get; set; }
            public bool Debug { get; set; }
            public int InjectionDelay { get; set; }
            public bool DarkTheme { get; set; }
            public bool LightTheme { get; set; }
            public string YimDLLPath { get; set; } = Path.Combine(ConfigManager.AppPath, "yimmenu.dll");
            public string CustomDLLPath { get; set; } = "";
        }
    }
}



