using Xunit;

namespace MelvorMods.Multitasking.Tests;

public class RunningMathTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void TheChipShowsFromTwoRunning(int running, bool shown) =>
        Assert.Equal(shown, RunningMath.ChipShown(running));

    [Theory]
    [InlineData(2, "+1")]
    [InlineData(4, "+3")]
    [InlineData(5, "+4")]
    [InlineData(0, "+0")]
    public void TheChipCountsTheActionsTheFooterDoesNotShow(int running, string label) =>
        Assert.Equal(label, RunningMath.ChipLabel(running));

    [Fact]
    public void WithoutAFightTheLastStartedIsShown()
    {
        Assert.Equal(3, RunningMath.ShownIndex(4, -1));
        Assert.Equal(0, RunningMath.ShownIndex(1, -1));
        Assert.Equal(-1, RunningMath.ShownIndex(0, -1));
    }

    [Fact]
    public void ARunningFightIsShownWhereverItStarted()
    {
        Assert.Equal(0, RunningMath.ShownIndex(3, 0));
        Assert.Equal(1, RunningMath.ShownIndex(3, 1));
        Assert.Equal(2, RunningMath.ShownIndex(3, 2));
    }

    [Fact]
    public void TheFooterShowsTheOpenPagesSkillWhenItRuns()
    {
        Assert.Equal(1, RunningMath.FooterIndex(3, 0, 1));
        Assert.Equal(2, RunningMath.FooterIndex(3, -1, 2));
    }

    [Fact]
    public void OnAnyOtherPageTheFooterShowsTheFightThenTheLastStarted()
    {
        Assert.Equal(0, RunningMath.FooterIndex(3, 0, -1));
        Assert.Equal(2, RunningMath.FooterIndex(3, -1, -1));
        Assert.Equal(-1, RunningMath.FooterIndex(0, -1, -1));
    }

    [Theory]
    [InlineData(10, 10, 0.0)]
    [InlineData(5, 10, 0.5)]
    [InlineData(0, 10, 1.0)]
    [InlineData(12, 10, 0.0)]
    public void ProgressFillsAsTicksRunDown(int left, int wait, double expected) =>
        Assert.Equal(expected, RunningMath.Progress(false, left, wait)!.Value, 6);

    [Fact]
    public void ActionsWithoutALengthHaveNoProgress()
    {
        Assert.Null(RunningMath.Progress(true, 5, 10));
        Assert.Null(RunningMath.Progress(false, null, null));
        Assert.Null(RunningMath.Progress(false, 5, 0));
    }
}
