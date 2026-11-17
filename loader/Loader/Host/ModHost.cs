using System.Reflection;
using System.Runtime.Loader;
using MelvorMods.Core;

namespace MelvorMods.Host;

// Loads the mods installed beside the one whose Loader copy was started (<game>/mods/<Id>/), each into its own
// context. A mod compiles the shared code in and starts from its entry point, which this host calls by name, passing
// only .NET and Godot values: the host and a mod share no type. A Debug build also reloads them (ModHost.Dev.cs).
internal static partial class ModHost
{
    // What a process that can't start its mods exits with.
    private const int FailedExit = 2;

    private sealed record LoadedMod(Manifest Manifest, Assembly Assembly, ModEntryCall Entry);

    private static readonly List<LoadedMod> _mods = [];
    private static readonly Dictionary<string, int> _generations = new(StringComparer.OrdinalIgnoreCase);
    private static List<Manifest> _order = [];
    private static Dictionary<string, string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private static AssemblyLoadContext _shared = null!;

    private static Version HostVersion { get; } = typeof(ModHost).Assembly.GetName().Version!;

    /// <summary>Starts the mods, and answers in one line what it did, for the Loader's log.</summary>
    /// <param name="loaderDir">The started Loader copy's folder, <c>mods/&lt;Id&gt;/loader</c>: the host's logs go under it.</param>
    public static string Start(string loaderDir)
    {
        var modsDir = ModDiscovery.ModsDir(loaderDir);
        var logs = Path.Combine(loaderDir, "logs");
        var worker = Standalone.Worker(Standalone.ProcessArgs(), out var malformed);
        if (malformed != null)
        {
            // Started as a worker but unusable: never carry on as a second live game, and leave mods.log alone.
            Log.Open(Path.Combine(logs, "mods.worker-error.log"));
            Log.Write("core", $"Host {HostVersion}: {malformed} is not a worker flag (<mod>:<role>); exiting");
            Environment.Exit(Standalone.MalformedWorkerExit);
        }
        var log = Path.Combine(logs, worker is { } w ? $"mods.{w.Mod}.{w.Role}.log" : "mods.log");
        Log.Open(log);
        // Which copy started isn't known to a mod, so the table says where this log is.
        ModTable.Process.FirstOpen(log);
        var build = GameBuilds.ReadSteamBuild(Path.GetDirectoryName(modsDir)!);
        Log.Write(
            "core",
            $"Host {HostVersion}, game build {build}"
                + (worker is { } named ? $", worker {named.Mod}:{named.Role}" : "")
        );

        // Everything is decided before the game is touched: a pre-release mod on a build it doesn't list is never
        // loaded, and with no mod left the host patches and adds nothing.
        var problems = new List<string>();
        var installed = ModDiscovery.Discover(modsDir, problems);
        var discovered = ModDiscovery.Gate(installed, build, GameBuilds.DevBuild, problems);
        _dirs = discovered.ToDictionary(m => m.Manifest.Id, m => m.Directory, StringComparer.OrdinalIgnoreCase);
        _order = ModDiscovery.LoadOrder(discovered.Select(m => m.Manifest), problems);
        foreach (var problem in problems)
            Log.Write("core", problem);

        _shared = AssemblyLoadContext.GetLoadContext(typeof(ModHost).Assembly)!;
        if (worker is { } target)
        {
            StartWorker(target.Mod);
            return $"worker {target.Mod}:{target.Role}";
        }
        if (_order.Count == 0)
        {
            // mods.log already holds each reason on its own line; the Loader's log gets them with the answer.
            const string none = "no mod to load, the game is left alone";
            Log.Write("core", none);
            return problems.Count > 0 ? $"{none} ({string.Join("; ", problems)})" : none;
        }
        StartDev();

        var loaded = _order.Count(manifest => Load(manifest.Id, reload: false));
        var skipped = installed.Count - discovered.Count;
        return $"{loaded} of {installed.Count} mods loaded"
            + (skipped > 0 ? $", {skipped} skipped for this game build (see mods.log)" : "");
    }

    // A worker process holds only the mod it was started for, which runs the worker itself from its entry point.
    private static void StartWorker(string id)
    {
        var target = _order.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Log.Write("core", $"worker mod {id} is not installed or can't load");
            Environment.Exit(FailedExit);
        }
        _order = [target];
        if (!Load(target.Id, reload: false))
            Environment.Exit(FailedExit);
    }

    private static LoadedMod? Find(string id) =>
        _mods.FirstOrDefault(m => m.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static bool IsLoaded(string id) => Find(id) != null;

    private static Assembly? LoadedAssembly(string id) => Find(id)?.Assembly;

    private static bool Load(string id, bool reload)
    {
        var verb = reload ? "reload" : "load";
        var dir = _dirs[id];
        var manifest = _order.First(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        NoteStamp(id);
        try
        {
            if (reload)
                manifest = ModDiscovery.ParseManifest(File.ReadAllText(Path.Combine(dir, "manifest.json")));
            var missing = manifest.Dependencies.FirstOrDefault(d => !IsLoaded(d));
            if (missing != null)
            {
                Log.Write("core", $"Skipping {id}: dependency {missing} not loaded");
                return false;
            }
            var generation = _generations[id] = _generations.GetValueOrDefault(id) + 1;
            var assembly = new ModLoadContext(id, generation, dir, _shared, LoadedAssembly).LoadMain();
            if (ModEntryCall.Find(assembly) is not { } entry)
            {
                Log.Write("core", $"Failed to {verb} {id}: no {ModEntryCall.EntryType} with Load and Unload");
                return false;
            }
            if (!entry.Load(dir, GameTree.Current))
            {
                Log.Write("core", $"Failed to {verb} {id} (see {id}/logs/{Standalone.LogName(id)})");
                return false;
            }
            _mods.Add(new LoadedMod(manifest, assembly, entry));
            Log.Write("core", $"{(reload ? "Reloaded" : "Loaded")} {id} {manifest.Version} (standalone)");
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"Failed to {verb} {id}: {e}");
            return false;
        }
    }

    static partial void StartDev();

    static partial void NoteStamp(string id);
}
