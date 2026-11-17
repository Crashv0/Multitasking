using GamesByMalcs.Melvor2.Entities.OfflineProgress;
using Godot;
using HarmonyLib;

namespace MelvorMods.Multitasking;

// Welcome Back puts one panel per skill side by side in a scroll container that never scrolls sideways, so it grows
// to fit them all and, with several skills, runs off both edges of the window, out of reach. When the panels are
// wider than the window, fit it to the window and let it scroll; when they fit, leave it as the game made it.
[HarmonyPatch(typeof(OfflineSnapshotModal), nameof(OfflineSnapshotModal.Update))]
internal static class OfflineSummaryScrolls
{
    // Room left at each side of the window, so the panels' own frames stay clear of its edges.
    private const float Margin = 64;

    // The game's own settings for each scroll container this has changed, to put back when the panels fit again.
    private static readonly Dictionary<
        ulong,
        (Vector2 Min, Control.SizeFlags Flags, ScrollContainer.ScrollMode Mode)
    > Original = [];

    private static bool Prepare() => Parts.OfflineSummary.Available;

    private static void Postfix(OfflineSnapshotModal __instance) =>
        Parts.OfflineSummary.Run(() => Fit(__instance.MessageContainer));

    private static void Fit(HBoxContainer panels)
    {
        // The game fills the summary before showing it, while it may be out of the tree: measure the window, not the
        // summary's own viewport, which is empty then.
        if (panels.GetParent() is not ScrollContainer scroll || Engine.GetMainLoop() is not SceneTree tree)
            return;
        var id = scroll.GetInstanceId();
        var room = tree.Root.GetVisibleRect().Size.X - 2 * Margin;
        if (room <= 0 || panels.GetCombinedMinimumSize().X <= room)
        {
            if (Original.Remove(id, out var game))
            {
                scroll.CustomMinimumSize = game.Min;
                scroll.SizeFlagsHorizontal = game.Flags;
                scroll.HorizontalScrollMode = game.Mode;
            }
            return;
        }
        Original.TryAdd(id, (scroll.CustomMinimumSize, scroll.SizeFlagsHorizontal, scroll.HorizontalScrollMode));
        scroll.CustomMinimumSize = new Vector2(room, scroll.CustomMinimumSize.Y);
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
    }
}
