namespace MelvorMods.Core;

/// <summary>One page of a menu entry's panel: its title (sidebar button and page header) and what builds its content.</summary>
/// <remarks><see cref="Build"/> makes a Godot control; it is kept as <c>object</c> so the registry can be tested
/// without Godot.</remarks>
internal sealed record MenuPageEntry(string Title, Func<object> Build);

/// <summary>
/// One pause menu entry: a button labelled <see cref="Label"/> opening a panel of <see cref="Pages"/>, or, with
/// <see cref="Run"/>, closing the menu and running it.
/// </summary>
internal sealed record MenuEntry(string Owner, string Label, IReadOnlyList<MenuPageEntry> Pages, Action? Run = null);

/// <summary>
/// The pause menu entries this copy of the shared code holds, ordered by mod id (ignoring case), then the order each
/// mod added them: the same order whichever mod loaded first.
/// </summary>
internal sealed class MenuRegistry
{
    private readonly List<MenuEntry> _entries = [];

    /// <summary>Raised after an entry is added or removed.</summary>
    public event Action? Changed;

    public IReadOnlyList<MenuEntry> Entries =>
        [
            .. _entries
                .Select((e, i) => (e, i))
                .OrderBy(p => p.e.Owner, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.i)
                .Select(p => p.e),
        ];

    public void Add(MenuEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        Changed?.Invoke();
    }

    /// <summary>Removes every entry the mod added; returns how many.</summary>
    public int RemoveOwner(string owner)
    {
        var removed = _entries.RemoveAll(e => string.Equals(e.Owner, owner, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            Changed?.Invoke();
        return removed;
    }
}

/// <summary>
/// Where an entry goes among the mod entries already after Settings, which may be another copy's: before the first
/// whose owner sorts after its own, so entries from several mods stay in mod id order whichever copy places them.
/// </summary>
internal static class MenuOrder
{
    /// <param name="owners">The owners of the mod entries directly after Settings, in order.</param>
    /// <returns>The entry's index within them.</returns>
    public static int InsertAt(IReadOnlyList<string> owners, string owner)
    {
        ArgumentNullException.ThrowIfNull(owners);
        for (var i = 0; i < owners.Count; i++)
        {
            if (string.Compare(owners[i], owner, StringComparison.OrdinalIgnoreCase) > 0)
                return i;
        }
        return owners.Count;
    }
}
