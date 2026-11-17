using GamesByMalcs.Melvor2.Core;
using GamesByMalcs.Melvor2.GameMenus.PauseOverlay;
using Godot;
using HarmonyLib;

namespace MelvorMods.Core;

sealed partial class ModContext
{
    private ModMenu? _menu;

    /// <summary>The game's pause menu; entries added here belong to this mod.</summary>
    public ModMenu Menu => _menu ??= new ModMenu(PauseMenus.Registry, Id, Hooks);
}

/// <summary>A page of a mod's options panel: the title on its sidebar button and header, and its content.</summary>
sealed record MenuPage(string Title, Func<Control> Build);

/// <summary>
/// A mod's handle on the game's pause menu; entries it adds belong to the mod. Labels and page titles are the mod's
/// messages (mark them with <see cref="ModText.Key"/>): Core shows them translated by the mod's catalogues and
/// redraws them when the language changes.
/// </summary>
sealed class ModMenu
{
    private readonly MenuRegistry _registry;
    private readonly string _owner;

    internal ModMenu(MenuRegistry registry, string owner, ModHooks hooks)
    {
        _registry = registry;
        hooks.AddOwned(() => registry.RemoveOwner(owner));
        _owner = owner;
    }

    /// <summary>
    /// Adds a button labelled <paramref name="label"/> to the game's pause menu, after Settings. Pressing it hides the
    /// menu's options and shows a panel drawn like the game's Settings panel: <paramref name="label"/> in its title
    /// tab, a close button, a sidebar with a button per page under a <paramref name="label"/> heading, and the
    /// selected page's content under its own title. Closing it (the button or Escape) shows the menu again. A page's
    /// content is built the first time the panel opens, per pause menu. Core removes the entry and frees its panel
    /// when the mod unloads.
    /// </summary>
    public void Add(string label, params MenuPage[] pages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Length == 0)
            throw new ArgumentException("a menu entry needs at least one page", nameof(pages));
        _registry.Add(new MenuEntry(_owner, label, [.. pages.Select(p => new MenuPageEntry(p.Title, p.Build))]));
    }

    /// <summary>
    /// Adds a button labelled <paramref name="label"/> to the game's pause menu, after Settings, drawn like the
    /// entries above. Pressing it closes the pause menu, then runs <paramref name="action"/> (open a modal, say).
    /// Core removes the entry when the mod unloads.
    /// </summary>
    public void AddAction(string label, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(action);
        _registry.Add(new MenuEntry(_owner, label, [], action));
    }
}

/// <summary>
/// Places menu entries in every <see cref="PauseMenu"/> and hosts their panels, built from a copy of the game's
/// Settings panel (without its scripts) so they look native. The game's names it relies on are the constants below,
/// so a game update that renames them is a fix in one place; if the Settings panel's layout can't be read, panels
/// fall back to a plain box with a title row.
/// </summary>
internal static partial class PauseMenus
{
    // The game's names this file relies on.
    private const string SettingsButtonProperty = "SettingsButton"; // PauseMenuOptions: the button entries follow
    private const string SettingsPanelProperty = "Settings"; // PauseMenu: the panel copied and whose place hosts take
    private const string NavButtonProperty = "GameSettingsButton"; // SettingsPanel: a sidebar button
    private const string ViewProperty = "GameSettingsView"; // SettingsPanel: a page (its first child is the frame)
    private const string CloseButtonName = "CloseButton"; // in the Settings panel's frame
    private const string TitleName = "Title"; // the title tab's label, in the frame and in a page's frame

    private const string Meta = "melvormods_menu";

    private sealed class Placed
    {
        public required PauseMenu Menu { get; init; }
        public List<Button> Buttons { get; } = [];
        public Dictionary<MenuEntry, Control> Hosts { get; } = [];
    }

    private static readonly Dictionary<ulong, Placed> _menus = [];
    private static readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
    private static MenuRegistry? _registry;

    /// <summary>This copy's entries, from every mod it hosts.</summary>
    public static MenuRegistry Registry { get; } = new();

    public static void Start()
    {
        if (_registry != null)
            return;
        _registry = Registry;
        Registry.Changed += SyncAll;
    }

    /// <summary>Called for every node entering the tree: places entries in a new pause menu once it's ready.</summary>
    public static void OnNodeAdded(Node node)
    {
        if (node is not PauseMenu menu)
            return;
        if (menu.IsNodeReady())
            Sync(menu);
        else
            menu.Connect(
                Node.SignalName.Ready,
                Callable.From(() => Sync(menu)),
                (uint)GodotObject.ConnectFlags.OneShot
            );
    }

    /// <summary>Finds pause menus already in the tree (a mod loaded after the menu was made).</summary>
    public static void OnTreeReady(SceneTree tree)
    {
        var stack = new Stack<Node>();
        stack.Push(tree.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is PauseMenu menu && menu.IsNodeReady())
                Sync(menu);
            foreach (var child in node.GetChildren())
                stack.Push(child);
        }
    }

    // A mod's entries changed: every live menu gets them. A failure here is logged, never thrown into a mod's Load.
    private static void SyncAll()
    {
        try
        {
            foreach (var (id, placed) in _menus.ToList())
            {
                if (GodotObject.IsInstanceValid(placed.Menu))
                    Sync(placed.Menu);
                else
                    _menus.Remove(id);
            }
            if (GameTree.Current is { } tree)
                OnTreeReady(tree);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"WARN pause menu entries not updated: {e}");
        }
    }

    // Rebuilds this menu's entry buttons from the registry. Hosts of entries that are gone are freed; a host that
    // was showing puts the menu back first.
    private static void Sync(PauseMenu menu)
    {
        if (_registry == null || !GodotObject.IsInstanceValid(menu))
            return;
        var id = menu.GetInstanceId();
        if (!_menus.TryGetValue(id, out var placed))
        {
            placed = new Placed { Menu = menu };
            _menus[id] = placed;
            // Shown: Core's modals close, so the menu is never behind one. Hidden as a whole (resumed, or a click on
            // the backdrop): no panel stays open behind it.
            menu.VisibilityChanged += () =>
            {
                if (!GodotObject.IsInstanceValid(menu))
                    return;
                if (menu.Visible)
                {
                    CloseModals();
                    return;
                }
                foreach (var host in placed.Hosts.Values.Where(GodotObject.IsInstanceValid))
                    host.Visible = false;
            };
        }

        var entries = _registry.Entries;
        foreach (var button in placed.Buttons.Where(GodotObject.IsInstanceValid))
            Free(button);
        placed.Buttons.Clear();
        var showing = false;
        foreach (var (entry, host) in placed.Hosts.ToList())
        {
            if (entries.Contains(entry))
                continue;
            if (GodotObject.IsInstanceValid(host))
            {
                showing |= host.Visible;
                Free(host);
            }
            placed.Hosts.Remove(entry);
        }
        if (showing)
            menu.Open();
        if (entries.Count == 0)
            return;

        var settings = menu.Options is { } options ? Get<Button>(options, SettingsButtonProperty) : null;
        if (settings?.GetParent() is not { } parent)
        {
            foreach (var owner in entries.Select(e => e.Owner).Distinct())
            {
                if (_warned.Add(owner))
                    Log.Write(
                        owner,
                        $"WARN pause menu entry not placed: no {SettingsButtonProperty} in the pause menu"
                    );
            }
            return;
        }
        var after = settings.GetIndex() + 1;
        foreach (var entry in entries)
        {
            var button = Copy<Button>(settings, $"MelvorMods_{entry.Owner}_{entry.Label}");
            SetText(button, entry.Owner, entry.Label);
            button.SetMeta(Meta, entry.Owner);
            button.Pressed += entry.Run == null ? () => Open(placed, entry) : () => Run(placed, entry);
            var at = after + MenuOrder.InsertAt(EntryOwners(parent, after), entry.Owner);
            parent.AddChild(button);
            parent.MoveChild(button, at);
            placed.Buttons.Add(button);
        }
    }

    // The owners of the mod entries, this copy's or another's, from index `from` on until the first other node.
    private static List<string> EntryOwners(Node parent, int from)
    {
        var owners = new List<string>();
        for (var i = from; i < parent.GetChildCount(); i++)
        {
            var child = parent.GetChild(i);
            if (!child.HasMeta(Meta))
                break;
            owners.Add(child.GetMeta(Meta).AsString());
        }
        return owners;
    }

    // A copy of a game control (look, sizes, overrides; no script, no signals), renamed.
    private static T Copy<T>(Node source, string name)
        where T : Node
    {
        var copy = (T)source.Duplicate((int)Node.DuplicateFlags.Groups);
        copy.Name = name.Replace(' ', '_');
        return copy;
    }

    // Labels are the owning mod's messages: shown in its language, and redrawn when the language changes.
    private static void SetText(Node node, string owner, string message)
    {
        if (node is not (Button or Label))
            return;
        ((Control)node).AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        CoreLabels.Set(node, owner, message);
    }

    private static void Open(Placed placed, MenuEntry entry)
    {
        var menu = placed.Menu;
        if (!GodotObject.IsInstanceValid(menu))
            return;
        if (!placed.Hosts.TryGetValue(entry, out var host) || !GodotObject.IsInstanceValid(host))
        {
            try
            {
                host = MakeHost(placed, entry);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Write(entry.Owner, $"ERROR menu panel '{entry.Label}' failed to build: {e}");
                return;
            }
            placed.Hosts[entry] = host;
        }
        if (menu.Options is { } options)
            options.Visible = false;
        host.Visible = true;
        menu.Visible = true;
    }

    // An action entry: the menu goes first, so whatever the action opens isn't closed by the menu showing.
    private static void Run(Placed placed, MenuEntry entry)
    {
        if (GodotObject.IsInstanceValid(placed.Menu))
            placed.Menu.Close();
        try
        {
            entry.Run?.Invoke();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write(entry.Owner, $"ERROR menu action '{entry.Label}' failed: {e}");
        }
    }

    private static void Close(Placed placed, Control host)
    {
        if (GodotObject.IsInstanceValid(host))
            host.Visible = false;
        if (GodotObject.IsInstanceValid(placed.Menu))
            placed.Menu.Open();
    }

    // The panel, in the Settings panel's place: a copy of its frame filled with the entry's pages, or a plain box.
    private static PanelContainer MakeHost(Placed placed, MenuEntry entry)
    {
        var menu = placed.Menu;
        var settings = Get<Control>(menu, SettingsPanelProperty);
        var host = new PanelContainer { Name = $"MelvorMods_{entry.Owner}_Panel".Replace(' ', '_'), Visible = false };
        host.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        host.SetMeta(Meta, entry.Owner);
        // The pages are the mod's: its translation domain, and only what it looks up is translated.
        CoreLabels.Adopt(host, entry.Owner);
        // Above the menu's other overlays (the wishlist button), whatever their order.
        host.ZIndex = 1;
        var frame = settings != null ? NativeFrame(settings, entry, () => Close(placed, host)) : null;
        if (frame == null)
        {
            Log.Write(entry.Owner, "WARN the game's Settings panel couldn't be copied; panel drawn plain");
            frame = PlainFrame(entry, () => Close(placed, host));
        }
        host.AddChild(frame);

        if (settings?.GetParent() is { } parent)
        {
            parent.AddChild(host);
            host.LayoutMode = settings.LayoutMode;
            host.AnchorLeft = settings.AnchorLeft;
            host.AnchorTop = settings.AnchorTop;
            host.AnchorRight = settings.AnchorRight;
            host.AnchorBottom = settings.AnchorBottom;
            host.OffsetLeft = settings.OffsetLeft;
            host.OffsetTop = settings.OffsetTop;
            host.OffsetRight = settings.OffsetRight;
            host.OffsetBottom = settings.OffsetBottom;
            host.GrowHorizontal = settings.GrowHorizontal;
            host.GrowVertical = settings.GrowVertical;
            host.SizeFlagsHorizontal = settings.SizeFlagsHorizontal;
            host.SizeFlagsVertical = settings.SizeFlagsVertical;
            host.CustomMinimumSize = settings.CustomMinimumSize;
        }
        else
        {
            menu.AddChild(host);
            host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
            host.GrowHorizontal = Control.GrowDirection.Both;
            host.GrowVertical = Control.GrowDirection.Both;
        }
        return host;
    }

    // The Settings panel's frame (title tab, close button, sidebar, page frame) copied without scripts, with the
    // game's sidebar and pages taken out and the entry's put in. Null when its layout isn't what this expects.
    private static Control? NativeFrame(Control settings, MenuEntry entry, Action close)
    {
        var nav = Get<Button>(settings, NavButtonProperty);
        var view = Get<Control>(settings, ViewProperty);
        if (
            settings.GetChildCount() == 0
            || settings.GetChild(0) is not Control source
            || nav?.GetParent() is not Control category
            || category.GetParent() is not Control sidebar
            || category.GetChildCount() == 0
            || view?.GetParent() is not Control pageArea
            || view.GetChildCount() == 0
            || view.GetChild(0) is not Control pageSource
            || !source.IsAncestorOf(nav)
            || !source.IsAncestorOf(view)
        )
            return null;

        var frame = Copy<Control>(source, "Frame");
        Node In(Node original) => frame.GetNode(source.GetPathTo(original));
        if (
            frame.FindChild(CloseButtonName, true, false) is not Button closeButton
            || In(sidebar) is not Control ourSidebar
            || In(pageArea) is not Control ourPageArea
        )
        {
            frame.Free();
            return null;
        }
        closeButton.Pressed += close;
        // The panel's own title: the first title label outside the pages.
        foreach (var child in frame.GetChildren())
        {
            if (child != In(source.GetChild(0)) && child.FindChild(TitleName, true, false) is Label title)
            {
                SetText(title, entry.Owner, entry.Label);
                break;
            }
        }

        var heading = category.GetChild(0);
        Clear(ourSidebar);
        Clear(ourPageArea);
        var section = Copy<Container>(category, "Section");
        Clear(section);
        var ourHeading = Copy<Node>(heading, "Heading");
        foreach (var label in ourHeading.FindChildren("*", "Label", true, false))
            SetText(label, entry.Owner, entry.Label);
        section.AddChild(ourHeading);
        ourSidebar.AddChild(section);

        var group = new ButtonGroup();
        var pages = new List<Control>();
        foreach (var page in entry.Pages)
        {
            var button = Copy<Button>(nav, $"Page_{page.Title}");
            SetText(button, entry.Owner, page.Title);
            button.ToggleMode = true;
            button.ButtonGroup = group;
            section.AddChild(button);

            var holder = Copy<Control>(pageSource, $"Page_{page.Title}");
            if (holder.FindChild(TitleName, true, false) is Label pageTitle)
                SetText(pageTitle, entry.Owner, page.Title);
            var content = (Control)page.Build();
            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            if (holder.FindChildren("*", "ScrollContainer", true, false).FirstOrDefault() is ScrollContainer scroll)
            {
                Clear(scroll);
                scroll.AddChild(content);
            }
            else
            {
                holder.AddChild(content);
            }
            holder.Visible = pages.Count == 0;
            ourPageArea.AddChild(holder);
            pages.Add(holder);
            button.Toggled += on =>
            {
                if (!on)
                    return;
                foreach (var other in pages)
                    other.Visible = ReferenceEquals(other, holder);
            };
            if (pages.Count == 1)
                button.ButtonPressed = true;
        }
        return frame;
    }

    // Without the game's Settings panel: a title row with a close button, then the pages one under another.
    private static VBoxContainer PlainFrame(MenuEntry entry, Action close)
    {
        var box = new VBoxContainer { Name = "Frame" };
        var top = new HBoxContainer();
        var title = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        SetText(title, entry.Owner, entry.Label);
        top.AddChild(title);
        var closeButton = new Button { Text = "Close" };
        closeButton.Pressed += close;
        top.AddChild(closeButton);
        box.AddChild(top);
        foreach (var page in entry.Pages)
            box.AddChild((Control)page.Build());
        return box;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
            Free(child);
    }

    // Out of the tree now, so a replacement can take its name; freed at the end of the frame.
    private static void Free(Node node)
    {
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }

    // Escape on an open panel: back to the menu, as for the game's own sub-panels.
    private static bool CloseOpenHost(PauseMenu menu)
    {
        if (!_menus.TryGetValue(menu.GetInstanceId(), out var placed))
            return false;
        var open = placed.Hosts.Values.FirstOrDefault(h => GodotObject.IsInstanceValid(h) && h.Visible);
        if (open == null)
            return false;
        Close(placed, open);
        return true;
    }

    // The modal host, where this copy has one: the menu showing closes its modals, and Escape closes the top one
    // first. Without a modal host these do nothing.
    static partial void CloseModals();

    static partial void CancelModal(ref bool handled);

    private static bool CancelModal()
    {
        var handled = false;
        CancelModal(ref handled);
        return handled;
    }

    private static T? Get<T>(object target, string property)
        where T : class
    {
        try
        {
            return AccessTools.Property(target.GetType(), property)?.GetValue(target) as T;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"WARN reading {target.GetType().Name}.{property} failed: {e.Message}");
            return null;
        }
    }

    // Escape: an open Core modal closes first (the pause menu stays shut), then an open menu panel goes back to the
    // menu; otherwise the game's own handling.
    [HarmonyPatchCategory(Standalone.PatchCategory)]
    [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu._UnhandledInput))]
    private static class EscapeHook
    {
        private static bool Prefix(PauseMenu __instance, InputEvent @event)
        {
            if (!@event.IsActionPressed(InputActions.UiCancel) || !(CancelModal() || CloseOpenHost(__instance)))
                return true;
            __instance.GetViewport().SetInputAsHandled();
            return false;
        }
    }
}
