using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Combat;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.GameMenus.Footer;
using GamesByMalcs.Melvor2.GameMenus.Pages;
using GamesByMalcs.Melvor2.GameMenus.Sidebar;
using HarmonyLib;

namespace MelvorMods.Multitasking;

// In the base game a fight is the only action running, so the footer and the sidebar's active-action button show it.
// With skills running beside it, these keep the fight in view while it runs; on a running skill's own page the
// footer shows that skill, whose numbers the player is looking at.

// The footer shows the last action started; instead it shows the open page's skill if it runs, else a running fight.
[HarmonyPatch(typeof(GameFooter), nameof(GameFooter.Update))]
internal static class FooterShowsFight
{
    private static bool Prepare() => Parts.CombatFirst.Available;

    private static bool Prefix(GameFooter __instance) => Parts.CombatFirst.Run(() => Bind(__instance), off: true);

    // Returns whether the game's own Update still runs: it does when nothing runs (it unbinds the footer).
    private static bool Bind(GameFooter footer)
    {
        if (footer.Game is not { } game || Running.InFooter(game.ActionManager.ActiveActions) is not { } shown)
            return true;
        footer.BindAction(shown);
        return false;
    }
}

// Leaving a page with a passive tracker (Farming) rebinds the footer to the last action started; apply the rule above.
[HarmonyPatch(typeof(GameFooter), nameof(GameFooter.UnbindPassiveTracker))]
internal static class FooterShowsFightAfterPassive
{
    private static bool Prepare() => Parts.CombatFirst.Available;

    private static void Postfix(GameFooter __instance) => Parts.CombatFirst.Run(__instance.Update);
}

// The footer follows the page: the game only rebinds it when an action starts or stops.
[HarmonyPatch(typeof(GameView), nameof(GameView.SetCurrentPage))]
internal static class FooterFollowsPage
{
    /// <summary>The skill whose page is open, or null on a page of no skill. Main thread.</summary>
    public static IGameEntity? Entity { get; private set; }

    private static bool Prepare() => Parts.CombatFirst.Available;

    private static void Postfix(GameView __instance, GamePage pageData) =>
        Parts.CombatFirst.Run(() =>
        {
            Entity = (pageData as EntityPage)?.Entity;
            __instance.Footer?.Update();
        });
}

// The sidebar button follows each action as it starts; while a fight runs, it stays on the fight.
[HarmonyPatch(typeof(GameSidebar), nameof(GameSidebar.UpdateForActiveAction))]
internal static class SidebarShowsFight
{
    private static bool Prepare() => Parts.CombatFirst.Available;

    private static bool Prefix(GameSidebar __instance, ref ActionComponent action)
    {
        var shown = action;
        var runs = Parts.CombatFirst.Run(() => Choose(__instance, ref shown), off: true);
        action = shown;
        return runs;
    }

    // Returns whether the game's own method runs, with the action it is to show.
    private static bool Choose(GameSidebar sidebar, ref ActionComponent action)
    {
        if (
            sidebar.Game is not { } game
            || Running.Shown(game.ActionManager.ActiveActions) is not CombatActionComponent fight
        )
            return true;
        if (ReferenceEquals(action, fight))
            return true;
        // Already showing the fight: a skill starting beside it changes nothing (and doesn't replay the animation).
        if (ReferenceEquals(sidebar.LastActionComponent, fight))
            return false;
        action = fight;
        return true;
    }

    /// <summary>
    /// The game leaves the sidebar button on an action after it stops; move it to the action shown now, as a newly
    /// started one would.
    /// </summary>
    public static void Follow(GameSidebar sidebar, IReadOnlyList<ActionComponent> actions) =>
        Parts.CombatFirst.Run(() =>
        {
            if (Running.Shown(actions) is not { } shown || ReferenceEquals(sidebar.LastActionComponent, shown))
                return;
            if (sidebar.LastActionComponent is { IsStarted: true } && shown is not CombatActionComponent)
                return;
            sidebar.UpdateForActiveAction(shown);
        });
}
