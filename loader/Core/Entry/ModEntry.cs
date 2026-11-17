using GamesByMalcs.Melvor2.AutoLoads;
using Godot;
using HarmonyLib;

namespace MelvorMods.Core;

/// <summary>
/// The mod's own start and stop, compiled into each standalone mod. Whatever loads the mod calls these: our dev host
/// now, an adapter for the official mod API later. Values come by name and are .NET or Godot types only, so the caller
/// shares no type with the mod: <c>directory</c> (string, required) is the mod's install folder, <c>dataRoot</c>
/// (string) its data folder (the install folder when not given), and <c>tree</c> (SceneTree) the
/// running game's scene tree, when it is already up.
/// </summary>
internal static partial class ModEntry
{
    private static ModContext? _context;
    private static IMod? _mod;
    private static CommandRegistry? _commands;
    private static SceneTree? _tree;
    private static string _build = GameBuilds.Unknown;

    // The mod's id: the source of the shared code's lines in the mod's log.
    private static string _id = "core";

    /// <summary>
    /// Loads the mod; false when it didn't load (another mod's worker, a pre-release on a game build it doesn't list,
    /// or a failure its log names). Ends the process
    /// when it was started with a worker flag that can't be read.
    /// </summary>
    internal static bool Load(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (_context != null)
            return true;
        var directory = Value<string>(values, "directory") ?? throw new ArgumentException("no directory given");
        var manifest = Manifests.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        // Before anything is opened: in another mod's worker this mod isn't here at all.
        var worker = Standalone.Decide(Standalone.ProcessArgs(), manifest.Id);
        if (worker.Kind == WorkerKind.Other)
            return false;
        _id = manifest.Id;
        var dataRoot = Standalone.DataRoot(directory, Value<string>(values, "dataRoot"));
        if (worker.Kind == WorkerKind.Malformed)
        {
            // Started as a worker but unusable: it must never carry on as a second live game. Whichever mod a loader
            // starts first ends the process, in a log of its own so the live game's is left alone.
            Log.Open(Path.Combine(dataRoot, "logs", Standalone.LogName(manifest.Id, "worker-error")));
            Log.Write(_id, $"{manifest.Id} {manifest.Version}: {worker.Flag} is not a worker flag (<mod>:<role>); exiting");
            System.Environment.Exit(Standalone.MalformedWorkerExit);
        }
        var logPath = Path.Combine(dataRoot, "logs", Standalone.LogName(manifest.Id, worker.Role));
        Log.Open(logPath, empty: ModTable.Process.FirstOpen(logPath));
        Log.Write(_id, $"{manifest.Id} {manifest.Version}, standalone" + (worker.Role is { } r ? $", worker {r}" : ""));
        // Before anything but the log: a pre-release stays out of a game build it wasn't checked with, whoever loads it.
        var exe = System.Environment.ProcessPath ?? throw new InvalidOperationException("no exe");
        _build = GameBuilds.ReadSteamBuild(Standalone.GameDirectory(exe));
        var gate = GameBuilds.Decide(manifest, _build, GameBuilds.DevBuild);
        if (gate.Kind == BuildGate.Refused)
        {
            Log.Write(_id, $"not loaded: {gate.Reason}");
            return false;
        }
        if (gate.Kind == BuildGate.Skipped)
            Log.Write(_id, $"{gate.Reason}; loading anyway (Debug build)");
        if (worker.Kind == WorkerKind.Own)
        {
            var started = false;
            RunAsWorker(manifest, directory, dataRoot, worker.Role!, ref started);
            if (!started)
                Log.Write(_id, $"{manifest.Id} can't run as a worker");
            return started;
        }

        var assembly = typeof(ModEntry).Assembly;
        var types = assembly
            .GetTypes()
            .Where(t => typeof(IMod).IsAssignableFrom(t) && t is { IsAbstract: false })
            .ToList();
        if (types.Count != 1)
        {
            Log.Write(_id, $"ERROR expected one IMod class, found {types.Count}");
            return false;
        }
        var hooks = new ModHooks(manifest.Id, message => Log.Write(_id, "WARN " + message));
        var commands = new CommandRegistry(message => Log.Write(_id, "WARN " + message));
        var listed = GameBuilds.Listed(manifest, _build) != null;
        if (manifest.GameBuilds.Length > 0)
            Log.Write(_id, $"game build {_build} is {(listed ? "" : "not ")}on the manifest's list");
        var context = new ModContext(manifest, directory, assembly, dataRoot, commands, hooks, listed);
        // The shared code's own patches first: the ready hook must be in place before the game's tree is.
        context.Harmony.PatchCategory(assembly, Standalone.PatchCategory);
        if (Value<SceneTree>(values, "tree") is { } running)
            GameTree.Current = running;
        if (GameTree.Current != null)
        {
            TextHost.Start();
            context.Text.Attach();
        }
        IMod? mod = null;
        var ok = ModLoading.LoadGuarded(
            hooks,
            commands,
            () =>
            {
                mod = (IMod)Activator.CreateInstance(types[0])!;
                mod.Load(context);
            },
            context.Unpatch,
            message => Log.Write(_id, message)
        );
        if (!ok)
        {
            context.Text.Detach();
            return false;
        }
        (_context, _mod, _commands) = (context, mod, commands);
        ModTable.Process.AddMod(manifest.Id, context.TableRecord());
        if (GameTree.Current is { } tree)
            OnTreeReady(tree);
        // False when the game was already up and its version sent the mod straight out again.
        return _context != null;
    }

    /// <summary>Unloads the mod: its hooks, commands, entries, owned nodes and patches go, then its own Unload runs.</summary>
    internal static void Unload()
    {
        if (_context is not { } context)
            return;
        if (_tree is { } tree && GodotObject.IsInstanceValid(tree))
        {
            tree.ProcessFrame -= OnFrame;
            tree.NodeAdded -= OnNodeAdded;
            if (GodotObject.IsInstanceValid(tree.Root))
                tree.Root.TreeExiting -= OnExit;
        }
        _tree = null;
        var mod = _mod;
        var failed = context.Hooks.Unload(
            () => _commands?.RemoveOwner(context.Id),
            context.Unpatch,
            () => mod?.Unload(context)
        );
        try
        {
            context.Text.Detach();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            failed++;
            Log.Write(_id, $"WARN removing the translations failed: {e}");
        }
        ModTable.Process.RemoveMod(context.Id);
        ModTable.Process.ForgetExit(context.Id);
        Log.Write(_id, "unloaded" + (failed > 0 ? $" ({failed} cleanup steps failed)" : ""));
        (_context, _mod, _commands) = (null, null, null);
    }

    private static T? Value<T>(IReadOnlyDictionary<string, object?> values, string key)
        where T : class => values.TryGetValue(key, out var value) ? value as T : null;

    // The game's tree is up: text, frame, node and exit hooks, then the mod's ready callbacks.
    private static void OnTreeReady(SceneTree tree)
    {
        if (_context is not { } context || _tree != null)
            return;
        if (WrongGameVersion(context.Manifest))
        {
            Unload();
            return;
        }
        _tree = tree;
        GameTree.Current = tree;
        try
        {
            TextHost.Start();
            context.Text.Attach();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write(_id, $"WARN the translations couldn't be loaded: {e}");
        }
        tree.ProcessFrame += OnFrame;
        tree.NodeAdded += OnNodeAdded;
        tree.Root.TreeExiting += OnExit;
        ModTable.Process.JoinExit(context.Id);
        StartMenu(tree);
        context.RunPendingReady(tree);
    }

    // The game's version text can't be read before the engine is up, so a pre-release is checked against it here,
    // ahead of everything the mod does at ready. A build id is one version, so this only fires for a wrong list.
    private static bool WrongGameVersion(Manifest manifest)
    {
        var version = GameBuilds.VersionText(
            GamesByMalcs.Melvor2.Core.Constants.GameVersion,
            GamesByMalcs.Melvor2.Core.Constants.GameSubversion
        );
        if (GameBuilds.VersionMismatch(manifest, _build, version) is not { } reason)
            return false;
        Log.Write(_id, GameBuilds.DevBuild ? $"{reason}; staying loaded (Debug build)" : $"{reason}; unloading");
        return !GameBuilds.DevBuild;
    }

    private static void OnFrame() => _context?.Hooks.RunFrame();

    private static void OnNodeAdded(Node node)
    {
        try
        {
            MenuNodeAdded(node);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write(_id, $"WARN pause menu entries failed: {e}");
        }
        _context?.Hooks.RunNodeAdded(node);
    }

    // The game is quitting: this mod's exit callbacks, and the one full collection if every other mod has run its
    // own, so Godot objects C# no longer reaches are freed while Godot's servers still run.
    private static void OnExit()
    {
        if (_context is not { } context)
            return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        context.Hooks.RunExit();
        var last = ModTable.Process.LeaveExit(context.Id);
        if (last)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Log.Write(
            _id,
            $"game quitting: exit callbacks{(last ? " and the collection" : "")} in {watch.ElapsedMilliseconds} ms"
        );
    }

    // The pause-menu entries, when the mod compiles them in.
    static partial void StartMenu(SceneTree tree);

    static partial void MenuNodeAdded(Node node);

    // A mod that runs part of itself in a worker of its own takes over here, and sets started.
    static partial void RunAsWorker(
        Manifest manifest,
        string directory,
        string dataRoot,
        string role,
        ref bool started
    );

    [HarmonyPatchCategory(Standalone.PatchCategory)]
    [HarmonyPatch(typeof(CloudConfigurationUpdater), nameof(CloudConfigurationUpdater._Ready))]
    private static class ReadyHook
    {
        private static void Postfix(CloudConfigurationUpdater __instance) => OnTreeReady(__instance.GetTree());
    }
}
