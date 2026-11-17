using System.Collections.Concurrent;

namespace MelvorMods.Core;

/// <summary>
/// What mods share in the process: which mods are loaded, every mod's commands, the log files opened so far and who
/// still has exit callbacks to run. It holds only .NET framework types and lives in <see cref="AppContext"/> data, so
/// every copy of the shared code, in any mod and any load context, reads the same objects whatever its own revision.
/// Each mod writes and removes only its own entries. Readers skip values they don't know; a changed shape gets a new
/// key, written beside the old one for a while.
/// </summary>
internal sealed class ModTable
{
    public const string ModsKey = "MelvorMods.Mods/1";
    public const string CommandsKey = "MelvorMods.Commands/1";
    public const string LogsKey = "MelvorMods.Logs/1";
    public const string ExitKey = "MelvorMods.Exit/1";

    // Command record keys.
    public const string Owner = "owner";
    public const string Usage = "usage";
    public const string Help = "help";
    public const string Dev = "dev";

    /// <summary>A command's handler: the argument text, and a reply (ok, text) called once, now or later.</summary>
    public const string Run = "run";

    // Mod record keys.
    public const string Version = "version";
    public const string Directory = "directory";
    public const string DataRoot = "dataRoot";
    public const string TextMessages = "text.messages";
    public const string TextLocales = "text.locales";
    public const string TextPseudo = "text.pseudo";
    public const string TextSetPseudo = "text.setPseudo";
    public const string TextLanguage = "text.language";
    public const string TextSetLanguage = "text.setLanguage";

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _mods;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _commands;
    private readonly ConcurrentDictionary<string, bool> _logs;
    private readonly ConcurrentDictionary<string, bool> _exit;

    private ModTable(
        ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> mods,
        ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> commands,
        ConcurrentDictionary<string, bool> logs,
        ConcurrentDictionary<string, bool> exit
    )
    {
        _mods = mods;
        _commands = commands;
        _logs = logs;
        _exit = exit;
    }

    /// <summary>The process's table.</summary>
    public static ModTable Process { get; } =
        new(
            Shared<ConcurrentDictionary<string, IReadOnlyDictionary<string, object>>>(ModsKey, NewRecords),
            Shared<ConcurrentDictionary<string, IReadOnlyDictionary<string, object>>>(CommandsKey, NewRecords),
            Shared<ConcurrentDictionary<string, bool>>(LogsKey, () => new(StringComparer.OrdinalIgnoreCase)),
            Shared<ConcurrentDictionary<string, bool>>(ExitKey, () => new(StringComparer.OrdinalIgnoreCase))
        );

    /// <summary>A table of its own, outside the process's, for tests.</summary>
    public static ModTable Private() =>
        new(NewRecords(), NewRecords(), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));

    private static ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> NewRecords() =>
        new(StringComparer.OrdinalIgnoreCase);

    // Mods load one at a time, so the first to need an entry creates it. Read back after publishing, in case another
    // mod published first.
    private static T Shared<T>(string key, Func<T> create)
        where T : class
    {
        if (AppContext.GetData(key) is T existing)
            return existing;
        AppContext.SetData(key, create());
        return AppContext.GetData(key) as T
            ?? throw new InvalidOperationException($"{key} holds a {AppContext.GetData(key)?.GetType().Name}");
    }

    /// <summary>A record's value as <typeparamref name="T"/>, or the default when it's missing or of another type.</summary>
    public static T? Get<T>(IReadOnlyDictionary<string, object> record, string key) =>
        record.TryGetValue(key, out var value) && value is T typed ? typed : default;

    // Mods

    public void AddMod(string id, IReadOnlyDictionary<string, object> record) => _mods[id] = record;

    public void RemoveMod(string id) => _mods.TryRemove(id, out _);

    public IReadOnlyDictionary<string, object>? FindMod(string id) =>
        _mods.TryGetValue(id, out var record) ? record : null;

    /// <summary>Every loaded mod, by id.</summary>
    public IReadOnlyList<KeyValuePair<string, IReadOnlyDictionary<string, object>>> Mods() =>
        [.. _mods.OrderBy(m => m.Key, StringComparer.OrdinalIgnoreCase)];

    // Commands

    /// <summary>Adds a command unless the name is taken; <paramref name="owner"/> then names who holds it.</summary>
    public bool AddCommand(string name, IReadOnlyDictionary<string, object> record, out string? owner)
    {
        if (_commands.TryAdd(name, record))
        {
            owner = null;
            return true;
        }
        owner = _commands.TryGetValue(name, out var held) ? Get<string>(held, Owner) : null;
        return false;
    }

    public IReadOnlyDictionary<string, object>? FindCommand(string name) =>
        _commands.TryGetValue(name, out var record) ? record : null;

    /// <summary>Every command, by name.</summary>
    public IReadOnlyList<KeyValuePair<string, IReadOnlyDictionary<string, object>>> Commands() =>
        [.. _commands.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Removes the commands <paramref name="owner"/> registered; returns how many.</summary>
    public int RemoveCommands(string owner)
    {
        var removed = 0;
        foreach (var (name, record) in _commands.ToArray())
        {
            if (
                string.Equals(Get<string>(record, Owner), owner, StringComparison.OrdinalIgnoreCase)
                && _commands.TryRemove(name, out _)
            )
                removed++;
        }
        return removed;
    }

    // Logs

    /// <summary>True the first time a log file is opened in this process: the caller empties it then.</summary>
    public bool FirstOpen(string file) => _logs.TryAdd(file, true);

    /// <summary>Every log file opened in this process so far.</summary>
    public IReadOnlyList<string> Logs() => [.. _logs.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    // Exit

    /// <summary>Someone (a mod, or the dev host) has exit callbacks to run when the game quits.</summary>
    public void JoinExit(string id) => _exit[id] = true;

    /// <summary>Its exit callbacks ran; true when it was the last one, which then runs the one collection.</summary>
    public bool LeaveExit(string id) => _exit.TryRemove(id, out _) && _exit.IsEmpty;

    /// <summary>It unloaded or failed to load: it has nothing left to run at exit.</summary>
    public void ForgetExit(string id) => _exit.TryRemove(id, out _);
}
