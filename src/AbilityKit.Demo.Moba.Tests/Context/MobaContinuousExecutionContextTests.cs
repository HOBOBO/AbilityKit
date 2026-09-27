using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Context;

public sealed class MobaContinuousExecutionContextTests
{
    [Fact]
    public void Combat_context_source_can_create_managed_execution_context_for_continuous_runtime()
    {
        var source = new MobaCombatContextSource(
            EffectContextKind.Buff,
            MobaExecutionKind.BuffTick,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: 3001,
            rootContextId: 3000,
            ownerContextId: 3001,
            configId: 9001,
            triggerId: 7001,
            frame: 12,
            runtimeKind: "Buff",
            runtimeConfigId: 9001,
            hasLiveRuntime: true);

        var context = MobaCombatContextBuilder.FromSource(payload: "continuous-buff", in source);

        Assert.True(context.HasExecutionSource);
        Assert.Equal(EffectContextKind.Buff, context.ContextKind);
        Assert.Equal(MobaExecutionKind.BuffTick, context.OriginKind);
        Assert.Equal(101, context.SourceActorId);
        Assert.Equal(202, context.TargetActorId);
        Assert.Equal(3001, context.ParentContextId);
        Assert.Equal(3000, context.RootContextId);
        Assert.Equal(3001, context.OwnerContextId);
        Assert.Equal(9001, context.ConfigId);
        Assert.Equal(7001, context.TriggerId);
        Assert.Equal(12, context.Frame);
        Assert.True(context.TryGetContextSource(out var view));
        Assert.True(view.HasExecutionSource);
        Assert.Equal(MobaContextSourceBoundary.Execution, view.Boundary);
        Assert.Equal(3001, view.SourceContextId);
    }
    [Fact]
    public void Snapshot_context_source_can_create_execution_context_after_live_runtime_is_gone()
    {
        var source = new MobaCombatContextSource(
            EffectContextKind.Buff,
            MobaExecutionKind.BuffTick,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: 3001,
            rootContextId: 3000,
            ownerContextId: 3001,
            configId: 9001,
            triggerId: 7001,
            frame: 18,
            runtimeKind: "Buff",
            runtimeConfigId: 9001,
            hasLiveRuntime: false);

        var context = MobaCombatContextBuilder.FromSource(payload: "buff-snapshot", in source);

        Assert.True(context.HasExecutionSource);
        Assert.Equal(101, context.SourceActorId);
        Assert.Equal(202, context.TargetActorId);
        Assert.Equal(3001, context.ParentContextId);
        Assert.Equal(3000, context.RootContextId);
        Assert.Equal(3001, context.OwnerContextId);
        Assert.True(context.TryGetContextSource(out var view));
        Assert.True(view.HasExecutionSource);
        Assert.Equal(MobaContextSourceBoundary.Execution, view.Boundary);
        Assert.False(view.HasLiveRuntime);
        Assert.Equal(3001, view.SourceContextId);
    }

    [Fact]
    public void Persistent_source_snapshot_can_rebuild_execution_context_without_live_source()
    {
        var liveSource = new MobaContextSourceView(
            MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.LiveRuntime,
            EffectContextKind.Buff,
            MobaExecutionKind.BuffTick,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: 3001,
            parentContextId: 3001,
            rootContextId: 3000,
            ownerContextId: 3001,
            configId: 9001,
            triggerId: 7001,
            frame: 18,
            runtimeKind: "Buff",
            runtimeConfigId: 9001,
            hasLiveRuntime: true,
            skillRuntimeHandle: default);
        var snapshot = MobaPersistentContextSourceSnapshot.FromContextSource(in liveSource);

        Assert.True(snapshot.HasExecutionSource);
        Assert.True(snapshot.TryGetContextSource(out var snapshotView));
        Assert.Equal(MobaContextSourceBoundary.Snapshot, snapshotView.Boundary);
        Assert.False(snapshotView.HasLiveRuntime);
        Assert.True(snapshot.TryGetCombatContextSource(out var durableSource));

        var context = MobaCombatContextBuilder.FromSource(snapshot, in durableSource);

        Assert.True(context.HasExecutionSource);
        Assert.Equal(101, context.SourceActorId);
        Assert.Equal(202, context.TargetActorId);
        Assert.Equal(3001, context.ParentContextId);
        Assert.Equal(3000, context.RootContextId);
        Assert.Equal(3001, context.OwnerContextId);
        Assert.Equal(9001, context.ConfigId);
        Assert.Equal(7001, context.TriggerId);
    }

    [Fact]
    public void Origin_to_view_to_snapshot_keeps_context_boundary_and_skill_runtime_handle()
    {
        var skillHandle = new MobaSkillCastRuntimeHandle(runtimeId: 88, generation: 2, rootContextId: 3001);
        var origin = new MobaGameplayOrigin(
            sourceActorId: 101,
            targetActorId: 202,
            immediateKind: MobaExecutionKind.BuffTick,
            immediateConfigId: 9001,
            immediateContextId: 3001,
            parentContextId: 3001,
            rootContextId: 3000,
            ownerContextId: 3001,
            skillRuntimeHandle: skillHandle);

        var liveView = MobaContextSourceView.FromOrigin(in origin, MobaContextSourceResolveKind.Origin, MobaContextSourceBoundary.LiveRuntime, hasLiveRuntime: true, runtimeKind: "Buff", runtimeConfigId: 9001);
        var snapshot = MobaPersistentContextSourceSnapshot.FromContextSource(in liveView);

        Assert.True(origin.IsValid);
        Assert.True(liveView.IsValid);
        Assert.Equal(MobaContextSourceBoundary.LiveRuntime, liveView.Boundary);
        Assert.True(liveView.SkillRuntimeHandle.IsValid);
        Assert.True(snapshot.IsValid);
        Assert.True(snapshot.TryGetContextSource(out var snapshotView));
        Assert.Equal(MobaContextSourceBoundary.Snapshot, snapshotView.Boundary);
        Assert.False(snapshotView.HasLiveRuntime);
        Assert.Equal(skillHandle, snapshotView.SkillRuntimeHandle);
        Assert.Equal(3001, snapshotView.SourceContextId);
        Assert.Equal(3000, snapshotView.RootContextId);
        Assert.Equal(3001, snapshotView.OwnerContextId);
    }

    [Fact]
    public void Source_query_can_match_buff_snapshot_after_live_runtime_is_gone()
    {
        var liveSource = new MobaContextSourceView(
            MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.LiveRuntime,
            EffectContextKind.Buff,
            MobaExecutionKind.BuffTick,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: 3001,
            parentContextId: 3001,
            rootContextId: 3000,
            ownerContextId: 3001,
            configId: 9001,
            triggerId: 7001,
            frame: 18,
            runtimeKind: MobaRuntimeKindNames.Buff,
            runtimeConfigId: 9001,
            hasLiveRuntime: true,
            skillRuntimeHandle: default);
        var snapshot = MobaPersistentContextSourceSnapshot.FromContextSource(in liveSource);

        Assert.True(MobaSourceQueryResolver.TryResolve(snapshot, out var query));
        Assert.True(query.IsBuff(9001));
        Assert.True(query.IsExecutionKind(MobaExecutionKind.BuffTick));
        Assert.True(query.HasContext(3000));
        Assert.False(query.HasLiveRuntime);
        Assert.Equal(MobaContextSourceBoundary.Snapshot, query.Boundary);
    }

    [Fact]
    public void Source_query_can_classify_damage_result_and_preserve_original_root()
    {
        var origin = new MobaGameplayOrigin(
            sourceActorId: 101,
            targetActorId: 202,
            immediateKind: MobaExecutionKind.BuffTick,
            immediateConfigId: 9001,
            immediateContextId: 3001,
            parentContextId: 3001,
            rootContextId: 3000,
            ownerContextId: 3001);
        var result = new DamageResult
        {
            AttackerActorId = 101,
            TargetActorId = 202,
            ReasonKind = DamageReasonKind.Buff,
            ReasonParam = 9101,
            Value = 128f
        };
        result.SetOrigin(in origin);

        Assert.True(MobaSourceQueryResolver.TryResolve(result, out var query));
        Assert.True(query.IsDamage());
        Assert.False(query.IsBuff(9001));
        Assert.True(query.IsExecutionKind(MobaExecutionKind.DamageApply));
        Assert.True(query.HasContext(3000));
        Assert.Equal(9101, query.ConfigId);
        Assert.Equal(MobaRuntimeKindNames.DamageResult, query.RuntimeKind);
    }
}
