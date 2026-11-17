using GamesByMalcs.Melvor2.AutoLoads;
using Godot;
using HarmonyLib;
using MelvorMods.Core;

namespace MelvorMods.Host;

// Debug builds of the host only: reload a mod when a new build is deployed, and the `reload` dev command. For that
// the host patches the game, to learn when its scene tree is ready.
internal static partial class ModHost
{
    private static readonly CommandRegistry _commands = new(message => Log.Write("core", "WARN " + message));

    private const ulong PollMs = 500;
    private const int StablePolls = 2;

    private sealed record Stamp(long Size, DateTime Written);

    private static readonly Dictionary<string, Stamp> _stamps = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (Stamp Stamp, int Polls)> _changing = new(
        StringComparer.OrdinalIgnoreCase
    );
    private static readonly List<(HashSet<string> Ids, Action<bool, string>? Done)> _reloadQueue = [];
    private static ulong _lastPoll;

    static partial void StartDev()
    {
        _commands.Register(
            "core",
            "reload",
            "reload [id]",
            "Reloads a mod (and the mods that depend on it) from its folder; no id reloads every mod.",
            Reload,
            dev: true
        );
        new Harmony("melvormods.host").PatchAll(typeof(ModHost).Assembly);
        Log.Write("core", "reload on deploy: on");
    }

    private static void Unload(LoadedMod mod)
    {
        _mods.Remove(mod);
        try
        {
            mod.Entry.Unload();
            Log.Write("core", $"Unloaded {mod.Manifest.Id}");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"WARN {mod.Manifest.Id}: unload failed: {e}");
        }
    }

    /// <summary>
    /// Queues a reload of these mods (and their dependents) for the start of the next frame. <paramref name="done"/>
    /// gets whether every mod loaded again, and one line per mod.
    /// </summary>
    private static void RequestReload(IEnumerable<string> ids, Action<bool, string>? done = null) =>
        _reloadQueue.Add((new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase), done));

    private static void RunReloads()
    {
        if (_reloadQueue.Count == 0)
            return;
        var requests = _reloadQueue.ToList();
        _reloadQueue.Clear();
        var plan = ReloadPlan.Make(requests.SelectMany(r => r.Ids), _order, IsLoaded);
        foreach (var id in plan.Unknown)
            Log.Write("core", $"Can't reload {id}: not a known mod");
        foreach (var id in plan.Unload)
            Unload(Find(id)!);
        var lines = new List<string>();
        var allOk = plan.Unknown.Count == 0;
        foreach (var id in plan.Load)
        {
            if (Load(id, reload: true))
            {
                lines.Add($"reloaded {id} {Find(id)!.Manifest.Version}");
            }
            else
            {
                allOk = false;
                lines.Add($"{id} not loaded (see mods.log)");
            }
        }
        foreach (var (_, done) in requests)
        {
            done?.Invoke(allOk, string.Join("\n", lines));
        }
    }

    // The host's one patch: the first autoload to become ready. A mod loaded or reloaded after that is given the
    // tree, which its own ready hook has already missed, and reloads run between frames from then on.
    [HarmonyPatch(typeof(CloudConfigurationUpdater), nameof(CloudConfigurationUpdater._Ready))]
    private static class ReadyHook
    {
        private static void Postfix(CloudConfigurationUpdater __instance) => OnTreeReady(__instance.GetTree());
    }

    private static void OnTreeReady(SceneTree tree)
    {
        if (GameTree.Current != null)
            return;
        GameTree.Current = tree;
        tree.ProcessFrame += OnFrame;
    }

    private static void OnFrame()
    {
        try
        {
            PollDeploys();
            RunReloads();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"ERROR reload failed: {e}");
        }
    }

    // The deploy's files together: a change to any of them counts, and all must have settled.
    private static Stamp? Read(string id)
    {
        var dir = _dirs[id];
        long size = 0;
        var written = DateTime.MinValue;
        foreach (var name in new[] { id + ".dll", id + ".pdb", "manifest.json" })
        {
            var info = new FileInfo(Path.Combine(dir, name));
            if (!info.Exists)
            {
                if (name.EndsWith(".pdb", StringComparison.Ordinal))
                    continue;
                return null;
            }
            size += info.Length;
            if (info.LastWriteTimeUtc > written)
                written = info.LastWriteTimeUtc;
        }
        return new Stamp(size, written);
    }

    static partial void NoteStamp(string id)
    {
        if (Read(id) is { } stamp)
            _stamps[id] = stamp;
        _changing.Remove(id);
    }

    private static void PollDeploys()
    {
        var now = Time.GetTicksMsec();
        if (now - _lastPoll < PollMs)
            return;
        _lastPoll = now;
        var ready = new List<(string Id, Stamp Stamp)>();
        foreach (var id in _stamps.Keys)
        {
            Stamp? current;
            try
            {
                current = Read(id);
            }
            catch (IOException)
            {
                continue;
            }
            if (current == null || current == _stamps[id])
            {
                _changing.Remove(id);
                continue;
            }
            var polls = _changing.TryGetValue(id, out var seen) && seen.Stamp == current ? seen.Polls + 1 : 0;
            _changing[id] = (current, polls);
            if (polls >= StablePolls - 1)
                ready.Add((id, current));
        }
        if (ready.Count == 0)
            return;
        foreach (var (id, stamp) in ready)
        {
            // Taken now so a build that can't load isn't retried every poll; the next deploy changes it again.
            _changing.Remove(id);
            _stamps[id] = stamp;
            Log.Write("core", $"New build of {id} deployed, reloading");
        }
        RequestReload(ready.Select(r => r.Id));
    }

    private static void Reload(CommandCall call)
    {
        var known = _order.Select(m => m.Id).ToList();
        var ids = call.Args.Count == 0 ? known : [.. call.Args];
        var unknown = ids.Where(i => !known.Contains(i, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0)
        {
            var loaded = string.Join(", ", _mods.Select(m => m.Manifest.Id));
            call.Error(
                $"unknown mod '{string.Join("', '", unknown)}' (loaded: {(loaded.Length > 0 ? loaded : "none")})"
            );
            return;
        }
        RequestReload(ids, (ok, text) => _ = ok ? call.Ok(text) : call.Error(text));
    }
}
