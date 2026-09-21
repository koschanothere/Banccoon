namespace Banccoon.App.Diagnostics;

public static class DiagnosticLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Banccoon",
        "diagnostics.log");

    public static void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort diagnostics only - logging itself must never be a new failure point.
        }
    }
}
