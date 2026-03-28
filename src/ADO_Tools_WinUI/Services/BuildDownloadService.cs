using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace ADO_Tools.Services
{
    /// <summary>
    /// Orchestrates downloading and extracting build artifacts from Azure DevOps.
    /// Raises events for status/progress reporting and exposes callbacks for UI interaction.
    /// </summary>
    public class BuildDownloadService
    {
        private readonly TfsRestClient _client;

        public event Action<string>? StatusUpdated;
        public event Action<double>? ProgressUpdated;

        /// <summary>
        /// Callback for yes/no confirmation dialogs. The UI layer should set this.
        /// </summary>
        public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

        public BuildDownloadService(TfsRestClient client)
        {
            _client = client;
        }

        private void UpdateStatus(string message) => StatusUpdated?.Invoke(message);

        /// <summary>
        /// Downloads and extracts build artifacts for the given build.
        /// </summary>
        public async Task DownloadAndExtractArtifactsAsync(
            string buildId,
            string downloadFolder,
            string extractFolder,
            InstallFunctions installFunctions,
            CancellationToken cancellationToken = default)
        {
            var artifacts = await _client.GetBuildArtifactsAsync(buildId);

            if (artifacts == null || artifacts.Count == 0)
            {
                UpdateStatus("No build artifacts were found for the selected build.");
                return;
            }

            foreach (var artifact in artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string name = artifact["name"]?.ToString() ?? "";
                if (name != "InstallerExternalPayloads" && name != "product") continue;

                string downloadUrl = artifact["resource"]?["downloadUrl"]?.ToString() ?? "";
                string zipPath = Path.Combine(downloadFolder, $"{name}.zip");

                bool shouldDownload = true;

                var remoteSize = await _client.GetArtifactSizeAsync(buildId, name);

                // Check if file exists and compare sizes. Only perform the size check for files larger than 100MB
                if (File.Exists(zipPath) && remoteSize.HasValue && remoteSize.Value > 100 * 1024 * 1024)
                {
                    bool isZipValid = true;
                    try
                    {
                        using (var archive = ZipFile.OpenRead(zipPath))
                        {
                            foreach (var entry in archive.Entries) { /* validate entries */ }
                        }
                    }
                    catch
                    {
                        isZipValid = false;
                        shouldDownload = true;
                        UpdateStatus($"Existing file '{name}.zip' is corrupted or not a valid zip. It will be re-downloaded.");
                    }

                    double localSize = new FileInfo(zipPath).Length;
                    double remote = remoteSize.Value;
                    double lowerBound = remote * 0.8;
                    double upperBound = remote * 1.2;
                    double sizeratio = remote / localSize;
                    string percentString = $"{Math.Round((sizeratio - 1) * 100, 2)}%";
                    string localSizeMB = $"{(localSize / (1024 * 1024)):N2} MB";
                    string remoteSizeMB = $"{(remote / (1024 * 1024)):N2} MB";

                    if (localSize >= lowerBound && localSize <= upperBound && isZipValid)
                    {
                        var message =
                            $"'{name}.zip' already exists on disk (and not corrupted).\n\n" +
                            $"Local size: {localSizeMB} \n" +
                            $"Server size: {remoteSizeMB} (within {percentString})\n\n" +
                            "Note: Small size differences are normal.\n\n" +
                            "Do you want to overwrite the file and download it again?";

                        bool userConfirmed = ConfirmAsync != null
                            && await ConfirmAsync(message, "Confirm Overwrite");

                        if (!userConfirmed)
                        {
                            UpdateStatus($"Skipped downloading '{name}.zip' (user declined overwrite).");
                            shouldDownload = false;
                        }
                    }
                }

                if (shouldDownload)
                {
                    await DownloadWithProgressAsync(downloadUrl, zipPath, remoteSize, cancellationToken);
                }

                installFunctions.ExtractZipToDirectory(zipPath, extractFolder);
            }
        }

        /// <summary>
        /// Downloads a file in chunks with progress reporting and retry logic.
        /// </summary>
        private async Task DownloadWithProgressAsync(string downloadUrl, string outputPath, long? remoteSize, CancellationToken cancellationToken)
        {
            int chunkSize = 1024 * 1024;
            int maxRetries = 5;
            int attempt = 0;
            bool success = false;

            string remoteSizeMB = $"{(remoteSize / (1024 * 1024)):N2} MB";

            UpdateStatus($"Starting download: {outputPath}");

            while (attempt < maxRetries && !success)
            {
                try
                {
                    using (var response = await _client.GetStreamAsync(downloadUrl))
                    {
                        response.EnsureSuccessStatusCode();

                        long totalBytes = remoteSize ?? response.Content.Headers.ContentLength ?? -1;
                        long downloadedBytes = 0;
                        long logThreshold = 10 * 1024 * 1024; // Log every 10 MB
                        long nextLogPoint = logThreshold;
                        DateTime lastLogTime = DateTime.UtcNow;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[chunkSize];
                            int bytesRead;
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                downloadedBytes += bytesRead;

                                // Log progress every 10 MB
                                if (downloadedBytes >= nextLogPoint)
                                {
                                    DateTime currentTime = DateTime.UtcNow;
                                    double elapsedSeconds = (currentTime - lastLogTime).TotalSeconds;
                                    lastLogTime = currentTime;

                                    double downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                                    double speedMBps = logThreshold / (1024.0 * 1024.0) / elapsedSeconds;

                                    double percentage = remoteSize.HasValue && remoteSize.Value > 0
                                        ? ((double)downloadedBytes / remoteSize.Value) * 100
                                        : 0;

                                    ProgressUpdated?.Invoke(percentage);
                                    UpdateStatus($"Downloaded: {downloadedMB:F2} MB of ~{remoteSizeMB} at {speedMBps:F2} MB/s");
                                    nextLogPoint += logThreshold;
                                }
                            }
                        }
                        success = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("Download cancelled by user.");
                    if (File.Exists(outputPath))
                    {
                        try { File.Delete(outputPath); } catch { /* best effort */ }
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    attempt++;
                    UpdateStatus($"Attempt {attempt} failed: {ex.Message}");
                    if (attempt >= maxRetries)
                    {
                        UpdateStatus("Max retries reached. Download failed.");
                    }
                }
            }
        }
    }
}