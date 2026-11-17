using System.Text.Json;
using MelvorMods.Core;

namespace MelvorMods.Host;

internal sealed record DiscoveredMod(Manifest Manifest, string Directory);

// Pure discovery and ordering; ModHost does the loading and logging.
internal static class ModDiscovery
{
    public static Manifest ParseManifest(string json) => Manifests.Parse(json);

    // The started Loader copy is mods/<Id>/loader, so every mod's folder sits beside the one that brought it.
    public static string ModsDir(string loaderDir) =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(loaderDir)))
        ?? throw new ArgumentException("not a mod's loader folder", nameof(loaderDir));

    // Reads mods/<dir>/manifest.json; bad manifests are reported and skipped. A folder with no manifest is not a mod:
    // the dev Loader's own folder holds only its Loader copy.
    public static List<DiscoveredMod> Discover(string modsDir, List<string> problems)
    {
        var mods = new List<DiscoveredMod>();
        if (!System.IO.Directory.Exists(modsDir))
            return mods;
        foreach (var dir in System.IO.Directory.GetDirectories(modsDir).Order(StringComparer.OrdinalIgnoreCase))
        {
            var file = Path.Combine(dir, "manifest.json");
            if (!File.Exists(file))
                continue;
            try
            {
                var manifest = ParseManifest(File.ReadAllText(file));
                if (mods.Any(m => m.Manifest.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"Duplicate mod id {manifest.Id} in {dir}");
                else
                    mods.Add(new DiscoveredMod(manifest, dir));
            }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                problems.Add($"Bad manifest {file}: {e.Message}");
            }
        }
        return mods;
    }

    // The mods that may load on this game build. A pre-release on a build it doesn't list is dropped with one line
    // saying why; a dev host keeps it and says the rule was skipped.
    public static List<DiscoveredMod> Gate(List<DiscoveredMod> mods, string build, bool dev, List<string> problems)
    {
        var kept = new List<DiscoveredMod>();
        foreach (var mod in mods)
        {
            var decision = GameBuilds.Decide(mod.Manifest, build, dev);
            if (decision.Kind == BuildGate.Refused)
            {
                problems.Add($"Skipping {mod.Manifest.Id}: {decision.Reason}");
                continue;
            }
            if (decision.Kind == BuildGate.Skipped)
                problems.Add($"{decision.Reason}; loading it anyway (Debug host)");
            kept.Add(mod);
        }
        return kept;
    }

    // Dependencies before dependents, otherwise alphabetical; mods with missing or circular dependencies are dropped.
    public static List<Manifest> LoadOrder(IEnumerable<Manifest> manifests, List<string> problems)
    {
        var byId = manifests.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var order = new List<Manifest>();
        var state = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string id)
        {
            if (state.TryGetValue(id, out var done))
                return done;
            if (!byId.TryGetValue(id, out var manifest))
                return false;
            state[id] = false;
            foreach (var dep in manifest.Dependencies)
            {
                if (!Visit(dep))
                {
                    problems.Add($"Skipping {id}: dependency {dep} missing, circular or skipped");
                    return false;
                }
            }
            state[id] = true;
            order.Add(manifest);
            return true;
        }

        foreach (var id in byId.Keys.Order(StringComparer.OrdinalIgnoreCase))
            Visit(id);
        return order;
    }
}
