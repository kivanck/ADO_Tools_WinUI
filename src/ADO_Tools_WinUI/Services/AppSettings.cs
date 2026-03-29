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
        public List<string> SearchResultColumns { get; set; } = new()
        {
            "System.Id", "System.Title", "System.State",
            "System.AreaPath", "Microsoft.VSTS.Common.Priority",
            "Microsoft.VSTS.Common.Severity", "System.Tags",
            "System.CreatedBy", "System.CreatedDate",
            "System.WorkItemType", "System.IterationPath"
        };

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
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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