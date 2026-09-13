using TouchDeck.App.Rendering;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// When the panel is allowed to rebuild itself. A button fires on the way down, so a button
/// that changes the page changes it with the finger still on the glass; rebuilding there
/// would destroy the button the gesture belongs to and drop the rest of that gesture onto
/// whatever the new page put in the same square.
/// </summary>
public sealed class SurfaceGateTests
{
    [Fact]
    public void NothingBeingPressedMeansRebuildNow()
    {
        var gate = new SurfaceGate();

        Assert.True(gate.Changed());
        Assert.False(gate.Owed);
    }

    [Fact]
    public void APageChangeUnderAFingerWaitsForThatFinger()
    {
        var gate = new SurfaceGate();

        gate.Down();

        Assert.False(gate.Changed());
        Assert.True(gate.Owed);
        Assert.True(gate.Held);

        Assert.True(gate.Up());
        Assert.False(gate.Owed);
        Assert.False(gate.Held);
    }

    [Fact]
    public void AReleaseWithNothingOwedDoesNotRebuild()
    {
        var gate = new SurfaceGate();

        gate.Down();

        Assert.False(gate.Up());
    }

    [Fact]
    public void TheLastFingerIsTheOneThatReleasesTheRebuild()
    {
        var gate = new SurfaceGate();

        gate.Down();
        gate.Down();
        gate.Changed();

        Assert.False(gate.Up());
        Assert.True(gate.Up());
    }

    [Fact]
    public void SeveralChangesWhileHeldStillOnlyRebuildOnce()
    {
        var gate = new SurfaceGate();

        gate.Down();

        Assert.False(gate.Changed());
        Assert.False(gate.Changed());
        Assert.False(gate.Changed());

        Assert.True(gate.Up());
        Assert.False(gate.Up());
    }

    [Fact]
    public void APressThatIsNeverReportedAsFinishedDoesNotStopThePanelForever()
    {
        var gate = new SurfaceGate();

        gate.Down();
        gate.Changed();

        Assert.True(gate.GiveUp());
        Assert.False(gate.Held);
        Assert.False(gate.Owed);

        // And having given up, the next change goes straight through.
        Assert.True(gate.Changed());
    }

    [Fact]
    public void GivingUpWithNothingOwedRebuildsNothing()
    {
        var gate = new SurfaceGate();

        gate.Down();

        Assert.False(gate.GiveUp());
    }

    [Fact]
    public void AStrayReleaseCannotPushTheCountBelowZero()
    {
        var gate = new SurfaceGate();

        gate.Up();
        gate.Up();
        gate.Down();

        Assert.True(gate.Held);
        Assert.False(gate.Changed());
        Assert.True(gate.Up());
    }
}
