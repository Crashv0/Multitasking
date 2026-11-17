using GamesByMalcs.Melvor2.Components.Actions;
using HarmonyLib;

namespace MelvorMods.Multitasking;

// Starting an action asks the manager to stop whatever is running first; answering "done" without stopping
// anything lets the new action start beside the others. The action's own start checks still run before this.
// Only a start's own request is answered so: anyone else asking to stop everything (another mod settling a copy of
// the game, or a caller a game update adds) gets the game's stop-all.
[HarmonyPatch(typeof(ActiveActionManager), nameof(ActiveActionManager.TryStopActive))]
internal static class KeepOthersRunning
{
    private static bool Prepare() => Parts.ParallelStarts.Available;

    private static bool Prefix(ref bool __result)
    {
        if (!StartScope.Starting || !Parts.ParallelStarts.On)
            return true;
        __result = true;
        return false;
    }
}

// Marks that an action is starting, on the thread starting it: offline progress starts actions on a worker thread.
[HarmonyPatch(typeof(ActionComponent), nameof(ActionComponent.TryStart))]
internal static class StartScope
{
    [ThreadStatic]
    private static int _depth;

    public static bool Starting => _depth > 0;

    private static bool Prepare() => Parts.ParallelStarts.Available;

    private static void Prefix() => _depth++;

    private static void Finalizer() => _depth--;
}
