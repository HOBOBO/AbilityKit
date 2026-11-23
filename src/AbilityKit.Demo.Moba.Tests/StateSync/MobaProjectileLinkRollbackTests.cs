using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Combat.Projectile;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaProjectileLinkRollbackTests
{
    [Fact]
    public void Impact_kills_count_once_per_hit_and_restore_with_the_original_projectile()
    {
        var links = new MobaProjectileLinkService();
        var provider = new MobaProjectileLinkRollbackProvider(links, new MobaSkillCastRuntimeService());
        var source = new ProjectileSourceContext(11, 12, 301, 401, 402, 403, default, default);
        var projectile = new ProjectileId(501);
        links.Link(projectile, 601);
        links.BindSource(projectile, in source);
        links.RecordImpactKill(projectile, source.SourceContextId, 1);
        links.RecordImpactKill(projectile, source.SourceContextId, 1);
        Assert.True(links.TryReadPriorImpactKills(projectile, source.SourceContextId, 1, out var onKillingHit));
        Assert.Equal(0, onKillingHit);

        var frame = new FrameIndex(7);
        var payload = provider.Export(frame);
        links.RecordImpactKill(projectile, source.SourceContextId, 2);
        Assert.True(links.TryReadPriorImpactKills(projectile, source.SourceContextId, 3, out var futureKills));
        Assert.Equal(2, futureKills);
        provider.ValidateImport(frame, payload);
        provider.Import(frame, payload);
        Assert.False(links.TryReadPriorImpactKills(projectile, 999, 3, out _));
        Assert.True(links.TryReadPriorImpactKills(projectile, source.SourceContextId, 3, out var restoredKills));
        Assert.Equal(1, restoredKills);
        Assert.Equal(payload, provider.Export(frame));

        links.UnlinkByProjectileId(projectile);
        links.Link(projectile, 602);
        links.BindSource(projectile, in source);
        Assert.True(links.TryReadPriorImpactKills(projectile, source.SourceContextId, 1, out var respawnedKills));
        Assert.Equal(0, respawnedKills);
    }

    [Fact]
    public void Link_is_idempotent_for_same_pair_and_rejects_conflicting_identity_without_mutation()
    {
        var links = new MobaProjectileLinkService();
        var first = new ProjectileId(501);
        links.Link(first, 601);

        links.Link(first, 601);
        Assert.Equal(1, links.ActiveCount);
        Assert.True(links.TryGetActorId(first, out var actorId));
        Assert.Equal(601, actorId);

        Assert.Throws<System.InvalidOperationException>(() => links.Link(new ProjectileId(502), 601));
        Assert.Throws<System.InvalidOperationException>(() => links.Link(first, 602));

        Assert.Equal(1, links.ActiveCount);
        Assert.True(links.TryGetActorId(first, out actorId));
        Assert.Equal(601, actorId);
        Assert.False(links.TryGetActorId(new ProjectileId(502), out _));
        Assert.False(links.TryGetProjectileId(602, out _));
    }

    [Fact]
    public void Import_restores_target_links_sources_and_removes_future_relationships()
    {
        var links = new MobaProjectileLinkService();
        var skills = new MobaSkillCastRuntimeService();
        var provider = new MobaProjectileLinkRollbackProvider(links, skills);
        var source = new ProjectileSourceContext(11, 12, 301, 401, 402, 403, default, default);
        var targetProjectile = new ProjectileId(501);
        links.Link(targetProjectile, 601);
        links.BindSource(targetProjectile, in source);
        var payload = provider.Export(new FrameIndex(7));

        links.UnlinkByProjectileId(targetProjectile);
        var futureProjectile = new ProjectileId(502);
        links.Link(futureProjectile, 602);
        var futureSource = new ProjectileSourceContext(11, 13, 302, 411, 412, 413, default, default);
        links.BindSource(futureProjectile, in futureSource);

        provider.ValidateImport(new FrameIndex(7), payload);
        provider.Import(new FrameIndex(7), payload);

        Assert.Equal(1, links.ActiveCount);
        Assert.True(links.TryGetActorId(targetProjectile, out var restoredActor));
        Assert.Equal(601, restoredActor);
        Assert.True(links.TryGetProjectileId(601, out var restoredProjectile));
        Assert.Equal(targetProjectile, restoredProjectile);
        Assert.True(links.TryGetSource(targetProjectile, out var restoredSource));
        Assert.Equal(401, restoredSource.SourceContextId);
        Assert.False(links.TryGetActorId(futureProjectile, out _));
        Assert.False(links.TryGetRetiredActorId(targetProjectile, out _));
    }

    [Fact]
    public void Preflight_rejects_active_projectile_without_target_source()
    {
        var links = new MobaProjectileLinkService();
        var provider = new MobaProjectileLinkRollbackProvider(links, new MobaSkillCastRuntimeService());
        links.Link(new ProjectileId(701), 801);
        var payload = provider.Export(new FrameIndex(1));
        Assert.Throws<System.InvalidOperationException>(() => provider.ValidateImport(new FrameIndex(1), payload));
    }

    [Fact]
    public void Summon_import_restores_owner_source_and_removes_future_index_entries()
    {
        var context = new ActorContext();
        var actors = new MobaActorRegistry();
        var summon = context.CreateEntity();
        summon.AddActorId(2);
        summon.AddSummonMeta(80, true);
        summon.AddOwnerLink(1, 1);
        actors.Register(2, summon);
        var summons = new MobaSummonService();
        var owners = GetField<Dictionary<int, List<int>>>(summons, "_summonsByRootOwner");
        owners.Add(1, new List<int> { 2 });
        typeof(MobaSummonService).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(summons, actors);
        var sourceContext = new SummonSourceContext(1, 2, 80, 101, 102, 103, default, default);
        GetField<Dictionary<int, SummonSourceContext>>(summons, "_sourceBySummonActorId").Add(2, sourceContext);
        var provider = new MobaSummonRollbackProvider(summons, actors, new MobaSkillCastRuntimeService());
        var payload = provider.Export(new FrameIndex(3));

        owners[1].Clear();
        owners.Add(9, new List<int> { 99 });
        GetField<Dictionary<int, SummonSourceContext>>(summons, "_sourceBySummonActorId").Clear();
        summon.ReplaceOwnerLink(9, 9);

        provider.ValidateImport(new FrameIndex(3), payload);
        provider.Import(new FrameIndex(3), payload);

        Assert.Equal(new[] { 2 }, owners[1]);
        Assert.False(owners.ContainsKey(9));
        Assert.True(summon.hasOwnerLink);
        Assert.Equal(1, summon.ownerLink.OwnerActorId);
        Assert.Equal(1, summon.ownerLink.RootOwnerActorId);
        Assert.True(GetField<Dictionary<int, SummonSourceContext>>(summons, "_sourceBySummonActorId").TryGetValue(2, out var restored));
        Assert.Equal(101, restored.SourceContextId);
    }

    private static T GetField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(target));
    }
}
