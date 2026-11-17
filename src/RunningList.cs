using GamesByMalcs.Melvor2.Components.Actions;
using Godot;

namespace MelvorMods.Multitasking;

/// <summary>
/// The popup listing every running action, opened above the footer chip. Rows are rebuilt when actions start or
/// stop; their text and bars are refreshed each frame while it is open. Main thread.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA1001",
    Justification = "The scene tree owns and frees the nodes."
)]
internal sealed class RunningList
{
    // A press on the chip right after the popup closed (the same click closed it) shouldn't open it again.
    private const ulong ReopenGuardMs = 200;
    private const int Gap = 4;
    private const int IconSize = 28;

    private readonly Running _running;
    private readonly Func<Node, Node> _own;
    private PopupPanel? _popup;
    private VBoxContainer? _rows;
    private Label? _title;
    private Label? _count;
    private Button? _stopAll;
    private readonly List<Row> _shown = [];
    private ulong _closedAt;

    public RunningList(Running running, Func<Node, Node> own)
    {
        _running = running;
        _own = own;
    }

    public bool IsOpen => _popup != null && GodotObject.IsInstanceValid(_popup) && _popup.Visible;

    /// <summary>Opens the list above <paramref name="anchor"/> (the chip), or closes it if open.</summary>
    public void Toggle(Rect2? anchor)
    {
        if (IsOpen)
        {
            Close();
            return;
        }
        if (Time.GetTicksMsec() - _closedAt < ReopenGuardMs || anchor is not { } rect)
            return;
        Open(rect);
    }

    public void Close()
    {
        if (IsOpen)
            _popup!.Hide();
    }

    /// <summary>Actions started or stopped: rebuild the rows, or close when nothing is left.</summary>
    public void Rebuild()
    {
        if (!IsOpen)
            return;
        if (_running.Actions.Count == 0)
        {
            Close();
            return;
        }
        Fill();
        Resize();
    }

    /// <summary>The language changed: redraw the open list in the new one.</summary>
    public void Retext()
    {
        if (!IsOpen)
            return;
        Fill();
        Resize();
    }

    /// <summary>Frame callback while open: names, targets and bars follow the game.</summary>
    public void Refresh()
    {
        if (!IsOpen)
            return;
        foreach (var row in _shown)
            row.Refresh();
    }

    public void Free()
    {
        if (_popup != null && GodotObject.IsInstanceValid(_popup))
            _popup.QueueFree();
        _popup = null;
        _shown.Clear();
    }

    private void Open(Rect2 anchor)
    {
        if (_running.Actions.Count == 0)
            return;
        var popup = Build();
        Fill();
        var size = (Vector2I)popup.GetContentsMinimumSize().Ceil();
        var window = popup.GetParent<Node>() is Viewport vp ? vp.GetVisibleRect().Size : new Vector2(1920, 1080);
        var x = Math.Clamp((int)anchor.Position.X, Gap, Math.Max(Gap, (int)window.X - size.X - Gap));
        var y = Math.Max(Gap, (int)anchor.Position.Y - size.Y - Gap);
        popup.Popup(new Rect2I(new Vector2I(x, y), size));
    }

    // Keeps the bottom edge where it was, so the list grows upwards from the chip as rows are added.
    private void Resize()
    {
        var popup = _popup!;
        var bottom = popup.Position.Y + popup.Size.Y;
        var size = (Vector2I)popup.GetContentsMinimumSize().Ceil();
        popup.Size = size;
        popup.Position = new Vector2I(popup.Position.X, Math.Max(Gap, bottom - size.Y));
    }

    private PopupPanel Build()
    {
        if (_popup != null && GodotObject.IsInstanceValid(_popup))
            return _popup;
        var popup = Tr.Adopt(new PopupPanel { Name = "MultitaskingRunningList" });
        popup.PopupHide += () => _closedAt = Time.GetTicksMsec();
        // The game's theme draws popups with no panel, so the list would float over the page; give it the dark,
        // bordered panel the game's tooltips and dropdowns use.
        popup.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxFlat
            {
                BgColor = new Color(0.07f, 0.07f, 0.07f, 0.97f),
                BorderColor = new Color(0.23f, 0.23f, 0.23f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                ContentMarginLeft = 12,
                ContentMarginTop = 8,
                ContentMarginRight = 12,
                ContentMarginBottom = 10,
            }
        );

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        popup.AddChild(box);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        box.AddChild(header);
        _title = new Label { VerticalAlignment = VerticalAlignment.Center };
        header.AddChild(_title);
        _count = new Label { VerticalAlignment = VerticalAlignment.Center, Modulate = new Color(1, 1, 1, 0.7f) };
        header.AddChild(_count);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var stopAll = _stopAll = new Button { FocusMode = Control.FocusModeEnum.None };
        stopAll.Pressed += () =>
        {
            _running.StopAll();
            Rebuild();
        };
        header.AddChild(stopAll);

        box.AddChild(new HSeparator());
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 4);
        box.AddChild(_rows);

        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.AddChild(popup);
        _own(popup);
        _popup = popup;
        return popup;
    }

    private void Fill()
    {
        var rows = _rows!;
        foreach (var child in rows.GetChildren())
        {
            rows.RemoveChild(child);
            child.QueueFree();
        }
        _shown.Clear();
        var actions = _running.Actions;
        var footer = Running.InFooterIndex(actions);
        for (var i = 0; i < actions.Count; i++)
        {
            var row = new Row(actions[i], i == footer, this);
            rows.AddChild(row.Node);
            _shown.Add(row);
        }
        _title!.Text = Tr.Get("Running");
        _count!.Text = Tr.Format("{0} running", actions.Count);
        _stopAll!.Text = Tr.Get("Stop all");
    }

    private void OpenPage(ActionComponent action)
    {
        if (_running.OpenPage(action))
            Close();
    }

    private void Stop(ActionComponent action)
    {
        action.TryStop();
        Rebuild();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA1001",
        Justification = "The scene tree owns and frees the nodes."
    )]
    private sealed class Row
    {
        private readonly ActionComponent _action;
        private readonly Label _target;
        private readonly ProgressBar _bar;

        public Row(ActionComponent action, bool inFooter, RunningList list)
        {
            _action = action;
            var name = action.Entity.DisplayName.Text;
            var row = new HBoxContainer { Name = "Row" };
            row.AddThemeConstantOverride("separation", 10);
            Node = row;

            row.AddChild(
                new TextureRect
                {
                    Texture = action.Entity.Icon,
                    CustomMinimumSize = new Vector2(IconSize, IconSize),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                }
            );

            var words = new VBoxContainer { CustomMinimumSize = new Vector2(180, 0) };
            words.AddThemeConstantOverride("separation", 0);
            row.AddChild(words);
            var title = new HBoxContainer();
            title.AddThemeConstantOverride("separation", 6);
            words.AddChild(title);
            title.AddChild(new Label { Text = name });
            if (inFooter)
                title.AddChild(new Label { Text = Tr.Get("in footer"), Modulate = new Color(1, 1, 1, 0.6f) });
            _target = new Label
            {
                Modulate = new Color(1, 1, 1, 0.75f),
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            words.AddChild(_target);

            _bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(132, 8),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                ShowPercentage = false,
                MaxValue = 1,
                Step = 0,
            };
            row.AddChild(_bar);

            var open = new Button
            {
                Text = ">",
                TooltipText = Tr.Format("Open the {0} page", name),
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            open.Pressed += () => list.OpenPage(_action);
            row.AddChild(open);

            var stop = new Button
            {
                Text = "x",
                TooltipText = Tr.Format("Stop {0}", name),
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            stop.Pressed += () => list.Stop(_action);
            row.AddChild(stop);

            Refresh();
        }

        public Control Node { get; }

        public void Refresh()
        {
            _target.Text = _action.CurrentDescription?.DisplayName.Text ?? "";
            var timer = _action.ActionTimer;
            var progress = RunningMath.Progress(_action.IsTimerIndeterminate, timer?.TicksLeft, timer?.WaitTicks);
            _bar.Indeterminate = progress == null;
            if (progress is { } p)
                _bar.Value = p;
        }
    }
}
