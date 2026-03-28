using System;
using System.Collections.Generic;

namespace ADO_Tools_WinUI.Models
{
    public class QueryDto
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Wiql { get; set; } = "";
        public bool IsFolder { get; set; }

        /// <summary>
        /// Child queries and subfolders. Populated for folder nodes.
        /// </summary>
        public List<QueryDto> Children { get; set; } = [];

        /// <summary>
        /// Column reference names returned by the ADO query definition
        /// (e.g. "System.Id", "System.Title", "System.State").
        /// Populated when the query is executed.
        /// </summary>
        public List<string> Columns { get; set; } = [];

        public override string ToString() => Name;
    }
}