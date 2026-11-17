using Godot;

namespace MelvorMods.Core;

/// <summary>
/// The game's scene tree, once its autoloads are ready; null before. Set by whatever compiles this copy of the
/// shared code: a mod's own entry point, or the dev host for itself.
/// </summary>
internal static class GameTree
{
    public static SceneTree? Current { get; set; }
}
