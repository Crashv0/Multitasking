using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Entities.Combat;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.GameMenus.Pages;
using Godot;
using MelvorGame = GamesByMalcs.Melvor2.Game;

namespace MelvorMods.Multitasking;

/// <summary>
/// The loaded character's running actions. Follows the game as characters load and exit, and raises
/// <see cref="Changed"/> on the next frame after an action starts or stops or the character changes. Main thread.
/// </summary>
internal sealed class Running
{
    // While no character is loaded, look for one every this many frames rather than walking the tree each frame.
    private const int SearchEvery = 30;

    private WeakReference<GameView>? _view;
    private MelvorGame? _hooked;
    private int _wait;

    // Offline progress ticks the game on a worker thread, where actions also start and stop; Changed handlers build
    // nodes, which only the main thread may do, so the game's events only mark the change for the next frame.
    private volatile bool _changed;

    public event Action? Changed;

    public IReadOnlyList<ActionComponent> Actions => _hooked?.ActionManager.ActiveActions ?? [];

    /// <summary>The loaded character's game view, or null at the main menu.</summary>
    public GameView? View => TryView(out var view) ? view : null;

    /// <summary>
    /// Whether the game is running offline progress. Frames go on meanwhile, but the worker thread is changing the
    /// actions, so nothing may read or draw them until it ends.
    /// </summary>
    public bool IsOffline { get; private set; }

    /// <summary>Frame callback: re-hooks when the loaded character changes, and raises the changes since the last.</summary>
    public void Frame()
    {
        var game = Find();
        IsOffline = game?.Flags.IsOffline == true;
        if (IsOffline)
            return;
        if (ReferenceEquals(game, _hooked))
        {
            if (_changed)
            {
                _changed = false;
                Changed?.Invoke();
            }
            return;
        }
        _changed = false;
        Unhook();
        if (game != null)
        {
            game.ActionManager.ActionStarted += OnActionsChanged;
            game.ActionManager.ActionStopped += OnActionsChanged;
        }
        _hooked = game;
        Changed?.Invoke();
    }

    public void Unhook()
    {
        if (_hooked == null)
            return;
        _hooked.ActionManager.ActionStarted -= OnActionsChanged;
        _hooked.ActionManager.ActionStopped -= OnActionsChanged;
        _hooked = null;
    }

    /// <summary>The action the sidebar shows as active: a running fight first, else the last started.</summary>
    public static ActionComponent? Shown(IReadOnlyList<ActionComponent> actions) =>
        RunningMath.ShownIndex(actions.Count, FightIndex(actions)) is var i and >= 0 ? actions[i] : null;

    /// <summary>
    /// The index of the action the footer shows: the open page's skill when it runs, else as <see cref="Shown"/>; the
    /// last started, as in the base game, when that part is off.
    /// </summary>
    public static int InFooterIndex(IReadOnlyList<ActionComponent> actions)
    {
        if (!Parts.CombatFirst.On)
            return actions.Count - 1;
        var page = -1;
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i] is { IsStarted: true } action && ReferenceEquals(action.Entity, FooterFollowsPage.Entity))
            {
                page = i;
                break;
            }
        }
        return RunningMath.FooterIndex(actions.Count, FightIndex(actions), page);
    }

    /// <summary>The action the footer shows, or null with none running.</summary>
    public static ActionComponent? InFooter(IReadOnlyList<ActionComponent> actions) =>
        InFooterIndex(actions) is var i and >= 0 ? actions[i] : null;

    private static int FightIndex(IReadOnlyList<ActionComponent> actions)
    {
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i] is CombatActionComponent { IsStarted: true })
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Asks the game to stop everything, as another mod or a part of the game other than a start would. Returns how
    /// many are still running.
    /// </summary>
    public int StopAsGame()
    {
        _hooked?.ActionManager.TryStopActive();
        return Actions.Count;
    }

    /// <summary>Stops every running action that its own rules let stop. Returns how many are still running.</summary>
    public int StopAll()
    {
        foreach (var action in Actions.ToList())
            action.TryStop();
        return Actions.Count;
    }

    /// <summary>Opens the page of an action's skill, as the game's own "open the active page" does.</summary>
    public bool OpenPage(ActionComponent action)
    {
        if (!TryView(out var view) || view.Game == null)
            return false;
        if (!view.Game.Data.TryGetObject<EntityPage>(action.Entity.Id, out var page))
            return false;
        view.SetCurrentPage(page);
        return true;
    }

    private void OnActionsChanged(object? sender, EventArgs e) => _changed = true;

    private MelvorGame? Find()
    {
        if (TryView(out var view))
            return view.Game is { IsSaveLoaded: true } game ? game : null;
        _view = null;
        if (_wait-- > 0)
            return null;
        _wait = SearchEvery;
        if (Engine.GetMainLoop() is not SceneTree tree || FindView(tree.Root) is not { } found)
            return null;
        _view = new WeakReference<GameView>(found);
        return found.Game;
    }

    private bool TryView(out GameView view)
    {
        if (_view != null && _view.TryGetTarget(out var cached) && GodotObject.IsInstanceValid(cached))
        {
            view = cached;
            return true;
        }
        view = null!;
        return false;
    }

    private static GameView? FindView(Node node)
    {
        if (node is GameView { Game.IsSaveLoaded: true } view)
            return view;
        foreach (var child in node.GetChildren())
        {
            if (FindView(child) is { } found)
                return found;
        }
        return null;
    }
}
