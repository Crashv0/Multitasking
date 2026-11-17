namespace MelvorMods.Core;

/// <summary>A registered command as clients see it in <c>help</c>.</summary>
/// <param name="Name">The command's name, the first word of a command line.</param>
/// <param name="Usage">Usage line, e.g. <c>shot [view|#n|rect x y w h]</c>.</param>
/// <param name="Help">One line saying what the command does.</param>
/// <param name="Dev">True for dev commands, which exist only in Debug builds of their mod.</param>
/// <param name="Owner">Id of the mod that registered it.</param>
sealed record CommandInfo(string Name, string Usage, string Help, bool Dev, string Owner);

/// <summary>
/// One run of a command. The handler replies with <see cref="Ok"/> or <see cref="Error"/>, either before it
/// returns or later (for example after the next frame draws). Only the first reply counts.
/// </summary>
sealed class CommandCall
{
    private readonly Action<bool, string>? _onReply;

    internal CommandCall(string name, string argText, Action<bool, string>? onReply = null)
    {
        Name = name;
        ArgText = argText;
        Args = argText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        _onReply = onReply;
    }

    /// <summary>The command's name as typed.</summary>
    public string Name { get; }

    /// <summary>Everything after the name, trimmed.</summary>
    public string ArgText { get; }

    /// <summary>The arguments, split on whitespace.</summary>
    public IReadOnlyList<string> Args { get; }

    /// <summary>True once the handler has replied.</summary>
    public bool IsDone { get; private set; }

    /// <summary>True if the reply was <see cref="Ok"/>.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>The reply text; lines after the first are details.</summary>
    public string Reply { get; private set; } = "";

    /// <summary>Replies success. Returns false if the call had already replied.</summary>
    public bool Ok(string text = "") => Complete(true, text);

    /// <summary>Replies failure. Returns false if the call had already replied.</summary>
    public bool Error(string text) => Complete(false, text);

    // The handler's call passes its reply on to the caller's, which may be in another mod's copy of this code.
    internal bool Complete(bool ok, string text)
    {
        if (IsDone)
            return false;
        IsDone = true;
        Succeeded = ok;
        Reply = text;
        _onReply?.Invoke(ok, text);
        return true;
    }
}

/// <summary>
/// Every mod's commands, keyed by name (case-insensitive), held in the process's <see cref="ModTable"/> so a mod can
/// run another's without referencing it. Pure .NET: runners supply the frame loop and time limits.
/// </summary>
internal sealed class CommandRegistry(Action<string> warn, ModTable? table = null)
{
    private readonly ModTable _table = table ?? ModTable.Process;

    /// <summary>
    /// Registers a command. A dev command is left out of a Release build of the mod that compiles this code, and
    /// false is returned.
    /// </summary>
    public bool Register(string owner, string name, string usage, string help, Action<CommandCall> handler, bool dev)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        if (name.Any(char.IsWhiteSpace))
            throw new ArgumentException($"Command name '{name}' contains whitespace", nameof(name));
        if (dev && !DevBuild)
            return false;
        Action<string, Action<bool, string>> run = (argText, reply) => handler(new CommandCall(name, argText, reply));
        var record = new Dictionary<string, object>
        {
            [ModTable.Owner] = owner,
            [ModTable.Usage] = usage,
            [ModTable.Help] = help,
            [ModTable.Dev] = dev,
            [ModTable.Run] = run,
        };
        if (_table.AddCommand(name, record, out var holder))
            return true;
        warn($"Command '{name}' from {owner} refused: already registered by {holder ?? "another mod"}");
        return false;
    }

#if DEBUG
    private static bool DevBuild => true;
#else
    private static bool DevBuild => false;
#endif

    public IReadOnlyList<CommandInfo> List() =>
        [
            .. _table
                .Commands()
                .Select(c => new CommandInfo(
                    c.Key,
                    ModTable.Get<string>(c.Value, ModTable.Usage) ?? "",
                    ModTable.Get<string>(c.Value, ModTable.Help) ?? "",
                    ModTable.Get<bool>(c.Value, ModTable.Dev),
                    ModTable.Get<string>(c.Value, ModTable.Owner) ?? ""
                )),
        ];

    // Runs one command line. The returned call may still be pending if the handler replies later.
    public CommandCall Run(string line)
    {
        var trimmed = line.Trim();
        var split = trimmed.IndexOfAny([' ', '\t']);
        var name = split < 0 ? trimmed : trimmed[..split];
        var call = new CommandCall(name, split < 0 ? "" : trimmed[(split + 1)..].Trim());
        var run = _table.FindCommand(name) is { } record
            ? ModTable.Get<Action<string, Action<bool, string>>>(record, ModTable.Run)
            : null;
        if (run == null)
        {
            call.Error($"unknown command '{name}' (try help)");
            return call;
        }
        try
        {
            run(call.ArgText, (ok, text) => call.Complete(ok, text));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            call.Error($"{name} failed: {e.GetType().Name}: {e.Message}");
        }
        return call;
    }

    public int RemoveOwner(string owner) => _table.RemoveCommands(owner);
}

/// <summary>A mod's handle on the shared command registry; commands it registers are owned by the mod.</summary>
sealed class ModCommands
{
    private readonly CommandRegistry _registry;
    private readonly string _owner;

    internal ModCommands(CommandRegistry registry, string owner)
    {
        _registry = registry;
        _owner = owner;
    }

    /// <summary>
    /// Registers a command. Names are single words, matched case-insensitively. If another command already has
    /// the name, the first one is kept, a warning naming both owners is logged and false is returned.
    /// Mark commands that must not ship with <paramref name="dev"/>, and compile them only in Debug builds.
    /// </summary>
    public bool Register(string name, string usage, string help, Action<CommandCall> handler, bool dev = false) =>
        _registry.Register(_owner, name, usage, help, handler, dev);

    /// <summary>Every registered command, sorted by name.</summary>
    public IReadOnlyList<CommandInfo> List() => _registry.List();

    /// <summary>
    /// Runs a command line (name, then arguments). Call from the main thread. Unknown commands and handlers
    /// that throw reply with an error. The call may still be pending; the caller enforces any time limit.
    /// </summary>
    public CommandCall Run(string line) => _registry.Run(line);
}
