using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.UI.Xaml;
//using static System.Runtime.InteropServices.JavaScript.JSType;
//using System.Windows.Forms;
//using Timer = System.Windows.Forms.Timer;

namespace ADO_Tools.Services
{
  


    public class InstallFunctions
    {
        private DispatcherTimer installerCheckTimer;
        private int installerProgressTicks = 0;
        private int maxAsterisks = 10;
        private bool lastInstallerRunning = false;

        // Event for status updates (already present)
        public event Action<string> StatusUpdated;
        public void UpdateStatus(string message)
        {
            StatusUpdated?.Invoke(message);
        }
        public InstallFunctions()
        {
            installerCheckTimer = new DispatcherTimer();
            installerCheckTimer.Interval = TimeSpan.FromSeconds(5); // 5 seconds
            installerCheckTimer.Tick += InstallerCheckTimer_Tick;
        }

        public void StartInstallerTimer()
        {
            installerProgressTicks = 0;
            lastInstallerRunning = false;
            installerCheckTimer.Start();
        }

        public void StopInstallerTimer()
        {
            installerCheckTimer.Stop();
            installerProgressTicks = 0;
            lastInstallerRunning = false;
        }

        private void InstallerCheckTimer_Tick(object sender, object e)
        {
            bool installerRunning = IsInstallerRunning();

            if (installerRunning)
            {
                installerProgressTicks++;
                if (installerProgressTicks > maxAsterisks) installerProgressTicks = 1;

                string progressLine = "Windows Installer is running. Please wait..." + new string('*', installerProgressTicks);
                StatusUpdated?.Invoke(progressLine);
            }
            else if (lastInstallerRunning)
            {
                // Only stop the timer and reset when installer just finished
                StopInstallerTimer();
                StatusUpdated?.Invoke("Windows Installer finished.");
            }

            lastInstallerRunning = installerRunning;
        }

        

         // Registry query for installed software
        public List<InstalledSoftwareInfo> GetInstalledBentleySoftware()
        {
            var result = new List<InstalledSoftwareInfo>();
            string[] registryKeys = {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

            foreach (var keyPath in registryKeys)
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) continue;
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using (var subkey = key.OpenSubKey(subkeyName))
                        {
                            var _QuietUninstallString = subkey?.GetValue("QuietUninstallString") as string;
                            var _publisher = subkey?.GetValue("Publisher") as string;
                            var _displayName = subkey?.GetValue("DisplayName") as string;
                            var _displayVersion = subkey?.GetValue("DisplayVersion") as string;
                            if (!string.IsNullOrEmpty(_QuietUninstallString) && !string.IsNullOrEmpty(_publisher) && _publisher.Contains("Bentley"))
                            {
                                var versionParts = (_displayVersion ?? "0.0.0.0").Split('.');
                                int major = versionParts.Length > 0 ? int.Parse(versionParts[0]) : 0;
                                int majorSeq = versionParts.Length > 1 ? int.Parse(versionParts[1]) : 0;
                                int minor = versionParts.Length > 2 ? int.Parse(versionParts[2]) : 0;
                                int builditeration = versionParts.Length > 3 ? int.Parse(versionParts[3]) : 0;

                                result.Add(new InstalledSoftwareInfo
                                {
                                    DisplayName = _displayName,
                                    DisplayVersion = _displayVersion ?? "Unknown",
                                    QuietUninstallString = _QuietUninstallString,
                                    MajorVersion = major,
                                    MajorVersionSequence = majorSeq,
                                    MinorVersion = minor,
                                    MinorVersionIteration = builditeration
                                });
                            }
                        }
                    }
                }
            }
            return result.OrderBy(s => s.DisplayName ?? string.Empty).ToList();
        }



        public async Task UninstallMatchingSoftwareAsync(InstalledSoftwareInfo matchingInstalledBuild, string productName, bool cleanUninstall)
        {
            if (matchingInstalledBuild == null)
            {
                UpdateStatus($"No matching installed version found for {productName}. Uninstall skipped.");
                return;
            }

            string uninstallString = matchingInstalledBuild.QuietUninstallString;
            string version = matchingInstalledBuild.DisplayVersion;
            string majorMinorBuild = string.Join(".", version.Split('.').Take(3));

            UpdateStatus($"Searching for installed version {majorMinorBuild}.* ...");

            bool uninstallOk = await UninstallSoftwareAsync(matchingInstalledBuild, cleanUninstall);

            
        }


        public async Task<bool> UninstallSoftwareAsync(InstalledSoftwareInfo matchingInstalledBuild, bool cleanUninstall)
        {
            bool installerRunning = IsInstallerRunning();

            if (installerRunning)
            {
                UpdateStatus("Windows Installer already running. Uninstall process aborted!");
                return false;
            }


            string uninstallString = matchingInstalledBuild.QuietUninstallString;
            bool success = false;

            //string uninstallString = FindQuietUninstallString(productName, version);
            UpdateStatus($"Starting uninstall process with command: {uninstallString}");


            

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + uninstallString,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    StartInstallerTimer();

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode == 0)
                    {
                        UpdateStatus("Uninstall process completed successfully.");
                        success = true;
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            UpdateStatus("Output: " + output.Trim());
                        }

                    }
                    else
                    {
                        
                        UpdateStatus(GetHumanReadableExitCode(process.ExitCode));
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            UpdateStatus("Error Output: " + error.Trim());
                        }
                        UpdateStatus("Uninstall failed or was cancelled. Uninstall process aborted!");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Exception during uninstall: " + ex.Message);
            }



            if (success && cleanUninstall)
            {
                CleanUninstallBentleyProduct(matchingInstalledBuild);
            }



            StopInstallerTimer();
            return success;
        }





        public async Task InstallSoftwareAsync(string setupFilePath)
        {
            bool installerRunning = IsInstallerRunning();

            if (installerRunning)
            {
                UpdateStatus("Windows Installer already running. Install process aborted!");
                return;
            }

            UpdateStatus($"Starting installation from: {setupFilePath}");
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = setupFilePath,
                    Arguments = "/quiet",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Verb = "runas"
                };
                using (Process process = new Process { StartInfo = processInfo })
                {
                    process.Start();
                    StartInstallerTimer();

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode == 0)
                    {
                        UpdateStatus("Installation process completed successfully.");
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            UpdateStatus("Output: " + output.Trim());
                        }
                    }
                    else
                    {
                        UpdateStatus($"Installation process exited with code {process.ExitCode}.");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            UpdateStatus("Error Output: " + error.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during installation: {ex.Message}");
            }
            StopInstallerTimer();
        }




        public void CleanUninstallBentleyProduct(InstalledSoftwareInfo matchingInstalledBuild)
        {
            if (matchingInstalledBuild == null)
            {
                UpdateStatus("Clean Uninstall: No matching installed version found for clean uninstall.");
                return;
            }

            // Safety check: verify the software is actually uninstalled before deleting folders
            var stillInstalled = GetInstalledBentleySoftware()
                .Any(s => s.DisplayName == matchingInstalledBuild.DisplayName
                        && s.DisplayVersion == matchingInstalledBuild.DisplayVersion);

            if (stillInstalled)
            {
                UpdateStatus($"Clean Uninstall aborted: {matchingInstalledBuild.DisplayName} is still installed.");
                return;
            }

            UpdateStatus($"Clean Uninstall started. Recycling remaining folders for {matchingInstalledBuild.DisplayName}...");

            string userName = Environment.UserName;

            var DisplayName = matchingInstalledBuild.DisplayName; //"OpenRail Designer 2025"
            string normalizedProductName = Regex.Replace(DisplayName, @"[\d\s]", ""); //"OpenRailDesigner"

            var DisplayVersion = matchingInstalledBuild.DisplayVersion;
            var MajorVersion = matchingInstalledBuild.MajorVersion;
            var MajorVersionSequence = matchingInstalledBuild.MajorVersionSequence;
            var MinorVersion = matchingInstalledBuild.MinorVersion;
            var MinorRelease = matchingInstalledBuild.MinorRelease;
            var MinorVersionIteration = matchingInstalledBuild.MinorVersionIteration;

            // Folder patterns (directory, searchPattern) to match and delete
            var folderPatterns = new List<(string Directory, string SearchPattern)>
            {
                (@"C:\Program Files\Bentley", $"*{DisplayName}*"),
                (@"C:\ProgramData\Bentley", $"*{DisplayName}*{MajorVersionSequence}*"),
                ($@"C:\Users\{userName}\AppData\Local\Bentley\{normalizedProductName}", $"*{MajorVersion}*"),
                ($@"C:\Users\{userName}\AppData\Local\Temp\Bentley\{normalizedProductName}", $"*{MajorVersion}*"),
                ($@"C:\Users\{userName}\AppData\Roaming\Bentley", $"*{normalizedProductName}*"),
            };

            foreach (var (directory, searchPattern) in folderPatterns)
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        UpdateStatus($"Skipped (directory not found): {directory}");
                        continue;
                    }

                    var matches = Directory.GetDirectories(directory, searchPattern);
                    foreach (var folder in matches)
                    {
                        try
                        {
                            FileSystem.DeleteDirectory(
                                folder,
                                UIOption.OnlyErrorDialogs,
                                RecycleOption.SendToRecycleBin
                            );
                            UpdateStatus("Removed: " + folder);
                        }
                        catch (Exception ex)
                        {
                            UpdateStatus("Error Output: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus("Error Output: " + ex.Message);
                }
            }

            // Registry keys
            ////try
            ////{
            ////    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Bentley\{normalizedProductName}", false);
            ////    Registry.LocalMachine.DeleteSubKeyTree($@"SOFTWARE\Bentley\{normalizedProductName}", false);
            ////}
            ////catch { /* log or ignore */ }

            UpdateStatus("Clean uninstall process completed.");
        }


        public string ExtractVersionFromSetupFile(string setupFileName)
        {
            var match = Regex.Match(setupFileName, @"(\d{2}\.\d{2}\.\d{2}\.\d{3})");
            return match.Success ? match.Value : null;
        }


        private string FindQuietUninstallString(string productName, string version)
        {
            const string uninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";


            //convert version name from format "25.00.01" to ""25.0.1" as this is how it is stored in registry
            version = string.Join(".", version
                .Split('.')
                .Select(part => int.Parse(part).ToString()));


            // Extract only the first three parts of the version
            string majorMinorBuild = string.Join(".", version.Split('.').Take(3));

            using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(uninstallKey))
            {
                if (rk == null) return null;

                foreach (string subKeyName in rk.GetSubKeyNames())
                {
                    using (RegistryKey subKey = rk.OpenSubKey(subKeyName))
                    {
                        string uninstallString = subKey?.GetValue("UninstallString") as string; //Check uninstallstring to make sure product name is correct
                        string ver = subKey?.GetValue("DisplayVersion") as string;


                        if (!string.IsNullOrEmpty(uninstallString) &&
                            uninstallString.IndexOf(productName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !string.IsNullOrEmpty(ver) && ver.StartsWith(majorMinorBuild))
                        {
                            return subKey.GetValue("QuietUninstallString") as string;
                        }
                    }
                }
            }

            return null;
        }



        // Helper class
        public class InstalledSoftwareInfo
        {
            public string DisplayName { get; set; }
            public string DisplayVersion { get; set; }
            public string QuietUninstallString { get; set; }
            public int MajorVersion { get; set; }
            public int MajorVersionSequence { get; set; }
            public int MinorVersion { get; set; }
            public int MinorRelease { get; set; }
            public int MinorVersionIteration { get; set; }
        }





        //Extracts the contents of the zip files, in the subfolders to a single extractPath folder
        public void ExtractZipToDirectory(string zipFilePath, string extractPath)
        {
            // Ensure the extract path exists
            Directory.CreateDirectory(extractPath);

            UpdateStatus($"Starting extraction of ZIP file: {Path.GetFileName(zipFilePath)}");

            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedCount = 0;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories

                    string destinationPath = Path.Combine(extractPath, entry.Name);

                    try
                    {
                        entry.ExtractToFile(destinationPath, true);
                        extractedCount++;

                        UpdateStatus($"Extracted {entry.Name} ({extractedCount}/{totalEntries})");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Failed to extract {entry.Name}: {ex.Message}");
                    }
                }
            }

            UpdateStatus($"Extraction complete. Files extracted to: {extractPath}");
        }

        private void UninstallSoftwareOLD(string uninstallCommand)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + uninstallCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    UpdateStatus("Uninstall process completed.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Error during uninstall: " + ex.Message);
            }
        }



        private void UninstallSoftware(string uninstallCommand)
        {
            UpdateStatus($"Starting uninstall process with command: {uninstallCommand}");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + uninstallCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        UpdateStatus("Uninstall process completed successfully.");
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            UpdateStatus("Output: " + output.Trim());
                        }
                    }
                    else
                    {
                        UpdateStatus($"Uninstall process exited with code {process.ExitCode}.");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            UpdateStatus("Error Output: " + error.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Exception during uninstall: " + ex.Message);
            }
        }


        private void InstallSoftwareOLD(string setupFilePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = setupFilePath,
                Arguments = "/quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            });//
        }


        private void InstallSoftware(string setupFilePath)
        {
            UpdateStatus($"Starting installation from: {setupFilePath}");

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = setupFilePath,
                    Arguments = "/quiet",
                    UseShellExecute = false, // Enables UI
                    Verb = "runas" // Prompts for admin rights
                };





                Process process = Process.Start(processInfo);

                if (process != null)
                {
                    UpdateStatus("Installation process started successfully.");
                }
                else
                {
                    UpdateStatus("Failed to start the installation process.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during installation: {ex.Message}");
            }
        }


        private string GetHumanReadableExitCode(int exitCode)
        {
            switch (exitCode)
            {
                case 0:
                    return "Operation completed successfully.";
                case -2147023294: // 0x80070002
                case unchecked((int)0x80070002):
                    return "The system cannot find the file specified. This may occur if the user cancels the operation or the required file is missing.";
                case 1:
                    return "Operation failed. General error.";
                case 2:
                    return "File not found.";
                case 3:
                    return "Path not found.";
                case 5:
                    return "Access denied. You may need administrator privileges.";
                case 1602: // MSI installer
                    return "User cancelled the installation or uninstallation.";
                case 1603: // MSI installer
                    return "Fatal error during installation or uninstallation.";
                case 1618: // MSI installer
                    return "Another installation is already in progress.";
                default:
                    return $"Unknown error (exit code: {exitCode}).";
            }
        }

        public static bool IsInstallerRunning()
        {
            // The Windows Installer holds the "Global\_MSIExecute" mutex
            // while any MSI installation is in progress.
            try
            {
                using var mutex = Mutex.OpenExisting(@"Global\_MSIExecute");
                // If we reach here, the mutex exists — an install is running.
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Mutex does not exist — no MSI install is running.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex exists but we can't open it — an install is running.
                return true;
            }
        }
      
    }


}















