using Octokit;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YAU.Config;
using YAU.Logs;
using static YAU.MainWindow;

namespace YAU.Updater
{
    /// <summary>
    /// 
    /// Updater class for YAU to check for YimMenu and YAU updates and download them
    /// 
    /// </summary>

    internal class UpdateManager
    {
        // Expose elements from MainWindow to UpdateManager
        private readonly MainWindow _mainWindow;

        // Path to the YAU folder
        string YAUFolderPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YAU");

        // YAU GitHub API URLs
        private const string YAU_latest_release = "https://api.github.com/repos/Harmless05/YAU/releases/latest";

        // YimMenu GitHub API URLs
        private const string YIM_API_URL = "https://api.github.com/repos/YimMenu/YimMenu/releases/latest";
        private const string YIM_DLL_URL = "https://github.com/YimMenu/YimMenu/releases/download/nightly/YimMenu.dll";

        public UpdateManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// 
        /// Checks for YAU updates at startup
        /// 
        /// </summary>

        public async void CheckYAUAtStartup()
        {
            bool checkAtStartup = (bool)ConfigManager.GetConfigValue("CheckYAUUpdates");
            if (!checkAtStartup) return;
        
            LogManager.Log("Checking for YAU updates", "INFO");
        
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("YAU"));
                var releases = await client.Repository.Release.GetAll("Harmless05", "YAU");
                var latest = releases[0];
                var latestTag = latest.TagName.Substring(1);
        
                if (latestTag == Properties.Resources.Version) return;
        
                MessageBoxResult result = MessageBox.Show($"A new version of YAU is available. Do you want to update?", $"Version {latestTag} is available!", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result != MessageBoxResult.Yes) return;
        
                string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YAU.exe");
                string appPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YAU", "YAU.exe");
                string scriptPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YAU", "Updater.bat");
        
                if (!Directory.Exists(YAUFolderPath))
                {
                    LogManager.Log("No YAU folder found. Creating the YAU folder", "INFO");
                    Directory.CreateDirectory(YAUFolderPath);
                }
        
                await DownloadFileAsync(latest.Assets[0].BrowserDownloadUrl, tempFilePath);
                if (!File.Exists(scriptPath))
                {
                    LogManager.Log("Downloading the updater script", "INFO");
                    await DownloadFileAsync("https://raw.githubusercontent.com/Harmless05/YAU/master/UpdateHelper/Updater.bat", scriptPath);
                }
        
                StartUpdaterScript(scriptPath, tempFilePath, appPath);
            }
            catch (Exception ex)
            {
                LogManager.Log($"Failed to get repository: {ex.Message}", "ERROR");
                MessageBox.Show(ex.Message, "Failed to get repository", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "YAU");
                var responseMessage = await httpClient.GetAsync(url);
                responseMessage.EnsureSuccessStatusCode();
                using (var fileStream = new FileStream(destinationPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await responseMessage.Content.CopyToAsync(fileStream);
                }
            }
        }
        
        private void StartUpdaterScript(string scriptPath, string tempFilePath, string appPath)
        {
            LogManager.Log("Starting the updater script...", "INFO");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath} {tempFilePath} {appPath}\"",
                UseShellExecute = false,
                CreateNoWindow = false
            };
            System.Diagnostics.Process.Start(startInfo);
        
            LogManager.Log("Closing the current application...", "INFO");
            MessageBox.Show("YAU will now update and restart.", "YAU Update", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
        }


        /// <summary>
        /// 
        /// Checks for YimMenu DLL updates at startup
        /// 
        /// </summary>
        public async void CheckYimAtStartup()
        {
            bool checkAtStartup = (bool)ConfigManager.GetConfigValue("CheckYimUpdates");
            if (checkAtStartup)
            {
                LogManager.Log("Checking for YimMenu updates", "INFO");

                Hashes hashes = GetHashes();
                // Compare the two hashes
                if (hashes.WebHash != hashes.LocalHash)
                {
                    LogManager.Log("A new version of YimMenu is available. New SHA:" + hashes.WebHash, "INFO");
                    MessageBoxResult result = MessageBox.Show("A new version of YimMenu is available. Do you want to update?", "Update Available!", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Try to remove the old version, if failed then break
                        try
                        {
                            // Delete the old version
                            LogManager.Log("Deleting the old version...", "INFO");
                            File.Delete((string)ConfigManager.GetConfigValue("YimDLLPath"));
                        }
                        catch (Exception ex)
                        {
                            System.Media.SystemSounds.Exclamation.Play();
                            LogManager.Log("Failed to delete the old version: " + ex.Message, "ERROR");
                            MessageBox.Show(ex.Message, "Failed to delete the old version", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        LogManager.Log("Downloading the latest version...", "INFO");
                        using (HttpClient client = new HttpClient())
                        {
                            // Download the DLL
                            try
                            {
                                var response = await client.GetAsync(YIM_DLL_URL);
                                response.EnsureSuccessStatusCode();
                                using (var fileStream = new FileStream((string)ConfigManager.GetConfigValue("YimDLLPath"), System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }
                                LogManager.Log("Update completed", "INFO");

                                _mainWindow.InjectBtn.Content = "Update completed!";
                                await Task.Delay(3000);
                                _mainWindow.InjectBtn.Content = "Inject";
                            }
                            catch (Exception ex)
                            {
                                LogManager.Log("Failed to download the latest version: " + ex.Message, "ERROR");
                                MessageBox.Show(ex.Message, "Failed to download the latest version", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                    else
                    {
                        LogManager.Log("Download cancelled.", "INFO");
                    }
                }
            }
        }

        /// Gets the hashes of the local and web YimMenu DLLs
        public Hashes GetHashes()
        {
            string YimDLLPath = (string)ConfigManager.GetConfigValue("YimDLLPath");

            // Get the latest release from the GitHub API
            var client = new GitHubClient(new ProductHeaderValue("YimMenu"));
            var releases = client.Repository.Release.GetAll("YimMenu", "YimMenu");
            var latest = releases.Result[0];
            var response = latest.Body;

            // Get Web DLL hash
            var sha256Web = Regex.Match(response, "([a-fA-F\\d]{64})").Groups[1].Value;

            string sha256Local;


            if (!File.Exists(YimDLLPath))
            {
                MessageBox.Show($"YimMenu.dll not found in {YimDLLPath}", "Downloading the latest version...", MessageBoxButton.OK, MessageBoxImage.Information);
                LogManager.Log("Local YimMenu DLL not found", "INFO");
                LogManager.Log("Downloading the latest version...", "INFO");

                try
                {
                    // Download the DLL
                    using (WebClient webClient = new WebClient())
                    {
                        webClient.DownloadFile(YIM_DLL_URL, YimDLLPath);
                    }
                    sha256Local = GetFileHash(YimDLLPath);
                }
                catch (Exception ex)
                {
                    LogManager.Log("Failed to download the latest version: " + ex.Message, "ERROR");
                    MessageBox.Show(ex.Message, "Failed to download the latest version", MessageBoxButton.OK, MessageBoxImage.Error);
                    sha256Local = "null";
                }
            }
            else
            {
                sha256Local = GetFileHash(YimDLLPath);
            }

            var hashes = new Hashes
            {
                WebHash = sha256Web,
                LocalHash = sha256Local
            };
            //MessageBox.Show($"Web Hash: {hashes.WebHash}\nLocal Hash: {hashes.LocalHash}", "Hashes", MessageBoxButton.OK, MessageBoxImage.Information);
            return hashes;
        }

        private static string GetFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
        }
    }
}
