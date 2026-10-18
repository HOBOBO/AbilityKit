using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests;

/// <summary>
/// cue 生命周期的 E3 回归。这是本次重构之前最大的证据缺口：
/// 表现层没有任何测试覆盖"一个 buff 从施加到到期，产生的 cue 序列与 key 是否一致"，
/// 因此过去 4 份判定表各自漂移（包括 console 端漏掉 4 个 stage）一直无人发现。
///
/// 这里直接测 active store + stage 语义，不依赖 Unity，不依赖网络。
/// </summary>
public sealed class PresentationCueLifecycleTests
{
    private static MobaPresentationCueSnapshotEntry Entry(
        int stage,
        string instanceKey,
        int targetActorId = 7,
        int stackCount = 1,
        float remaining = 10f,
        int lifecycleReason = 0)
    {
        return new MobaPresentationCueSnapshotEntry
        {
            Stage = stage,
            TemplateId = 100,
            VfxId = 100,
            RequestKey = instanceKey,
            InstanceKey = instanceKey,
            SourceActorId = 3,
            TargetActorId = targetActorId,
            StackCount = stackCount,
            MaxStackCount = 3,
            RemainingSeconds = remaining,
            LifecycleReason = lifecycleReason,
            OwnerKind = "Buff",
            Scale = 1f,
            ColorR = 1f,
            ColorG = 1f,
            ColorB = 1f,
            ColorA = 1f,
        };
    }

    [Fact]
    public void Full_buff_lifecycle_produces_exactly_one_active_cue_at_a_time()
    {
        var store = new MobaActivePresentationCueStore();
        const string key = "buff:7:200:9001";

        // 施加 -> 建立
        store.Observe(Entry((int)MobaPresentationCueStage.Started, key));
        Assert.True(store.Contains(key));
        Assert.True(store.TryGet(key, out var active));
        Assert.Equal(1, active.Entry.StackCount);
        Assert.Equal(10f, active.Entry.RemainingSeconds);

        // 叠层 -> 仍是同一个 key，且数量上升
        store.Observe(Entry((int)MobaPresentationCueStage.StackChanged, key, stackCount: 2, remaining: 8f));
        Assert.True(store.TryGet(key, out active));
        Assert.Equal(2, active.Entry.StackCount);
        Assert.Equal(8f, active.Entry.RemainingSeconds);

        // 刷新 -> 仍是同一个 key
        store.Observe(Entry((int)MobaPresentationCueStage.Refreshed, key, stackCount: 2, remaining: 10f));
        Assert.True(store.TryGet(key, out active));
        Assert.Equal(10f, active.Entry.RemainingSeconds);

        // 到期 -> 移除
        store.Observe(Entry((int)MobaPresentationCueStage.Expired, key, remaining: 0f));
        Assert.False(store.Contains(key));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Stack_then_expire_uses_the_same_key_so_the_view_object_is_reused()
    {
        var store = new MobaActivePresentationCueStore();
        const string key = "buff:7:200:9001";

        store.Observe(Entry((int)MobaPresentationCueStage.Started, key, stackCount: 1));
        store.Observe(Entry((int)MobaPresentationCueStage.StackChanged, key, stackCount: 3));
        Assert.True(store.Contains(key));

        // 结束 cue 必须用同一把 key，否则表现层找不到要销毁的对象。
        store.Observe(Entry((int)MobaPresentationCueStage.Expired, key, stackCount: 3));
        Assert.False(store.Contains(key));
    }

    [Theory]
    [InlineData(MobaExecutionEndReasonShim.Expired, MobaPresentationCueStage.Expired)]
    [InlineData(MobaExecutionEndReasonShim.Completed, MobaPresentationCueStage.Completed)]
    [InlineData(MobaExecutionEndReasonShim.Dispel, MobaPresentationCueStage.Removed)]
    public void End_stage_mapping_classifies_as_stop(
        MobaExecutionEndReasonShim reason,
        MobaPresentationCueStage expected)
    {
        Assert.Equal(
            PresentationCueTransition.Stop,
            PresentationCueSemantics.Classify((int)expected));
    }

    [Fact]
    public void Ticked_keeps_the_cue_alive_rather_than_creating_a_new_one()
    {
        var store = new MobaActivePresentationCueStore();
        const string key = "buff:7:200:9001";

        store.Observe(Entry((int)MobaPresentationCueStage.Started, key));
        store.Observe(Entry((int)MobaPresentationCueStage.Ticked, key, remaining: 5f));
        store.Observe(Entry((int)MobaPresentationCueStage.Ticked, key, remaining: 4f));

        Assert.Equal(1, store.Count);
        Assert.True(store.TryGet(key, out var active));
        Assert.Equal(4f, active.Entry.RemainingSeconds);
    }

    [Fact]
    public void Distinct_source_contexts_are_independent_cues()
    {
        var store = new MobaActivePresentationCueStore();

        store.Observe(Entry((int)MobaPresentationCueStage.Started, "buff:7:200:9001"));
        store.Observe(Entry((int)MobaPresentationCueStage.Started, "buff:7:200:9002"));

        // 同一 buff 被两个来源施加 -> 两个独立的表现实例。
        Assert.Equal(2, store.Count);

        store.Observe(Entry((int)MobaPresentationCueStage.Removed, "buff:7:200:9001"));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Revision_increases_so_stale_client_updates_can_be_rejected()
    {
        var store = new MobaActivePresentationCueStore();
        const string key = "buff:7:200:9001";

        store.Observe(Entry((int)MobaPresentationCueStage.Started, key));
        Assert.True(store.TryGet(key, out var first));
        store.Observe(Entry((int)MobaPresentationCueStage.StackChanged, key, stackCount: 2));
        Assert.True(store.TryGet(key, out var second));

        Assert.True(second.Revision > first.Revision);
    }

    [Fact]
    public void Cue_without_a_stable_key_is_ignored_rather_than_creating_a_shared_bucket()
    {
        var store = new MobaActivePresentationCueStore();

        // 全空 key 的 cue 如果被接纳，会全部塌进同一个空字符串桶，互相覆盖。
        store.Observe(Entry((int)MobaPresentationCueStage.Started, null));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Legacy_cue_with_stable_key_keeps_persistent_lifetime_compatibility()
    {
        var entry = Entry((int)MobaPresentationCueStage.Started, "legacy:active");
        entry.OwnerKind = "unit-test";
        entry.Domain = 0;
        entry.Lifetime = 0;

        Assert.Equal(PresentationCueLifetime.PersistentState, MobaPresentationCueKeys.ResolveLifetime(in entry));
        var store = new MobaActivePresentationCueStore();
        Assert.Equal(MobaPresentationCueObserveResult.Started, store.Observe(in entry));
        Assert.True(store.Contains("legacy:active"));
    }

    [Fact]
    public void Cleared_store_resets_revisions()
    {
        var store = new MobaActivePresentationCueStore();
        const string key = "buff:7:200:9001";

        store.Observe(Entry((int)MobaPresentationCueStage.Started, key));
        Assert.True(store.TryGet(key, out var first));

        store.Clear();
        Assert.Equal(0, store.Count);

        store.Observe(Entry((int)MobaPresentationCueStage.Started, key));
        Assert.True(store.TryGet(key, out var afterClear));
        Assert.True(afterClear.Revision <= first.Revision);
    }

    /// <summary>
    /// 仅为测试用，避免直接引用逻辑侧的枚举类型造成额外耦合。
    /// 取值与 AbilityKit.Demo.Moba.Services.Buffs.Triggering.MobaExecutionEndReason 一致。
    /// </summary>
    public enum MobaExecutionEndReasonShim
    {
        None = 0,
        Expired = 1,
        Completed = 2,
        Dispel = 3,
    }
}
