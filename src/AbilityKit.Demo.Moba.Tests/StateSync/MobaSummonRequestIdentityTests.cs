using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaSummonRequestIdentityTests
{
    [Fact]
    public void Rejected_request_is_cached_per_ordinal_and_restored_with_summon_state()
    {
        var service = new MobaSummonService();
        var identity = new MobaSummonInputIdentity(12, 3, 91, 44);
        var first = service.ReserveInputRequest(in identity);
        service.RejectInputRequest(in first);

        Assert.True(service.TryGetInputRequestResult(in first, out var accepted, out var actorId));
        Assert.False(accepted);
        Assert.Equal(0, actorId);

        var checkpoint = service.CaptureRollbackState();
        var second = service.ReserveInputRequest(in identity);
        Assert.Equal(2, second.Ordinal);
        service.RejectInputRequest(in second);

        service.RestoreRollbackState(checkpoint);
        Assert.True(service.TryGetInputRequestResult(in first, out accepted, out actorId));
        Assert.False(accepted);
        Assert.Equal(0, actorId);
        var replayed = service.ReserveInputRequest(in identity);
        Assert.Equal(second.Ordinal, replayed.Ordinal);
        Assert.False(service.TryGetInputRequestResult(in replayed, out _, out _));
    }

    [Fact]
    public void Pipeline_context_rollback_preserves_originating_input_identity()
    {
        var identity = new MobaSummonInputIdentity(12, 3, 91, 44);
        var request = new SkillCastRequest(98123, 2, 12, 0, Vec3.Zero, Vec3.Forward,
            null, null, null, null, summonInputIdentity: identity);
        var original = new SkillPipelineContext();
        original.Initialize(new object(), request);

        var checkpoint = original.CaptureRollbackState();
        var restored = new SkillPipelineContext();
        restored.Initialize(new object(), request);
        restored.RestoreRollbackState(checkpoint);

        Assert.Equal(identity, restored.SummonInputIdentity);
        Assert.True(restored.TryGetExecutionSnapshot(out var snapshot));
        Assert.Equal(identity, snapshot.SummonInputIdentity);
    }

    [Fact]
    public void Trigger_snapshot_mutations_preserve_input_identity()
    {
        var identity = new MobaSummonInputIdentity(12, 3, 91, 44);
        var snapshot = new MobaTriggerExecutionSnapshot(EffectContextKind.Skill,
            12, 0, 701, 701, 701, 1, 98123, 44).WithSummonInputIdentity(in identity);

        var updated = snapshot.WithTrigger(2, 98124).WithFrame(45);
        var rebuilt = MobaTriggerExecutionSnapshotBuilder.Create().FromSnapshot(in updated).Build();

        Assert.Equal(identity, rebuilt.SummonInputIdentity);
        Assert.Equal(2, rebuilt.TriggerId);
        Assert.Equal(45, rebuilt.Frame);
    }

    [Fact]
    public void Summon_rollback_payload_restores_request_journal_and_ordinal_allocator()
    {
        var identity = new MobaSummonInputIdentity(12, 3, 91, 44);
        var source = new MobaSummonService();
        var ticket = source.ReserveInputRequest(in identity);
        source.RejectInputRequest(in ticket);
        var sourceProvider = new MobaSummonRollbackProvider(source, new MobaActorRegistry(),
            new MobaSkillCastRuntimeService());
        var payload = sourceProvider.Export(default);

        var restored = new MobaSummonService();
        var restoredProvider = new MobaSummonRollbackProvider(restored, new MobaActorRegistry(),
            new MobaSkillCastRuntimeService());
        restoredProvider.Import(default, payload);

        Assert.True(restored.TryGetInputRequestResult(in ticket, out var accepted, out var actorId));
        Assert.False(accepted);
        Assert.Equal(0, actorId);
        Assert.Equal(2, restored.ReserveInputRequest(in identity).Ordinal);
    }

    [Fact]
    public void Confirmation_prunes_results_without_resetting_delayed_descendant_ordinals()
    {
        var service = new MobaSummonService();
        var identity = new MobaSummonInputIdentity(12, 3, 91, 44);
        var first = service.ReserveInputRequest(in identity);
        service.RejectInputRequest(in first);

        service.PruneConfirmedRequests(44);
        Assert.False(service.TryGetInputRequestResult(in first, out _, out _));

        var delayedDescendant = service.ReserveInputRequest(in identity);
        Assert.Equal(2, delayedDescendant.Ordinal);
    }
}
