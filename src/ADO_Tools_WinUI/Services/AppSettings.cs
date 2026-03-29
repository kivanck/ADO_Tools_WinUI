using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ADO_Tools_WinUI.Services
{
    public class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ADO_Tools_WinUI",
            "settings.json");

        private static AppSettings? _default;
        public static AppSettings Default => _default ??= Load();
        public string RootFolder { get; set; } = "";
        public string Organization { get; set; } = "bentleycs";
        public string Project { get; set; } = "civil";
        public string WorkItemProject { get; set; } = "civil";
        public string SelectedQueryPath { get; set; } = "";
        public string DefinitionId { get; set; } = "6098";
        public string PersonalAccessToken { get; set; } = "";
        public string DownloadFolder { get; set; } = "";
        public List<string> UserDefinitionList { get; set; } = new()
        {
            "OpenRail Designer|6098|civil",
            "OpenRoads Designer|6057|civil",
            "Overhead Line Designer|6289|civil",
            "Microstation|5311|PowerPlatform",
            "OpenBridge Designer|7391|civil"
        };
        public int BuildCount { get; set; } = 30;
        public string ProductName { get; set; } = "OpenRail Designer";
        public string SearchAreaPath { get; set; } = @"Civil\OpenCivil Designer";
        public string SearchCutoffDate { get; set; } = "2020-01-01";

        /// <summary>Legacy property — migrated to SearchColumns on first load.</summary>
        public List<string> SearchResultColumns { get; set; } = new();

        // ?? Column picker: visible columns per mode ??

        public List<string> QueryColumns { get; set; } = new();
        public List<string> SearchColumns { get; set; } = new()
        {
            "System.Id", "System.Title", "System.State",
            "System.AreaPath", "Microsoft.VSTS.Common.Priority",
            "Microsoft.VSTS.Common.Severity", "System.Tags",
            "System.CreatedBy", "System.CreatedDate",
            "System.WorkItemType", "System.IterationPath"
        };
        public List<string> HiddenQueryColumns { get; set; } = new();

        // ?? Column widths per mode ??

        public Dictionary<string, double> QueryColumnWidths { get; set; } = new();
        public Dictionary<string, double> SearchColumnWidths { get; set; } = new();

        // ?? Window dimensions ??

        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 970;
        public bool IsMaximized { get; set; }

        // ?? Search preferences ??

        public bool ExcludeDone { get; set; } = true;
        public List<string> BacklogSearchHistory { get; set; } = new();
        public List<string> QuerySearchHistory { get; set; } = new();

        private const int MaxSearchHistory = 10;

        public void AddBacklogSearchHistory(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            BacklogSearchHistory.Remove(query);
            BacklogSearchHistory.Insert(0, query);
            if (BacklogSearchHistory.Count > MaxSearchHistory)
                BacklogSearchHistory.RemoveRange(MaxSearchHistory, BacklogSearchHistory.Count - MaxSearchHistory);
        }

        public void AddQuerySearchHistory(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            QuerySearchHistory.Remove(query);
            QuerySearchHistory.Insert(0, query);
            if (QuerySearchHistory.Count > MaxSearchHistory)
                QuerySearchHistory.RemoveRange(MaxSearchHistory, QuerySearchHistory.Count - MaxSearchHistory);
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }

        private static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                    // One-time migration: SearchResultColumns ? SearchColumns
                    if (settings.SearchResultColumns.Count > 0 && settings.SearchColumns.Count == 0)
                    {
                        settings.SearchColumns = new List<string>(settings.SearchResultColumns);
                        settings.SearchResultColumns.Clear();
                        settings.Save();
                    }

                    return settings;
                }
                catch
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }
    }
}