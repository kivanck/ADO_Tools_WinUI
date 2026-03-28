namespace ADO_Tools_WinUI.Services
{
    /// <summary>
    /// Parses Bentley-style version strings (e.g. "23.09.02.015") into four numeric components.
    /// </summary>
    public static class VersionParser
    {
        public static (int Major, int MajorSequence, int Minor, int Iteration) Parse(string? version)
        {
            var parts = (version ?? "0.0.0.0").Split('.');
            return (
                parts.Length > 0 && int.TryParse(parts[0], out var maj) ? maj : 0,
                parts.Length > 1 && int.TryParse(parts[1], out var seq) ? seq : 0,
                parts.Length > 2 && int.TryParse(parts[2], out var min) ? min : 0,
                parts.Length > 3 && int.TryParse(parts[3], out var iter) ? iter : 0
            );
        }
    }
}