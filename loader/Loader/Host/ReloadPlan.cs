using MelvorMods.Core;

namespace MelvorMods.Host;

/// <summary>Which mods a reload unloads and loads, in order.</summary>
/// <param name="Unload">Loaded mods to unload: dependents first (reverse load order).</param>
/// <param name="Load">Mods to load afterwards, in load order.</param>
/// <param name="Unknown">Requested ids that aren't known mods.</param>
internal sealed record ReloadPlan(List<string> Unload, List<string> Load, List<string> Unknown)
{
    // Targets plus every mod that depends on one of them, directly or not. `order` is the full load order
    // (dependencies first), loaded or not, so a mod that failed before can be loaded again.
    public static ReloadPlan Make(
        IEnumerable<string> targets,
        IReadOnlyList<Manifest> order,
        Func<string, bool> isLoaded
    )
    {
        var wanted = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(order.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
        var unknown = wanted.Where(id => !known.Contains(id)).Order(StringComparer.OrdinalIgnoreCase).ToList();

        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var load = new List<string>();
        foreach (var manifest in order)
        {
            if (wanted.Contains(manifest.Id) || manifest.Dependencies.Any(affected.Contains))
            {
                affected.Add(manifest.Id);
                load.Add(manifest.Id);
            }
        }
        var unload = load.Where(isLoaded).Reverse().ToList();
        return new ReloadPlan(unload, load, unknown);
    }
}
