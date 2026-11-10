using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.FrameSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaShieldChangeEventTests
{
    [Fact]
    public void Personal_shield_add_and_refresh_publish_committed_value_changes()
    {
        var service = new MobaShieldService();
        var changes = new List<ShieldChangeEvent>();
        service.Changed += (in ShieldChangeEvent change) => changes.Add(change);

        var instance = service.AddShield(41, CreateLayer(50, ShieldStackingPolicy.RefreshSameShieldAndSource));
        service.AddShield(41, CreateLayer(80, ShieldStackingPolicy.RefreshSameShieldAndSource));

        Assert.Equal(2, changes.Count);
        Assert.Equal(ShieldChangeKind.Added, changes[0].Kind);
        Assert.Equal(41, changes[0].ActorId);
        Assert.Equal(instance, changes[0].InstanceId);
        Assert.Equal(0, changes[0].OldValue);
        Assert.Equal(50, changes[0].NewValue);
        Assert.Equal(ShieldChangeKind.Refreshed, changes[1].Kind);
        Assert.Equal(instance, changes[1].InstanceId);
        Assert.Equal(50, changes[1].OldValue);
        Assert.Equal(80, changes[1].NewValue);
        Assert.True(changes[1].Revision > changes[0].Revision);
        Assert.Equal(80, service.GetTotalRemaining(41));
    }

    [Fact]
    public void Change_subscriber_failure_does_not_cancel_shield_commit()
    {
        var service = new MobaShieldService();
        service.Changed += (in ShieldChangeEvent _) => throw new InvalidOperationException("presentation failed");

        var instance = service.AddShield(41, CreateLayer(25, ShieldStackingPolicy.Independent));

        Assert.True(instance > 0);
        Assert.Equal(25, service.GetTotalRemaining(41));
    }

    [Fact]
    public void Replacement_removal_is_published_after_the_new_layer_is_committed()
    {
        var service = new MobaShieldService();
        var layer = CreateLayer(30, ShieldStackingPolicy.Independent);
        layer.Priority = 1;
        var replacedInstance = service.AddShield(41, layer);
        var replacement = CreateLayer(60, ShieldStackingPolicy.ReplaceLowerPriority);
        replacement.Priority = 2;
        var removalObservedCommittedTotal = false;
        service.Changed += (in ShieldChangeEvent change) =>
        {
            if (change.Kind == ShieldChangeKind.Removed && change.InstanceId == replacedInstance)
                removalObservedCommittedTotal = service.GetTotalRemaining(41) == 60;
        };

        service.AddShield(41, replacement);

        Assert.True(removalObservedCommittedTotal);
        Assert.Equal(60, service.GetTotalRemaining(41));
    }

    [Fact]
    public void Personal_shield_removal_and_expiry_publish_terminal_changes()
    {
        var service = new MobaShieldService();
        var changes = new List<ShieldChangeEvent>();
        var expiredObservedCommittedState = false;
        service.Changed += (in ShieldChangeEvent change) =>
        {
            changes.Add(change);
            if (change.Kind == ShieldChangeKind.Expired)
                expiredObservedCommittedState = service.GetTotalRemaining(41) == 0;
        };
        var removedInstance = service.AddShield(41, CreateLayer(30, ShieldStackingPolicy.Independent));
        var expiring = CreateLayer(20, ShieldStackingPolicy.Independent);
        expiring.ExpireFrame = 5;
        var expiringInstance = service.AddShield(41, expiring);

        Assert.True(service.RemoveShield(41, removedInstance));
        Assert.Equal(1, service.CleanupExpired(5));

        Assert.Contains(changes, change => change.Kind == ShieldChangeKind.Removed &&
            change.InstanceId == removedInstance && change.OldValue == 30 && change.NewValue == 0);
        Assert.Contains(changes, change => change.Kind == ShieldChangeKind.Expired &&
            change.InstanceId == expiringInstance && change.OldValue == 20 && change.NewValue == 0);
        Assert.True(expiredObservedCommittedState);
    }

    [Fact]
    public void Shared_pool_join_and_leave_publish_member_changes_with_pool_identity()
    {
        var service = new MobaShieldService();
        var changes = new List<ShieldChangeEvent>();
        service.Changed += (in ShieldChangeEvent change) => changes.Add(change);
        var template = CreateLayer(40, ShieldStackingPolicy.Independent);
        template.SharePolicy = ShieldSharePolicy.SharedTotalPool;
        var poolBinding = service.AddSharedShield(new[] { 41, 42 }, template);
        var poolId = service.GetSharedPoolIdForActor(41);
        var joined = changes.FindAll(change => change.Kind == ShieldChangeKind.MemberJoined);

        Assert.Equal(2, joined.Count);
        Assert.All(joined, change => Assert.Equal(poolId, change.PoolId));
        Assert.True(service.LeaveSharedPool(poolId, 42, 3));
        Assert.Contains(changes, change => change.Kind == ShieldChangeKind.MemberLeft &&
            change.ActorId == 42 && change.PoolId == poolId);
        Assert.True(poolBinding > 0);
    }

    [Fact]
    public void Shared_pool_expiry_publishes_one_terminal_event_per_active_member_after_removal()
    {
        var service = new MobaShieldService();
        var changes = new List<ShieldChangeEvent>();
        var poolExistsDuringTerminalEvent = true;
        service.Changed += (in ShieldChangeEvent change) =>
        {
            changes.Add(change);
            if (change.Kind == ShieldChangeKind.Expired)
                poolExistsDuringTerminalEvent &= service.TryGetSharedPool(change.PoolId, out _);
        };
        var template = CreateLayer(40, ShieldStackingPolicy.Independent);
        template.SharePolicy = ShieldSharePolicy.SharedTotalPool;
        template.ExpireFrame = 5;
        service.AddSharedShield(new[] { 41, 42 }, template);
        var poolId = service.GetSharedPoolIdForActor(41);

        service.CleanupExpired(5);

        var expired = changes.FindAll(change => change.Kind == ShieldChangeKind.Expired);
        Assert.Equal(2, expired.Count);
        Assert.All(expired, change =>
        {
            Assert.Equal(poolId, change.PoolId);
            Assert.Equal(40, change.OldValue);
            Assert.Equal(0, change.NewValue);
        });
        Assert.False(poolExistsDuringTerminalEvent);
        Assert.False(service.TryGetSharedPool(poolId, out _));
        Assert.Equal(0, service.GetPresentationSnapshot(41).TotalVisibleValue);
    }

    [Fact]
    public void Rollback_correction_reports_visible_state_difference_after_restore()
    {
        var service = new MobaShieldService();
        var changes = new List<ShieldChangeEvent>();
        service.Changed += (in ShieldChangeEvent change) => changes.Add(change);
        service.AddShield(41, CreateLayer(30, ShieldStackingPolicy.Independent));
        var previousValues = new Dictionary<int, AbilityKit.Deterministic.Fixed64>
        {
            [41] = 50,
            [42] = 20,
        };

        service.PublishRollbackCorrections(previousValues, 12);

        var corrections = changes.FindAll(change => change.Kind == ShieldChangeKind.RollbackCorrection);
        Assert.Collection(corrections,
            change => Assert.Equal((41, 50L, 30L, 12),
                (change.ActorId, (long)change.OldValue, (long)change.NewValue, change.Frame)),
            change => Assert.Equal((42, 20L, 0L, 12),
                (change.ActorId, (long)change.OldValue, (long)change.NewValue, change.Frame)));
    }

    [Fact]
    public void Rollback_import_publishes_correction_after_authoritative_state_is_restored()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(41);
        actors.Register(41, actor);
        var service = new MobaShieldService();
        service.AddShield(41, CreateLayer(30, ShieldStackingPolicy.Independent));
        var provider = new MobaShieldRollbackProvider(actors, service);
        var payload = provider.Export(new FrameIndex(10));
        service.RemoveShields(41, shieldId: 0, sourceActorId: 12, removeAll: true);
        var correctionObservedRestoredState = false;
        service.Changed += (in ShieldChangeEvent change) =>
        {
            if (change.Kind == ShieldChangeKind.RollbackCorrection)
                correctionObservedRestoredState = service.GetPresentationSnapshot(change.ActorId).TotalVisibleValue == 30;
        };

        provider.Import(new FrameIndex(10), payload);

        Assert.True(correctionObservedRestoredState);
        Assert.Equal(30, service.GetPresentationSnapshot(41).TotalVisibleValue);
    }

    private static ShieldLayer CreateLayer(int value, ShieldStackingPolicy stackingPolicy) => new()
    {
        ShieldId = 7,
        SourceActorId = 12,
        TargetActorId = 41,
        CurrentValue = value,
        MaxValue = value,
        InitialValue = value,
        AbsorbRatio = 1,
        StackingPolicy = stackingPolicy,
    };
}
