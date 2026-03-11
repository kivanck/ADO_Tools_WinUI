using System;

namespace ADO_Tools.Models
{
    public class EmbeddingCacheEntry
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
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
