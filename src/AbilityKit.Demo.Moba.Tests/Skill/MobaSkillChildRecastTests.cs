using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillChildRecastTests
{
    [Fact]
    public void Independent_recast_keeps_original_cast_and_rejects_flight_duplicate_and_stale_child()
    {
        var casts = new MobaSkillCastRuntimeService();
        var original = Create(casts, 7, 101);
        Assert.True(casts.TryBindChildChannel(original.Handle, 23));
        Assert.False(casts.TryBindChildChannel(Create(casts, 7, 102).Handle, 23));
        var flight = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, 11, 1001, 201);
        Assert.True(casts.RetainChild(original.Handle, flight, out var flightRetain));
        Assert.False(casts.TrySelectRecastArea(7, 23, out _, out _));
        // Retain the landed Area BEFORE releasing the projectile and ending the input flow.
        var area = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, 12, 1002, 202);
        Assert.True(casts.RetainChild(original.Handle, area, out var areaRetain));
        Assert.True(casts.ReleaseChild(flightRetain));
        casts.MarkPipelineEnded(original.Handle, MobaSkillRuntimeEndReason.PipelineCompleted);
        Assert.True(casts.TrySelectRecastArea(7, 23, out var binding, out var error), error);
        Assert.False(casts.TrySelectRecastArea(8, 23, out _, out _));
        var recast = Create(casts, 7, 103);
        Assert.True(casts.BindRecastArea(recast.Handle, binding));
        var snapshot = casts.CaptureRollbackSnapshot();
        Assert.True(casts.TryClaimChildTerminal(binding));
        Assert.False(casts.TryClaimChildTerminal(binding));
        Assert.False(casts.TrySelectRecastArea(7, 23, out _, out _));
        casts.ReleaseChild(areaRetain);
        Assert.False(casts.TryGetRecastArea(recast.Handle, out _));
        var replacement = Create(casts, 7, 104);
        Assert.True(casts.TryBindChildChannel(replacement.Handle, 23));
        casts.RetainChild(replacement.Handle, area, out _);
        Assert.False(casts.TryGetRecastArea(recast.Handle, out _));
        Assert.False(casts.TryClaimChildTerminal(binding));
        // Restore the authoritative associations and terminal latch without replaying spawn or explosion.
        casts.RestoreRollbackSnapshot(snapshot);
        Assert.True(casts.TryGetRecastArea(recast.Handle, out var restored));
        Assert.Equal(original.Handle, restored.Original);
        Assert.Equal(area, restored.Child);
        Assert.True(casts.TryClaimChildTerminal(restored));
        Assert.False(casts.TryClaimChildTerminal(restored));
        Assert.Equal(0, casts.ValidateRetentionState().OrphanedRetains);
    }

    [Fact]
    public void Multiple_deployed_children_fail_instead_of_choosing_first()
    {
        var casts = new MobaSkillCastRuntimeService();
        var original = Create(casts, 9, 201);
        Assert.True(casts.TryBindChildChannel(original.Handle, 31));
        casts.RetainChild(original.Handle, new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, 1, 11), out _);
        casts.RetainChild(original.Handle, new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, 2, 12), out _);
        Assert.False(casts.TrySelectRecastArea(9, 31, out _, out var error));
        Assert.Contains("Ambiguous", error);
    }

    [Fact]
    public void Owner_death_revokes_only_owned_channels_and_restores_associations_on_rollback()
    {
        var casts = new MobaSkillCastRuntimeService();
        var owned = Create(casts, 7, 101);
        var other = Create(casts, 8, 102);
        var detached = Create(casts, 7, 103);
        Assert.True(casts.TryBindChildChannel(owned.Handle, 23));
        Assert.True(casts.TryBindChildChannel(other.Handle, 23));
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, 1, 11, 201);
        casts.RetainChild(owned.Handle, child, out _);
        casts.RetainChild(other.Handle, new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, 2, 12, 202), out _);
        casts.MarkPipelineEnded(owned.Handle, MobaSkillRuntimeEndReason.PipelineCompleted);
        var checkpoint = casts.CaptureRollbackSnapshot();
        var removed = new System.Collections.Generic.List<MobaSkillRuntimeChildRef>();
        casts.EndOwnerChildChannels(7, value =>
        {
            Assert.False(casts.HasChildChannel(7, 23)); // Revoked before the object can emit callbacks.
            removed.Add(value);
        });
        Assert.Equal(child, Assert.Single(removed));
        Assert.False(casts.TryGet(owned.Handle, out _));
        Assert.True(casts.TryGet(other.Handle, out _));
        Assert.True(casts.TryGet(detached.Handle, out _));
        casts.EndOwnerChildChannels(7, removed.Add);
        Assert.Single(removed);
        casts.RestoreRollbackSnapshot(checkpoint);
        Assert.True(casts.TrySelectRecastArea(7, 23, out var restored, out _));
        Assert.Equal(child, restored.Child);
        Assert.Equal(0, casts.ValidateRetentionState().OrphanedRetains);
    }

    private static MobaSkillCastRuntime Create(MobaSkillCastRuntimeService casts, int owner, int context) =>
        casts.Create(new MobaSkillCastRuntimeCreateRequest(101, 1, 1, 1, owner, 0, Vec3.Zero, Vec3.Forward, context));
}
