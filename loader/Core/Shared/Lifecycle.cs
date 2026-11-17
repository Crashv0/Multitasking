namespace MelvorMods.Core;

/// <summary>
/// The hooks Core holds for one loaded copy of a mod, as plain delegates so they can be tested without Godot.
/// Unload runs the cleanup in a fixed order and keeps going when a step throws.
/// </summary>
internal sealed class ModHooks(string id, Action<string> warn)
{
    /// <summary>A frame callback that throws this many frames in a row is removed.</summary>
    public const int MaxFailures = 3;

    private sealed class Frame(Action run)
    {
        public Action Run { get; } = run;
        public int Failures { get; set; }
    }

    private readonly List<Frame> _frames = [];
    private readonly List<Action<object>> _nodes = [];
    private readonly List<Action> _owned = [];
    private readonly List<Action> _exits = [];

    public string Id { get; } = id;

    /// <summary>False once unloaded: no more frames run and new hooks are refused.</summary>
    public bool Active { get; private set; } = true;

    public int FrameCount => _frames.Count;

    public int NodeCallbackCount => _nodes.Count;

    public void AddFrame(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (Active)
            _frames.Add(new Frame(callback));
    }

    /// <summary>Adds a callback for each node entering the tree (a Godot <c>Node</c>, kept as object for tests).</summary>
    public void AddNodeAdded(Action<object> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (Active)
            _nodes.Add(callback);
    }

    /// <summary>Runs the node-added callbacks for one node. A throw is logged and the rest still run; unlike
    /// frame callbacks, a throwing one is kept, since a failure is about one node, not a stuck state.</summary>
    public void RunNodeAdded(object node)
    {
        if (!Active || _nodes.Count == 0)
            return;
        foreach (var callback in _nodes.ToArray())
        {
            if (!Active)
                return;
            try
            {
                callback(node);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                warn($"{Id}: node-added callback failed: {e}");
            }
        }
    }

    /// <summary>Adds a step that frees something the mod owns (a node); run on unload.</summary>
    public void AddOwned(Action free)
    {
        ArgumentNullException.ThrowIfNull(free);
        if (Active)
            _owned.Add(free);
    }

    /// <summary>Adds a callback for when the game quits; see <see cref="RunExit"/>.</summary>
    public void AddExit(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (Active)
            _exits.Add(callback);
    }

    /// <summary>
    /// The game is quitting: stops the frame and node-added callbacks, then runs the exit callbacks once, newest
    /// first. A throw is logged and the rest still run. Nothing runs after an unload.
    /// </summary>
    public void RunExit()
    {
        if (!Active)
            return;
        Active = false;
        _frames.Clear();
        _nodes.Clear();
        for (var i = _exits.Count - 1; i >= 0; i--)
        {
            try
            {
                _exits[i]();
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                warn($"{Id}: exit callback failed: {e}");
            }
        }
        _exits.Clear();
    }

    public void RunFrame()
    {
        if (!Active || _frames.Count == 0)
            return;
        foreach (var frame in _frames.ToArray())
        {
            if (!Active)
                return;
            try
            {
                frame.Run();
                frame.Failures = 0;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                frame.Failures++;
                warn($"{Id}: frame callback failed ({frame.Failures}/{MaxFailures}): {e}");
                if (frame.Failures >= MaxFailures)
                {
                    _frames.Remove(frame);
                    warn($"{Id}: frame callback removed after {MaxFailures} failures in a row");
                }
            }
        }
    }

    /// <summary>
    /// Removes frame and node-added callbacks, then commands and menu entries, owned nodes and patches, then calls the mod's own unload.
    /// Returns the number of steps that threw (each is logged).
    /// </summary>
    public int Unload(Action removeCommands, Action unpatch, Action modUnload)
    {
        Active = false;
        _frames.Clear();
        _nodes.Clear();
        _exits.Clear();
        var failed = 0;
        void Step(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                failed++;
                warn($"{Id}: unload step '{what}' failed: {e}");
            }
        }
        Step("commands", removeCommands);
        foreach (var free in _owned)
            Step("owned node", free);
        _owned.Clear();
        Step("patches", unpatch);
        Step("Unload", modUnload);
        return failed;
    }
}

internal static class ModLoading
{
    // Runs a mod's Load. If it throws, everything the mod set up so far (frames, commands, nodes, patches) is
    // removed, so a failed copy leaves no hooks behind.
    public static bool LoadGuarded(
        ModHooks hooks,
        CommandRegistry commands,
        Action load,
        Action unpatch,
        Action<string> log,
        string verb = "load"
    )
    {
        try
        {
            load();
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            var removed = 0;
            hooks.Unload(() => removed = commands.RemoveOwner(hooks.Id), unpatch, () => { });
            log($"Failed to {verb} {hooks.Id}: {e}" + (removed > 0 ? $" ({removed} commands removed)" : ""));
            return false;
        }
    }
}
