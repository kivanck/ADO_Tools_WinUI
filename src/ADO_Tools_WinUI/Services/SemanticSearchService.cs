using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADO_Tools.Models;
using ADO_Tools.Services;

namespace ADO_Tools_WinUI.Services
{
    public sealed class SemanticSearchService : IDisposable
    {
        private readonly LocalEmbeddingService _embedder;
        private readonly string _cacheDir;
        private EmbeddingCache? _cache;

        // Work item types to include in semantic search indexing
        private static readonly string[] TargetWorkItemTypes = ["Product Backlog Item", "Bug", "User Story"];

        public event Action<string>? StatusUpdated;

        public SemanticSearchService(string modelDir, string cacheDir)
        {
            _embedder = new LocalEmbeddingService(modelDir);
            _cacheDir = cacheDir;
        }

        public async Task BuildOrUpdateCacheAsync(
            TfsRestClient tfsClient,
            string organization,
            string project,
            string areaPath = "",
            bool forceRebuild = false,
            Action<int, int>? progressCallback = null)
        {
            string cacheKey = string.IsNullOrWhiteSpace(areaPath) ? project : $"{project}_{areaPath}";
            _cache = new EmbeddingCache(organization, cacheKey, _cacheDir);

            if (forceRebuild)
            {
                _cache.Clear();
                StatusUpdated?.Invoke("Force rebuild requested — cache cleared.");
            }
            else
            {
                bool cacheLoaded = _cache.TryLoad();

                if (cacheLoaded)
                    StatusUpdated?.Invoke($"Loaded {_cache.Count} cached embeddings from disk.");
                else
                    StatusUpdated?.Invoke("No existing cache found. Building from scratch…");
            }

            // Discover actual type names in this project and match against our targets
            StatusUpdated?.Invoke("Discovering work item types…");
            var allTypeNames = await tfsClient.GetWorkItemTypeNamesAsync();
            var matchedTypes = allTypeNames
                .Where(t => TargetWorkItemTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (matchedTypes.Count == 0)
            {
                StatusUpdated?.Invoke($"Warning: No matching types found. Available types: {string.Join(", ", allTypeNames)}");
                return;
            }

            // Build WIQL — only fetch items created since cutoff, or changed since last cache update
            string dateFilter;
            if (!forceRebuild && _cache.LastUpdatedUtc > DateTime.MinValue)
            {
                string sinceDate = _cache.LastUpdatedUtc.ToString("yyyy-MM-dd");
                dateFilter = $" AND [System.ChangedDate] >= '{sinceDate}'";
                StatusUpdated?.Invoke($"Incremental update — fetching items changed since {sinceDate}…");
            }
            else
            {
                dateFilter = " AND [System.CreatedDate] > '2023-01-01T00:00:00.0000000'";
                StatusUpdated?.Invoke("Fetching backlog items created since 2023-01-01…");
            }

            string typeFilter = $" AND [System.WorkItemType] IN ({string.Join(", ", matchedTypes.Select(t => $"'{t}'"))})";

            string wiql;
            if (!string.IsNullOrWhiteSpace(areaPath))
            {
                string escapedPath = areaPath.Replace("'", "''");
                wiql = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.IterationPath] UNDER '{escapedPath}'{dateFilter}{typeFilter} ORDER BY [System.Id]";
            }
            else
            {
                wiql = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project{dateFilter}{typeFilter} ORDER BY [System.Id]";
            }

            StatusUpdated?.Invoke($"WIQL: {wiql}");

            var allItems = await tfsClient.QueryByWiqlAsync(
                wiql,
                progressCallback: (fetched, total) =>
                {
                    StatusUpdated?.Invoke($"Fetching work items… {fetched}/{total}");
                });

            StatusUpdated?.Invoke($"Fetched {allItems.Count} work items from Azure DevOps.");

            var needsEmbedding = _cache.GetItemsNeedingEmbedding(allItems);
            StatusUpdated?.Invoke(needsEmbedding.Count == 0
                ? "Cache is up to date — no new embeddings needed."
                : $"Embedding {needsEmbedding.Count} new/changed items ({_cache.Count} already cached)…");

            if (needsEmbedding.Count > 0)
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < needsEmbedding.Count; i++)
                    {
                        var wi = needsEmbedding[i];
                        string text = BuildSearchableText(wi);
                        float[] embedding = _embedder.GetEmbedding(text);
                        _cache.AddOrUpdate(wi, embedding);

                        progressCallback?.Invoke(i + 1, needsEmbedding.Count);
                    }
                });

                await _cache.SaveAsync();
                StatusUpdated?.Invoke($"Cache saved — {_cache.Count} total items indexed.");
            }
        }

        public List<SemanticSearchResult> Search(string queryText, int topN = 20, bool excludeDone = false, float minScore = 0.2f)
        {
            if (_cache == null || string.IsNullOrWhiteSpace(queryText))
                return new List<SemanticSearchResult>();

            float[] queryEmbedding = _embedder.GetEmbedding(queryText);
            var entries = _cache.GetEntries(excludeDone);

            return entries
                .Select(entry => new SemanticSearchResult
                {
                    CacheEntry = entry,
                    Score = LocalEmbeddingService.CosineSimilarity(queryEmbedding, entry.Embedding)
                })
                .Where(r => r.Score >= minScore)
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }

        public bool IsReady => _cache != null && _cache.Count > 0;

        public int CachedItemCount => _cache?.Count ?? 0;

        private static string BuildSearchableText(WorkItemDto wi)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(wi.Title))
            {
                parts.Add(wi.Title);
                parts.Add(wi.Title);
            }

            string[] richFields =
            {
                "System.Description",
                "Microsoft.VSTS.TCM.ReproSteps",
                "Microsoft.VSTS.TCM.SystemInfo",
                "Microsoft.VSTS.Common.AcceptanceCriteria",
                "Microsoft.VSTS.Common.FixDetails",
                "System.History",
                "Custom.InvestigationNotes",
                "Custom.Notes",
                "Custom.ProductAffected",
                "Custom.DefectSource_EA",
                "_CommentsCombined"
            };

            foreach (var fieldName in richFields)
            {
                if (wi.Fields.TryGetValue(fieldName, out var val) && val is string s && !string.IsNullOrWhiteSpace(s))
                    parts.Add(StripHtml(s));
            }

            return string.Join(". ", parts);
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            return Regex.Replace(html, "<.*?>", " ").Trim();
        }

        public void Dispose() => _embedder?.Dispose();
    }

    public class SemanticSearchResult
    {
        public EmbeddingCacheEntry CacheEntry { get; set; } = null!;
        public float Score { get; set; }
    }
}
