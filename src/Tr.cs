using System.Globalization;
using Godot;
using MelvorMods.Core;

namespace MelvorMods.Multitasking;

/// <summary>
/// The mod's text in the game's language. Every message is a string literal here so <c>mm strings</c> finds it;
/// before load (or after unload) messages come back in English.
/// </summary>
internal static class Tr
{
    private static ModText? _text;

    public static void Use(ModText? text) => _text = text;

    public static string Get(string message) => _text?.Get(message) ?? message;

    public static string Format(string message, params object?[] args) =>
        _text?.Format(message, args) ?? string.Format(CultureInfo.InvariantCulture, message, args);

    /// <summary>Gives a node the mod's translation domain, with Godot's automatic translation off below it.</summary>
    public static T Adopt<T>(T root)
        where T : Node => _text?.Adopt(root) ?? root;
}
