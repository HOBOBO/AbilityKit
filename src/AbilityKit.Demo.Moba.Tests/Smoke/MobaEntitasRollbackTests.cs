using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaEntitasRollbackTests
{
    [Fact]
    public void Rollback_removes_predicted_actor_and_cast_and_rewinds_actor_ids()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var registry = new MobaActorRegistry();
        var actor = context.CreateEntity();
        actor.AddActorId(ids.Next());
        registry.Register(actor.actorId.Value, actor);
        var provider = new MobaEntitasEntityRollbackProvider(context, ids, registry, null);
        var frame = new FrameIndex(4);
        var payload = provider.Export(frame);

        var predicted = context.CreateEntity();
        predicted.AddActorId(ids.Next());
        registry.Register(predicted.actorId.Value, predicted);
        var cast = context.CreateEntity();
        cast.AddSkillCastInstanceId(101);

        provider.Import(frame, payload);

        Assert.True(actor.isEnabled);
        Assert.False(predicted.isEnabled);
        Assert.False(cast.isEnabled);
        Assert.False(registry.Contains(2));
        Assert.Equal(2, ids.Next());
    }

    [Fact]
    public void Component_snapshot_restores_presence_and_deep_copies_skill_runtime()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        actor.AddActorId(1);
        actor.AddLifetime(100);
        var original = new ActiveSkillRuntime { SkillId = 7, Level = 2, CurrentCharges = 3 };
        actor.AddSkillLoadout(new[] { original }, new[] { new PassiveSkillRuntime { PassiveSkillId = 9, Level = 1 } });
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var frame = new FrameIndex(4);
        var payload = provider.Export(frame);

        original.CurrentCharges = 0;
        actor.skillLoadout.PassiveSkills[0].Level = 8;
        actor.ReplaceLifetime(999);
        actor.AddActorDespawnRequest(5, 5, ActorDespawnReason.RollbackCleanup, 0, 0);
        provider.Import(frame, payload);

        Assert.Equal(100, actor.lifetime.EndTimeMs);
        Assert.False(actor.hasActorDespawnRequest);
        Assert.Equal(3, actor.skillLoadout.ActiveSkills[0].CurrentCharges);
        Assert.Equal(1, actor.skillLoadout.PassiveSkills[0].Level);
        Assert.NotSame(original, actor.skillLoadout.ActiveSkills[0]);
    }

    [Fact]
    public void Missing_confirmed_actor_fails_preflight_without_mutating_allocator()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actor = context.CreateEntity();
        actor.AddActorId(ids.Next());
        var provider = new MobaEntitasEntityRollbackProvider(context, ids, new MobaActorRegistry(), null);
        var frame = new FrameIndex(2);
        var payload = provider.Export(frame);
        actor.Destroy();
        ids.Next();

        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(frame, payload));
        Assert.Equal(3, ids.NextId);
    }

    [Fact]
    public void Predicted_summon_owner_tracking_is_pruned_without_touching_confirmed_summons()
    {
        var summons = new MobaSummonService();
        var field = typeof(MobaSummonService).GetField("_summonsByRootOwner", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var tracked = Assert.IsType<Dictionary<int, List<int>>>(field.GetValue(summons));
        tracked[1] = new List<int> { 2, 3 };

        summons.PruneRollbackActors(new[] { 1, 2 });

        Assert.Equal(new[] { 2 }, tracked[1]);
    }

    [Fact]
    public void Context_registry_rollback_removes_predicted_context_and_reuses_generated_id()
    {
        var contexts = new MobaRuntimeContextService();
        var confirmed = contexts.Registry.Create().Build();
        var provider = new MobaContextEntityRollbackProvider(contexts);
        var frame = new FrameIndex(3);
        var payload = provider.Export(frame);

        var predicted = contexts.Registry.Create().Build();
        contexts.Registry.GenerateId();
        provider.Import(frame, payload);

        Assert.True(contexts.Registry.Exists(confirmed));
        Assert.False(contexts.Registry.Exists(predicted));
        Assert.Equal(predicted, contexts.Registry.GenerateId());
    }
}
