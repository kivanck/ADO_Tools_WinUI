using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADO_Tools.Models;

namespace ADO_Tools_WinUI.Services
{
    public class EmbeddingCache
    {
        private readonly string _cacheDir;
        private readonly string _cacheFilePath;
        private Dictionary<int, EmbeddingCacheEntry> _entries = new();

        public DateTime LastUpdatedUtc { get; private set; } = DateTime.MinValue;

        public int Count => _entries.Count;

        public EmbeddingCache(string organization, string project, string cacheDir)
        {
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
            string safeName = $"{SanitizeFileName(organization)}_{SanitizeFileName(project)}";
            _cacheFilePath = Path.Combine(_cacheDir, $"{safeName}.json");
        }

        public bool TryLoad()
        {
            if (!File.Exists(_cacheFilePath))
                return false;

            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var wrapper = JsonSerializer.Deserialize<CacheFileWrapper>(json);
                if (wrapper?.Entries == null)
                    return false;

                _entries = wrapper.Entries.ToDictionary(e => e.WorkItemId);
                LastUpdatedUtc = wrapper.LastUpdatedUtc;
                return _entries.Count > 0;
            }
            catch
            {
                _entries.Clear();
                return false;
            }
        }

        public async Task SaveAsync()
        {
            var wrapper = new CacheFileWrapper
            {
                LastUpdatedUtc = LastUpdatedUtc,
                Entries = _entries.Values.ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = false };
            var json = JsonSerializer.Serialize(wrapper, options);
            await File.WriteAllTextAsync(_cacheFilePath, json);
        }

        public List<EmbeddingCacheEntry> GetEntries(bool excludeDone)
        {
            if (excludeDone)
                return _entries.Values
                    .Where(e => !string.Equals(e.State, "Done", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(e.State, "Closed", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(e.State, "Removed", StringComparison.OrdinalIgnoreCase))
                    .ToList();

            return _entries.Values.ToList();
        }

        public List<WorkItemDto> GetItemsNeedingEmbedding(List<WorkItemDto> freshItems)
        {
            var freshIds = new HashSet<int>(freshItems.Select(w => w.Id));

            // Remove cached items that no longer exist in the backlog
            var staleIds = _entries.Keys.Where(id => !freshIds.Contains(id)).ToList();
            foreach (var id in staleIds)
                _entries.Remove(id);

            var needsEmbedding = new List<WorkItemDto>();
            foreach (var wi in freshItems)
            {
                string changedDate = wi.Fields.TryGetValue("System.ChangedDate", out var cd) ? cd?.ToString() ?? "" : "";

                if (!_entries.TryGetValue(wi.Id, out var existing) || existing.ChangedDate != changedDate)
                {
                    needsEmbedding.Add(wi);
                }
            }

            return needsEmbedding;
        }

        public void AddOrUpdate(WorkItemDto wi, List<float[]> embeddings)
        {
            string changedDate = wi.Fields.TryGetValue("System.ChangedDate", out var cd) ? cd?.ToString() ?? "" : "";

            _entries[wi.Id] = new EmbeddingCacheEntry
            {
                WorkItemId = wi.Id,
                Title = wi.Title ?? "",
                State = wi.State ?? "",
                TypeName = wi.TypeName ?? "",
                CreatedBy = wi.CreatedBy ?? "",
                CreatedDate = wi.CreatedDate,
                IterationPath = wi.IterationPath ?? "",
                HtmlUrl = wi.HtmlUrl ?? "",
                ChangedDate = changedDate,
                Embedding = embeddings[0],
                ExtraEmbeddings = embeddings.Count > 1 ? embeddings.Skip(1).ToList() : null
            };

            if (DateTime.TryParse(changedDate, out var dt) && dt > LastUpdatedUtc)
                LastUpdatedUtc = dt;
        }

        public void Clear()
        {
            _entries.Clear();
            LastUpdatedUtc = DateTime.MinValue;
            if (File.Exists(_cacheFilePath))
                File.Delete(_cacheFilePath);
        }

        private static string SanitizeFileName(string input)
        {
            return Regex.Replace(input ?? "", @"[^\w\-]", "_");
        }

        private class CacheFileWrapper
        {
            public DateTime LastUpdatedUtc { get; set; }
            public List<EmbeddingCacheEntry> Entries { get; set; } = new();
        }
    }
}
