using System.Collections.Generic;

namespace ADO_Tools_WinUI.Models
{
    public class WiqlQueryResult
    {
        public List<WorkItemDto> WorkItems { get; set; } = new();
        public int TotalIdsReturned { get; set; }
        public bool QueryLimitHit { get; set; }
    }
}