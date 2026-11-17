using System.Text.Json;
using System.Text.Json.Serialization;

namespace MelvorMods.Core;

// Pure settings file handling; ModContext supplies the path and log.
internal sealed class ModConfig(string path, Action<string> warn, Action<string> error)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Path { get; } = path;

    // Missing file: defaults, nothing written. Unreadable: defaults, file left alone. Corrupt: moved to .bad.
    public T Load<T>()
        where T : class, new()
    {
        if (!File.Exists(Path))
            return new T();
        string text;
        try
        {
            text = File.ReadAllText(Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn($"Could not read settings {Path}, using defaults: {e.Message}");
            return new T();
        }
        try
        {
            return JsonSerializer.Deserialize<T>(text, _json) ?? throw new JsonException("settings file is null");
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            SetAside(e.Message);
            return new T();
        }
    }

    // Serialises first, then writes a temp file and renames it over the old one.
    public bool Save<T>(T settings)
    {
        var tmp = Path + ".tmp";
        try
        {
            var text = JsonSerializer.Serialize(settings, _json);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(tmp, text);
            File.Move(tmp, Path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error($"Could not save settings {Path}: {e.Message}");
            TryDelete(tmp);
            return false;
        }
    }

    private void SetAside(string reason)
    {
        var bad = Path + ".bad";
        try
        {
            File.Move(Path, bad, overwrite: true);
            warn($"Bad settings file moved to {bad}, using defaults: {reason}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn($"Bad settings file {Path}, using defaults: {reason} (could not move it aside: {e.Message})");
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
