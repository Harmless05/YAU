using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Octokit;
using YAU.Config;
using YAU.Injection;
using YAU.Logs;
using YAU.Lua;
using YAU.Updater;
using YAU.Views;
using static YAU.Config.ConfigManager;

namespace YAU
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private AppConfig _config;
        private UpdateManager _updateManager;
        private bool isDarkTheme = true;
        private string _injectionDelayText;
        private const string API_URL = "https://api.github.com/repos/YimMenu/YimMenu/releases/latest";
        private const string DLL_URL = "https://github.com/YimMenu/YimMenu/releases/download/nightly/YimMenu.dll";

        // Load the config into the UI
        public AppConfig Config{get => _config;set{_config = value;OnPropertyChanged();}}
        public string SelectedProcessName { get; set; }
        public Button InjectButton => InjectBtn;
        public Button SelectDLLButton => SelectDLLBtn;
        public ComboBox ProcessComboBox => SelectProcComBox;
        public TextBox SelectedDLLPathTextBox => SelectedDLLPathTB;
        public System.Windows.Controls.Label DLLInjectionStatusLabel => DLLInjectionStatusLbl;
        public string InjectionDelayText{get => _injectionDelayText;set{_injectionDelayText = value;OnPropertyChanged();}}
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        public MainWindow()
        {
            InitializeComponent();
            _config = ConfigManager.LoadConfig();
            DataContext = this;
            LogManager.Log("Application started", "INFO");

            // Check for YAU and YimMenu updates at startup
            CheckForUpdatesAtStartup();

            // Update the main window based on the config
            UpdateUIBasedOnConfig();

            // Fetch Lua scripts from the GitHub repository to Lua Scripts tab
            LoadLuaScripts();

            // Injection delay stuff
            InjectionDelayText = $"Injection delay: {_config.InjectionDelay}";
            InjectionDelaySlider.Value = _config.InjectionDelay;
        }

        //public async void RateLimitChecks()
        //{
        //    // Initialize the GitHub client
        //    var client = new GitHubClient(new ProductHeaderValue("YimMenu"));

        //    // Create & initialize the client here

        //    var miscellaneousRateLimit = await client.Miscellaneous.GetRateLimits();

        //    // The "core" object provides your rate limit status except for the Search API.
        //    var coreRateLimit = miscellaneousRateLimit.Resources.Core;

        //    var howManyCoreRequestsCanIMakePerHour = coreRateLimit.Limit;
        //    var howManyCoreRequestsDoIHaveLeft = coreRateLimit.Remaining;
        //    var whenDoesTheCoreLimitReset = coreRateLimit.Reset; // UTC time

        //    // The "search" object provides your rate limit status for the Search API.
        //    var searchRateLimit = miscellaneousRateLimit.Resources.Search;

        //    var howManySearchRequestsCanIMakePerMinute = searchRateLimit.Limit;
        //    var howManySearchRequestsDoIHaveLeft = searchRateLimit.Remaining;
        //    var whenDoesTheSearchLimitReset = searchRateLimit.Reset; // UTC time

        //    // If the rate limit has been reached, skip loading Lua scripts
        //    if (coreRateLimit.Remaining == 0)
        //    {
        //        MessageBox.Show("GitHub API rate limit reached. Please try again later.", "Rate Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
        //        return;
        //    }
        //}


        /// <summary>
        /// 
        /// Background Methods
        /// 
        /// </summary>

        // Check for YAU and YimMenu updates at startup
        private void CheckForUpdatesAtStartup()
        {
            LogManager.Log("Checking for updates at startup", "INFO");
            if (_config.CheckYAUUpdates)
            {
                _updateManager = new UpdateManager(this);

                _updateManager.CheckYAUAtStartup();
            }
            if (_config.CheckYimUpdates)
            {
                _updateManager = new UpdateManager(this);
                _updateManager.CheckYimAtStartup();
            }
        }

        // Update the main window based on the config
        private void UpdateUIBasedOnConfig()
        {
            LogManager.Log("Updating UI based on config", "INFO");

            // Hide the YAU folder button if Auto-Close YAU is not enabled
            bool isSelectProcess = _config.SelectProcess;
            InjectBtn.Margin = new Thickness(0, isSelectProcess ? 248 : 232, 0, 0);
            //DLLInjectionStatusLbl.Margin = new Thickness(0, isSelectProcess ? 200 : 232, 0, 0);
            settingsList.Margin = new Thickness(10, 10, 10, isSelectProcess ? 116 : 100);
            SelectProcComBox.Visibility = isSelectProcess ? Visibility.Visible : Visibility.Hidden;

            // Hide SelectDLL button if Custom DLL is not enabled
            bool isCustomDLL = _config.CustomDLL;
            InjectBtn.Margin = new Thickness(0, isCustomDLL ? 248 : 232, 0, 0);
            //DLLInjectionStatusLbl.Margin = new Thickness(0, isCustomDLL ? 200 : 232, 0, 0);
            settingsList.Margin = new Thickness(10, 10, 10, isCustomDLL ? 116 : 100);
            SelectDLLBtn.Visibility = isCustomDLL && !isSelectProcess ? Visibility.Visible : Visibility.Hidden;
        }

        // Fetch Lua scripts from the GitHub repository to Lua Scripts tab
        private async void LoadLuaScripts()
        {
            LogManager.Log("Fetching Lua scripts from GitHub", "INFO");
            var repos = await LuaManager.FetchReposAsync();
            string scriptsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YimMenu", "scripts");

            foreach (var repo in repos)
            {
                string scriptPath = Path.Combine(scriptsDirectory, repo.Name);
                repo.IsScriptPresent = File.Exists(scriptPath);

                if (repo.IsScriptPresent)
                {
                    string localHash = LuaManager.GetFileHash(scriptPath);
                    string webHash = await LuaManager.GetWebHashAsync(repo.HtmlUrl);
                }
            }
            LuaScriptsListBox.ItemsSource = repos;
        }

        /// <summary>
        /// 
        /// END of Background Methods
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// Dashboard tab
        /// 
        /// </summary>

        private void InjectBtn_Click(object sender, RoutedEventArgs e)
        {
            string yimMenuDLLPath = (string)ConfigManager.GetConfigValue("YimDLLPath");
            string selectedDLLPath = _config.CustomDLL ? SelectedDLLPathTB.Text : yimMenuDLLPath;
            string selectedProcess = _config.SelectProcess ? SelectedProcessName : null;

            // Check if the selected DLL path is empty
            if (string.IsNullOrEmpty(selectedDLLPath))
            {
                MessageBox.Show("Please select a DLL to inject.", "No DLL selected!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if the selected process is empty when SelectProcess is enabled
            if (_config.SelectProcess && string.IsNullOrEmpty(selectedProcess))
            {
                MessageBox.Show("Please select a process to inject the DLL into.", "No process selected!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if Auto-Start GTAV is enabled and no platform is selected
            if (_config.AutoStartGTAV && !(_config.PlatSteam || _config.PlatEpic || _config.PlatRockstar))
            {
                MessageBox.Show("Please select a platform to auto-start GTAV.", "No platform selected!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Call the InjectionManager to inject the DLL
            InjectionManager injectionManager = new InjectionManager(this);

            // If not selectprocess then call the platform checks with null
            if (!_config.SelectProcess && !_config.CustomDLL)
            {
                if (_config.AutoStartGTAV)
                {
                    injectionManager.PlatformChecks(null, selectedDLLPath);
                }
            }
            // If custom DLL and auto start GTAV is enabled but no process is selected
            else if (_config.CustomDLL && _config.AutoStartGTAV && !_config.SelectProcess)
            {
                injectionManager.PlatformChecks(selectedProcess, selectedDLLPath);
            }
            // If not auto start GTAV and custom DLL and select process is enabled
            else if (_config.CustomDLL && _config.SelectProcess)
            {
                injectionManager.DLLInjectionAsync(selectedProcess, selectedDLLPath);
            }
        }

        private void SelectDLLBtn_Click(object sender, RoutedEventArgs e)
        {
            // Open a file dialog to select a DLL
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Dynamic Link Libraries (*.dll)|*.dll",
                Title = "Select a DLL to inject"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Set the selected DLL path
                string selectedDLL = openFileDialog.FileName;
                // Set path to textbox (SelectedDLLPathTB)
                SelectedDLLPathTB.Text = selectedDLL;

                // Show DLL name in the button
                SelectDLLBtn.Content = Path.GetFileName(selectedDLL);
            }
            else
            {
                SelectDLLBtn.Content = "Select DLL";
            }

        }


        //Lists all the processes
        private void SelectProcComBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectProcComBox.SelectedItem is TextBlock selectedTextBlock)
            {
                // Extract the process name from the TextBlock
                string processName = selectedTextBlock.Inlines.OfType<Run>().FirstOrDefault()?.Text?.Split(' ')[0];
                if (!string.IsNullOrEmpty(processName))
                {
                    SelectedProcessName = processName;
                }
            }
        }


        private void SelectProcComBox_DropDownOpened(object sender, EventArgs e)
        {
            GetProcesses();
        }

        private void GetProcesses()
        {
            string lightForegroundColor = "#232323";
            string darkForegroundColor = "#dbdbdb";
            Brush lightForegroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(lightForegroundColor));
            Brush darkForegroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkForegroundColor));
            Brush idBrush = isDarkTheme ? Brushes.Khaki : Brushes.LightSalmon;

            // Loads all the processes with Name and PID to the combobox
            var processes = Process.GetProcesses()
                .OrderBy(p => p.ProcessName)
                .Select(p =>
                {
                    var textBlock = new TextBlock();
                    textBlock.Inlines.Add(new Run(p.ProcessName)
                    {
                        // Changes the foreground color based on the theme
                        Foreground = isDarkTheme ? darkForegroundBrush : lightForegroundBrush
                    });
                    textBlock.Inlines.Add(new Run(" [ "));
                    textBlock.Inlines.Add(new Run(p.Id.ToString())
                    {
                        Foreground = idBrush
                    });
                    textBlock.Inlines.Add(new Run(" ]"));
                    return textBlock;
                })
                .ToArray();

            SelectProcComBox.Items.Clear();
            foreach (var process in processes)
            {
                SelectProcComBox.Items.Add(process);
            }
        }

        // Show important enabled settings in the settingsList listbox
        private void UpdateSettingsList()
        {
            var settings = new List<string>();

            if (_config.SelectProcess)
                settings.Add("Process Selection: Enabled");

            if (_config.AutoStartGTAV)
            {
                settings.Add("Auto-Start GTAV: Enabled");

                if (_config.PlatSteam)
                    settings.Add("Platform: Steam");
                else if (_config.PlatEpic)
                    settings.Add("Platform: Epic Games");
                else if (_config.PlatRockstar)
                    settings.Add("Platform: Rockstar");
            }

            if (_config.CustomDLL)
                settings.Add("Use Custom DLL: Enabled");

            settings.Add($"Injection Delay: {_config.InjectionDelay} ms");

            if (_config.AutoCloseYAU)
                settings.Add("Auto-Close YAU: Enabled");

            if (_config.CheckYimUpdates)
                settings.Add("Automatic YimMenu Updates: Enabled");

            if (_config.CheckYAUUpdates)
                settings.Add("Automatic YAU Updates: Enabled");

            settingsList.ItemsSource = settings;
        }


        //private void InjectBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    string yimMenuDLLPath = (string)ConfigManager.GetConfigValue("YimDLLPath");
        //    string selectedDLLPath = _config.CustomDLL ? SelectedDLLPathTB.Text : yimMenuDLLPath;
        //    string selectedProcess = _config.SelectProcess ? SelectedProcessName : null;

        //    if (string.IsNullOrEmpty(selectedDLLPath))
        //    {
        //        MessageBox.Show("Please select a DLL to inject.", "No DLL selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }

        //    if (_config.SelectProcess && string.IsNullOrEmpty(selectedProcess))
        //    {
        //        MessageBox.Show("Please select a process to inject the DLL into.", "No process selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }

        //    if (_config.AutoStartGTAV && !(_config.PlatSteam || _config.PlatEpic || _config.PlatRockstar))
        //    {
        //        MessageBox.Show("Please select a platform to auto-start GTAV.", "No platform selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }

        //    // If not selectprocess then call the platform checks with null
        //    if (!_config.SelectProcess && !_config.CustomDLL)
        //    {
        //        //MessageBox.Show($"PROC: {selectedProcess}\nDLL PATH: {selectedDLLPath}", "asd", MessageBoxButton.OK, MessageBoxImage.Information);

        //        // Check if auto start and platform is selected
        //        if (_config.AutoStartGTAV && (_config.PlatSteam || _config.PlatEpic || _config.PlatRockstar))
        //        {
        //            // Call the InjectionManager to inject the DLL
        //            InjectionManager injectionManager = new InjectionManager(this);
        //            injectionManager.PlatformChecks(null, selectedDLLPath);
        //        }
        //        else if (_config.AutoStartGTAV && !_config.PlatSteam && !_config.PlatEpic && !_config.PlatRockstar)
        //        {
        //            MessageBox.Show("Please select a platform to auto-start GTAV.", "No platform selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }
        //    }
        //    else if (_config.CustomDLL && _config.AutoStartGTAV && !_config.SelectProcess)
        //    {
        //        if (string.IsNullOrEmpty(selectedDLLPath))
        //        {
        //            MessageBox.Show("Please select a DLL to inject.", "No DLL selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        // Check if auto start and platform is selected
        //        if (_config.AutoStartGTAV && (_config.PlatSteam || _config.PlatEpic || _config.PlatRockstar))
        //        {
        //            // Call the InjectionManager to inject the DLL
        //            InjectionManager injectionManager = new InjectionManager(this);
        //            injectionManager.PlatformChecks(selectedProcess, selectedDLLPath);
        //        }
        //        else if (_config.AutoStartGTAV && !_config.PlatSteam && !_config.PlatEpic && !_config.PlatRockstar)
        //        {
        //            MessageBox.Show("Please select a platform to auto-start GTAV.", "No platform is selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }
        //    }
        //    else if (!_config.AutoStartGTAV && _config.CustomDLL && _config.SelectProcess)
        //    {
        //        // Check if the selected DLL path is null
        //        if (string.IsNullOrEmpty(selectedDLLPath))
        //        {
        //            MessageBox.Show("Please select a DLL to inject.", "No DLL selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        // Check if the selected process is null
        //        if (selectedProcess == null)
        //        {
        //            MessageBox.Show("Please select a process to inject the DLL into.", "No process selected!", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        //MessageBox.Show($"PROC: {selectedProcess}\nDLL PATH: {selectedDLLPath}", "asd", MessageBoxButton.OK, MessageBoxImage.Information);

        //        // Call the InjectionManager to inject the DLL
        //        InjectionManager injectionManager = new InjectionManager(this);
        //        injectionManager.DLLInjectionAsync(selectedProcess, selectedDLLPath);
        //    }

        //    //MessageBox.Show(selectedProcess);
        //    //MessageBox.Show(selectedDLLPath);
        //}

        /// <summary>
        /// 
        /// END of Dashboard tab
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// Lua Script Tab Functions
        /// 
        /// </summary>
        private async void DownloadLuaBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string repoUrl && !string.IsNullOrEmpty(repoUrl))
            {
                var repoName = new Uri(repoUrl).Segments.Last().TrimEnd('/');
                var downloadUrl = $"{repoUrl}/archive/refs/heads/main.zip";
                string tempPath = Path.GetTempPath();
                string zipFilePath = Path.Combine(tempPath, $"{repoName}_main.zip");
                string extractDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YimMenu", "scripts");

                LogManager.Log($"Downloading and extracting {repoName} from {downloadUrl}", "INFO");
                try
                {
                    await LuaManager.DownloadAndExtractZipAsync(downloadUrl, zipFilePath, extractDirectory, repoName);
                    LogManager.Log("Download and extraction completed", "INFO");
                    MessageBox.Show("Download and extraction completed.");
                }
                catch (Exception ex)
                {
                    LogManager.Log(ex.Message, "ERROR");
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
            else
            {
                LogManager.Log("Failed to retrieve repository URL", "ERROR");
                MessageBox.Show("Failed to retrieve repository URL.");
            }
        }

        private void GitHubRepoBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                var repoUrl = button.Tag.ToString();
                var psi = new ProcessStartInfo
                {
                    FileName = repoUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
        }

        /// <summary>
        /// 
        /// END of Lua Script Tab Functions
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// Settings tab checkboxes
        /// 
        /// </summary>

        // Auto-Start GTAV with selected platform
        private void AutoStartCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.AutoStartGTAV = true);
            SelectProcCB.IsChecked = false;

            //if (CustomDLLCB.IsChecked == true)
            //{
            //    InjectBtn.Margin = new Thickness(0, 248, 0, 0);
            //    settingsList.Margin = new Thickness(10, 10, 10, 116);
            //}
        }

        private void AutoStartCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.AutoStartGTAV = false);
            PlatSteamCB.IsChecked = false;
            PlatEpicCB.IsChecked = false;
            PlatRockstarCB.IsChecked = false;
            SelectProcCB.IsChecked = true;
        }

        /// GTAV Platform checkboxes
        // Steam
        private void PlatSteamCB_Checked(object sender, RoutedEventArgs e)
        {
            //if (AutoStartCB.IsChecked == false)
            //{
            //    PlatSteamCB.IsChecked = false;
            //}
            //else
            //{
            //    UpdateConfigValue(_config => _config.PlatSteam = true);
            //    PlatEpicCB.IsChecked = false;
            //    PlatRockstarCB.IsChecked = false;
            //}
            UpdateConfigValue(_config => _config.PlatSteam = true);
            PlatEpicCB.IsChecked = false;
            PlatRockstarCB.IsChecked = false;

        }

        private void PlatSteamCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.PlatSteam = false);
        }

        //Epic Games
        private void PlatEpicCB_Checked(object sender, RoutedEventArgs e)
        {
            //if (AutoStartCB.IsChecked == false)
            //{
            //    PlatEpicCB.IsChecked = false;
            //}
            //else
            //{
            //    UpdateConfigValue(_config => _config.PlatEpic = true);
            //    PlatSteamCB.IsChecked = false;
            //    PlatRockstarCB.IsChecked = false;
            //}
            UpdateConfigValue(_config => _config.PlatEpic = true);
            PlatSteamCB.IsChecked = false;
            PlatRockstarCB.IsChecked = false;
        }

        private void PlatEpicCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.PlatEpic = false);
        }

        // Rockstar Games
        private void PlatRockstarCB_Checked(object sender, RoutedEventArgs e)
        {
            //if (AutoStartCB.IsChecked == false)
            //{
            //    PlatRockstarCB.IsChecked = false;
            //}
            //else
            //{
            //    UpdateConfigValue(_config => _config.PlatRockstar = true);
            //    PlatSteamCB.IsChecked = false;
            //    PlatEpicCB.IsChecked = false;
            //}
            UpdateConfigValue(_config => _config.PlatRockstar = true);
            PlatSteamCB.IsChecked = false;
            PlatEpicCB.IsChecked = false;
        }

        private void PlatRockstarCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.PlatRockstar = false);
        }

        /// YAU settings checkboxes
        // Auto-close YAU after successful injection
        private void AutoCloseCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.AutoCloseYAU = true);
        }

        private void AutoCloseCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.AutoCloseYAU = false);
        }

        // Process selection (Use a custom process to inject DLL into)
        private void SelectProcCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.SelectProcess = true);
            SelectProcComBox.Visibility = Visibility.Visible;

            AutoStartCB.IsChecked = false;
            PlatSteamCB.IsChecked = false;
            PlatEpicCB.IsChecked = false;
            PlatRockstarCB.IsChecked = false;

            InjectBtn.Margin = new Thickness(0, 248, 0, 0);
            settingsList.Margin = new Thickness(10, 10, 10, 116);
        }

        private void SelectProcCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.SelectProcess = false);
            SelectProcComBox.Visibility = Visibility.Hidden;

            AutoStartCB.IsChecked = true;
            if (CustomDLLCB.IsChecked == true)
            {
                InjectBtn.Margin = new Thickness(0, 248, 0, 0);
                settingsList.Margin = new Thickness(10, 10, 10, 116);
            }
            else
            {
                InjectBtn.Margin = new Thickness(0, 232, 0, 0);
                settingsList.Margin = new Thickness(10, 10, 10, 100);
            }
        }

        // Custom DLL (Use a custom DLL to inject)
        private void CustomDLLCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.CustomDLL = true);
            SelectDLLBtn.Visibility = Visibility.Visible;

            InjectBtn.Margin = new Thickness(0, 248, 0, 0);
            settingsList.Margin = new Thickness(10, 10, 10, 116);
        }

        private void CustomDLLCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.CustomDLL = false);
            SelectDLLBtn.Visibility = Visibility.Hidden;

            if (SelectProcCB.IsChecked == true)
            {
                InjectBtn.Margin = new Thickness(0, 248, 0, 0);
                settingsList.Margin = new Thickness(10, 10, 10, 116);
            }
            else
            {
                InjectBtn.Margin = new Thickness(0, 232, 0, 0);
                settingsList.Margin = new Thickness(10, 10, 10, 100);
            }
        }

        // Check for YimMenu updates
        private void AutoUpdateYimCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.CheckYimUpdates = true);
        }

        private void AutoUpdateYimCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.CheckYimUpdates = false);
        }

        // Check for YAU updates
        private void AutoUpdateYAUCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.CheckYAUUpdates = true);
        }

        private void AutoUpdateYAUCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.CheckYAUUpdates = false);
        }

        // Debug mode (Toggles some visual debug features)
        private void DebugCB_Checked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.Debug = true);

            SelectedDLLPathTB.Visibility = Visibility.Visible;
        }

        private void DebugCB_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateConfigValue(_config => _config.Debug = false);

            SelectedDLLPathTB.Visibility = Visibility.Hidden;
        }

        // Simplifies the code for updating config values
        private void UpdateConfigValue(Action<AppConfig> updateAction)
        {
            updateAction(_config);
            ConfigManager.SaveConfig(_config);
            UpdateSettingsList();
        }

        /// YAU Theme checkboxes
        // Dark theme
        private void DarkCB_Checked(object sender, RoutedEventArgs e)
        {
            isDarkTheme = true;
            LightCB.IsChecked = false;
            UpdateConfigValue(config => _config.DarkTheme = true);
            App.ApplyTheme(App.Theme.Dark);
        }

        private void DarkCB_Unchecked(object sender, RoutedEventArgs e)
        {
            isDarkTheme = false;
            LightCB.IsChecked = true;
            UpdateConfigValue(config => _config.DarkTheme = false);
        }

        // Light theme
        private void LightCB_Checked(object sender, RoutedEventArgs e)
        {
            isDarkTheme = false;
            DarkCB.IsChecked = false;
            UpdateConfigValue(config => _config.LightTheme = true);
            App.ApplyTheme(App.Theme.Light);
        }

        private void LightCB_Unchecked(object sender, RoutedEventArgs e)
        {
            isDarkTheme = true;
            DarkCB.IsChecked = true;
            UpdateConfigValue(config => _config.LightTheme = false);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_config == null)
            {
                LogManager.Log("Failed to set injection delay", "ERROR");
                return;
            }

            int newDelay = (int)InjectionDelaySlider.Value;
            if (_config.InjectionDelay != newDelay)
            {
                _config.InjectionDelay = newDelay;
                ConfigManager.SaveConfig(_config);
                InjectionDelayText = $"Injection delay: {newDelay}";
                UpdateSettingsList();
            }
        }

        /// <summary>
        /// 
        /// Settings tab buttons
        /// 
        /// </summary>

        private async void ChangeDLLSaveLoc_Click(object sender, RoutedEventArgs e)
        {
            // Open dialog window to select folder
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select DLL Save Location",
            };

            if (dialog.ShowDialog() == true)
            {
                // Set the selected DLL save location
                string selectedDLLSaveLocation = dialog.FolderName;
                string combinedPath = Path.Combine(selectedDLLSaveLocation, "YimMenu.dll");
                // Update the config with the new DLL save location
                _config.YimDLLPath = combinedPath;
                ConfigManager.SaveConfig(_config);

                ChangeDLLSaveLoc.Content = "Changed Path!";
                await Task.Delay(2000);
                ChangeDLLSaveLoc.Content = "Change DLL Path";
            }
        }

        // Opens the Yim folder in %appdata%\YimMenu
        private void YimFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            // Get the path to the Yim folder
            string yimFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YimMenu");

            // Check if the folder exists
            if (Directory.Exists(yimFolderPath))
            {
                try
                {
                    MessageBox.Show("To open the folder, you will need to run this application as an administrator.", "Do you understand?", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Open the folder in File Explorer Admin
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = yimFolderPath,
                        UseShellExecute = true,
                        Verb = "runas" // This will prompt for elevation
                    };
                    Process.Start(startInfo);
                }
                catch (Win32Exception ex)
                {
                    LogManager.Log(ex.Message, "ERROR");
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("The specified folder does not exist.");
            }
        }

        // Opens the YAU folder in %appdata%\YAU
        private void YAUFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            // Get the path to the YAU folder
            string YAUFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YAU");

            // Check if the folder exists
            if (Directory.Exists(YAUFolderPath))
            {
                try
                {
                    MessageBox.Show("To open the folder, you will need to run this application as an administrator.", "Do you understand?", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Open the folder in File Explorer Admin
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = YAUFolderPath,
                        UseShellExecute = true,
                        Verb = "runas" // This will prompt for elevation
                    };
                    Process.Start(startInfo);
                }
                catch (Win32Exception ex)
                {
                    LogManager.Log(ex.Message, "ERROR");
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("The specified folder does not exist.");
            }
        }

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            // Clear YimMenu cache folder in %appdata%
            string cacheFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YimMenu", "cache");
            if (Directory.Exists(cacheFolderPath))
            {
                Directory.Delete(cacheFolderPath, true);
                MessageBox.Show("Cache cleared successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("The cache folder does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Show the About window
        private void AboutViewBtn_Click(object sender, RoutedEventArgs e)
        {
            // Open the About window
            About about = new About();
            about.Show();
        }

        // Button to kill GTAV process
        private void KillGTAVBtn_Click(object sender, RoutedEventArgs e)
        {
            // Check if GTAV is running
            if (Process.GetProcessesByName("GTA5").Length == 0)
            {
                MessageBox.Show("GTAV is not running.", "Not running", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Kill/stop the GTAV process
            Process[] processes = Process.GetProcessesByName("GTA5");
            foreach (Process process in processes)
            {
                process.Kill();
            }
        }

        // Restart the application
        private void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            // Get the path to the current executable
            string executablePath = Process.GetCurrentProcess().MainModule.FileName;

            // Start a new instance of the application
            Process.Start(executablePath);

            // Shutdown the current application
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// 
        /// END of Settings tab buttons
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// END of Settings tab checkboxes
        /// 
        /// </summary>

        public class Repository
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string HtmlUrl { get; set; }
            public bool IsScriptPresent { get; set; }
            public bool IsUpdateAvailable { get; set; }
        }

        public class Hashes
        {
            public string WebHash { get; set; }
            public string LocalHash { get; set; }
        }
    }
}