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
            var result = new List<QueryDto>();
            string url = $"{baseUrl}/wit/queries?$depth=2&api-version=7.1-preview.2";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            void Walk(JToken node, string parentPath)
            {
                if (node == null) return;
                foreach (var q in node["value"] ?? Enumerable.Empty<JToken>())
                {
                    //var type = q["type"]?.ToString();
                    var path = string.IsNullOrEmpty(parentPath) ? q["name"]?.ToString() : $"{parentPath}\\{q["name"]?.ToString()}";
                    if (q["queryType"] != null)
                    {
                        result.Add(new QueryDto
                        {
                            Id = q["id"]?.ToString(),
                            Name = q["name"]?.ToString(),
                            Path = path,
                            Wiql = q["_links"]?["wiql"]?["href"]?.ToString()
                        });
                    }
                    if (q["children"] != null && q["children"].Any())
                    {
                        foreach (var child in q["children"])
                        {
                            // child may be a folder or query; create a wrapper and recurse
                            Walk(new JObject(new JProperty("value", new JArray(child))), path);
                        }
                    }
                }
            }

            Walk(json, "");
            return result;
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

            result.WorkItemIds = json["workItems"]?
                .Select(x => (int?)x["id"])
                .Where(i => i.HasValue)
                .Select(i => i!.Value)
                .ToList() ?? [];

            return result;
        }

        public async Task<QueryExecutionResult> QueryWorkItemsAsync(string savedQueryUrl)
        {
            var result = new QueryExecutionResult();
            if (string.IsNullOrWhiteSpace(savedQueryUrl)) return result;

            // Step 1: Execute the saved query directly
            var resp = await _http.GetAsync($"{savedQueryUrl}?api-version=7.1");
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            // Capture column definitions from the query
            result.Columns = json["columns"]?
                .Select(c => c["referenceName"]?.ToString() ?? "")
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList() ?? [];

            var ids = json["workItems"]?.Select(x => (int?)x["id"])?.Where(i => i.HasValue)?.Select(i => i.Value).ToList();
            if (ids == null || ids.Count == 0) return result;

            result.WorkItems = await FetchWorkItemsByIdsAsync(ids);
            return result;
        }

        public async Task<List<WorkItemDto>> QueryByWiqlAsync(string wiql, int top = 20000, Action<int, int>? progressCallback = null)
        {
            var list = new List<WorkItemDto>();
            if (string.IsNullOrWhiteSpace(wiql)) return list;

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

            var ids = json["workItems"]?.Select(x => (int?)x["id"])?.Where(i => i.HasValue)?.Select(i => i.Value).ToList();
            if (ids == null || ids.Count == 0) return list;

            return await FetchWorkItemsByIdsAsync(ids, progressCallback);
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
            var commentsCutoff = new DateTime(2024, 1, 1);
            int totalIds = ids.Count;

            for (int i = 0; i < ids.Count; i += chunkSize)
            {
                var chunk = ids.Skip(i).Take(chunkSize).ToList();
                string idsCsv = string.Join(",", chunk);
                string getUrl = $"{baseUrl}/wit/workitems?ids={idsCsv}&$expand=All&api-version=7.1";
                var r2 = await _http.GetAsync(getUrl);
                r2.EnsureSuccessStatusCode();
                var j2 = JObject.Parse(await r2.Content.ReadAsStringAsync());

                foreach (var wi in j2["value"])
                {
                    var dto = ParseWorkItem(wi);

                    // Fetch discussion comments only for recent items with meaningful discussion
                    int commentCount = wi["fields"]?["System.CommentCount"]?.Value<int>() ?? 0;
                    if (commentCount > 1 && dto.CreatedDate >= commentsCutoff)
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
            // ?? 1. Core typed properties ????????????????????????????????
            // These are stored as typed properties on WorkItemDto and persisted
            // into EmbeddingCacheEntry / QueryCacheEntry for backlog & query search.
            // They provide the minimal metadata needed for display and caching.

            DateTime createdDate = DateTime.MinValue;
            var createdDateStr = wi["fields"]?["System.CreatedDate"]?.ToString();
            if (!string.IsNullOrEmpty(createdDateStr)) DateTime.TryParse(createdDateStr, out createdDate);

            var dto = new WorkItemDto
            {
                Id = wi["id"].Value<int>(),
                Title = wi["fields"]?["System.Title"]?.ToString(),
                State = wi["fields"]?["System.State"]?.ToString(),
                CreatedBy = wi["fields"]?["System.CreatedBy"]?["displayName"]?.ToString()
                            ?? wi["fields"]?["System.CreatedBy"]?.ToString(),
                CreatedDate = createdDate,
                TypeName = wi["fields"]?["System.WorkItemType"]?.ToString(),
                IterationPath = wi["fields"]?["System.IterationPath"]?.ToString(),
                HtmlUrl = wi["_links"]?["html"]?["href"]?.ToString()
            };

            // ?? 2. ChangedDate for incremental cache updates ????????????
            // Used by EmbeddingCache and QuerySearchCache to detect whether
            // a work item has been modified since the last cache build.
            dto.Fields["System.ChangedDate"] = wi["fields"]?["System.ChangedDate"]?.ToString() ?? "";

            // ?? 3. Rich-text fields for semantic / BM25 search ??????????
            // These HTML-heavy fields are stripped to plain text and combined
            // into SearchableText by SemanticSearchService.BuildSearchableText().
            string[] searchFields =
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
                "Custom.DefectSource_EA"
            };
            foreach (var fieldName in searchFields)
            {
                var val = wi["fields"]?[fieldName]?.ToString();
                if (!string.IsNullOrEmpty(val))
                    dto.Fields[fieldName] = val;
            }

            // ?? 4. All remaining fields for dynamic query column display ?
            // The ADO API returns every field in the JSON response. This loop
            // captures anything not already stored above, so that when a query
            // defines columns like AssignedTo, Priority, Tags, etc., the values
            // are available in dto.Fields ? WorkItemRow.FieldValues ? DataGrid.
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
                foreach (var rel in wi["relations"])
                {
                    if (rel["rel"]?.ToString() == "AttachedFile")
                    {
                        var urlRef = rel["url"]?.ToString();
                        var fileName = rel["attributes"]?["name"]?.ToString() ?? Path.GetFileName(urlRef ?? string.Empty);
                        dto.Attachments.Add(new AttachmentDto { Url = urlRef, FileName = fileName });
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
