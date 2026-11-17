using System.Text.RegularExpressions;

namespace MelvorMods.Core;

/// <summary>What a mod's list of checked game builds means for loading it into the running game.</summary>
internal enum BuildGate
{
    Load,

    /// <summary>The rule would have kept the mod out, and a dev build skipped it.</summary>
    Skipped,
    Refused,
}

/// <param name="Reason">Why the mod doesn't belong on this build, unless <see cref="Kind"/> is <see cref="BuildGate.Load"/>.</param>
internal readonly record struct BuildDecision(BuildGate Kind, string? Reason = null);

/// <summary>
/// The game build a mod runs in, and whether the mod may: a pre-release version of a mod loads only on the builds its
/// manifest lists. Godot-free and game-free, so a loader can decide before it loads the mod.
/// </summary>
internal static partial class GameBuilds
{
    /// <summary>The build id when Steam's record of the game can't be read. It matches no entry.</summary>
    public const string Unknown = "unknown";

    /// <summary>A Debug build, which skips the rule so work on a new game build can start.</summary>
    public static bool DevBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// The Steam build id of the game in <paramref name="gameDirectory"/>, or <see cref="Unknown"/>.
    /// </summary>
    // <steam library>/steamapps/common/<game> → <steam library>/steamapps/appmanifest_<app>.acf "buildid".
    public static string ReadSteamBuild(string gameDirectory)
    {
        try
        {
            var game = new DirectoryInfo(gameDirectory);
            var steamapps = game.Parent?.Parent;
            if (steamapps is not { Exists: true })
                return Unknown;
            foreach (var acf in steamapps.EnumerateFiles("appmanifest_*.acf"))
            {
                var text = File.ReadAllText(acf.FullName);
                if (!text.Contains($"\"{game.Name}\"", StringComparison.OrdinalIgnoreCase))
                    continue;
                var match = BuildId().Match(text);
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unknown keeps a bound mod out, which is the safe answer when Steam's file can't be read.
        }
        return Unknown;
    }

    /// <summary>A pre-release version (one with a suffix) is bound to the manifest's list of builds.</summary>
    public static bool IsBound(Manifest manifest) => manifest.Version.Contains('-', StringComparison.Ordinal);

    /// <summary>The manifest's entry for <paramref name="build"/>; null for an unknown build or one not listed.</summary>
    public static CheckedBuild? Listed(Manifest manifest, string build) =>
        build == Unknown ? null : manifest.GameBuilds.FirstOrDefault(b => b.Build == build);

    /// <summary>Whether the mod loads on <paramref name="build"/>; <paramref name="dev"/> skips the rule.</summary>
    public static BuildDecision Decide(Manifest manifest, string build, bool dev)
    {
        if (!IsBound(manifest) || Listed(manifest, build) != null)
            return new(BuildGate.Load);
        var reason =
            manifest.GameBuilds.Length == 0
                ? $"{manifest.Id} {manifest.Version} is a pre-release and its manifest lists no game build"
                : $"{manifest.Id} {manifest.Version} is a pre-release for game build {Names(manifest)}; "
                    + $"this is build {build}";
        return new(dev ? BuildGate.Skipped : BuildGate.Refused, reason);
    }

    /// <summary>
    /// Why a bound mod must unload now that the game's version text can be read: the entry its build id matched names
    /// another version. Null when it may stay.
    /// </summary>
    public static string? VersionMismatch(Manifest manifest, string build, string gameVersion) =>
        IsBound(manifest) && Listed(manifest, build) is { } entry && entry.Version != gameVersion
            ? $"{manifest.Id} {manifest.Version} lists game build {build} as version {entry.Version}; "
                + $"this game is version {gameVersion}"
            : null;

    /// <summary>The game's version as its version overlay shows it: the version, then the subversion if it has one.</summary>
    public static string VersionText(string version, string subversion) =>
        string.IsNullOrEmpty(subversion) ? version : $"{version}-{subversion}";

    private static string Names(Manifest manifest) =>
        string.Join(", ", manifest.GameBuilds.Select(b => $"{b.Build} ({b.Version})"));

    [GeneratedRegex("\"buildid\"\\s+\"(\\d+)\"")]
    private static partial Regex BuildId();
}
