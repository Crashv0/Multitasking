using System.Reflection;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Entities.OfflineProgress;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.GameMenus.Footer;
using GamesByMalcs.Melvor2.GameMenus.Pages;
using GamesByMalcs.Melvor2.GameMenus.Sidebar;
using GamesByMalcs.Melvor2.GameMenus.SidebarLegacy;
using HarmonyLib;

namespace MelvorMods.Multitasking;

/// <summary>
/// The mod's parts, each switched off on its own when a game update breaks it. A part needs its patch classes'
/// targets (so a missing one keeps them all unapplied, never half of them) and the game members it reads by name.
/// </summary>
internal static class Parts
{
    public static readonly Part ParallelStarts = new(
        "Parallel starts",
        () => Targets(typeof(StartScope), typeof(KeepOthersRunning))
    );

    public static readonly Part CombatFirst = new(
        "Combat first and the footer",
        () =>
        {
            Targets(
                typeof(FooterShowsFight),
                typeof(FooterShowsFightAfterPassive),
                typeof(SidebarShowsFight),
                typeof(FooterFollowsPage)
            );
            Members(typeof(GameFooter), "Game", "BindAction");
            Members(typeof(GameSidebar), "Game", "LastActionComponent", "UpdateForActiveAction");
            Members(typeof(GameView), "Footer");
        }
    );

    public static readonly Part Rings = new(
        "Sidebar rings",
        () =>
        {
            Members(typeof(GameSidebar), "_options", "ActiveActionButton");
            Members(typeof(ActionComponent), "ActionTimer", "IsTimerIndeterminate");
        }
    );

    public static readonly Part LegacyBars = new(
        "Legacy sidebar bars",
        () =>
        {
            Targets(typeof(LegacyBarsFollowActions));
            Members(typeof(GameSidebarLegacy), "_options", "Game");
            Members(typeof(GameView), "SidebarLegacy");
            Members(typeof(LegacySkillNavigationButton), "Action", "BindActionComponent", "UnbindActionComponent");
        }
    );

    public static readonly Part OfflineSummary = new(
        "Welcome Back scrolling",
        () =>
        {
            Targets(typeof(OfflineSummaryScrolls));
            Members(typeof(OfflineSnapshotModal), "MessageContainer");
        }
    );

    public static readonly Part RunningList = new(
        "Footer chip and Running list",
        () => Members(typeof(GameView), "SetCurrentPage")
    );

    private const BindingFlags Any =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // Resolves each class's [HarmonyPatch] target as Harmony will, so a missing one is found before patching.
    private static void Targets(params Type[] patchClasses)
    {
        foreach (var patchClass in patchClasses)
        {
            var info = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
            if (
                info.declaringType == null
                || info.methodName == null
                || AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes) == null
            )
                throw new MissingMethodException($"no target for {patchClass.Name}");
        }
    }

    private static void Members(Type type, params string[] names)
    {
        foreach (var name in names)
        {
            if (type.GetMember(name, Any).Length == 0)
                throw new MissingMemberException(type.Name, name);
        }
    }
}
