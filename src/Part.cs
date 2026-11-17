namespace MelvorMods.Multitasking;

/// <summary>
/// One part of the mod that a game update can break on its own. It checks once that the game still has what it
/// needs, and runs its code guarded: the first failure switches it off for the session with one error, and from then
/// on the game's own behaviour stands in for it while the other parts keep working. Game-free, so it is unit tested.
/// </summary>
/// <remarks>
/// Guarded code goes in as a delegate of its own: a game member removed by an update fails when the method using it
/// is compiled, which happens in the delegate's call inside <see cref="Run(Action)"/>, not before it.
/// </remarks>
internal sealed class Part
{
    private readonly Action _check;
    private bool? _available;
    private bool _off;

    /// <param name="name">How the log names the part.</param>
    /// <param name="check">Throws when the game lacks something the part needs.</param>
    public Part(string name, Action check)
    {
        Name = name;
        _check = check;
    }

    /// <summary>Where a part's one error goes; null drops it (the mod not loaded).</summary>
    public static Action<string>? Report { get; set; }

    public string Name { get; }

    /// <summary>Whether the game had what the part needs when it was first asked.</summary>
    public bool Available => _available ??= Check();

    /// <summary>Available and not switched off since.</summary>
    public bool On => Available && !_off;

    public void Run(Action body)
    {
        if (!On)
            return;
        try
        {
            body();
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    /// <returns><paramref name="body"/>'s result, or <paramref name="off"/> when the part is or goes off.</returns>
    public T Run<T>(Func<T> body, T off)
    {
        if (!On)
            return off;
        try
        {
            return body();
        }
        catch (Exception e)
        {
            Fail(e);
            return off;
        }
    }

    /// <summary>Switches the part off for the session, saying so once.</summary>
    public void Fail(Exception e)
    {
        if (_off)
            return;
        _off = true;
        Report?.Invoke($"{Name} off after an error; the game's own behaviour stands in: {e}");
    }

    private bool Check()
    {
        try
        {
            _check();
            return true;
        }
        catch (Exception e)
        {
            _off = true;
            Report?.Invoke($"{Name} off: the game lacks what it needs (game update?): {e.GetBaseException().Message}");
            return false;
        }
    }
}
