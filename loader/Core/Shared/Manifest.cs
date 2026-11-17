using System.Text.Json;
using System.Text.RegularExpressions;

namespace MelvorMods.Core;

/// <summary>A game build the mod was checked with: Steam's build id, and the version text the game shows.</summary>
sealed record CheckedBuild(string Build, string Version);

sealed record Manifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string[] Dependencies
)
{
    /// <summary>The game builds the mod was checked with; empty when the manifest lists none.</summary>
    public CheckedBuild[] GameBuilds { get; init; } = [];
}

// Reads a mod's manifest.json; the dev host and a mod's own entry point both use it.
internal static partial class Manifests
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public static Manifest Parse(string json)
    {
        var manifest =
            JsonSerializer.Deserialize<Manifest>(json, _json) ?? throw new InvalidDataException("manifest is empty");
        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidDataException("manifest has no id");
        if (manifest.Version == null || !VersionText().IsMatch(manifest.Version))
            throw new InvalidDataException($"manifest version '{manifest.Version}' is not a version");
        var builds = manifest.GameBuilds ?? [];
        if (builds.Any(b => string.IsNullOrWhiteSpace(b?.Build) || string.IsNullOrWhiteSpace(b.Version)))
            throw new InvalidDataException("a gameBuilds entry needs a build and a version");
        return manifest with { Dependencies = manifest.Dependencies ?? [], GameBuilds = builds };
    }

    // Three numbers, and for a pre-release a suffix after a hyphen: 0.1.0, 0.1.0-preview.1.
    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex VersionText();
}
