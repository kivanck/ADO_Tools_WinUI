using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADO_Tools.Models;
using ADO_Tools.Services;
using Newtonsoft.Json.Linq;

namespace ADO_Tools_WinUI.Services
{
    public sealed class SemanticSearchService : IDisposable
    {
        private readonly LocalEmbeddingService _embedder;
        private readonly string _cacheDir;
        private EmbeddingCache? _cache;

        // Work item types to include in semantic search indexing
        private static readonly string[] TargetWorkItemTypes = ["Product Backlog Item", "Bug", "User Story"];

        // Domain-specific terms from OpenRoads/OpenRail Designer and civil engineering.
        // The general-purpose embedding model (MiniLM) misinterprets many of these as everyday English.
        // Each entry maps a term to a descriptive expansion the model can understand.
        private static readonly Dictionary<string, string> DomainGlossary = new(StringComparer.OrdinalIgnoreCase)
        {
            // === Alignment & Geometry ===
            ["alignment"]       = "alignment horizontal geometry centerline road rail route",
            ["horizontal"]      = "horizontal alignment plan view geometry curves tangents",
            ["vertical"]        = "vertical alignment profile grade elevation design",
            ["PGL"]             = "profile grade line vertical alignment elevation",
            ["profile"]         = "profile vertical alignment grade elevation section",
            ["PI"]              = "point of intersection alignment geometry vertex",
            ["POI"]             = "point of intersection alignment geometry",
            ["PC"]              = "point of curvature curve start alignment",
            ["PT"]              = "point of curvature curve end alignment",
            ["tangent"]         = "tangent straight segment alignment geometry",
            ["spiral"]          = "spiral transition curve clothoid Euler alignment",
            ["clothoid"]        = "clothoid spiral transition curve alignment",
            ["stationing"]      = "stationing chainage distance along alignment measure",
            ["chainage"]        = "chainage stationing distance along alignment",
            ["offset"]          = "offset lateral distance perpendicular alignment",
            ["bearing"]         = "bearing direction azimuth angle survey",
            ["azimuth"]         = "azimuth bearing direction angle north",
            ["curve"]           = "curve arc circular geometry alignment radius",
            ["radius"]          = "radius curve circular arc geometry",
            ["degree of curve"]  = "degree of curve radius arc alignment geometry",

            // === Corridor Modeling ===
            ["corridor"]        = "corridor road highway alignment template 3D model design",
            ["template"]        = "template cross section corridor component point constraint",
            ["template drop"]   = "template drop corridor parametric constraint apply",
            ["component"]       = "component template element corridor point constraint",
            ["point control"]   = "point control corridor constraint parametric",
            ["parametric constraint"] = "parametric constraint corridor template point control",
            ["end condition"]   = "end condition corridor daylight cut fill slope grading",
            ["daylight"]        = "daylight end condition corridor slope cut fill existing ground",
            ["cut"]             = "cut excavation earthwork corridor end condition",
            ["fill"]            = "fill embankment earthwork corridor end condition",
            ["subgrade"]        = "subgrade pavement structure layer foundation",
            ["pavement"]        = "pavement surface road structure layer course",
            ["shoulder"]        = "shoulder road edge pavement lateral corridor",
            ["median"]          = "median divider road center barrier corridor",
            ["lane"]            = "lane travel road width corridor carriageway",
            ["carriageway"]     = "carriageway road lane surface travel corridor",
            ["curb"]            = "curb kerb edge road gutter corridor",
            ["kerb"]            = "kerb curb edge road gutter corridor",
            ["gutter"]          = "gutter drainage curb kerb flow road edge",
            ["sidewalk"]        = "sidewalk footpath pedestrian path corridor",
            ["ditch"]           = "ditch channel drainage swale roadside corridor",

            // === Superelevation & Cant ===
            ["cant"]            = "cant superelevation rail banking track curve tilt",
            ["superelevation"]  = "superelevation cant road rail banking curve cross slope tilt",
            ["cross slope"]     = "cross slope transverse grade road surface crown superelevation",
            ["crown"]           = "crown road cross slope center high point surface",
            ["normal crown"]    = "normal crown standard cross slope road surface",
            ["runoff"]          = "runoff superelevation transition length tangent curve",
            ["runout"]          = "runout superelevation transition normal crown tangent",
            ["rollover"]        = "rollover superelevation algebraic difference cross slope adjacent lanes",
            ["pivot"]           = "pivot superelevation rotation point axis road rail",
            ["rotation"]        = "rotation superelevation pivot axis cant banking",
            ["applied cant"]    = "applied cant superelevation rail track actual banking",
            ["cant deficiency"]  = "cant deficiency superelevation shortfall rail speed equilibrium",
            ["equilibrium cant"] = "equilibrium cant superelevation rail speed balance",

            // === Terrain & Surface ===
            ["terrain"]         = "terrain surface elevation ground model DTM TIN",
            ["DTM"]             = "digital terrain model surface elevation ground TIN",
            ["TIN"]             = "triangulated irregular network surface terrain mesh",
            ["contour"]         = "contour elevation line surface terrain level",
            ["breakline"]       = "breakline terrain surface feature line ridge valley",
            ["spot elevation"]  = "spot elevation point height terrain surface",
            ["feature"]         = "feature terrain element line point surface definition",
            ["existing ground"]  = "existing ground surface terrain original elevation",
            ["finished grade"]   = "finished grade design surface proposed elevation",
            ["grading"]         = "grading earthwork cut fill slope surface design",
            ["earthwork"]       = "earthwork cut fill volume grading excavation",
            ["volume"]          = "volume earthwork cut fill quantity calculation",

            // === Drainage & Utilities (D&U) ===
            ["DU"]              = "drainage and utilities storm sewer pipe network",
            ["D&U"]             = "drainage and utilities storm sewer pipe network",
            ["drainage"]        = "drainage storm water sewer pipe network catchment",
            ["storm"]           = "storm water drainage sewer pipe network runoff",
            ["sanitary"]        = "sanitary sewer wastewater pipe network gravity",
            ["conduit"]         = "conduit pipe link drainage sewer network segment",
            ["node"]            = "node junction manhole inlet structure drainage network",
            ["manhole"]         = "manhole junction structure node drainage access",
            ["inlet"]           = "inlet catch basin grate drainage surface collection",
            ["catch basin"]     = "catch basin inlet grate drainage surface collection",
            ["outfall"]         = "outfall outlet discharge drainage network end",
            ["headwall"]        = "headwall culvert outlet end wall drainage",
            ["culvert"]         = "culvert pipe crossing drainage road stream",
            ["pipe"]            = "pipe conduit segment drainage sewer network",
            ["scenario"]        = "scenario drainage hydraulic analysis compute simulation",
            ["compute"]         = "compute scenario analysis calculate drainage hydraulic run",
            ["catchment"]       = "catchment area drainage basin watershed contributing",
            ["HGL"]             = "hydraulic grade line pressure head pipe drainage",
            ["EGL"]             = "energy grade line hydraulic head pipe drainage",
            ["headloss"]        = "headloss energy loss friction pipe hydraulic",
            ["SewerGEMS"]       = "SewerGEMS sewer analysis hydraulic modeling Bentley",
            ["StormCAD"]        = "StormCAD storm drainage analysis hydraulic modeling Bentley",

            // === Rail Design ===
            ["rail"]            = "rail railway railroad track design alignment",
            ["track"]           = "track rail railway alignment geometry design",
            ["turnout"]         = "turnout switch rail track diverging route",
            ["switch"]          = "switch turnout rail track diverging point",
            ["crossover"]       = "crossover rail track connection parallel switch",
            ["gauge"]           = "gauge track width rail distance measure",
            ["ballast"]         = "ballast track rail bed foundation aggregate",
            ["sleeper"]         = "sleeper tie rail track support cross beam",
            ["tie"]             = "tie sleeper rail track support cross beam",
            ["catenery"]        = "catenary overhead wire electric rail power",
            ["OLE"]             = "overhead line equipment catenary electric rail",
            ["platform"]        = "platform station rail passenger boarding",
            ["clearance"]       = "clearance envelope gauge structure rail safety",
            ["kinematic envelope"] = "kinematic envelope clearance gauge rail vehicle swept path",

            // === Cross Sections ===
            ["cross section"]   = "cross section transverse cut view corridor template",
            ["section"]         = "section cross transverse cut view corridor",
            ["typical section"]  = "typical section standard cross template corridor design",
            ["right of way"]    = "right of way ROW boundary property limit corridor",
            ["ROW"]             = "right of way ROW boundary property limit corridor",

            // === Plan Production & Sheets ===
            ["plan production"]  = "plan production sheet drawing output print layout",
            ["sheet"]           = "sheet plan drawing output layout production border",
            ["annotation"]      = "annotation label text note dimension drawing plan",
            ["label"]           = "label annotation text tag element display",
            ["named boundary"]   = "named boundary sheet clip volume view plan production",
            ["clip volume"]     = "clip volume named boundary view plan production sheet",
            ["drawing model"]    = "drawing model sheet layout plan production output",

            // === Survey & Points ===
            ["survey"]          = "survey field data point measurement observation",
            ["COGO"]            = "COGO coordinate geometry survey point calculation",
            ["point"]           = "point coordinate survey COGO location station",

            // === Quantities & Reports ===
            ["quantity"]        = "quantity material volume area length measurement report",
            ["pay item"]        = "pay item quantity bid cost contract material",
            ["mass haul"]       = "mass haul earthwork volume cut fill balance diagram",

            // === General Software / Bentley ===
            ["ORD"]             = "OpenRoads Designer road design software Bentley civil",
            ["OpenRoads"]       = "OpenRoads Designer road design software Bentley civil",
            ["OpenRail"]        = "OpenRail Designer rail track design software Bentley",
            ["CONNECT"]         = "CONNECT Edition Bentley platform MicroStation",
            ["MicroStation"]    = "MicroStation CAD platform Bentley design drawing",
            ["DGN"]             = "DGN design file MicroStation Bentley format",
            ["DWG"]             = "DWG drawing file AutoCAD format CAD",
            ["XIN"]             = "XIN file civil standards configuration settings",
            ["DGNLIB"]          = "DGNLIB library standards template resource file",
            ["civil cell"]      = "civil cell reusable component 3D design element",
            ["linear"]          = "linear element alignment feature line civil geometry",
            ["complex"]         = "complex element chain compound alignment geometry",
            ["element"]         = "element object graphics design entity",
            ["model"]           = "model space design drawing file container reference",
            ["reference"]       = "reference attachment external file model link",
            ["import"]          = "import load read file data external bring in",
            ["export"]          = "export save write file data output send out",

            // === Common Bug/Issue Terms ===
            ["crash"]           = "crash application failure exception fatal error unhandled",
            ["hang"]            = "hang freeze unresponsive application stuck not responding",
            ["regression"]      = "regression bug previously working broken introduced",
            ["PBI"]             = "product backlog item user story requirement feature request",
            ["AASHTO"]          = "AASHTO highway transportation design standard US road",
        };

        public event Action<string>? StatusUpdated;

        public SemanticSearchService(string modelDir, string cacheDir)
        {
            _embedder = new LocalEmbeddingService(modelDir);
            _cacheDir = cacheDir;
        }

        public async Task<(int Added, int Total)> BuildOrUpdateCacheAsync(
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
                return (0, _cache.Count);
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
                string cutoffDate = AppSettings.Default.SearchCutoffDate;
                if (string.IsNullOrWhiteSpace(cutoffDate)) cutoffDate = "2023-01-01";
                dateFilter = $" AND [System.CreatedDate] > '{cutoffDate}T00:00:00.0000000'";
                StatusUpdated?.Invoke($"Fetching backlog items created since {cutoffDate}…");
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

            var queryResult = await tfsClient.QueryByWiqlAsync(
                wiql,
                progressCallback: (fetched, total) =>
                {
                    StatusUpdated?.Invoke($"Fetching work items… {fetched}/{total}");
                },
                statusCallback: (status) =>
                {
                    StatusUpdated?.Invoke(status);
                });

            var allItems = queryResult.WorkItems;

            if (queryResult.QueryLimitHit)
            {
                StatusUpdated?.Invoke($"⚠ Warning: Query returned {queryResult.TotalIdsReturned} items — the Azure DevOps 20,000 item limit was reached. Some items may be missing. Narrow the Iteration Path or move the cutoff date forward in Settings.");
            }

            StatusUpdated?.Invoke($"Fetched {allItems.Count} work items from Azure DevOps.");

            bool isIncremental = !forceRebuild && _cache.LastUpdatedUtc > DateTime.MinValue;
            var needsEmbedding = _cache.GetItemsNeedingEmbedding(allItems, isIncrementalUpdate: isIncremental);
            StatusUpdated?.Invoke(needsEmbedding.Count == 0
                ? $"Cache is up to date — no new embeddings needed. {_cache.Count} total items indexed."
                : $"Embedding {needsEmbedding.Count} new/changed items ({_cache.Count} already cached)…");

            if (needsEmbedding.Count > 0)
            {
                await Task.Run(() =>
                {
                    const int batchSize = 32;
                    for (int batchStart = 0; batchStart < needsEmbedding.Count; batchStart += batchSize)
                    {
                        var batch = needsEmbedding.Skip(batchStart).Take(batchSize).ToList();

                        var searchableTexts = new List<string>();
                        foreach (var wi in batch)
                            searchableTexts.Add(BuildSearchableText(wi));

                        var batchEmbeddings = _embedder.GetBatchedChunkedEmbeddings(searchableTexts);

                        for (int i = 0; i < batch.Count; i++)
                            _cache.AddOrUpdate(batch[i], batchEmbeddings[i], searchableTexts[i]);

                        progressCallback?.Invoke(
                            Math.Min(batchStart + batchSize, needsEmbedding.Count),
                            needsEmbedding.Count);
                    }
                });

                await _cache.SaveAsync();
                StatusUpdated?.Invoke($"Cache saved — added {needsEmbedding.Count} new items, {_cache.Count} total indexed.");
            }

            return (needsEmbedding.Count, _cache.Count);
        }

        public List<SemanticSearchResult> Search(string queryText, int topN = 20, bool excludeDone = false, float minScore = 0.2f)
        {
            if (_cache == null || string.IsNullOrWhiteSpace(queryText))
                return new List<SemanticSearchResult>();

            // Extract quoted phrases for mandatory post-filtering
            var requiredPhrases = ExtractQuotedPhrases(queryText);

            // Use the full query (minus quote characters) for embedding so ranking stays relevant
            string strippedQuery = queryText.Replace("\"", "");
            // Expand domain-specific terms so the model understands their technical meaning
            string expandedQuery = ExpandDomainTerms(strippedQuery);
            float[] queryEmbedding = _embedder.GetEmbedding(expandedQuery);
            var entries = _cache.GetEntries(excludeDone);

            return entries
                .Select(entry =>
                {
                    // Score against all chunk embeddings, take the best match
                    float bestScore = LocalEmbeddingService.CosineSimilarity(queryEmbedding, entry.Embedding);

                    if (entry.ExtraEmbeddings != null)
                    {
                        foreach (var extra in entry.ExtraEmbeddings)
                        {
                            float score = LocalEmbeddingService.CosineSimilarity(queryEmbedding, extra);
                            if (score > bestScore) bestScore = score;
                        }
                    }

                    return new SemanticSearchResult
                    {
                        CacheEntry = entry,
                        Score = bestScore
                    };
                })
                .Where(r => r.Score >= minScore)
                .Where(r => MatchesRequiredPhrases(r.CacheEntry, requiredPhrases))
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Finds work items semantically similar to a source work item.
        /// Embeds the source item's full searchable text and compares against
        /// all cached embeddings using cosine similarity.
        /// </summary>
        public List<SemanticSearchResult> FindSimilar(WorkItemDto sourceItem, int topN = 30, bool excludeDone = false, float minScore = 0.2f)
        {
            if (_cache == null) return new List<SemanticSearchResult>();

            string sourceText = BuildSearchableText(sourceItem);
            if (string.IsNullOrWhiteSpace(sourceText)) return new List<SemanticSearchResult>();

            string expandedText = ExpandDomainTerms(sourceText);
            float[] sourceEmbedding = _embedder.GetEmbedding(expandedText);
            var entries = _cache.GetEntries(excludeDone);

            return entries
                .Where(entry => entry.WorkItemId != sourceItem.Id)
                .Select(entry =>
                {
                    float bestScore = LocalEmbeddingService.CosineSimilarity(sourceEmbedding, entry.Embedding);

                    if (entry.ExtraEmbeddings != null)
                    {
                        foreach (var extra in entry.ExtraEmbeddings)
                        {
                            float s = LocalEmbeddingService.CosineSimilarity(sourceEmbedding, extra);
                            if (s > bestScore) bestScore = s;
                        }
                    }

                    return new SemanticSearchResult
                    {
                        CacheEntry = entry,
                        Score = bestScore
                    };
                })
                .Where(r => r.Score >= minScore)
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Extracts double-quoted phrases from a query string.
        /// Example: 'crash "open dialog" bug' → ["open dialog"]
        /// </summary>
        internal static List<string> ExtractQuotedPhrases(string query)
        {
            var phrases = new List<string>();
            foreach (Match match in Regex.Matches(query, "\"([^\"]+)\""))
            {
                string phrase = match.Groups[1].Value.Trim();
                if (phrase.Length > 0)
                    phrases.Add(phrase);
            }
            return phrases;
        }

        /// <summary>
        /// Returns true if the cache entry's searchable text contains ALL required phrases (case-insensitive).
        /// </summary>
        internal static bool MatchesRequiredPhrases(EmbeddingCacheEntry entry, List<string> requiredPhrases)
        {
            if (requiredPhrases.Count == 0)
                return true;

            string text = !string.IsNullOrWhiteSpace(entry.SearchableText)
                ? entry.SearchableText
                : entry.Title;

            foreach (var phrase in requiredPhrases)
            {
                if (!text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Expands recognized domain terms in the query so the embedding model
        /// can capture their technical meaning rather than everyday English meaning.
        /// </summary>
        private static string ExpandDomainTerms(string query)
        {
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var expansions = new List<string>();

            foreach (var word in words)
            {
                // Strip common punctuation for matching but keep original in query
                string clean = word.Trim(',', '.', '!', '?', ':', ';');
                if (DomainGlossary.TryGetValue(clean, out var expansion))
                    expansions.Add(expansion);
            }

            // Also check multi-word terms (e.g. "cross slope", "D&U")
            foreach (var kvp in DomainGlossary)
            {
                if (kvp.Key.Contains(' ') && query.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    expansions.Add(kvp.Value);
            }

            if (expansions.Count == 0)
                return query;

            // Prepend expansions so the model understands domain context
            return $"{string.Join(". ", expansions)}. {query}";
        }

        internal static string BuildSearchableText(WorkItemDto wi)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(wi.Title))
                parts.Add(wi.Title);

            // Rich-text field suffixes that contribute meaningful search content.
            // Matched by suffix so they work regardless of prefix
            // (Custom.*, beconnect-test.*, Microsoft.VSTS.Common.*, etc.)
            // Note: System.History is excluded — it only returns the latest revision.
            // Full discussion threads are captured via _CommentsCombined instead.
            var richFieldSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Description",
                "ReproSteps",
                "SystemInfo",
                "AcceptanceCriteria",
                "FixDetails",
                "InvestigationNotes",
                "TestingNotes",
                "Notes",
                "_CommentsCombined"
            };

            foreach (var kvp in wi.Fields)
            {
                // Extract suffix: "beconnect-test.FixDetails" → "FixDetails"
                string suffix = kvp.Key;
                int lastDot = kvp.Key.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < kvp.Key.Length - 1)
                    suffix = kvp.Key[(lastDot + 1)..];

                if (!richFieldSuffixes.Contains(suffix))
                    continue;

                if (kvp.Value is string s && !string.IsNullOrWhiteSpace(s))
                    parts.Add(StripHtml(s));
            }

            return string.Join(". ", parts);
        }

        internal static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            // Remove all HTML tags
            var text = Regex.Replace(html, "<.*?>", " ");
            // Decode HTML entities (&nbsp; → space, &quot; → ", &amp; → &, etc.)
            text = System.Net.WebUtility.HtmlDecode(text);
            // Collapse multiple whitespace into single spaces
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        public bool IsReady => _cache != null && _cache.Count > 0;

        public int CachedItemCount => _cache?.Count ?? 0;

        public List<EmbeddingCacheEntry> GetCacheEntries(bool excludeDone)
        {
            return _cache?.GetEntries(excludeDone) ?? new List<EmbeddingCacheEntry>();
        }

        /// <summary>
        /// Attempts to load an existing embedding cache from disk without connecting to Azure DevOps.
        /// Returns true if a cache was found and loaded.
        /// </summary>
        public bool TryLoadCache(string organization, string project, string areaPath = "")
        {
            string cacheKey = string.IsNullOrWhiteSpace(areaPath) ? project : $"{project}_{areaPath}";
            _cache = new EmbeddingCache(organization, cacheKey, _cacheDir);
            return _cache.TryLoad();
        }

        public void Dispose() => _embedder?.Dispose();
        public bool IsUsingGpu => _embedder.IsUsingGpu;
    }

    public class SemanticSearchResult
    {
        public EmbeddingCacheEntry CacheEntry { get; set; } = null!;
        public float Score { get; set; }
    }
}
