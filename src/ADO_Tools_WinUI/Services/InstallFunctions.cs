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
using Microsoft.UI.Xaml;
using Microsoft.VisualBasic.FileIO;

namespace ADO_Tools_WinUI.Services
{
    /// <summary>
    /// Provides functionality for installing, uninstalling, and managing Bentley software products.
    /// Handles MSI-based installer operations, registry queries for installed software,
    /// ZIP extraction, and clean uninstall with leftover file/folder cleanup.
    /// </summary>
    public class InstallFunctions : IStatusReporter
    {
        // Timer that periodically checks whether the Windows Installer (msiexec) is still running
        private DispatcherTimer installerCheckTimer;

        // Counts how many 5-second intervals have passed since the installer started
        private int installerProgressTicks = 0;

        // Tracks the previous tick's installer state to detect when the installer finishes
        private bool lastInstallerRunning = false;

        // Label describing the current operation (e.g. "Windows Installer" or "Windows Uninstaller")
        private string currentOperationLabel = "Windows Installer";

        /// <summary>Raised whenever a status message should be displayed to the user (log line).</summary>
        public event Action<string>? StatusUpdated;

        /// <summary>Convenience method to raise <see cref="StatusUpdated"/> with the given message.</summary>
        public void UpdateStatus(string message)
        {
            StatusUpdated?.Invoke(message);
        }

        /// <summary>
        /// Initializes the installer check timer that polls every 5 seconds
        /// to determine whether a Windows Installer (MSI) operation is in progress.
        /// </summary>
        public InstallFunctions()
        {
            installerCheckTimer = new DispatcherTimer();
            installerCheckTimer.Interval = TimeSpan.FromSeconds(5);
            installerCheckTimer.Tick += InstallerCheckTimer_Tick!;
        }

        /// <summary>
        /// Starts polling for Windows Installer activity. Resets elapsed time counters
        /// and begins raising <see cref="InstallerRunningChanged"/> on each tick.
        /// </summary>
        /// <param name="operationLabel">A friendly label shown in status messages (e.g. "Windows Uninstaller").</param>
        public void StartInstallerTimer(string operationLabel = "Windows Installer")
        {
            installerProgressTicks = 0;
            lastInstallerRunning = false;
            currentOperationLabel = operationLabel;
            installerCheckTimer.Start();
        }

        /// <summary>Stops the installer polling timer and resets tracking state.</summary>
        public void StopInstallerTimer()
        {
            installerCheckTimer.Stop();
            installerProgressTicks = 0;
            lastInstallerRunning = false;
        }

        /// <summary>
        /// Fires when the MSI installer state changes.
        /// Parameters: (bool isRunning, int elapsedSeconds, string operationLabel)
        /// </summary>
        public event Action<bool, int, string>? InstallerRunningChanged;

        /// <summary>
        /// Called every 5 seconds while the timer is active.
        /// Checks if the MSI engine is running and reports progress or completion.
        /// </summary>
        private void InstallerCheckTimer_Tick(object sender, object e)
        {
            bool installerRunning = IsInstallerRunning();

            if (installerRunning)
            {
                // Installer is still active – increment counter and notify listeners
                installerProgressTicks++;
                int elapsed = installerProgressTicks * 5;

                string progressLine = $"{currentOperationLabel} is running. Please wait… ({elapsed}s elapsed)";
                StatusUpdated?.Invoke(progressLine);
                InstallerRunningChanged?.Invoke(true, elapsed, currentOperationLabel);
            }
            else if (lastInstallerRunning)
            {
                // Installer was running on the previous tick but has now stopped – it just finished
                StopInstallerTimer();
                StatusUpdated?.Invoke($"{currentOperationLabel} finished.");
                InstallerRunningChanged?.Invoke(false, 0, currentOperationLabel);
            }

            lastInstallerRunning = installerRunning;
        }

        /// <summary>
        /// Queries the Windows registry (both 32-bit and 64-bit uninstall keys) to find
        /// all Bentley-published software installed on the machine.
        /// Returns the list sorted alphabetically by display name.
        /// </summary>
        public List<InstalledSoftwareInfo> GetInstalledBentleySoftware()
        {
            var result = new List<InstalledSoftwareInfo>();
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var keyPath in registryKeys)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) continue;
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using (var subkey = key.OpenSubKey(subkeyName))
                        {
                            // Read relevant registry values for each installed program
                            var _QuietUninstallString = subkey?.GetValue("QuietUninstallString") as string;
                            var _publisher = subkey?.GetValue("Publisher") as string;
                            var _displayName = subkey?.GetValue("DisplayName") as string;
                            var _displayVersion = subkey?.GetValue("DisplayVersion") as string;

                            // Only include entries published by Bentley that have a quiet uninstall command
                            if (!string.IsNullOrEmpty(_QuietUninstallString) && !string.IsNullOrEmpty(_publisher) && _publisher.Contains("Bentley"))
                            {
                                var (major, majorSeq, minor, builditeration) = VersionParser.Parse(_displayVersion);

                                result.Add(new InstalledSoftwareInfo
                                {
                                    DisplayName = _displayName ?? "",
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

        /// <summary>
        /// Runs the quiet uninstall command for the specified Bentley product.
        /// Optionally performs a clean uninstall (removes leftover folders) on success.
        /// </summary>
        /// <param name="matchingInstalledBuild">The software entry to uninstall.</param>
        /// <param name="cleanUninstall">If true, removes residual folders after a successful uninstall.</param>
        /// <returns>True if the uninstall completed successfully; false otherwise.</returns>
        public async Task<bool> UninstallSoftwareAsync(InstalledSoftwareInfo matchingInstalledBuild, bool cleanUninstall)
        {
            if (IsInstallerRunning())
            {
                UpdateStatus("Windows Installer already running. Uninstall process aborted!");
                return false;
            }

            string uninstallString = matchingInstalledBuild.QuietUninstallString;
            bool success = false;

            UpdateStatus($"Starting uninstall: {uninstallString}");

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
                    StartInstallerTimer("Windows Uninstaller");

                    // Read stdout/stderr asynchronously to avoid deadlocks
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode == 0)
                    {
                        UpdateStatus("Uninstall completed successfully.");
                        success = true;
                        if (!string.IsNullOrWhiteSpace(output))
                            UpdateStatus("Output: " + output.Trim());
                    }
                    else
                    {
                        // Translate the MSI exit code to a human-readable message
                        UpdateStatus(GetHumanReadableExitCode(process.ExitCode));
                        if (!string.IsNullOrWhiteSpace(error))
                            UpdateStatus("Error: " + error.Trim());
                        UpdateStatus("Uninstall failed or was cancelled.");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Exception during uninstall: " + ex.Message);
            }

            // If the standard uninstall succeeded, optionally remove leftover folders
            if (success && cleanUninstall)
            {
                CleanUninstallBentleyProduct(matchingInstalledBuild);
            }

            StopInstallerTimer();
            return success;
        }

        /// <summary>
        /// Launches the setup executable in quiet mode and waits for it to complete.
        /// Reports progress via the installer timer and status events.
        /// </summary>
        /// <param name="setupFilePath">Full path to the setup .exe file.</param>
        public async Task InstallSoftwareAsync(string setupFilePath)
        {
            if (IsInstallerRunning())
            {
                UpdateStatus("Windows Installer already running. Install process aborted!");
                return;
            }

            UpdateStatus($"Starting installation: {setupFilePath}");
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
                    StartInstallerTimer("Windows Installer");

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode == 0)
                    {
                        UpdateStatus("Installation completed successfully.");
                        if (!string.IsNullOrWhiteSpace(output))
                            UpdateStatus("Output: " + output.Trim());
                    }
                    else
                    {
                        UpdateStatus(GetHumanReadableExitCode(process.ExitCode));
                        if (!string.IsNullOrWhiteSpace(error))
                            UpdateStatus("Error: " + error.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during installation: {ex.Message}");
            }
            StopInstallerTimer();
        }

        /// <summary>
        /// Removes residual folders left behind after a Bentley product has been uninstalled.
        /// Sends leftover directories to the Recycle Bin rather than permanently deleting them.
        /// Checks the registry first to confirm the product is actually uninstalled.
        /// </summary>
        /// <param name="matchingInstalledBuild">The product whose leftovers should be cleaned up.</param>
        public void CleanUninstallBentleyProduct(InstalledSoftwareInfo matchingInstalledBuild)
        {
            if (matchingInstalledBuild == null)
            {
                UpdateStatus("Clean Uninstall: No matching installed version found.");
                return;
            }

            // Safety check: verify the product was actually removed before deleting folders
            var stillInstalled = GetInstalledBentleySoftware()
                .Any(s => s.DisplayName == matchingInstalledBuild.DisplayName
                        && s.DisplayVersion == matchingInstalledBuild.DisplayVersion);

            if (stillInstalled)
            {
                UpdateStatus($"Clean Uninstall aborted: {matchingInstalledBuild.DisplayName} is still installed.");
                return;
            }

            UpdateStatus($"Clean Uninstall: Recycling remaining folders for {matchingInstalledBuild.DisplayName}...");

            string userName = Environment.UserName;
            var DisplayName = matchingInstalledBuild.DisplayName;

            // Strip digits and whitespace from the product name to form a normalized key
            // e.g. "OpenRail Designer 23" → "OpenRailDesigner"
            string normalizedProductName = Regex.Replace(DisplayName, @"[\d\s]", "");

            var MajorVersion = matchingInstalledBuild.MajorVersion;
            var MajorVersionSequence = matchingInstalledBuild.MajorVersionSequence;

            // Define the directories and glob patterns where Bentley products leave residual files
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
                        UpdateStatus($"Skipped (not found): {directory}");
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
                            UpdateStatus("Error: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus("Error: " + ex.Message);
                }
            }

            UpdateStatus("Clean uninstall process completed.");
        }

        /// <summary>
        /// Extracts all files from a ZIP archive into the specified directory,
        /// overwriting existing files. Reports per-file extraction progress via <see cref="StatusUpdated"/>.
        /// </summary>
        /// <param name="zipFilePath">Full path to the ZIP file.</param>
        /// <param name="extractPath">Destination directory (created if it doesn't exist).</param>
        public void ExtractZipToDirectory(string zipFilePath, string extractPath)
        {
            Directory.CreateDirectory(extractPath);
            UpdateStatus($"Extracting: {Path.GetFileName(zipFilePath)}");

            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedCount = 0;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

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

            UpdateStatus($"Extraction complete: {extractPath}");
        }

        /// <summary>
        /// Translates common MSI / process exit codes into user-friendly messages.
        /// </summary>
        private string GetHumanReadableExitCode(int exitCode)
        {
            return exitCode switch
            {
                0 => "Operation completed successfully.",
                unchecked((int)0x80070002) => "File not found or user cancelled the operation.",
                1 => "General error.",
                2 => "File not found.",
                3 => "Path not found.",
                5 => "Access denied. Administrator privileges may be required.",
                1602 => "User cancelled the operation.",
                1603 => "Fatal error during operation.",
                1618 => "Another installation is already in progress.",
                _ => $"Unknown error (exit code: {exitCode})."
            };
        }

        /// <summary>
        /// Checks whether the Windows Installer engine (msiexec) is currently running
        /// by attempting to open the global MSI mutex. If the mutex exists, an MSI
        /// operation is in progress.
        /// </summary>
        /// <returns>True if Windows Installer is currently executing an operation.</returns>
        public static bool IsInstallerRunning()
        {
            try
            {
                // The "Global\_MSIExecute" mutex is held by msiexec while an install/uninstall is active
                using var mutex = Mutex.OpenExisting(@"Global\_MSIExecute");
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Mutex doesn't exist – no MSI operation in progress
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex exists but we can't access it – installer is running under a different context
                return true;
            }
        }

        /// <summary>
        /// Represents a Bentley software product discovered in the Windows registry.
        /// Version components follow the Bentley convention: Major.MajorSequence.Minor.Iteration
        /// (e.g. 23.09.02.015).
        /// </summary>
        public class InstalledSoftwareInfo
        {
            /// <summary>Display name as shown in "Programs and Features" (e.g. "OpenRail Designer").</summary>
            public string DisplayName { get; set; } = "";

            /// <summary>Full version string (e.g. "23.09.02.015").</summary>
            public string DisplayVersion { get; set; } = "";

            /// <summary>Command line used to silently uninstall the product.</summary>
            public string QuietUninstallString { get; set; } = "";

            /// <summary>First component of the version (e.g. 23).</summary>
            public int MajorVersion { get; set; }

            /// <summary>Second component – major version sequence (e.g. 09).</summary>
            public int MajorVersionSequence { get; set; }

            /// <summary>Third component – minor version (e.g. 02).</summary>
            public int MinorVersion { get; set; }

            /// <summary>Fourth component – build iteration (e.g. 015).</summary>
            public int MinorVersionIteration { get; set; }
        }
    }
}