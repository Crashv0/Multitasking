using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Core.TickTimer;
using GamesByMalcs.Melvor2.Core.TickTimer.Views;
using GamesByMalcs.Melvor2.GameMenus.Sidebar;
using Godot;
using MelvorMods.Core;

namespace MelvorMods.Multitasking;

/// <summary>
/// Puts the game's activity ring on every running skill's sidebar icon. The game shows it only on its one
/// active-action button; each ring here is a copy of that button's ring, following its own action's timer. Rings are
/// rebuilt when actions start or stop or the character changes. Main thread.
/// </summary>
internal sealed class SidebarRings
{
    // The game's names this relies on, under the sidebar's active-action button.
    private const string RingContainer = "IntervalContainer";
    private const string TimerBar = "IntervalProgress";
    private const string SpinnerHolder = "Control"; // the game spins this for actions without a fixed length
    private const string Spinner = "Control/RandomIntervalProgress";

    private const string RingName = "MultitaskingRing";
    private const double TurnsPerSecond = 0.75;
    private const int CheckEvery = 30;

    private readonly Running _running;
    private readonly ModLog _log;
    private readonly Func<Node, Node> _own;
    private readonly List<Ring> _rings = [];
    private bool _warned;
    private int _check;

    public SidebarRings(Running running, ModLog log, Func<Node, Node> own)
    {
        _running = running;
        _log = log;
        _own = own;
    }

    /// <summary>Puts a ring on each running skill's icon and removes the others.</summary>
    public void Rebuild()
    {
        Clear();
        var actions = _running.Actions;
        if (actions.Count == 0 || FindSidebar() is not { } sidebar)
            return;
        SidebarShowsFight.Follow(sidebar, actions);
        var template = sidebar.ActiveActionButton?.GetNodeOrNull<Control>(RingContainer);
        if (
            template?.GetNodeOrNull<TickTimerTextureProgressBar>(TimerBar) == null
            || template.GetNodeOrNull<Control>(Spinner) == null
        )
        {
            Warn($"ActiveActionButton/{RingContainer} with {TimerBar} and {Spinner}");
            return;
        }
        foreach (var action in actions)
        {
            if (
                sidebar._options.TryGetValue(action.Entity, out var option)
                && option.Button is Control button
                && Make(template, action) is { } ring
            )
            {
                button.AddChild(ring.Node);
                _own(ring.Node);
                _rings.Add(ring);
            }
        }
    }

    /// <summary>
    /// Frame callback: fills each ring from its action's timer, spins the rings of actions without a fixed length, and
    /// rebuilds lost rings.
    /// </summary>
    public void Frame()
    {
        var angle = (float)(Time.GetTicksMsec() / 1000.0 * TurnsPerSecond * Math.Tau % Math.Tau);
        var lost = false;
        foreach (var ring in _rings)
        {
            if (!GodotObject.IsInstanceValid(ring.Node) || !ring.Node.IsInsideTree())
            {
                lost = true;
                continue;
            }
            if (ring.Spin is { } spin)
            {
                spin.PivotOffset = spin.Size / 2;
                spin.Rotation = angle;
            }
            else if (ring.Bar is { } bar && ring.Timer is { } timer)
            {
                bar.MaxValue = Math.Max(1, timer.WaitTicks);
                bar.Value = timer.IsRunning ? timer.WaitTicks - timer.TicksLeft : 0;
            }
        }
        if (lost && _check-- <= 0)
        {
            _check = CheckEvery;
            Rebuild();
        }
    }

    public void Clear()
    {
        foreach (var ring in _rings)
        {
            if (GodotObject.IsInstanceValid(ring.Node))
                ring.Node.QueueFree();
        }
        _rings.Clear();
    }

    private static Ring? Make(Control template, ActionComponent action)
    {
        var timer = action.ActionTimer;
        var spins = action.IsTimerIndeterminate;
        if (!spins && timer == null)
            return null;
        var node = (Control)template.Duplicate();
        node.Name = RingName;
        node.Visible = true;
        IgnoreMouse(node);
        // The copy is the game's timer bar, but it is never bound: a bound bar handles the timer's events inside the
        // game's tick, where any failure (a freed ring) aborts the tick and leaves the timer broken for good. Frame
        // reads the timer instead, and the bar's own interpolation stays off so it doesn't overwrite that.
        var bar = node.GetNode<TickTimerTextureProgressBar>(TimerBar);
        bar.ProcessMode = Node.ProcessModeEnum.Disabled;
        var holder = node.GetNode<Control>(SpinnerHolder);
        var spinner = node.GetNode<Control>(Spinner);
        bar.Visible = !spins;
        holder.Visible = spins;
        spinner.Visible = spins;
        return spins ? new Ring(node, null, null, holder) : new Ring(node, bar, timer, null);
    }

    // The ring sits over the skill's button; clicks and hovers go to the button as before.
    private static void IgnoreMouse(Node node)
    {
        if (node is Control control)
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren())
            IgnoreMouse(child);
    }

    private GameSidebar? FindSidebar()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return null;
        if (Find(tree.Root) is { } sidebar)
            return sidebar;
        Warn("GameSidebar");
        return null;
    }

    private static GameSidebar? Find(Node node)
    {
        if (node is GameSidebar sidebar)
            return sidebar;
        foreach (var child in node.GetChildren())
        {
            if (Find(child) is { } found)
                return found;
        }
        return null;
    }

    private void Warn(string missing)
    {
        if (_warned)
            return;
        _warned = true;
        _log.Warn($"sidebar rings off: no {missing} in the game's sidebar");
    }

    private sealed record Ring(Control Node, Godot.Range? Bar, TickTimer? Timer, Control? Spin);
}
