namespace ADO_Tools_WinUI.Models
{
    /// <summary>
    /// Represents a single build retrieved from Azure DevOps.
    /// Version components follow the Bentley convention: Major.MajorSequence.Minor.Iteration.
    /// </summary>
    public class BuildInfo
    {
        public string BuildId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string Result { get; set; } = "";
        public string FinishTime { get; set; } = "";
        public string DisplayVersion { get; set; } = "";
        public int MajorVersion { get; set; }
        public int MajorVersionSequence { get; set; }
        public int MinorVersion { get; set; }
        public int MinorVersionIteration { get; set; }
    }
}