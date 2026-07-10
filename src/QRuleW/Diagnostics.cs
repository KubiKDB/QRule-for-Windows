using System.IO;

namespace QRuleW;

/// <summary>
/// Minimal best-effort logging to %APPDATA%\QRuleW\log.txt. Used for the share flow and unhandled
/// exceptions — writes only on user actions / errors, never during scanning.
/// </summary>
internal static class Diagnostics
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QRuleW", "log.txt");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:o}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    public static void Log(string stage, Exception? ex) => Log($"{stage}: {ex}");
}
