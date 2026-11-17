using System.Reflection;
using Godot;
using HarmonyLib;

namespace MelvorMods.Core;

interface IMod
{
    void Load(ModContext context);

    /// <summary>
    /// Called when the mod is unloaded (a reload, or a live switch-off), after Core has removed its frame
    /// callbacks, commands, menu entries, owned nodes and Harmony patches. Undo anything else here, such as theme edits.
    /// </summary>
    void Unload(ModContext context) { }
}

sealed partial class ModContext
{
    private readonly Assembly _assembly;
    private readonly ModConfig _config;
    private readonly List<Action<SceneTree>> _pendingReady = [];
    private readonly string _root;

    internal ModContext(
        Manifest manifest,
        string directory,
        Assembly assembly,
        string root,
        CommandRegistry commands,
        ModHooks hooks,
        bool gameBuildListed
    )
    {
        GameBuildListed = gameBuildListed;
        Hooks = hooks;
        _root = root;
        Manifest = manifest;
        Directory = directory;
        _assembly = assembly;
        Log = new ModLog(manifest.Id);
        Harmony = new Harmony($"melvormods.{manifest.Id.ToLowerInvariant()}");
        _config = new ModConfig(Path.Combine(root, Standalone.SettingsFile), Log.Warn, Log.Error);
        Commands = new ModCommands(commands, manifest.Id);
        Text = new ModText(manifest.Id, directory, Log.Warn);
    }

    internal ModHooks Hooks { get; }

    public Manifest Manifest { get; }
    public string Id => Manifest.Id;
    public string Directory { get; }

    /// <summary>
    /// The mod's own folder for its settings, logs and other files: the folder it is installed in, unless the mod's
    /// loader gave another.
    /// </summary>
    public string DataRoot => _root;

    /// <summary>The game's folder: the one holding its exe.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "Part of a mod's folders, beside DataRoot"
    )]
    public string GameDirectory =>
        Standalone.GameDirectory(
            System.Environment.ProcessPath ?? throw new InvalidOperationException("no process path")
        );

    /// <summary>
    /// Whether the running game's Steam build is one the manifest lists as checked (<c>gameBuilds</c>). False for a
    /// build that can't be identified and for a manifest with no list. A pre-release version only ever loads on a
    /// listed build; a full version loads anywhere and can warn from this.
    /// </summary>
    public bool GameBuildListed { get; }
    public ModLog Log { get; }
    public Harmony Harmony { get; }

    /// <summary>The shared command registry; commands registered here belong to this mod.</summary>
    public ModCommands Commands { get; }

    /// <summary>
    /// The mod's translated text: its messages looked up in its own catalogues (<c>data/locale/*.po</c>) for the
    /// game's language, and formatted as the game formats its own.
    /// </summary>
    public ModText Text { get; }

    /// <summary>The mod's settings file, <c>&lt;Id&gt;/settings.json</c>. It survives reinstalls.</summary>
    public string ConfigPath => _config.Path;

    /// <summary>
    /// Loads the mod's settings. A missing file or missing fields give the defaults set by <c>new T()</c>,
    /// and unknown fields are ignored. A file that can't be parsed is renamed to <c>.json.bad</c>, a warning
    /// is logged and defaults are returned. Never throws for file problems. Call from the main thread.
    /// </summary>
    public T LoadConfig<T>()
        where T : class, new() => _config.Load<T>();

    /// <summary>
    /// Saves the mod's settings as indented JSON. The write is atomic: an interrupted save leaves the
    /// previous file intact. Returns false and logs an error if the file can't be written. Call from the
    /// main thread, on discrete actions rather than every frame.
    /// </summary>
    public bool SaveConfig<T>(T settings) => _config.Save(settings);

    /// <summary>
    /// Runs <paramref name="callback"/> once the game's scene tree is ready (the main menu's autoloads have run),
    /// or straight away if it already is, as on a reload. Use this instead of patching an autoload's <c>_Ready</c>.
    /// </summary>
    public void OnReady(Action<SceneTree> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!Hooks.Active)
            return;
        if (GameTree.Current is { } tree)
            callback(tree);
        else
            _pendingReady.Add(callback);
    }

    /// <summary>
    /// Runs <paramref name="callback"/> every frame on the main thread until the mod unloads. Use this instead of
    /// subscribing to <c>SceneTree.ProcessFrame</c>, which would survive a reload. A callback that throws three
    /// frames in a row is removed.
    /// </summary>
    public void OnFrame(Action callback) => Hooks.AddFrame(callback);

    /// <summary>
    /// Runs <paramref name="callback"/> on the main thread for each node entering the scene tree, before it first
    /// draws, until the mod unloads. Use this instead of subscribing to <c>SceneTree.NodeAdded</c>, which would
    /// survive a reload. Nodes already in the tree are not replayed: walk the tree yourself from <see cref="OnReady"/>.
    /// A throw is logged and the callback keeps running for later nodes.
    /// </summary>
    public void OnNodeAdded(Action<Node> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Hooks.AddNodeAdded(node => callback((Node)node));
    }

    /// <summary>
    /// Runs <paramref name="callback"/> once on the main thread when the game quits, while its scene tree is being
    /// taken down and Godot's servers still run; frame and node-added callbacks stop first. Godot objects a mod still
    /// holds from C# (fonts, textures, themes it made) are otherwise freed only after the servers they belong to are
    /// gone, which crashes the game as it closes: put back what the mod changed in shared resources and dispose what
    /// it made here. Not called on an unload, where <see cref="IMod.Unload"/> runs instead.
    /// </summary>
    public void OnExit(Action callback) => Hooks.AddExit(callback);

    /// <summary>Marks a node the mod added; Core frees it when the mod unloads. Returns the node.</summary>
    public T Own<T>(T node)
        where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        Hooks.AddOwned(() =>
        {
            if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
                node.QueueFree();
        });
        return node;
    }

    // Runs ready callbacks queued before the tree was ready; each failure is logged and the rest still run.
    internal void RunPendingReady(SceneTree tree)
    {
        var pending = _pendingReady.ToList();
        _pendingReady.Clear();
        foreach (var callback in pending)
        {
            if (!Hooks.Active)
                return;
            try
            {
                callback(tree);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Error($"ready callback failed: {e}");
            }
        }
    }

    internal void Unpatch() => Harmony.UnpatchAll(Harmony.Id);

    // The mod's entry in the process's mod table: who it is, where it lives, and its text for dev tools.
    internal IReadOnlyDictionary<string, object> TableRecord()
    {
        Func<int> messages = () => Text.MessageCount;
        Func<IReadOnlyList<string>> locales = () => Text.CatalogueLocales;
        Func<bool> pseudo = () => Text.Pseudo;
        Action<bool> setPseudo = on => Text.Pseudo = on;
        Func<string?> language = () => Text.Language;
        Action<string?> setLanguage = locale => Text.Language = locale;
        return new Dictionary<string, object>
        {
            [ModTable.Version] = Manifest.Version,
            [ModTable.Directory] = Directory,
            [ModTable.DataRoot] = _root,
            [ModTable.TextMessages] = messages,
            [ModTable.TextLocales] = locales,
            [ModTable.TextPseudo] = pseudo,
            [ModTable.TextSetPseudo] = setPseudo,
            [ModTable.TextLanguage] = language,
            [ModTable.TextSetLanguage] = setLanguage,
        };
    }

    // Applies the mod's own [HarmonyPatch] classes. The shared code's, which carry a category, are applied by its host.
    public void PatchAll()
    {
        Harmony.PatchAllUncategorized(_assembly);
        Log.Info($"{Harmony.GetPatchedMethods().Count()} methods patched");
    }
}

sealed class ModLog(string id)
{
    public void Info(string message) => Core.Log.Write(id, message);

    public void Warn(string message) => Core.Log.Write(id, "WARN " + message);

    public void Error(string message) => Core.Log.Write(id, "ERROR " + message);
}
