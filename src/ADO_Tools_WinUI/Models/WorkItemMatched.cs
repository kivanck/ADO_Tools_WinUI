namespace ADO_Tools.Models
{
    public class WorkItemMatched
    {
        public required WorkItemDto WorkItem { get; set; }
        public int MatchScore { get; set; }
    }
}