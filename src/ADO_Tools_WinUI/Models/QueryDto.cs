using System;
using System.Collections.Generic;

namespace ADO_Tools.Models
{
    public class QueryDto
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public string Wiql { get; set; }

        /// <summary>
        /// Column reference names returned by the ADO query definition
        /// (e.g. "System.Id", "System.Title", "System.State").
        /// Populated when the query is executed.
        /// </summary>
        public List<string> Columns { get; set; } = [];
    }
}