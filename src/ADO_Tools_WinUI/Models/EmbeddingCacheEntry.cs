using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Plain-text content used for keyword (BM25) search.
        /// Built from Title + Description + ReproSteps + Comments etc.
        /// Null for entries created before this field was added.
        /// </summary>
        public string? SearchableText { get; set; }

        /// <summary>
        /// All string field values from the work item (e.g. AreaPath, Priority, Tags).
        /// Used for dynamic column display in search results.
        /// Empty for entries created before this field was added.
        /// </summary>
        public Dictionary<string, string> Fields { get; set; } = new();

        /// <summary>
        /// Primary embedding (first chunk, or the single embedding for short items).
        /// Kept for backward compatibility with existing cache files.
        /// </summary>
        public float[] Embedding { get; set; } = [];

        /// <summary>
        /// Additional chunk embeddings for long work items (chunks 2+).
        /// Null or empty means the item fits in a single chunk.
        /// </summary>
        public List<float[]>? ExtraEmbeddings { get; set; }
    }
}
