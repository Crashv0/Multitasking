using System.Reflection;

namespace MelvorMods.Host;

/// <summary>
/// How the host starts a mod, the shape every released mod is built to: a type found by name, a static <c>Load</c>
/// given a dictionary of values that answers whether the mod started, and a static <c>Unload</c>. Mods already in
/// players' folders keep this shape for good, so it changes only by adding beside it, never in place.
/// </summary>
internal sealed class ModEntryCall
{
    public const string EntryType = "MelvorMods.Core.ModEntry";
    public const string LoadMethod = "Load";
    public const string UnloadMethod = "Unload";

    /// <summary>The mod's install folder.</summary>
    public const string DirectoryKey = "directory";

    /// <summary>The running scene tree, or null before there is one.</summary>
    public const string TreeKey = "tree";

    private const BindingFlags EntryMethods = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private readonly MethodInfo _load;
    private readonly MethodInfo _unload;

    private ModEntryCall(MethodInfo load, MethodInfo unload)
    {
        _load = load;
        _unload = unload;
    }

    /// <summary>The entry of <paramref name="assembly"/>; null when it lacks the type or either method.</summary>
    public static ModEntryCall? Find(Assembly assembly)
    {
        var entry = assembly.GetType(EntryType);
        var load = entry?.GetMethod(LoadMethod, EntryMethods);
        var unload = entry?.GetMethod(UnloadMethod, EntryMethods);
        return load != null && unload != null ? new ModEntryCall(load, unload) : null;
    }

    /// <summary>Starts the mod; true when it says it started.</summary>
    public bool Load(string directory, object? tree) =>
        _load.Invoke(null, [new Dictionary<string, object?> { [DirectoryKey] = directory, [TreeKey] = tree }]) is true;

    public void Unload() => _unload.Invoke(null, null);
}
