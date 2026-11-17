using System.Globalization;
using MelvorMods.Core;

namespace MelvorMods.Multitasking;

internal sealed class MultitaskingMod : IMod
{
    private Running? _running;
    private RunningList? _list;
    private FooterChip? _chip;
    private Action? _retext;
    private static FooterChip? _chipForChecks;
    private SidebarRings? _rings;
    private bool _listHidden;

    public void Load(ModContext context)
    {
        Tr.Use(context.Text);
        Part.Report = context.Log.Error;
        if (!context.GameBuildListed)
            context.Log.Warn("this game build isn't one the mod was checked on; a part it breaks switches itself off");
        context.PatchAll();

        var running = _running = new Running();
        var list = _list = new RunningList(running, node => context.Own(node));
        var rings = _rings = new SidebarRings(running, context.Log, node => context.Own(node));
        var chip =
            _chip =
            _chipForChecks =
                new FooterChip(
                    context.Log,
                    node => context.Own(node),
                    () => ListPart(() => list.Toggle(_chip?.ScreenRect))
                )
                {
                    Label = RunningMath.ChipLabel(0),
                };
        context.OnNodeAdded(node => ListPart(() => chip.OnNodeAdded(node)));
        context.OnReady(tree => ListPart(() => chip.Scan(tree)));

        running.Changed += () =>
        {
            ListPart(() =>
            {
                var count = running.Actions.Count;
                chip.Label = RunningMath.ChipLabel(count);
                chip.Visible = RunningMath.ChipShown(count);
                list.Rebuild();
            });
            Parts.Rings.Run(rings.Rebuild);
            LegacyBarsFollowActions.Refresh(running.View);
        };
        _retext = () =>
            ListPart(() =>
            {
                list.Retext();
                chip.Retext();
            });
        context.Text.Changed += _retext;
        context.OnFrame(() =>
        {
            // Every part's start and stop updates come from here; it belongs to the list, their main reader.
            ListPart(running.Frame);
            if (running.IsOffline)
                return;
            ListPart(list.Refresh);
            Parts.Rings.Run(rings.Frame);
        });

        // In release builds too: the mod's in-game checks start from nothing running and say what runs.
        context.Commands.Register(
            "mt",
            "mt list | mt stopall | mt stopgame",
            "Lists the running actions (skill, [footer] on the one the footer shows, what it is doing, progress), or "
                + "stops them all; stopgame asks the game itself to stop everything, as another mod would.",
            call => Command(call, running)
        );
        context.Commands.Register(
            "mtchip",
            "mtchip",
            "Describes the footer chip: its label, whether it shows, and where.",
            call => call.Ok(_chipForChecks?.Describe() ?? "no chip"),
            dev: true
        );
    }

    public void Unload(ModContext context)
    {
        if (_retext != null)
            context.Text.Changed -= _retext;
        _list?.Free();
        _rings?.Clear();
        _running?.Unhook();
        Tr.Use(null);
        Part.Report = null;
        _list = null;
        _running = null;
        _chip = null;
        _rings = null;
        _retext = null;
    }

    // Runs the chip's and the list's code under their part; once it goes off, the chip hides and the list closes.
    private void ListPart(Action body)
    {
        Parts.RunningList.Run(body);
        if (Parts.RunningList.On || _listHidden)
            return;
        _listHidden = true;
        try
        {
            if (_chip != null)
                _chip.Visible = false;
            _list?.Close();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Hiding is a courtesy: the part is off either way, and its error is already in the log.
        }
    }

    private static void Command(CommandCall call, Running running)
    {
        switch (call.Args.Count > 0 ? call.Args[0] : null)
        {
            case "list":
                var actions = running.Actions;
                var footer = Running.InFooterIndex(actions);
                var lines = actions.Select(
                    (a, i) =>
                    {
                        var timer = a.ActionTimer;
                        var progress = RunningMath.Progress(a.IsTimerIndeterminate, timer?.TicksLeft, timer?.WaitTicks);
                        var shown = progress is { } p ? p.ToString("0.00", CultureInfo.InvariantCulture) : "-";
                        var mark = i == footer ? " [footer]" : "";
                        return $"{a.Entity.Id}{mark} | {a.CurrentDescription?.Id ?? "-"} | {shown}";
                    }
                );
                call.Ok($"{actions.Count} running" + string.Concat(lines.Select(l => "\n" + l)));
                break;
            case "stopall":
                var left = running.StopAll();
                call.Ok($"{left} still running");
                break;
            // For the checks: proves a stop-all that isn't a start's own still stops everything with the mod loaded.
            case "stopgame":
                call.Ok($"{running.StopAsGame()} still running");
                break;
            default:
                call.Error("usage: mt list | mt stopall | mt stopgame");
                break;
        }
    }
}
