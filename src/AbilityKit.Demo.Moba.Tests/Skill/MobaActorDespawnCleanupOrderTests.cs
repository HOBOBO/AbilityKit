using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems.EntityManager;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

/// <summary>
/// 验证正式 Actor 清理系统的每轮确定顺序、确认门限和回调派生边界。
/// </summary>
public sealed class MobaActorDespawnCleanupOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ready_batch_uses_actor_id_order_without_reordering_group_cache(bool reverseRequests)
    {
        using var f = new Fixture();
        var actors = new[] { f.Actor(30), f.Actor(10), f.Actor(20) };
        foreach (var actor in reverseRequests ? actors.Reverse() : actors)
            ActorLifecycleRequests.RequestDespawn(actor, 100, ActorDespawnReason.SceneCleanup);
        var cached = f.Contexts.actor.GetGroup(ActorMatcher.ActorDespawnRequest).GetEntities();
        var original = cached.ToArray();

        f.Cleanup.Execute();

        Assert.Equal(new[] { 10, 20, 30 }, f.DestroyedIds);
        Assert.Equal(original, cached);
        Assert.All(actors, actor => Assert.False(actor.isEnabled));
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Confirmation_gate_keeps_request_until_exact_threshold(bool useAuthority)
    {
        using var f = new Fixture(useAuthority);
        var waiting = f.Actor(10);
        waiting.AddActorDespawnRequest(90, 101, ActorDespawnReason.SceneCleanup, 42, 7001);
        var ready = f.Actor(30, request: true);

        f.Cleanup.Execute();

        Assert.Equal(new[] { 30 }, f.DestroyedIds);
        Assert.False(ready.isEnabled);
        Assert.True(waiting.isEnabled);
        Assert.True(waiting.hasActorDespawnRequest);
        Assert.Equal(90, waiting.actorDespawnRequest.RequestFrame);
        Assert.Equal(101, waiting.actorDespawnRequest.MinConfirmedFrame);
        Assert.Equal(ActorDespawnReason.SceneCleanup, waiting.actorDespawnRequest.Reason);
        Assert.Equal(42, waiting.actorDespawnRequest.SourceActorId);
        Assert.Equal(7001L, waiting.actorDespawnRequest.SourceContextId);

        f.SetConfirmedFrame(101);
        f.Cleanup.Execute();
        f.Cleanup.Execute();

        Assert.Equal(new[] { 30, 10 }, f.DestroyedIds);
        Assert.False(waiting.isEnabled);
    }

    [Fact]
    public void Derived_request_with_smaller_id_waits_for_next_batch()
    {
        using var f = new Fixture();
        var derived = f.Actor(5);
        f.Actor(30, request: true);
        f.Actor(10, request: true);
        f.DuringDestroy = actor =>
        {
            if (actor.actorId.Value == 10)
                ActorLifecycleRequests.RequestDespawn(derived, 100, ActorDespawnReason.SceneCleanup);
        };

        f.Cleanup.Execute();

        Assert.Equal(new[] { 10, 30, 5 }, f.DestroyedIds);
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    [Fact]
    public void Newly_ready_request_already_in_group_waits_for_next_batch()
    {
        using var f = new Fixture();
        var derived = f.Actor(5);
        derived.AddActorDespawnRequest(100, 101, ActorDespawnReason.SceneCleanup, 0, 0);
        f.Actor(30, request: true);
        f.Actor(10, request: true);
        f.DuringDestroy = actor =>
        {
            if (actor.actorId.Value == 10)
                derived.ReplaceActorDespawnRequest(100, 100, ActorDespawnReason.SceneCleanup, 0, 0);
        };

        f.Cleanup.Execute();

        Assert.Equal(new[] { 10, 30, 5 }, f.DestroyedIds);
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    [Fact]
    public void Derived_chain_preserves_eight_pass_budget_and_continues_next_frame()
    {
        using var f = new Fixture();
        var actors = Enumerable.Range(81, 10).ToDictionary(id => id, id => f.Actor(id));
        ActorLifecycleRequests.RequestDespawn(actors[90], 100, ActorDespawnReason.SceneCleanup);
        f.DuringDestroy = actor =>
        {
            var next = actor.actorId.Value - 1;
            if (actors.TryGetValue(next, out var child))
                ActorLifecycleRequests.RequestDespawn(child, 100, ActorDespawnReason.SceneCleanup);
        };

        f.Cleanup.Execute();

        Assert.Equal(Enumerable.Range(83, 8).Reverse(), f.DestroyedIds);
        Assert.True(actors[82].isEnabled);
        Assert.True(actors[82].hasActorDespawnRequest);
        Assert.True(actors[81].isEnabled);
        Assert.False(actors[81].hasActorDespawnRequest);

        f.SetConfirmedFrame(101);
        f.Cleanup.Execute();

        Assert.Equal(Enumerable.Range(81, 10).Reverse(), f.DestroyedIds);
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    [Fact]
    public void Invalid_actor_ids_keep_original_relative_order_after_valid_candidates()
    {
        using var f = new Fixture();
        f.Actor(-3, request: true);
        f.Actor(30, request: true);
        f.Actor(null, request: true);
        f.Actor(0, request: true);
        f.Actor(10, request: true);
        var invalid = f.Contexts.actor.GetGroup(ActorMatcher.ActorDespawnRequest).GetEntities()
            .Where(actor => !actor.hasActorId || actor.actorId.Value <= 0).ToArray();

        f.Cleanup.Execute();

        Assert.Equal(new[] { 10, 30 }, f.DestroyedIds.Take(2));
        Assert.Equal(invalid, f.DestroyedActors.Skip(2));
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Candidate_request_is_rechecked_after_earlier_cleanup(bool postpone)
    {
        using var f = new Fixture();
        f.Actor(10, request: true);
        var changed = f.Actor(20, request: true);
        f.Actor(30, request: true);
        f.DuringDestroy = actor =>
        {
            if (actor.actorId.Value != 10) return;
            if (postpone)
                changed.ReplaceActorDespawnRequest(100, 101, ActorDespawnReason.SceneCleanup, 0, 0);
            else
                changed.RemoveActorDespawnRequest();
        };

        f.Cleanup.Execute();

        Assert.Equal(new[] { 10, 30 }, f.DestroyedIds);
        Assert.True(changed.isEnabled);
        Assert.Equal(postpone, changed.hasActorDespawnRequest);

        f.SetConfirmedFrame(101);
        if (!postpone)
            ActorLifecycleRequests.RequestDespawn(changed, 101, ActorDespawnReason.SceneCleanup);
        f.Cleanup.Execute();

        Assert.Equal(new[] { 10, 30, 20 }, f.DestroyedIds);
        Assert.False(changed.isEnabled);
    }

    [Fact]
    public void Reused_entity_is_not_consumed_at_old_candidate_position()
    {
        using var f = new Fixture();
        f.Actor(10, request: true);
        var stale = f.Actor(20, request: true);
        f.Actor(30, request: true);
        ActorEntity? replacement = null;
        f.DuringDestroy = actor =>
        {
            if (actor.actorId.Value != 10) return;
            stale.Destroy();
            replacement = f.Actor(5, request: true);
        };

        f.Cleanup.Execute();

        Assert.Same(stale, replacement);
        Assert.Equal(new[] { 10, 20, 30, 5 }, f.DestroyedIds);
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    /// <summary>
    /// 不注册任何回滚 Provider 或日志，直接驱动真实清理系统并观察 Entitas 销毁边界。
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        public readonly Contexts Contexts = new();
        public readonly MobaActorDespawnCleanupSystem Cleanup;
        public readonly List<int> DestroyedIds = new();
        public readonly List<ActorEntity> DestroyedActors = new();
        public Action<ActorEntity>? DuringDestroy;
        private readonly FrameTime _time = new();
        private readonly AuthorityFrames _authority = new();

        public Fixture(bool useAuthority = false)
        {
            var services = new Resolver();
            _time.StepTo(new FrameIndex(useAuthority ? 1000 : 100), 1f / 30f);
            services.Add<IFrameTime>(_time);
            if (useAuthority)
            {
                var authority = new MobaAuthorityFrameService();
                typeof(MobaAuthorityFrameService).GetField("_authorityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(authority, _authority);
                authority.BindWorld(default);
                services.Add(authority);
            }
            Cleanup = new MobaActorDespawnCleanupSystem(Contexts, services);
            Cleanup.Initialize();
            Contexts.actor.OnEntityWillBeDestroyed += OnDestroying;
        }

        public ActorEntity Actor(int? id, bool request = false)
        {
            var actor = Contexts.actor.CreateEntity();
            if (id.HasValue) actor.AddActorId(id.Value);
            if (request) ActorLifecycleRequests.RequestDespawn(actor, 100, ActorDespawnReason.SceneCleanup);
            return actor;
        }

        public void SetConfirmedFrame(int frame)
        {
            _authority.Confirmed = frame;
            _time.StepTo(new FrameIndex(frame), 1f / 30f);
        }

        private void OnDestroying(Entitas.IContext context, Entitas.IEntity entity)
        {
            var actor = (ActorEntity)entity;
            DestroyedIds.Add(actor.hasActorId ? actor.actorId.Value : 0);
            DestroyedActors.Add(actor);
            DuringDestroy?.Invoke(actor);
        }

        public void Dispose()
        {
            Contexts.actor.OnEntityWillBeDestroyed -= OnDestroying;
            Cleanup.TearDown();
            Contexts.actor.DestroyAllEntities();
        }
    }

    /// <summary>
    /// 提供独立于预测帧的确认帧，以验证系统没有提前使用预测帧清理。
    /// </summary>
    private sealed class AuthorityFrames : IWorldAuthorityFramesSource
    {
        public int Confirmed = 100;
        public bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted)
        {
            confirmed = new FrameIndex(Confirmed);
            predicted = new FrameIndex(1000);
            return true;
        }
    }

    /// <summary>
    /// 仅提供当前测试明确注册的世界服务。
    /// </summary>
    private sealed class Resolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _values = new();
        public void Add<T>(T value) where T : notnull => _values[typeof(T)] = value;
        public object Resolve(Type type) => _values[type];
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object value) => _values.TryGetValue(type, out value!);
        public bool TryResolve<T>(out T value)
        {
            var found = _values.TryGetValue(typeof(T), out var item);
            value = found ? (T)item! : default!;
            return found;
        }
    }
}
