using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using YAU.Config;
using YAU.Logs;
using static YAU.Config.ConfigManager;

namespace YAU.Injection
{
    public class InjectionManager
    {
        private readonly MainWindow _mainWindow;
        private AppConfig _config;

        public InjectionManager(MainWindow mainWindow)
        {
            // Set elements ready for injection in main window
            _mainWindow = mainWindow;
            _mainWindow.SelectDLLButton.Visibility = Visibility.Hidden;
            _mainWindow.ProcessComboBox.Visibility = Visibility.Hidden;
            _mainWindow.DLLInjectionStatusLabel.Visibility = Visibility.Visible;
            _mainWindow.InjectBtn.Opacity = 0.5;
            _mainWindow.InjectButton.IsEnabled = false;
        }

        public class DLLImport
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
            public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

            [DllImport("kernel32.dll")]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll")]
            public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
        }

        public async Task DLLInjectionAsync(string customProc, string customDLL)
        {

            // Check if a custom process is being used, if not wait for GTA5.exe to start
            if (string.IsNullOrEmpty(customProc))
            {
                while (Process.GetProcessesByName("GTA5").Length == 0)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        _mainWindow.DLLInjectionStatusLabel.Content = $"Waiting for GTA5.exe to start{new string('.', i)}";
                        await Task.Delay(1000);
                    }
                }
            }

            // Change status label width to fit the text
            _mainWindow.DLLInjectionStatusLabel.Width = 250;

            // Wait for the injection delay
            int injectionDelay = (int)ConfigManager.GetConfigValue("InjectionDelay");
            for (int i = injectionDelay; i >= 0; i -= 1000)
            {
                _mainWindow.DLLInjectionStatusLabel.Content = $"Waiting {i / 1000} seconds before injecting...";
                await Task.Delay(1000);
            }
            _mainWindow.DLLInjectionStatusLabel.Width = 220;


            /// Injection process

            byte[] buffer;

            IntPtr procHandle;

            string YimDLLPath = (string)ConfigManager.GetConfigValue("YimDLLPath");

            if ((bool)ConfigManager.GetConfigValue("SelectProcess"))
            {
                procHandle = GetProcessHandle(customProc);
                buffer = Encoding.ASCII.GetBytes(YimDLLPath);
            }
            else if ((bool)ConfigManager.GetConfigValue("CustomDLL") && (bool)ConfigManager.GetConfigValue("SelectProcess"))
            {
                procHandle = GetProcessHandle(customProc);
                buffer = Encoding.ASCII.GetBytes(customDLL);
            }
            else
            {
                procHandle = GetProcessHandle("GTA5");
                if ((bool)ConfigManager.GetConfigValue("CustomDLL"))
                {
                    buffer = Encoding.ASCII.GetBytes(customDLL);
                }
                else
                {
                    buffer = Encoding.ASCII.GetBytes(YimDLLPath);
                }
            }

            IntPtr loadLibAddr = DLLImport.GetProcAddress(DLLImport.GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            IntPtr allocMemAddress = DLLImport.VirtualAllocEx(procHandle, IntPtr.Zero, 0x1000, 0x3000, 0x40);

            await InjectDLLAsync(procHandle, loadLibAddr, allocMemAddress, buffer);
        }

        // Get the process handle
        private IntPtr GetProcessHandle(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                int procId = processes[0].Id;
                return DLLImport.OpenProcess(0x1F0FFF, false, procId);
            }
            else
            {
                throw new Exception($"Process '{processName}' not found.");
            }
        }

        // Inject the DLL
        private async Task InjectDLLAsync(IntPtr procHandle, IntPtr loadLibAddr, IntPtr allocMemAddress, byte[] buffer)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            // Write the DLL path to the allocated memory
            DLLImport.WriteProcessMemory(procHandle, allocMemAddress, buffer, (uint)buffer.Length, out int bytesWritten);
            // Create a remote thread
            IntPtr createThreadResult = DLLImport.CreateRemoteThread(procHandle, IntPtr.Zero, 0, loadLibAddr, allocMemAddress, 0, IntPtr.Zero);

            // Check if the injection was successful
            if (createThreadResult == IntPtr.Zero)
            {
                _mainWindow.DLLInjectionStatusLabel.Content = "Injection failed!";
                _mainWindow.InjectButton.Content = "Failed";
                await Task.Delay(4000);
                _mainWindow.InjectButton.Content = "Inject";
                _mainWindow.InjectBtn.Opacity = 1;
                _mainWindow.InjectButton.IsEnabled = true;
                // Close the handle to the process
                DLLImport.CloseHandle(procHandle);
                // "Clear" the status text
                _mainWindow.DLLInjectionStatusLabel.Content = "";
                if ((bool)ConfigManager.GetConfigValue("SelectProcess"))
                {
                    _mainWindow.SelectDLLButton.Visibility = Visibility.Visible;
                }
                if ((bool)ConfigManager.GetConfigValue("CustomDLL"))
                {
                    _mainWindow.ProcessComboBox.Visibility = Visibility.Visible;
                }
                _mainWindow.DLLInjectionStatusLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                _mainWindow.DLLInjectionStatusLabel.Content = "Injection successful";
                _mainWindow.InjectButton.Content = "Successful";
                await Task.Delay(2000);
                _mainWindow.InjectButton.Content = "Inject";
                _mainWindow.InjectBtn.Opacity = 1;
                _mainWindow.InjectButton.IsEnabled = true;
                // Close the handle to the process
                DLLImport.CloseHandle(procHandle);
                // Close the script after injection checking if AutoCloseYAU is enabled
                if ((bool)ConfigManager.GetConfigValue("AutoCloseYAU"))
                {
                    for (int i = 10000; i >= 0; i -= 1000)
                    {
                        _mainWindow.DLLInjectionStatusLabel.Content = $"Closing in {i / 1000} seconds...";
                        await Task.Delay(1000);
                    }
                    mainWindow.Close();
                }
                else
                {
                    if ((bool)ConfigManager.GetConfigValue("SelectProcess"))
                    {
                        _mainWindow.SelectDLLButton.Visibility = Visibility.Visible;
                    }
                    if ((bool)ConfigManager.GetConfigValue("CustomDLL"))
                    {
                        _mainWindow.ProcessComboBox.Visibility = Visibility.Visible;
                    }
                    _mainWindow.DLLInjectionStatusLabel.Content = "";
                    _mainWindow.DLLInjectionStatusLabel.Visibility = Visibility.Hidden;
                }
            }
        }


        /// GTAV Platform checks before injecting the DLL
        public void PlatformChecks(string customProc, string customDLL)
        {
            LogManager.Log("Checking platform...", "INFO");

            bool autoStart = (bool)ConfigManager.GetConfigValue("AutoStartGTAV");
            // Get the platform and convert it to a int value for easier use
            string platform = null;
            if ((bool)ConfigManager.GetConfigValue("PlatSteam"))
            {
                platform = "1";
            }
            else if ((bool)ConfigManager.GetConfigValue("PlatEpic"))
            {
                platform = "2";
            }
            else if ((bool)ConfigManager.GetConfigValue("PlatRockstar"))
            {
                platform = "3";
            }
            else
            {
                platform = "4";
            }

            if (autoStart && platform != "4")
            {
                if (platform == "1") // Steam
                {
                    LogManager.Log("Starting Steam...", "INFO");
                    _mainWindow.DLLInjectionStatusLabel.Content = "Starting Steam...";
                    string uri = "steam://run/271590";
                    Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                    DLLInjectionAsync(customProc, customDLL);
                }
                else if (platform == "2") // Epic Games
                {
                    LogManager.Log("Starting Epic Games...", "INFO");
                    _mainWindow.DLLInjectionStatusLabel.Content = "Starting Epic Games...";
                    string uri = "com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true";
                    Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                    DLLInjectionAsync(customProc, customDLL);
                }
                else if (platform == "3") // Rockstar Games Launcher
                {
                    LogManager.Log("Starting Rockstar Games...", "INFO");
                    _mainWindow.DLLInjectionStatusLabel.Content = "Starting Rockstar Games...";
                    string[] keys = {
                            @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV",
                            @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
                            @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
                            @"SOFTWARE\Rockstar Games\GTAV"
                        };

                    string selfGTAVDirPath = null;
                    foreach (string key in keys)
                    {
                        selfGTAVDirPath = GetRegistryValue(key, "InstallFolder");
                        if (!string.IsNullOrEmpty(selfGTAVDirPath)) break;
                    }

                    if (string.IsNullOrEmpty(selfGTAVDirPath))
                    {
                        string[] keysSteam = {
                                @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV",
                                @"SOFTWARE\Rockstar Games\GTAV",
                                @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
                                @"SOFTWARE\Rockstar Games\Grand Theft Auto V"
                            };

                        foreach (string key in keysSteam)
                        {
                            selfGTAVDirPath = GetRegistryValue(key, "InstallFolderSteam");
                            if (!string.IsNullOrEmpty(selfGTAVDirPath)) break;
                        }
                    }

                    if (!string.IsNullOrEmpty(selfGTAVDirPath))
                    {
                        Process.Start(new ProcessStartInfo($"{selfGTAVDirPath}\\PlayGTAV.exe") { UseShellExecute = true });
                        DLLInjectionAsync(customProc, customDLL);
                    }
                }
            }
            else if (autoStart && platform == "4") // Auto start is enabled but no platform is selected
            {
                LogManager.Log("Auto start is enabled but no platform is selected", "WARNING");
                MessageBox.Show("Please select a platform or disable auto start!", "Auto start is enabled but no platform is selected", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Get the registry value from the specified key
        private string GetRegistryValue(string key, string valueName)
        {
            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(key))
            {
                return registryKey?.GetValue(valueName)?.ToString();
            }
        }
    }
}
