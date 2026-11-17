using Xunit;

namespace MelvorMods.Multitasking.Tests;

// Part.Report is static; xunit runs one class's tests one at a time, and only this class sets it.
public sealed class PartTests : IDisposable
{
    private readonly List<string> _reports = [];

    public PartTests() => Part.Report = _reports.Add;

    public void Dispose() => Part.Report = null;

    [Fact]
    public void APartWhoseGameCodeIsMissingIsOffAndSaysWhy()
    {
        var part = new Part("Legacy bars", () => throw new MissingMemberException("GameSidebarLegacy._options"));

        Assert.False(part.Available);
        Assert.False(part.On);
        Assert.Single(_reports);
        Assert.Contains("Legacy bars", _reports[0], StringComparison.Ordinal);
        Assert.Contains("_options", _reports[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AnOffPartRunsNothingAndGivesTheGamesAnswer()
    {
        var part = new Part("Footer", () => throw new MissingMethodException());
        var ran = false;

        part.Run(() => ran = true);

        Assert.False(ran);
        Assert.True(part.Run(() => false, off: true));
    }

    [Fact]
    public void TheFirstFailureSwitchesThePartOffWithOneError()
    {
        var part = new Part("Footer", () => { });
        var runs = 0;

        for (var i = 0; i < 5; i++)
            part.Run(() =>
            {
                runs++;
                throw new InvalidOperationException("broken");
            });

        Assert.Equal(1, runs);
        Assert.False(part.On);
        Assert.Single(_reports);
        Assert.Contains("Footer", _reports[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AFailureGivesTheGamesAnswerFromThenOn()
    {
        var part = new Part("Parallel starts", () => { });

        Assert.Equal(1, part.Run(() => 1, off: 0));
        Assert.Equal(0, part.Run<int>(() => throw new InvalidOperationException(), off: 0));
        Assert.Equal(0, part.Run(() => 1, off: 0));
    }

    [Fact]
    public void APartIsCheckedOnce()
    {
        var checks = 0;
        var part = new Part("Rings", () => checks++);

        _ = part.Available;
        _ = part.On;
        part.Run(() => { });

        Assert.Equal(1, checks);
        Assert.Empty(_reports);
    }

    [Fact]
    public void PartsFailApart()
    {
        var footer = new Part("Footer", () => { });
        var rings = new Part("Rings", () => { });

        footer.Run(() => throw new InvalidOperationException());

        Assert.False(footer.On);
        Assert.True(rings.On);
    }
}
