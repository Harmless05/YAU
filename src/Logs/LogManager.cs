using System.IO;

namespace YAU.Logs
{
    public class LogManager
    {
        private static readonly string AppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YAU");
        private static readonly string ConfigFilePath = Path.Combine(AppPath, "YAUconfig.json");

        public static void Log(string message, string value)
        {
            //Save message to log file
            string logFilePath = Path.Combine(AppPath, $"YAU-{DateTime.Now:dd-MM-yyyy}.log");
            string logMessage = $"[{DateTime.Now}] [{value}] {message} {Environment.NewLine}";
            File.AppendAllText(logFilePath, logMessage);
        }
    }
}
