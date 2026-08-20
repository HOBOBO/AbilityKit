using AbilityKit.Ability.Host.Extensions.Client.StateSync;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

public sealed class ShooterRemoteInputSubmitQueueTests
{
    [Fact]
    public void DefaultQueueReplacementKeepsLatestValue()
    {
        var firstCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var submitted = new List<int>();
        var queue = new RemoteClientInputSubmitQueue<int, int>(
            (value, _) =>
            {
                submitted.Add(value);
                return submitted.Count == 1 ? firstCompletion.Task : Task.FromResult(value);
            },
            TimeSpan.FromSeconds(1));

        Assert.True(queue.SubmitOrQueue(1));
        Assert.False(queue.SubmitOrQueue(2));
        Assert.False(queue.SubmitOrQueue(3));

        firstCompletion.SetResult(1);
        queue.CompleteIfFinished();

        Assert.Equal(new[] { 1, 3 }, submitted);
        Assert.Equal(1, queue.QueuedCount);
        Assert.Equal(1, queue.ReplacedCount);
    }

    [Fact]
    public void MergeHookCombinesReplacedQueuedValues()
    {
        var firstCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var submitted = new List<int>();
        var queue = new RemoteClientInputSubmitQueue<int, int>(
            (value, _) =>
            {
                submitted.Add(value);
                return submitted.Count == 1 ? firstCompletion.Task : Task.FromResult(value);
            },
            TimeSpan.FromSeconds(1),
            mergeQueued: (queued, latest) => queued + latest);

        queue.SubmitOrQueue(1);
        queue.SubmitOrQueue(2);
        queue.SubmitOrQueue(3);
        firstCompletion.SetResult(1);
        queue.CompleteIfFinished();

        Assert.Equal(new[] { 1, 5 }, submitted);
    }

    [Fact]
    public void BoundedWindowPipelinesRequestsUntilCapacityThenKeepsLatestQueuedInput()
    {
        var completions = Enumerable.Range(0, 5)
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var submitted = new List<int>();
        var queue = new RemoteClientInputSubmitQueue<int, int>(
            (value, _) =>
            {
                submitted.Add(value);
                return completions[value - 1].Task;
            },
            TimeSpan.FromSeconds(1),
            maxInFlight: 3);

        Assert.True(queue.SubmitOrQueue(1));
        Assert.True(queue.SubmitOrQueue(2));
        Assert.True(queue.SubmitOrQueue(3));
        Assert.False(queue.SubmitOrQueue(4));
        Assert.False(queue.SubmitOrQueue(5));
        Assert.Equal(3, queue.PendingCount);
        Assert.True(queue.HasQueued);
        Assert.Equal(1, queue.QueuedCount);
        Assert.Equal(1, queue.ReplacedCount);

        completions[1].SetResult(2);
        queue.CompleteIfFinished();

        Assert.Equal(new[] { 1, 2, 3, 5 }, submitted);
        Assert.Equal(3, queue.PendingCount);
        Assert.False(queue.HasQueued);
    }

    [Fact]
    public void OutOfOrderCompletionDoesNotReplaceNewestCompletedResultWithOlderResult()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new RemoteClientInputSubmitQueue<int, int>(
            (value, _) => value == 1 ? first.Task : second.Task,
            TimeSpan.FromSeconds(1),
            maxInFlight: 2);

        queue.SubmitOrQueue(1);
        queue.SubmitOrQueue(2);
        second.SetResult(20);
        queue.CompleteIfFinished();
        first.SetResult(10);
        queue.CompleteIfFinished();

        Assert.Equal(20, queue.LastResult);
        Assert.Equal(2, queue.CompletedCount);
        Assert.False(queue.HasPending);
    }

    [Fact]
    public void ShooterMergeCarriesFireEdgeOntoLatestMovementAndMetadata()
    {
        var queued = CreateResult(
            requestedFrame: 10,
            submissionId: 100,
            moveX: 1f,
            moveY: 0f,
            aimX: 1f,
            aimY: 0f,
            fire: true,
            attackSlot: ShooterPlayerAttackSlots.Spread);
        var latest = CreateResult(
            requestedFrame: 11,
            submissionId: 101,
            moveX: 0f,
            moveY: 1f,
            aimX: 0f,
            aimY: -1f,
            fire: false,
            attackSlot: ShooterPlayerAttackSlots.Primary);

        var merged = ShooterRemoteInputSubmitStrategy.MergeQueuedInput(queued, latest);
        var decoded = Assert.Single(ShooterInputCodec.Deserialize(merged.Packet.Payload));

        Assert.Equal(latest.AcceptedInputs, merged.AcceptedInputs);
        Assert.Equal(latest.RequestedFrame, merged.RequestedFrame);
        Assert.Equal(latest.SubmissionId, merged.SubmissionId);
        Assert.Equal(latest.Packet.OpCode, merged.Packet.OpCode);
        Assert.Equal(latest.Packet.Command.PlayerId, merged.Packet.Command.PlayerId);
        Assert.Equal(latest.Packet.Command.MoveX, merged.Packet.Command.MoveX);
        Assert.Equal(latest.Packet.Command.MoveY, merged.Packet.Command.MoveY);
        Assert.Equal(latest.Packet.Command.AimX, merged.Packet.Command.AimX);
        Assert.Equal(latest.Packet.Command.AimY, merged.Packet.Command.AimY);
        Assert.True(merged.Packet.Command.Fire);
        Assert.Equal(ShooterPlayerAttackSlots.Spread, merged.Packet.Command.AttackSlot);
        Assert.True(decoded.Fire);
        Assert.Equal(merged.Packet.Command.AttackSlot, decoded.AttackSlot);
        Assert.Equal(merged.Packet.Command.MoveY, decoded.MoveY);
    }

    [Fact]
    public void ShooterMergeKeepsLatestPacketWhenLatestAlreadyFires()
    {
        var queued = CreateResult(10, 100, 1f, 0f, 1f, 0f, true, ShooterPlayerAttackSlots.Spread);
        var latest = CreateResult(11, 101, 0f, 1f, 0f, 1f, true, ShooterPlayerAttackSlots.Twin);

        var merged = ShooterRemoteInputSubmitStrategy.MergeQueuedInput(queued, latest);

        Assert.Equal(latest.Packet.Payload, merged.Packet.Payload);
        Assert.Equal(ShooterPlayerAttackSlots.Twin, merged.Packet.Command.AttackSlot);
        Assert.Equal(latest.SubmissionId, merged.SubmissionId);
    }

    private static ShooterClientInputSubmitResult CreateResult(
        int requestedFrame,
        long submissionId,
        float moveX,
        float moveY,
        float aimX,
        float aimY,
        bool fire,
        int attackSlot)
    {
        var packet = ShooterClientInputBuilder.CreatePacket(7, moveX, moveY, aimX, aimY, fire, attackSlot);
        return new ShooterClientInputSubmitResult(1, requestedFrame, in packet, submissionId);
    }
}
