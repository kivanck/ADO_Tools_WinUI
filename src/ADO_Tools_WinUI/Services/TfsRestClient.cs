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

        public async Task<List<WorkItemDto>> QueryWorkItemsAsync(string savedQueryUrl)
        {
            var list = new List<WorkItemDto>();
            if (string.IsNullOrWhiteSpace(savedQueryUrl)) return list;

            // Step 1: Execute the saved query directly
            var resp = await _http.GetAsync($"{savedQueryUrl}?api-version=7.1");
            resp.EnsureSuccessStatusCode();
            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());

            var ids = json["workItems"]?.Select(x => (int?)x["id"])?.Where(i => i.HasValue)?.Select(i => i.Value).ToList();
            if (ids == null || ids.Count == 0) return list;

            // Step 2: Fetch work item details
            var chunkSize = 200;
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
                    DateTime createdDate = DateTime.MinValue;
                    var createdDateStr = wi["fields"]?["System.CreatedDate"]?.ToString();
                    if (!string.IsNullOrEmpty(createdDateStr)) DateTime.TryParse(createdDateStr, out createdDate);

                    var dto = new WorkItemDto
                    {
                        Id = wi["id"].Value<int>(),
                        Title = wi["fields"]?["System.Title"]?.ToString(),
                        State = wi["fields"]?["System.State"]?.ToString(),
                        CreatedBy = wi["fields"]?["System.CreatedBy"]?["displayName"]?.ToString() ?? wi["fields"]?["System.CreatedBy"]?.ToString(),
                        CreatedDate = createdDate,
                        TypeName = wi["fields"]?["System.WorkItemType"]?.ToString(),
                        IterationPath = wi["fields"]?["System.IterationPath"]?.ToString(),
                        HtmlUrl = wi["_links"]?["html"]?["href"]?.ToString()
                    };

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

                    list.Add(dto);
                }
            }

            return list;
        }

        public async Task<WorkItemDto?> GetWorkItemAsync(int id)
        {
            string url = $"{baseUrl}/wit/workitems/{id}?$expand=All&api-version=7.1";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var wi = JObject.Parse(await resp.Content.ReadAsStringAsync());

            DateTime createdDate = DateTime.MinValue;
            var createdDateStr = wi["fields"]?["System.CreatedDate"]?.ToString();
            if (!string.IsNullOrEmpty(createdDateStr)) DateTime.TryParse(createdDateStr, out createdDate);

            var dto = new WorkItemDto
            {
                Id = wi["id"].Value<int>(),
                Title = wi["fields"]?["System.Title"]?.ToString(),
                State = wi["fields"]?["System.State"]?.ToString(),
                CreatedBy = wi["fields"]?["System.CreatedBy"]?["displayName"]?.ToString(),
                CreatedDate = createdDate,
                TypeName = wi["fields"]?["System.WorkItemType"]?.ToString(),
                IterationPath = wi["fields"]?["System.IterationPath"]?.ToString(),
                HtmlUrl = wi["_links"]?["html"]?["href"]?.ToString()
            };
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
    }
}
