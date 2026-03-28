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
    /// <summary>
    /// Lightweight per-query cache that stores searchable text and metadata
    /// for BM25 search within query results. No embeddings — keeps it fast.
    /// </summary>
    public class QuerySearchCache
    {
        private readonly string _cacheDir;
        private readonly string _cacheFilePath;
        private Dictionary<int, QueryCacheEntry> _entries = new();

        public DateTime LastUpdatedUtc { get; private set; } = DateTime.MinValue;
        public int Count => _entries.Count;

        public QuerySearchCache(string queryId, string cacheDir)
        {
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
            string safeName = SanitizeFileName(queryId);
            _cacheFilePath = Path.Combine(_cacheDir, $"query_{safeName}.json");
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

        /// <summary>
        /// Determines which items need their SearchableText rebuilt,
        /// and removes stale items no longer in the query results.
        /// Returns items that are new or changed since last cache.
        /// </summary>
        public List<WorkItemDto> GetItemsNeedingUpdate(List<WorkItemDto> freshItems)
        {
            var freshIds = new HashSet<int>(freshItems.Select(w => w.Id));

            // Remove items no longer in query results
            var staleIds = _entries.Keys.Where(id => !freshIds.Contains(id)).ToList();
            foreach (var id in staleIds)
                _entries.Remove(id);

            var needsUpdate = new List<WorkItemDto>();
            foreach (var wi in freshItems)
            {
                string changedDate = wi.Fields.TryGetValue("System.ChangedDate", out var cd)
                    ? cd?.ToString() ?? "" : "";

                if (!_entries.TryGetValue(wi.Id, out var existing) || existing.ChangedDate != changedDate)
                    needsUpdate.Add(wi);
            }

            return needsUpdate;
        }

        public void AddOrUpdate(WorkItemDto wi, string searchableText)
        {
            string changedDate = wi.Fields.TryGetValue("System.ChangedDate", out var cd)
                ? cd?.ToString() ?? "" : "";

            _entries[wi.Id] = new QueryCacheEntry
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
                SearchableText = searchableText
            };

            if (DateTime.TryParse(changedDate, out var dt) && dt > LastUpdatedUtc)
                LastUpdatedUtc = dt;
        }

        /// <summary>
        /// Returns a map of WorkItemId ? ChangedDate for all cached entries.
        /// Used to compare against lightweight API results to determine which items need re-fetching.
        /// </summary>
        public Dictionary<int, string> GetChangedDateMap()
        {
            return _entries.ToDictionary(e => e.Key, e => e.Value.ChangedDate);
        }

        /// <summary>
        /// Given the full set of IDs from the query and a map of Id ? ChangedDate from the API,
        /// returns the list of IDs that need a full re-fetch (new or changed).
        /// Also removes stale entries no longer in the query.
        /// </summary>
        public List<int> GetIdsNeedingFetch(List<int> freshIds, Dictionary<int, string> freshChangedDates)
        {
            var freshIdSet = new HashSet<int>(freshIds);

            // Remove items no longer in query results
            var staleIds = _entries.Keys.Where(id => !freshIdSet.Contains(id)).ToList();
            foreach (var id in staleIds)
                _entries.Remove(id);

            var needsFetch = new List<int>();
            foreach (var id in freshIds)
            {
                string freshChanged = freshChangedDates.TryGetValue(id, out var cd) ? cd : "";
                if (!_entries.TryGetValue(id, out var existing) || existing.ChangedDate != freshChanged)
                    needsFetch.Add(id);
            }
            return needsFetch;
        }

        /// <summary>
        /// Updates cache entries with full work item data from freshly fetched items.
        /// </summary>
        public void MergeFullItems(List<WorkItemDto> freshItems)
        {
            foreach (var wi in freshItems)
            {
                string changedDate = wi.Fields.TryGetValue("System.ChangedDate", out var cd)
                    ? cd?.ToString() ?? "" : "";

                // Serialize Fields dict — strip HTML from rich-text fields to reduce cache size
                var fieldStrings = new Dictionary<string, string>();
                foreach (var kvp in wi.Fields)
                {
                    string value = kvp.Value?.ToString() ?? "";
                    if (HtmlFieldSuffixes.Contains(GetFieldSuffix(kvp.Key)))
                        value = SemanticSearchService.StripHtml(value);
                    fieldStrings[kvp.Key] = value;
                }

                var attachments = wi.Attachments.Select(a => new CachedAttachment
                {
                    Url = a.Url ?? "",
                    FileName = a.FileName ?? "",
                    Length = a.Length
                }).ToList();

                string searchText = SemanticSearchService.BuildSearchableText(wi);

                if (_entries.TryGetValue(wi.Id, out var existing))
                {
                    // Update existing entry
                    existing.Title = wi.Title ?? "";
                    existing.State = wi.State ?? "";
                    existing.TypeName = wi.TypeName ?? "";
                    existing.CreatedBy = wi.CreatedBy ?? "";
                    existing.CreatedDate = wi.CreatedDate;
                    existing.IterationPath = wi.IterationPath ?? "";
                    existing.HtmlUrl = wi.HtmlUrl ?? "";
                    existing.ChangedDate = changedDate;
                    existing.SearchableText = searchText;
                    existing.Fields = fieldStrings;
                    existing.Attachments = attachments;
                }
                else
                {
                    _entries[wi.Id] = new QueryCacheEntry
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
                        SearchableText = searchText,
                        Fields = fieldStrings,
                        Attachments = attachments
                    };
                }

                if (DateTime.TryParse(changedDate, out var dt) && dt > LastUpdatedUtc)
                    LastUpdatedUtc = dt;
            }
        }

        /// <summary>
        /// Reconstructs WorkItemDto objects from cache for the given IDs, preserving the order.
        /// </summary>
        public List<WorkItemDto> GetCachedWorkItems(List<int> orderedIds)
        {
            var result = new List<WorkItemDto>();
            foreach (var id in orderedIds)
            {
                if (!_entries.TryGetValue(id, out var entry))
                    continue;

                var dto = new WorkItemDto
                {
                    Id = entry.WorkItemId,
                    Title = entry.Title,
                    State = entry.State,
                    CreatedBy = entry.CreatedBy,
                    CreatedDate = entry.CreatedDate,
                    TypeName = entry.TypeName,
                    IterationPath = entry.IterationPath,
                    HtmlUrl = entry.HtmlUrl
                };

                // Restore Fields
                foreach (var kvp in entry.Fields)
                    dto.Fields[kvp.Key] = kvp.Value;

                // Restore Attachments
                foreach (var att in entry.Attachments)
                    dto.Attachments.Add(new AttachmentDto
                    {
                        Url = att.Url,
                        FileName = att.FileName,
                        Length = att.Length
                    });

                result.Add(dto);
            }
            return result;
        }

        /// <summary>
        /// Returns all entries as EmbeddingCacheEntry (compatible with Bm25SearchService).
        /// </summary>
        public List<EmbeddingCacheEntry> GetAsBm25Entries()
        {
            return _entries.Values.Select(e => new EmbeddingCacheEntry
            {
                WorkItemId = e.WorkItemId,
                Title = e.Title,
                State = e.State,
                TypeName = e.TypeName,
                CreatedBy = e.CreatedBy,
                CreatedDate = e.CreatedDate,
                IterationPath = e.IterationPath,
                HtmlUrl = e.HtmlUrl,
                ChangedDate = e.ChangedDate,
                SearchableText = e.SearchableText,
                Fields = e.Fields
            }).ToList();
        }

        /// <summary>
        /// Rich-text field suffixes that contain HTML markup.
        /// These are stripped to plain text when cached to reduce file size.
        /// </summary>
        private static readonly HashSet<string> HtmlFieldSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Description", "ReproSteps", "SystemInfo", "AcceptanceCriteria",
            "FixDetails", "InvestigationNotes", "TestingNotes", "Notes",
            "History", "_CommentsCombined"
        };

        private static string GetFieldSuffix(string fieldName)
        {
            int lastDot = fieldName.LastIndexOf('.');
            return lastDot >= 0 && lastDot < fieldName.Length - 1
                ? fieldName[(lastDot + 1)..]
                : fieldName;
        }

        private static string SanitizeFileName(string input)
        {
            return Regex.Replace(input ?? "", @"[^\w\-]", "_");
        }

        private class CacheFileWrapper
        {
            public DateTime LastUpdatedUtc { get; set; }
            public List<QueryCacheEntry> Entries { get; set; } = [];
        }
    }

    /// <summary>
    /// Cache entry for query search — same fields as EmbeddingCacheEntry
    /// but without the embedding vectors, keeping the file small and fast.
    /// Also stores all Fields and Attachments for full WorkItemDto reconstruction.
    /// </summary>
    public class QueryCacheEntry
    {
        public int WorkItemId { get; set; }
        public string Title { get; set; } = "";
        public string State { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string IterationPath { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public string ChangedDate { get; set; } = "";
        public string? SearchableText { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
        public List<CachedAttachment> Attachments { get; set; } = [];
    }

    public class CachedAttachment
    {
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Length { get; set; }
    }
}
