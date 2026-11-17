using System.Reflection;
using System.Runtime.Loader;

namespace MelvorMods.Host;

// One load of one mod. Read from bytes so the files stay unlocked and a new build can load beside the old one.
// Resolves: loaded mods (their current copy), then the game's context for anything it already holds (game, Godot,
// Harmony), then the mod's own folder, then the game's context again. Never collectible: Harmony detours and
// Godot delegates keep references into old copies.
internal sealed class ModLoadContext(
    string id,
    int generation,
    string directory,
    AssemblyLoadContext shared,
    Func<string, Assembly?> loadedMod
) : AssemblyLoadContext($"MelvorMods {id} #{generation}", isCollectible: false)
{
    public Assembly LoadMain()
    {
        var dll = Path.Combine(directory, id + ".dll");
        var pdb = Path.ChangeExtension(dll, ".pdb");
        using var dllStream = new MemoryStream(File.ReadAllBytes(dll));
        using var pdbStream = File.Exists(pdb) ? new MemoryStream(File.ReadAllBytes(pdb)) : null;
        return LoadFromStream(dllStream, pdbStream);
    }

    protected override Assembly? Load(AssemblyName name)
    {
        var simple = name.Name ?? "";
        if (!simple.Equals(id, StringComparison.OrdinalIgnoreCase) && loadedMod(simple) is { } other)
            return other;
        if (shared.Assemblies.FirstOrDefault(a => a.GetName().Name == simple) is { } held)
            return held;
        var local = Path.Combine(directory, simple + ".dll");
        if (File.Exists(local))
        {
            using var stream = new MemoryStream(File.ReadAllBytes(local));
            return LoadFromStream(stream);
        }
        try
        {
            return shared.LoadFromAssemblyName(name);
        }
        catch (FileNotFoundException)
        {
            return null; // falls back to Default (framework)
        }
    }
}
