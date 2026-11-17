namespace MelvorMods.Core;

internal static class Log
{
    private static readonly Lock _lock = new();
    private static string? _path;

    /// <summary>Opens the log file; <paramref name="empty"/> clears what an earlier run left in it.</summary>
    public static void Open(string path, bool empty = true)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (empty || !File.Exists(path))
            File.WriteAllText(path, "");
        _path = path;
    }

    /// <summary>
    /// Adds a line. Never throws: a log that can't be written (another writer has it, such as a copy of the mod still
    /// winding down after a reload, or a scanner) loses the line rather than break what was logging it.
    /// </summary>
    public static void Write(string source, string message)
    {
        if (_path == null)
            return;
        var line = System.Text.Encoding.UTF8.GetBytes($"{DateTime.Now:HH:mm:ss.fff} [{source}] {message}\n");
        lock (_lock)
            for (var attempt = 0; attempt < 3; attempt++)
                try
                {
                    using var file = new FileStream(
                        _path,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete
                    );
                    file.Write(line);
                    return;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
