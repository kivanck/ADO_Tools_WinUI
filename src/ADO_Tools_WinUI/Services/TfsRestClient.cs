using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ADO_Tools.Models;

namespace ADO_Tools.Services
{
    public class TfsRestClient
    {
        readonly HttpClient _http;
        readonly string baseUrl;

        public TfsRestClient(string organization, string project, string personalAccessToken)
        {

            baseUrl = $"https://dev.azure.com/{organization}/{project}/_apis";

            _http = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        /// <summary>
        /// Returns a list of work item type names available in the project (e.g. "Bug", "Product Backlog Item", "User Story").
        /// </summary>
        public async Task<List<string>> GetWorkItemTypeNamesAsync()
        {
            string url = $"{baseUrl}/wit/workitemtypes?api-version=7.1";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            return json["value"]?
                .Select(t => t["name"]?.ToString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? new List<string>();
        }

        public async Task<List<QueryDto>> GetQueriesAsync()
        {
            // Fetch main tree and favorites in parallel
            var allTask = FetchQueryTreeAsync();
            var favTask = FetchAllFavoriteIdsAsync();

            await Task.WhenAll(allTask, favTask);

            var allQueries = allTask.Result;
            var (myFavEntries, teamFavEntries) = favTask.Result;

            var roots = new List<QueryDto>();

            // Build Favorites root if there are any
            if (myFavEntries.Count > 0 || teamFavEntries.Count > 0)
            {
                var favRoot = new QueryDto { Name = "\u2605 Favorites", Path = "\u2605 Favorites", IsFolder = true };

                if (myFavEntries.Count > 0)
                {
                    var myFavFolder = new QueryDto { Name = "My Favorites", Path = "\u2605 Favorites/My Favorites", IsFolder = true };
                    foreach (var (id, name) in myFavEntries)
                        myFavFolder.Children.Add(new QueryDto
                        {
                            Id = id,
                            Name = name,
                            Path = $"\u2605 Favorites/My Favorites/{name}",
                            Wiql = $"{baseUrl}/wit/wiql/{id}"
                        });
                    favRoot.Children.Add(myFavFolder);
                }
                if (teamFavEntries.Count > 0)
                {
                    var teamFavFolder = new QueryDto { Name = "Team Favorites", Path = "\u2605 Favorites/Team Favorites", IsFolder = true };
                    foreach (var (id, name) in teamFavEntries)
                        teamFavFolder.Children.Add(new QueryDto
                        {
                            Id = id,
                            Name = name,
                            Path = $"\u2605 Favorites/Team Favorites/{name}",
                            Wiql = $"{baseUrl}/wit/wiql/{id}"
                        });
                    favRoot.Children.Add(teamFavFolder);
                }
                roots.Add(favRoot);
            }

            // Wrap the main tree under "All Queries"
            var allRoot = new QueryDto { Name = "All Queries", Path = "All Queries", IsFolder = true };
            allRoot.Children.AddRange(allQueries);
            roots.Add(allRoot);

            return roots;
        }

        private async Task<(List<(string id, string name)> myFavEntries, List<(string id, string name)> teamFavEntries)> FetchAllFavoriteIdsAsync()
        {
            var myFavEntries = new List<(string id, string name)>();
            var teamFavEntries = new List<(string id, string name)>();

            string orgBase = baseUrl.Replace("/_apis", "");
            int lastSlash = orgBase.LastIndexOf('/');
            string projectName = orgBase[(lastSlash + 1)..];
            string orgUrl = orgBase[..lastSlash];

            // Get the project GUID — needed for favorites API scope
            string projectId = "";
            try
            {
                var projResp = await _http.GetAsync($"{orgUrl}/_apis/projects/{projectName}?api-version=7.1-preview.4");
                if (projResp.IsSuccessStatusCode)
                {
                    var projJson = JObject.Parse(await projResp.Content.ReadAsStringAsync());
                    projectId = projJson["id"]?.ToString() ?? "";
                }
            }
            catch { /* project lookup failed */ }

            if (string.IsNullOrEmpty(projectId))
                return (myFavEntries, teamFavEntries);

            try
            {
                // Personal favorites — project-scoped
                // This API returns favorites owned by the authenticated user (PAT owner)
                string myFavUrl = $"{orgUrl}/_apis/Favorite/Favorites?artifactType=Microsoft.TeamFoundation.WorkItemTracking.QueryItem&artifactScopeType=Project&artifactScopeId={projectId}&api-version=7.1-preview.1";
                var myResp = await _http.GetAsync(myFavUrl);
                if (myResp.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(await myResp.Content.ReadAsStringAsync());
                    foreach (var fav in json["value"] ?? Enumerable.Empty<JToken>())
                    {
                        if (fav["artifactIsDeleted"]?.Value<bool>() == true) continue;
                        var id = fav["artifactId"]?.ToString() ?? "";
                        var name = fav["artifactName"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                            myFavEntries.Add((id, name));
                    }
                }
            }
            catch { /* personal favorites unavailable */ }

            // ===================================================================================
            // TEAM FAVORITES - NOT ACCESSIBLE VIA PAT
            // ===================================================================================
            // Investigation conducted March 2026:
            //
            // Azure DevOps team favorites are NOT accessible via REST API with PAT authentication.
            // 
            // APIs tested that DO NOT work with PAT:
            // 1. Favorites API with artifactScopeType=Team — returns empty
            // 2. Favorites API with ownerType=Team&ownerId={teamId} — ignored, returns user's favorites
            // 3. Favorites API with identityId={teamId} — ignored, returns user's favorites  
            // 4. Favorites API with team's subjectDescriptor — ignored, returns user's favorites
            // 5. Queries API with $filter=favorites&$team={teamId} — requires team membership, limited
            //
            // The ONLY way to access team favorites is via the Contribution API:
            //   POST _apis/Contribution/HierarchyQuery
            //   with contributionId: "ms.vss-work-web.query-list-team-favorites-data-provider"
            //
            // However, this API requires SESSION authentication (cookies + Bearer token from AAD login)
            // and returns 401 Unauthorized when using PAT authentication.
            //
            // Additionally, query objects themselves have no property indicating they are team favorites.
            // The favorite relationship is stored separately and not exposed via the Queries API.
            //
            // WORKAROUND: Users should add team favorites to their personal favorites in the Azure DevOps
            // web UI. This is a simple one-click operation (star icon on the query) and the queries
            // will then appear in "My Favorites" which IS accessible via PAT.
            // ===================================================================================

            return (myFavEntries, teamFavEntries);
        }

        private async Task<List<QueryDto>> FetchQueryTreeAsync()
        {
            string url = $"{baseUrl}/wit/queries?$depth=2&$expand=minimal&api-version=7.1-preview.2";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            var roots = new List<QueryDto>();
            foreach (var topLevel in json["value"] ?? Enumerable.Empty<JToken>())
                roots.Add(await ParseQueryNodeAsync(topLevel, "All Queries"));

            return roots;
        }

        private async Task<QueryDto> ParseQueryNodeAsync(JToken node, string parentPath)
        {
            var name = node["name"]?.ToString() ?? "";
            var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
            bool isFolder = node["isFolder"]?.Value<bool>() ?? (node["queryType"] == null);
            bool hasChildren = node["hasChildren"]?.Value<bool>() ?? false;

            var dto = new QueryDto
            {
                Id = node["id"]?.ToString() ?? "",
                Name = name,
                Path = path,
                IsFolder = isFolder,
                Wiql = node["_links"]?["wiql"]?["href"]?.ToString() ?? ""
            };

            // If _links wasn't included (e.g. $expand=minimal), construct the wiql URL from the ID
            if (string.IsNullOrEmpty(dto.Wiql) && !isFolder && !string.IsNullOrEmpty(dto.Id))
                dto.Wiql = $"{baseUrl}/wit/wiql/{dto.Id}";

            var children = node["children"];
            if (children != null && children.Any())
            {
                foreach (var child in children)
                    dto.Children.Add(await ParseQueryNodeAsync(child, path));
            }
            else if (isFolder && hasChildren)
            {
                // Folder has children beyond the initial depth — fetch them
                try
                {
                    string folderUrl = $"{baseUrl}/wit/queries/{dto.Id}?$depth=2&$expand=minimal&api-version=7.1-preview.2";
                    var folderResp = await _http.GetAsync(folderUrl);
                    if (folderResp.IsSuccessStatusCode)
                    {
                        var folderJson = JObject.Parse(await folderResp.Content.ReadAsStringAsync());
                        var folderChildren = folderJson["children"];
                        if (folderChildren != null)
                        {
                            foreach (var child in folderChildren)
                                dto.Children.Add(await ParseQueryNodeAsync(child, path));
                        }
                    }
                }
                catch { /* skip unreachable folders */ }
            }

            return dto;
        }

        /// <summary>
        /// Returns a flat list of all non-folder queries from a tree of QueryDto nodes.
        /// </summary>
        public static List<QueryDto> FlattenQueries(List<QueryDto> roots)
        {
            var result = new List<QueryDto>();
            void Walk(List<QueryDto> nodes)
            {
                foreach (var node in nodes)
                {
                    if (!node.IsFolder)
                        result.Add(node);
                    Walk(node.Children);
                }
            }
            Walk(roots);
            return result;
        }

        /// <summary>
        /// Extracts work item IDs from a WIQL query response.
        /// Flat queries return "workItems"; tree/one-hop queries return "workItemRelations".
        /// </summary>
        private static List<int> ExtractWorkItemIds(JObject json)
        {
            // Flat list queries
            var workItems = json["workItems"];
            if (workItems is JArray wiArray && wiArray.Count > 0)
            {
                return wiArray
                    .Select(x => (int?)x["id"])
                    .Where(i => i.HasValue)
                    .Select(i => i!.Value)
                    .ToList();
            }

            // Tree / one-hop queries — extract unique IDs from source and target
            var relations = json["workItemRelations"];
            if (relations is not JArray relArray || relArray.Count == 0) return [];

            var ids = new HashSet<int>();
            foreach (var rel in relArray)
            {
                var source = rel["source"];
                var target = rel["target"];
                if (source is JObject && source["id"] != null)
                    ids.Add(source["id"]!.Value<int>());
                if (target is JObject && target["id"] != null)
                    ids.Add(target["id"]!.Value<int>());
            }
            return ids.ToList();
        }

        /// <summary>
        /// Executes a saved query and returns only the matching work item IDs and column definitions.
        /// Does NOT fetch full work item data — use this for incremental cache scenarios.
        /// </summary>
        public async Task<QueryExecutionResult> ExecuteQueryAsync(string savedQueryUrl)
        {
            var result = new QueryExecutionResult();
            if (string.IsNullOrWhiteSpace(savedQueryUrl)) return result;

            var resp = await _http.GetAsync($"{savedQueryUrl}?api-version=7.1");
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            result.Columns = json["columns"]?
                .Select(c => c["referenceName"]?.ToString() ?? "")
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList() ?? [];

            result.WorkItemIds = ExtractWorkItemIds(json);

            return result;
        }

        public async Task<QueryExecutionResult> QueryWorkItemsAsync(string savedQueryUrl)
        {
            var result = new QueryExecutionResult();
            if (string.IsNullOrWhiteSpace(savedQueryUrl)) return result;

            var resp = await _http.GetAsync($"{savedQueryUrl}?api-version=7.1");
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            result.Columns = json["columns"]?
                .Select(c => c["referenceName"]?.ToString() ?? "")
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList() ?? [];

            var ids = ExtractWorkItemIds(json);
            if (ids.Count == 0) return result;

            result.WorkItems = await FetchWorkItemsByIdsAsync(ids);
            return result;
        }

        public async Task<WiqlQueryResult> QueryByWiqlAsync(string wiql, int top = 20000, Action<int, int>? progressCallback = null)
        {
            var result = new WiqlQueryResult();
            if (string.IsNullOrWhiteSpace(wiql)) return result;

            var body = new StringContent(
                JsonConvert.SerializeObject(new { query = wiql }),
                Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync($"{baseUrl}/wit/wiql?$top={top}&api-version=7.1", body);
            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync();
                throw new HttpRequestException($"WIQL query failed ({resp.StatusCode}): {errorBody}");
            }
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            var ids = json["workItems"]?.Select(x => (int?)x["id"])?.Where(i => i.HasValue)?.Select(i => i!.Value).ToList();
            if (ids == null || ids.Count == 0) return result;

            result.TotalIdsReturned = ids.Count;
            result.QueryLimitHit = ids.Count >= top;
            result.WorkItems = await FetchWorkItemsByIdsAsync(ids, progressCallback);
            return result;
        }

        /// <summary>
        /// Lightweight batch fetch that returns only (Id, ChangedDate) for each work item.
        /// Uses $select to avoid downloading all fields/relations/comments.
        /// </summary>
        public async Task<Dictionary<int, string>> FetchWorkItemChangedDatesAsync(List<int> ids)
        {
            var result = new Dictionary<int, string>();
            var chunkSize = 200; // larger chunks are fine for lightweight calls

            for (int i = 0; i < ids.Count; i += chunkSize)
            {
                var chunk = ids.Skip(i).Take(chunkSize).ToList();
                string idsCsv = string.Join(",", chunk);
                string url = $"{baseUrl}/wit/workitems?ids={idsCsv}&fields=System.Id,System.ChangedDate&api-version=7.1";
                var resp = await _http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

                foreach (var wi in json["value"] ?? Enumerable.Empty<JToken>())
                {
                    int id = wi["id"]?.Value<int>() ?? 0;
                    string changed = wi["fields"]?["System.ChangedDate"]?.ToString() ?? "";
                    if (id > 0)
                        result[id] = changed;
                }
            }
            return result;
        }

        public async Task<List<WorkItemDto>> FetchWorkItemsByIdsAsync(List<int> ids, Action<int, int>? progressCallback = null)
        {
            var list = new List<WorkItemDto>();
            var chunkSize = 100;
            var commentsCutoff = new DateTime(2023, 1, 1);
            int totalIds = ids.Count;

            for (int i = 0; i < ids.Count; i += chunkSize)
            {
                var chunk = ids.Skip(i).Take(chunkSize).ToList();
                string idsCsv = string.Join(",", chunk);
                string getUrl = $"{baseUrl}/wit/workitems?ids={idsCsv}&$expand=All&api-version=7.1";
                var r2 = await _http.GetAsync(getUrl);
                r2.EnsureSuccessStatusCode();
                var j2 = JObject.Parse(await r2.Content.ReadAsStringAsync());

                foreach (var wi in j2["value"] ?? Enumerable.Empty<JToken>())
                {
                    var dto = ParseWorkItem(wi);

                    // Fetch discussion comments for recent items.
                    // System.History only returns the latest revision, so we rely on
                    // the Comments API for full discussion threads.
                    int commentCount = wi["fields"]?["System.CommentCount"]?.Value<int>() ?? 0;
                    if (commentCount > 0 && dto.CreatedDate >= commentsCutoff)
                    {
                        var comments = await GetWorkItemCommentsAsync(dto.Id);
                        if (comments.Count > 0)
                            dto.Fields["_CommentsCombined"] = string.Join("\n", comments);
                    }

                    list.Add(dto);
                }

                progressCallback?.Invoke(Math.Min(i + chunkSize, totalIds), totalIds);
            }
            return list;
        }

        private static WorkItemDto ParseWorkItem(JToken wi)
        {
            // §1. Core typed properties
            // These are stored as typed properties on WorkItemDto and persisted
            // into EmbeddingCacheEntry / QueryCacheEntry for backlog & query search.
            // They provide the minimal metadata needed for display and caching.

            DateTime createdDate = DateTime.MinValue;
            var createdDateStr = wi["fields"]?["System.CreatedDate"]?.ToString();
            if (!string.IsNullOrEmpty(createdDateStr)) DateTime.TryParse(createdDateStr, out createdDate);

            var dto = new WorkItemDto
            {
                Id = wi["id"]?.Value<int>() ?? 0,
                Title = wi["fields"]?["System.Title"]?.ToString() ?? "",
                State = wi["fields"]?["System.State"]?.ToString() ?? "",
                CreatedBy = wi["fields"]?["System.CreatedBy"]?["displayName"]?.ToString()
                            ?? wi["fields"]?["System.CreatedBy"]?.ToString() ?? "",
                CreatedDate = createdDate,
                TypeName = wi["fields"]?["System.WorkItemType"]?.ToString() ?? "",
                IterationPath = wi["fields"]?["System.IterationPath"]?.ToString() ?? "",
                HtmlUrl = wi["_links"]?["html"]?["href"]?.ToString() ?? ""
            };

            // §2. ChangedDate for incremental cache updates
            // Used by EmbeddingCache and QuerySearchCache to detect whether
            // a work item has been modified since the last cache build.
            dto.Fields["System.ChangedDate"] = wi["fields"]?["System.ChangedDate"]?.ToString() ?? "";

            // §3. All fields from the API response
            // The ADO API only returns fields that have a value — empty/null fields
            // are simply absent from the JSON, which is by-design API behavior.
            // We capture everything returned here. BuildSearchableText() selects
            // the relevant rich-text fields using suffix matching (so prefix
            // differences like Custom.* vs beconnect-test.* don't matter).
            if (wi["fields"] is JObject allFields)
            {
                foreach (var prop in allFields.Properties())
                {
                    if (dto.Fields.ContainsKey(prop.Name))
                        continue;

                    var token = prop.Value;

                    // Person/identity fields are JSON objects — extract displayName
                    if (token.Type == JTokenType.Object)
                    {
                        var displayName = token["displayName"]?.ToString();
                        if (!string.IsNullOrEmpty(displayName))
                            dto.Fields[prop.Name] = displayName;
                    }
                    else if (token.Type != JTokenType.Null)
                    {
                        var val = token.ToString();
                        if (!string.IsNullOrEmpty(val))
                            dto.Fields[prop.Name] = val;
                    }
                }
            }

            // Extract file attachment URLs and names for the download feature.
            if (wi["relations"] != null)
            {
                foreach (var rel in wi["relations"]!)
                {
                    if (rel["rel"]?.ToString() == "AttachedFile")
                    {
                        var urlRef = rel["url"]?.ToString() ?? "";
                        var fileName = rel["attributes"]?["name"]?.ToString() ?? Path.GetFileName(urlRef);
                        var resourceSize = rel["attributes"]?["resourceSize"]?.Value<long>() ?? 0;
                        dto.Attachments.Add(new AttachmentDto { Url = urlRef, FileName = fileName ?? "", Length = resourceSize });
                    }
                }
            }

            return dto;
        }

        public async Task<WorkItemDto?> GetWorkItemAsync(int id)
        {
            string url = $"{baseUrl}/wit/workitems/{id}?$expand=All&api-version=7.1";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var wi = JObject.Parse(await resp.Content.ReadAsStringAsync());
            return ParseWorkItem(wi);
        }

        public async Task<string> DownloadAttachmentAsync(AttachmentDto att, string saveFolder)
        {
            Directory.CreateDirectory(saveFolder);
            string filePath = Path.Combine(saveFolder, att.FileName);
            var resp = await _http.GetAsync(att.Url + "?api-version=7.1");
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fs);
            return filePath;
        }

        public async Task<List<string>> GetWorkItemCommentsAsync(int workItemId)
        {
            var comments = new List<string>();
            string url = $"{baseUrl}/wit/workItems/{workItemId}/comments?api-version=7.1-preview.4";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return comments;

            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
            foreach (var comment in json["comments"] ?? Enumerable.Empty<JToken>())
            {
                var text = comment["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    comments.Add(text);
            }
            return comments;
        }
    }
}
