using System.Globalization;

namespace MelvorMods.Multitasking;

/// <summary>What the chip and the list show for a number of running actions. Godot-free, so it is unit tested.</summary>
internal static class RunningMath
{
    /// <summary>The chip shows once a second action runs.</summary>
    public static bool ChipShown(int running) => running >= 2;

    /// <summary><c>+N</c>, N being the running actions the footer doesn't show (it shows one).</summary>
    public static string ChipLabel(int running) =>
        "+" + Math.Max(0, running - 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The action the game shows as active: a running fight (<paramref name="fight"/> is its index, -1 for none), as
    /// in the base game, otherwise the last action started. -1 with none running.
    /// </summary>
    public static int ShownIndex(int running, int fight) => fight >= 0 && fight < running ? fight : running - 1;

    /// <summary>
    /// The action the footer shows: the open page's skill when it runs (<paramref name="page"/> is its index, -1 for
    /// none), whose numbers the player is looking at, else as <see cref="ShownIndex"/>.
    /// </summary>
    public static int FooterIndex(int running, int fight, int page) =>
        page >= 0 && page < running ? page : ShownIndex(running, fight);

    /// <summary>
    /// How far through its current action a row is, 0 to 1, or null when the action has no length to show (combat,
    /// or no timer).
    /// </summary>
    public static double? Progress(bool indeterminate, int? ticksLeft, int? waitTicks)
    {
        if (indeterminate || ticksLeft is not { } left || waitTicks is not { } wait || wait <= 0)
            return null;
        return Math.Clamp(1.0 - (double)left / wait, 0.0, 1.0);
    }
}
