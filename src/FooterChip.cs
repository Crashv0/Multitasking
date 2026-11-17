using GamesByMalcs.Melvor2.Components.Trackers;
using Godot;
using MelvorMods.Core;

namespace MelvorMods.Multitasking;

/// <summary>
/// The <c>+N</c> chip in the game's footer, right after the tracker's current-action box. It sits inside the
/// tracker's details, so the game hides and shows it with them (collapsed, or nothing running). Every footer gets
/// one, including the new footer after each character load. Main thread.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA1001",
    Justification = "The scene tree owns and frees the nodes."
)]
internal sealed class FooterChip
{
    // The game's names this relies on.
    private const string ActionPath = "MainHBoxContainer/ActionContainer"; // in the tracker's MainScrollContainer

    private const string ChipName = "MultitaskingChip";

    private readonly ModLog _log;
    private readonly Func<Node, Node> _own;
    private readonly Action _pressed;
    private readonly Dictionary<ulong, (TrackerComponentFooterView Tracker, Button Chip)> _chips = [];
    private string _label = "";
    private bool _visible;
    private bool _warned;

    public FooterChip(ModLog log, Func<Node, Node> own, Action pressed)
    {
        _log = log;
        _own = own;
        _pressed = pressed;
    }

    public string Label
    {
        get => _label;
        set
        {
            _label = value;
            foreach (var chip in Chips())
                chip.Text = value;
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            foreach (var chip in Chips())
                chip.Visible = value;
        }
    }

    /// <summary>For checks: how many footers have the chip, whether it's shown, and its node names.</summary>
    public string Describe() =>
        $"{Chips().Count} chips, visible {_visible}, label {_label}: "
        + string.Join(", ", Chips().Select(c => $"{c.Name} {c.IsInsideTree()} {c.IsVisibleInTree()}"));

    /// <summary>Where the chip is on screen in the footer showing it; null when none shows it.</summary>
    public Rect2? ScreenRect => Chips().FirstOrDefault(c => c.IsVisibleInTree())?.GetGlobalRect();

    /// <summary>Node-added callback: gives a new footer tracker its chip once it's ready.</summary>
    public void OnNodeAdded(Node node)
    {
        if (node is not TrackerComponentFooterView tracker)
            return;
        if (tracker.IsNodeReady())
            Place(tracker);
        else
            tracker.Connect(
                Node.SignalName.Ready,
                Callable.From(() => Place(tracker)),
                (uint)GodotObject.ConnectFlags.OneShot
            );
    }

    /// <summary>Gives the footers already in the tree their chip (the mod loaded into a running game).</summary>
    public void Scan(SceneTree tree)
    {
        var stack = new Stack<Node>();
        stack.Push(tree.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is TrackerComponentFooterView tracker && tracker.IsNodeReady())
            {
                Place(tracker);
                continue;
            }
            foreach (var child in node.GetChildren())
                stack.Push(child);
        }
    }

    /// <summary>The language changed: the tooltip is looked up again.</summary>
    public void Retext()
    {
        foreach (var chip in Chips())
            chip.TooltipText = Tr.Get("Show running actions");
    }

    private void Place(TrackerComponentFooterView tracker)
    {
        if (!GodotObject.IsInstanceValid(tracker) || _chips.ContainsKey(tracker.GetInstanceId()))
            return;
        if (tracker.MainScrollContainer?.GetNodeOrNull<Control>(ActionPath) is not { } anchor)
        {
            if (!_warned)
                _log.Warn($"footer chip not placed: no MainScrollContainer/{ActionPath} in the game's footer");
            _warned = true;
            return;
        }
        // A chip from before a reload may still be waiting to be freed; take it out so this one keeps the name.
        if (anchor.GetParent()?.GetNodeOrNull(ChipName) is { } old)
        {
            old.GetParent().RemoveChild(old);
            old.QueueFree();
        }
        var chip = Tr.Adopt(
            new Button
            {
                Name = ChipName,
                FocusMode = Control.FocusModeEnum.None,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                Text = _label,
                TooltipText = Tr.Get("Show running actions"),
                Visible = _visible,
            }
        );
        chip.Pressed += _pressed;
        anchor.AddSibling(chip);
        _own(chip);
        _chips[tracker.GetInstanceId()] = (tracker, chip);
    }

    // The chips still in the game, forgetting those whose footer has gone.
    private List<Button> Chips()
    {
        var live = new List<Button>();
        foreach (var (id, (tracker, chip)) in _chips.ToList())
        {
            if (GodotObject.IsInstanceValid(tracker) && GodotObject.IsInstanceValid(chip))
                live.Add(chip);
            else
                _chips.Remove(id);
        }
        return live;
    }
}
