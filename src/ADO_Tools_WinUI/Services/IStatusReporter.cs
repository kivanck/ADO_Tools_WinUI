using System;

namespace ADO_Tools_WinUI.Services
{
    /// <summary>
    /// Common interface for services that report status messages to the UI.
    /// Implemented by <see cref="InstallFunctions"/>, <see cref="BuildDownloadService"/>,
    /// and <see cref="SemanticSearchService"/>.
    /// </summary>
    public interface IStatusReporter
    {
        event Action<string>? StatusUpdated;
    }
}
