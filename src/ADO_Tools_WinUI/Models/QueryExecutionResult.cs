using System.Collections.Generic;

namespace ADO_Tools.Models
{
    /// <summary>
    /// Result of executing an ADO saved query, including the column
    /// definitions chosen by the user in the query editor.
    /// </summary>
    public class QueryExecutionResult
    {
        public List<WorkItemDto> WorkItems { get; set; } = [];
        public List<string> Columns { get; set; } = [];
    }
}
