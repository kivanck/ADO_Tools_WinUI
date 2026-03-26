using ADO_Tools.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ADO_Tools_WinUI.Pages
{
    public class BuildInfoViewModel
    {
        public string BuildId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string DisplayVersion { get; set; } = "";
        public string Result { get; set; } = "";
        public string FinishTime { get; set; } = "";
        public int MajorVersion { get; set; }
        public int MajorVersionSequence { get; set; }
        public int MinorVersion { get; set; }
        public int MinorVersionIteration { get; set; }

        public int LatestMajorVersion { get; set; }

        public SolidColorBrush VersionBrush => MajorVersion >= LatestMajorVersion
            ? new SolidColorBrush(Colors.LimeGreen)
            : new SolidColorBrush(Colors.Orange);

        public SolidColorBrush ResultBrush => Result switch
        {
            "succeeded" => new SolidColorBrush(Colors.LimeGreen),
            "partiallySucceeded" => new SolidColorBrush(Colors.Orange),
            _ => new SolidColorBrush(Colors.IndianRed),
        };

        public static BuildInfoViewModel FromBuildInfo(TFSFunctions.BuildInfo b) => new()
        {
            BuildId = b.BuildId,
            ProductName = b.ProductName,
            DisplayVersion = b.DisplayVersion,
            Result = b.Result,
            FinishTime = b.FinishTime,
            MajorVersion = b.MajorVersion,
            MajorVersionSequence = b.MajorVersionSequence,
            MinorVersion = b.MinorVersion,
            MinorVersionIteration = b.MinorVersionIteration,
        };
    }
}
