using Octokit;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using YAU.Logs;

namespace YAU.Lua
{
    public class LuaManager
    {
        private static readonly List<string> BlacklistRepo = new List<string> { "submission", "Example", "SAMURAI-Scenarios", "SAMURAI-Animations", "TokyoDrift" };
        private static readonly List<string> WhitelistedRepos = new List<string>
            {
                "https://api.github.com/repos/Deadlineem/Extras-Addon-for-YimMenu"
            };
        private static readonly List<string> WhitelistedFileTypes = new List<string> { ".lua", ".bat" };

        public static async Task<List<Repository>> FetchReposAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                var gitHubClient = new GitHubClient(new ProductHeaderValue("YimMenu"));
                var repos = await gitHubClient.Repository.GetAllForOrg("YimMenu-Lua");

                var result = new List<Repository>();
                var installedScripts = GetInstalledScripts();

                foreach (var repo in repos)
                {
                    if (BlacklistRepo.Contains(repo.Name))
                    {
                        continue;
                    }

                    var isScriptPresent = installedScripts.Contains(repo.Name);

                    result.Add(new Repository
                    {
                        Name = repo.Name,
                        Description = repo.Description,
                        HtmlUrl = repo.HtmlUrl,
                        IsScriptPresent = isScriptPresent,
                        IsUpdateAvailable = false
                    });
                }

                // Add whitelisted repos
                foreach (var repoUrl in WhitelistedRepos)
                {
                    var repoUri = new Uri(repoUrl);
                    var repoName = repoUri.Segments.Last();
                    var repoOwner = repoUri.Segments[repoUri.Segments.Length - 2].TrimEnd('/');
                    var repo = await gitHubClient.Repository.Get(repoOwner, repoName);

                    var isScriptPresent = installedScripts.Contains(repo.Name);

                    result.Add(new Repository
                    {
                        Name = repo.Name,
                        Description = repo.Description,
                        HtmlUrl = repo.HtmlUrl,
                        IsScriptPresent = isScriptPresent,
                        IsUpdateAvailable = false
                    });
                }

                return result;
            }
        }

        public static async Task DownloadAndExtractZipAsync(string url, string zipFilePath, string extractDirectory, string scriptName)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }

            //MessageBox.Show(url);
            //MessageBox.Show(zipFilePath);
            //MessageBox.Show(extractDirectory);

            try
            {
                // Download the ZIP file
                try
                {
                    using (var client = new HttpClient())
                    {
                        var data = await client.GetByteArrayAsync(url);
                        File.WriteAllBytes(zipFilePath, data);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Log(ex.Message, "ERROR");
                    MessageBox.Show(ex.Message);
                }

                // Verify the file size to ensure it is not empty or corrupted
                if (new FileInfo(zipFilePath).Length == 0)
                {
                    throw new InvalidDataException("Downloaded ZIP file is empty or corrupted.");
                }

                using (var fileStream = new FileStream(zipFilePath, System.IO.FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(extractDirectory);
                    }
                }

                File.Delete(zipFilePath);

                string extractedFolderPath = Path.Combine(extractDirectory, scriptName);

                // Rename all directories with the "-main" suffix
                RenameDirectory(extractDirectory, scriptName);

                // Remove non-whitelisted files recursively
                RemoveNonWhitelistedFiles(extractedFolderPath);

                // Remove empty directories
                RemoveEmptyDirectories(extractedFolderPath);
            }
            catch (Exception ex)
            {
                LogManager.Log(ex.Message, "ERROR");
                MessageBox.Show(ex.Message, "Failed to download and extract the script", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        // Rename the extracted folder to remove the "-main" suffix
        private static void RenameDirectory(string rootDirectory, string scriptName)
        {
            var directories = Directory.GetDirectories(rootDirectory, $"*{scriptName}-main", SearchOption.AllDirectories);
            foreach (var directory in directories)
            {
                var newDirectoryName = directory.Replace($"{scriptName}-main", scriptName);
                if (newDirectoryName != directory)
                {
                    Directory.Move(directory, newDirectoryName);
                }
            }
        }

        // Remove non-whitelisted files recursively from the extracted folder
        private static void RemoveNonWhitelistedFiles(string rootDirectory)
        {
            var files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (!WhitelistedFileTypes.Contains(Path.GetExtension(file)))
                {
                    File.Delete(file);
                }
            }
        }

        // Remove empty directories from the extracted folder
        private static void RemoveEmptyDirectories(string rootDirectory)
        {
            bool directoriesRemoved;
            do
            {
                directoriesRemoved = false;
                var emptyDirectories = Directory.GetDirectories(rootDirectory, "*", SearchOption.AllDirectories)
                    .Where(directory => !Directory.EnumerateFileSystemEntries(directory).Any())
                    .ToList();

                foreach (var directory in emptyDirectories)
                {
                    // Ensure the directory is within the rootDirectory
                    if (directory.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.Delete(directory);
                        directoriesRemoved = true;
                    }
                }
            } while (directoriesRemoved);
        }

        public static string GetFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public static async Task<string> GetWebHashAsync(string url)
        {
            using (var client = new HttpClient())
            {
                var data = await client.GetByteArrayAsync(url);
                using (var sha256 = SHA256.Create())
                {
                    return BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }

    public class Repository
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string HtmlUrl { get; set; }
        public bool IsScriptPresent { get; set; }
        public bool IsUpdateAvailable { get; set; }
    }
}
