using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.GameMenus.Pages;
using GamesByMalcs.Melvor2.GameMenus.SidebarLegacy;
using HarmonyLib;

namespace MelvorMods.Multitasking;

// The legacy sidebar (a game setting) has a bar on each skill's button. It binds one as that skill's action starts
// and unbinds it as it stops, which handles any number running; but when it catches up after a character load it
// binds only the first running action, and it hears no starts while offline progress runs. Bind every running one.
[HarmonyPatch(typeof(GameSidebarLegacy), nameof(GameSidebarLegacy.Update))]
internal static class LegacyBarsFollowActions
{
    private static bool Prepare() => Parts.LegacyBars.Available;

    private static void Postfix(GameSidebarLegacy __instance) => Parts.LegacyBars.Run(() => Sync(__instance));

    /// <summary>Brings the legacy sidebar's bars in line with what runs; called when the running actions change.</summary>
    public static void Refresh(GameView? view) =>
        Parts.LegacyBars.Run(() =>
        {
            if (view?.SidebarLegacy is { } sidebar)
                Sync(sidebar);
        });

    private static void Sync(GameSidebarLegacy sidebar)
    {
        if (sidebar.Game is not { } game)
            return;
        var running = new Dictionary<IGameEntity, ActionComponent>();
        foreach (var action in game.ActionManager.ActiveActions)
        {
            if (action.IsStarted)
                running[action.Entity] = action;
        }
        foreach (var (entity, container) in sidebar._options)
        {
            if (container.Button is not LegacySkillNavigationButton button)
                continue;
            if (running.TryGetValue(entity, out var action))
                button.BindActionComponent(action);
            else if (button.Action != null)
                button.UnbindActionComponent();
        }
    }
}
