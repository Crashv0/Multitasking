using Godot;

namespace MelvorMods.Core;

// A standalone mod with pause-menu entries places them itself.
internal static partial class ModEntry
{
    static partial void StartMenu(SceneTree tree)
    {
        PauseMenus.Start();
        PauseMenus.OnTreeReady(tree);
    }

    static partial void MenuNodeAdded(Node node) => PauseMenus.OnNodeAdded(node);
}
