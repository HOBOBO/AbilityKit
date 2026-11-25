using AbilityKit.Demo.Moba.Services;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class MobaStateSyncInputTerminalLedgerTests
{
    private static readonly Guid OriginalConnection = Guid.NewGuid();

    [Fact]
    public void TerminatedOutOfOrderWithholdsProofUntilGapCloses()
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed);
        Assert.True(ledger.TryRead(1, 10, 1, OriginalConnection, out var initial));
        Assert.Equal(1L, initial);

        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 3, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 2, MobaFrameInputDisposition.Rejected);
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 3, MobaFrameInputDisposition.Duplicate);
        Assert.True(ledger.TryRead(1, 10, 1, OriginalConnection, out var closed));
        Assert.Equal(3L, closed);
    }

    [Theory]
    [InlineData(MobaFrameInputDisposition.Unknown)]
    [InlineData(MobaFrameInputDisposition.Duplicate)]
    public void UnprovenOutcomeDoesNotAuthorizePrefix(MobaFrameInputDisposition disposition)
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, disposition);
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 2, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
    }

    [Fact]
    public void LifetimeChangeAndFailedWorldTickDoNotReuseProof()
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 2, OriginalConnection, out _));
        ledger.ObserveCommitted(1, 10, 2, OriginalConnection, 2, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 2, OriginalConnection, out _));

        ledger.ObserveCommitted(2, 20, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed);
        Assert.True(ledger.TryRead(2, 20, 1, OriginalConnection, out _));
        ledger.Invalidate(2);
        Assert.False(ledger.TryRead(2, 20, 1, OriginalConnection, out _));
    }

    [Fact]
    public void BoundedEvidenceDoesNotSilentlyForgetGap()
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        for (ulong sequence = 2; sequence <= 66; sequence++)
            ledger.ObserveCommitted(1, 10, 1, OriginalConnection, sequence, MobaFrameInputDisposition.Executed);
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
    }

    [Fact]
    public void ANewInputProducerOnTheSameConnectionCannotCloseThePreviousGap()
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        var generation = Guid.NewGuid();
        var successor = Guid.NewGuid();
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed, generation);
        Assert.True(ledger.TryRead(1, 10, 1, OriginalConnection, out var first, generation));
        Assert.Equal(1, first);
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _, successor));
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));

        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 3, MobaFrameInputDisposition.Executed, generation);
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 2, MobaFrameInputDisposition.Rejected, successor);
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _, generation));
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _, successor));
    }

    [Fact]
    public void ReconnectedControllerCannotCloseOldConnectionGap()
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        var reconnect = Guid.NewGuid();
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 1, reconnect, out _));
        Assert.True(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 3, MobaFrameInputDisposition.Executed);
        ledger.ObserveCommitted(1, 10, 1, reconnect, 2, MobaFrameInputDisposition.Rejected);

        Assert.False(ledger.TryRead(1, 10, 1, reconnect, out _));
        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnboundConnectionOrUnresolvedActorWithholdsPrefix(bool unboundConnection)
    {
        var ledger = new MobaStateSyncInputTerminalLedger();
        ledger.ObserveCommitted(1, 10, 1, OriginalConnection, 1, MobaFrameInputDisposition.Executed);
        Assert.False(ledger.TryRead(1, 10, 1, Guid.Empty, out _));
        ledger.ObserveCommitted(1, unboundConnection ? 10 : 0, 1,
            unboundConnection ? Guid.Empty : OriginalConnection, 2, MobaFrameInputDisposition.Executed);

        Assert.False(ledger.TryRead(1, 10, 1, OriginalConnection, out _));
    }
}
