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
