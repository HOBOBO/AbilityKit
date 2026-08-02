using System;
using System.Reflection;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSummonRollbackTests
{
    [Fact]
    public void Summon_post_spawn_event_failure_reclaims_registered_entity()
    {
        var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        try
        {
            bootstrapper.Initialize();
            bootstrapper.Start();
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
            {
                bootstrapper.Tick();
            }
            bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++)
            {
                bootstrapper.Tick();
            }

            var services = bootstrapper.RuntimeServices;
            Assert.NotNull(services);
            Assert.True(services.TryResolve<MobaActorRegistry>(out var registry) && registry != null);
            Assert.True(services.TryResolve<MobaEntityManager>(out var entities) && entities != null);
            Assert.True(services.TryResolve<MobaSummonService>(out var summons) && summons != null);

            var casterActorId = FindCasterActorId(registry);
            Assert.True(casterActorId > 0, "No summon caster was found.");
            Assert.True(registry.TryGet(casterActorId, out var caster) && caster != null);

            SetPrivateField(summons, "_eventBus", new ThrowingEventBus());

            var spawnPosition = caster.transform.Value.Position + Vec3.Forward;
            Assert.False(summons.TrySummon(casterActorId, summonId: 1, in spawnPosition));
            Assert.Equal(0, summons.ActiveCount);
            Assert.Empty(FindSummonActorIds(registry));
            Assert.Empty(FindSummonActorIds(entities));
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }

    private static int FindCasterActorId(MobaActorRegistry registry)
    {
        foreach (var entry in registry.Entries)
        {
            var entity = entry.Value;
            if (entity != null && entity.hasTransform && entity.hasTeam)
            {
                return entry.Key;
            }
        }

        return 0;
    }

    private static int[] FindSummonActorIds(MobaActorRegistry registry)
    {
        var ids = new System.Collections.Generic.List<int>();
        foreach (var entry in registry.Entries)
        {
            if (entry.Value != null && entry.Value.hasSummonMeta)
            {
                ids.Add(entry.Key);
            }
        }

        return ids.ToArray();
    }

    private static int[] FindSummonActorIds(MobaEntityManager entities)
    {
        var ids = new System.Collections.Generic.List<int>();
        entities.GetRegisteredActorIds(ids);
        ids.RemoveAll(actorId =>
            !entities.TryGetActorEntity(actorId, out var entity) ||
            entity == null ||
            !entity.hasSummonMeta);
        return ids.ToArray();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private sealed class ThrowingEventBus : IEventBus
    {
        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            throw new InvalidOperationException("Intentional summon post-spawn event failure.");
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control)
        {
            throw new InvalidOperationException("Intentional summon post-spawn event failure.");
        }

        public bool HasSubscribers<TArgs>(EventKey<TArgs> key) => false;

        public IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs> handler)
        {
            throw new NotSupportedException();
        }

        public IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs, ExecutionControl> handler)
        {
            throw new NotSupportedException();
        }

        public void Flush()
        {
        }
    }
}
