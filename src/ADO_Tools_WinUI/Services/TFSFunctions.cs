using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Threading;

namespace ADO_Tools.Services
{
    // Lightweight, SDK-free TFSFunctions shim. Use TfsRestClient directly for real REST operations.
    public class TFSFunctions
    {
        public event Action<string> StatusUpdated;
        public event Action<double> ProgressUpdated;

        public void UpdateStatus(string message)
        {
            StatusUpdated?.Invoke(message);
        }

        /// <summary>
        /// Callback for yes/no confirmation dialogs. The UI layer should set this.
        /// Parameters: (message, title) → returns true if user clicked Yes.
        /// </summary>
        public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

        /// <summary>
        /// Callback for showing error/warning messages. The UI layer should set this.
        /// Parameters: (message, title).
        /// </summary>
        public Func<string, string, Task>? ShowMessageAsync { get; set; }

        bool IsADOProject;

        public TFSFunctions()
        {
        }

        // Minimal stub to keep compatibility with existing call sites. Prefer using TfsRestClient directly.
        public object ConnectTFS(string URIName, string projectName)
        {
            // This method previously returned a Microsoft.TeamFoundation.Project.
            // With REST usage, connect logic should be handled by TfsRestClient in the UI code.
            return null;
        }

        public List<object> ReadQuarries(object project)
        {
            // Previously returned List<QueryItem>. Use TfsRestClient.GetQueriesAsync instead.
            return new List<object>();
        }

        public List<object> ReadItems(string wiql)
        {
            // Previously returned List<WorkItem>. Use TfsRestClient.QueryWorkItemsAsync instead.
            return new List<object>();
        }

        public object ReadSingleItem(int workItemID)
        {
            return null;
        }

        public void DownloadWorkItems(object workItem, string topFolder)
        {
            // Previously downloaded attachments via SDK. Use TfsRestClient.DownloadAttachmentAsync for REST.
        }

        public string calculateAttachmentSize(object workItem)
        {
            return "0 B";
        }

        public void changeValue(object workItem, string field, string value)
        {
            // No-op in shim.
        }

        // Keep the existing async helpers intact (they do not depend on SDK types)


        // Main method to download and extract build artifacts. Uses REST API calls and HttpClient.
        public async Task DownloadLatestBuildArtifacts(
            string organization,
            string project,
            string buildId,
            string downloadFolder,
            string extractFolder,
            string personalAccessToken,
            InstallFunctions installFunctions,
            CancellationToken cancellationToken = default)
        {
            // Get artifact metadata
            string baseUrl = $"https://dev.azure.com/{organization}/{project}/_apis";
            string artifactsUrl = $"{baseUrl}/build/builds/{buildId}/artifacts?api-version=7.1";


            using (HttpClient client = new HttpClient())
            {
                var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{personalAccessToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                HttpResponseMessage response = await client.GetAsync(artifactsUrl);
                response.EnsureSuccessStatusCode();
                var artifactsJson = JObject.Parse(await response.Content.ReadAsStringAsync());

                if (artifactsJson["value"] is JArray valueArray && valueArray.Count == 0)
                {
                    UpdateStatus("No build artifacts were found for the selected build.");
                    return; // Exit the method early if no artifacts
                }

                foreach (var artifact in artifactsJson["value"])
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string name = artifact["name"].ToString();
                    if (name != "InstallerExternalPayloads" && name != "product") continue;

                    string downloadUrl = artifact["resource"]["downloadUrl"].ToString();
                    string zipPath = Path.Combine(downloadFolder, $"{name}.zip");

                    bool shouldDownload = true;

                    string metadataUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}/artifacts?artifactName={artifact["name"]}&api-version=7.1";
                    var remoteSize = await GetRemoteArtifactSizeAsync(metadataUrl, personalAccessToken);



                    // Check if file exists and compare sizes. Only perform the size check for files larger than 100MB
                    if (File.Exists(zipPath) && remoteSize.HasValue && remoteSize.Value > 100 * 1024 * 1024)
                    {
                        bool isZipValid = true;
                        {
                            try
                            {
                                using (var archive = ZipFile.OpenRead(zipPath))
                                {
                                    // Optionally, enumerate entries to force validation
                                    foreach (var entry in archive.Entries) { /* no-op */ }
                                }
                            }
                            catch
                            {
                                isZipValid = false;
                                shouldDownload = true; // Force re-download if zip is corrupted
                                UpdateStatus($"Existing file '{name}.zip' is corrupted or not a valid zip. It will be re-downloaded.");
                            }
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
                            // Report to user and ask for confirmation

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
                        // Download the artifact in chunks with progress reporting and retry logic
                        await DownloadZipFileInChunks(downloadUrl, zipPath, personalAccessToken, remoteSize, cancellationToken);
                    }
                    //Kivanc
                    installFunctions.ExtractZipToDirectory(zipPath, extractFolder);
                }
            }
        }

        public async Task DownloadZipFileInChunks(string downloadUrl, string outputPath, string personalAccessToken, long? remoteSize = null, CancellationToken cancellationToken = default)
        {
            int chunkSize = 1024 * 1024;
            int maxRetries = 5;

            var client = new HttpClient();
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            int attempt = 0;
            bool success = false;

            string remoteSizeMB = $"{(remoteSize / (1024 * 1024)):N2} MB";

            UpdateStatus($"Starting download: {outputPath}");

            while (attempt < maxRetries && !success)
            {
                try
                {
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        long totalBytes = remoteSize ?? response.Content.Headers.ContentLength ?? -1;
                        long downloadedBytes = 0;
                        long logThreshold = 10 * 1024 * 1024; // Log every 10 MB
                        long nextLogPoint = logThreshold;

                        DateTime lastLogTime = DateTime.UtcNow;
                        double lastReportedPercentage = -1; // Keep track of the last percentage we spammed to the UI

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[chunkSize];
                            int bytesRead;
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                downloadedBytes += bytesRead;

                                //if (remoteSize.HasValue && remoteSize.Value > 0)
                                //{
                                //    double percentage = ((double)downloadedBytes / remoteSize.Value) * 100;
                                //    // Throttle the progress updates to the UI to max ~1000 updates rather than millions
                                //    // Only invoke if percentage grew by at least 0.1%
                                //    if (percentage - lastReportedPercentage >= 0.1 || downloadedBytes == remoteSize.Value)
                                //    {
                                //        ProgressUpdated?.Invoke(percentage);
                                //        lastReportedPercentage = percentage;
                                //    }
                                //}

                                // Log progress every 10 MB
                                if (downloadedBytes >= nextLogPoint)
                                {
                                    DateTime currentTime = DateTime.UtcNow;
                                    double elapsedSeconds = (currentTime - lastLogTime).TotalSeconds;
                                    lastLogTime = currentTime;

                                    double downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                                    double speedMBps = logThreshold / (1024.0 * 1024.0) / elapsedSeconds;

                                    double percentage = ((double)downloadedBytes / remoteSize.Value) * 100;
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
                    // Clean up partial file
                    if (File.Exists(outputPath))
                    {
                        try { File.Delete(outputPath); } catch { /* best effort */ }
                    }
                    throw; // Re-throw so the caller knows it was cancelled
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


        public async Task<long?> GetRemoteArtifactSizeAsync(string artifactMetadataUrl, string personalAccessToken)
        {
            using (var client = new HttpClient())
            {
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                var response = await client.GetAsync(artifactMetadataUrl);
                if (!response.IsSuccessStatusCode)
                    return null;

                var jsonString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonString);

                var artifact = json; // The response is a single artifact object, not an array
                var properties = artifact["resource"]?["properties"] as JObject;
                var sizeToken = properties?["artifactsize"];
                if (sizeToken != null && long.TryParse(sizeToken.ToString(), out var size))
                    return size;
            }
            return null;
        }








        // Implement GetAvailableBuildsAsync so SoftwareDownloaderForm can use it (REST-based)
        public async Task<List<BuildInfo>> GetAvailableBuildsAsync(string organization, string project, int definitionId, string personalAccessToken, int top)
        {
            var builds = new List<BuildInfo>();
            string baseUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds?definitions={definitionId}&$top={top}&api-version=7.1";

            using (HttpClient client = new HttpClient())
            {
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(baseUrl);

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        if (ShowMessageAsync != null)
                        {
                            await ShowMessageAsync(
                                "Authentication failed. Please check your Personal Access Token.",
                                "Authentication Error");
                        }

                        return builds; // Return empty list and stop execution
                    }

                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request error: {ex.Message}");
                    return builds;
                }

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());

                foreach (var build in json["value"])
                {
                    string buildId = build["id"]?.ToString();
                    string productName = build["definition"]?["name"]?.ToString();
                    string buildNumber = build["buildNumber"]?.ToString();
                    string result = build["result"]?.ToString();

                    string finishTimeRaw = build["finishTime"]?.ToString();
                    string finishTime = "";
                    if (!string.IsNullOrEmpty(finishTimeRaw) && DateTime.TryParse(finishTimeRaw, out var dt))
                    {
                        finishTime = dt.ToString("dd/MM/yyyy HH:mm");
                    }
                    else
                    {
                        finishTime = finishTimeRaw; // fallback to original if parsing fails
                    }

                    //if (!string.IsNullOrEmpty(buildId) && !string.IsNullOrEmpty(buildNumber) &&
                    //    !string.IsNullOrEmpty(productName) && result == "succeeded")
                    if (!string.IsNullOrEmpty(buildId) && !string.IsNullOrEmpty(buildNumber))
                    {
                        var versionParts = (buildNumber ?? "0.0.0.0").Split('.');
                        int major = versionParts.Length > 0 ? int.Parse(versionParts[0]) : 0;
                        int majorSeq = versionParts.Length > 1 ? int.Parse(versionParts[1]) : 0;
                        int minor = versionParts.Length > 2 ? int.Parse(versionParts[2]) : 0;
                        int builditeration = versionParts.Length > 3 ? int.Parse(versionParts[3]) : 0;

                        builds.Add(new BuildInfo
                        {
                            BuildId = buildId,
                            ProductName = productName,
                            Result = result,
                            FinishTime = finishTime,

                            DisplayVersion = buildNumber,
                            MajorVersion = major,
                            MajorVersionSequence = majorSeq,
                            MinorVersion = minor,
                            MinorVersionIteration = builditeration
                        });
                    }
                }
            }

            return builds;
        }

        public class BuildInfo
        {
            public string BuildId { get; set; }
            public string ProductName { get; set; }
            public string Result { get; set; }
            public string FinishTime { get; set; }

            public string DisplayVersion { get; set; }
            public int MajorVersion { get; set; }
            public int MajorVersionSequence { get; set; }
            public int MinorVersion { get; set; }
            public int MinorVersionIteration { get; set; }
        }
    }
}
