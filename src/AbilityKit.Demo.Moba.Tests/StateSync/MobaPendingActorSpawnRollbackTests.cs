using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaPendingActorSpawnRollbackTests
{
    [Fact]
    public void Restore_replaces_only_unsent_queue_and_preserves_order()
    {
        var service = new MobaActorSpawnSnapshotService();
        var provider = new MobaPendingActorSpawnRollbackProvider(service);
        var first = new MobaActorSpawnSnapshotEntry(11, (int)SpawnEntityKind.Summon, 801, 4, 1f, 2f, 3f, 3);
        var later = new MobaActorSpawnSnapshotEntry(12, (int)SpawnEntityKind.Summon, 802, 4, 4f, 5f, 6f, 2);
        service.Enqueue(in first);
        var target = provider.Export(new FrameIndex(7));
        service.Enqueue(in later);

        provider.Import(new FrameIndex(7), target);

        Assert.True(service.TryGetSnapshot(new FrameIndex(8), out var snapshot));
        var entries = MobaActorSpawnSnapshotCodec.Deserialize(snapshot.Payload);
        var entry = Assert.Single(entries);
        Assert.Equal(first.NetId, entry.NetId);
        Assert.Equal(first.EntityVersion, entry.EntityVersion);
        Assert.False(service.TryGetSnapshot(new FrameIndex(9), out _));
    }

    [Fact]
    public void Already_emitted_spawn_is_not_replayed_by_restore()
    {
        var service = new MobaActorSpawnSnapshotService();
        var provider = new MobaPendingActorSpawnRollbackProvider(service);
        var emitted = new MobaActorSpawnSnapshotEntry(11, (int)SpawnEntityKind.Summon, 801, 4, 1f, 2f, 3f, 3);
        service.Enqueue(in emitted);
        Assert.True(service.TryGetSnapshot(new FrameIndex(7), out _));

        var target = provider.Export(new FrameIndex(8));
        provider.Import(new FrameIndex(8), target);

        Assert.False(service.TryGetSnapshot(new FrameIndex(9), out _));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void Snapshot_preflight_rejects_missing_or_reused_entity_version(bool includeActor, int version)
    {
        var service = new MobaActorSpawnSnapshotService();
        var provider = new MobaPendingActorSpawnRollbackProvider(service);
        var entry = new MobaActorSpawnSnapshotEntry(11, (int)SpawnEntityKind.Summon, 801, 4, 1f, 2f, 3f, 3);
        service.Enqueue(in entry);
        var frame = new FrameIndex(7);
        var entityIds = includeActor ? new[] { 11 } : Array.Empty<int>();
        var entityVersions = includeActor ? new[] { version } : Array.Empty<int>();
        var entityPayload = MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(
            2, 12, entityIds, Array.Empty<long>(), entityVersions));
        var providers = new Dictionary<int, IRollbackStateProvider>
        {
            [provider.Key] = provider,
        };
        var context = new RollbackSnapshotPreflightContext(frame,
            new[]
            {
                new WorldRollbackSnapshotEntry(provider.Key, provider.Export(frame)),
                new WorldRollbackSnapshotEntry(MobaEntitasEntityRollbackProvider.DefaultKey, entityPayload),
            }, providers);

        Assert.Throws<InvalidOperationException>(() => provider.ValidateSnapshot(context));
    }

    [Fact]
    public void Snapshot_preflight_accepts_matching_actor_version()
    {
        var service = new MobaActorSpawnSnapshotService();
        var provider = new MobaPendingActorSpawnRollbackProvider(service);
        var entry = new MobaActorSpawnSnapshotEntry(11, (int)SpawnEntityKind.Summon, 801, 4, 1f, 2f, 3f, 3);
        service.Enqueue(in entry);
        var frame = new FrameIndex(7);
        var entityPayload = MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(
            2, 12, new[] { 11 }, Array.Empty<long>(), new[] { 3 }));
        var providers = new Dictionary<int, IRollbackStateProvider>
        {
            [provider.Key] = provider,
        };
        var context = new RollbackSnapshotPreflightContext(frame,
            new[]
            {
                new WorldRollbackSnapshotEntry(provider.Key, provider.Export(frame)),
                new WorldRollbackSnapshotEntry(MobaEntitasEntityRollbackProvider.DefaultKey, entityPayload),
            }, providers);

        provider.ValidateSnapshot(context);
    }

    [Fact]
    public void Empty_pending_queue_keeps_legacy_entity_payload_compatible()
    {
        var service = new MobaActorSpawnSnapshotService();
        var provider = new MobaPendingActorSpawnRollbackProvider(service);
        var frame = new FrameIndex(7);
        var legacyEntityPayload = MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(
            1, 12, new[] { 11 }, Array.Empty<long>()));
        var providers = new Dictionary<int, IRollbackStateProvider>
        {
            [provider.Key] = provider,
        };
        var context = new RollbackSnapshotPreflightContext(frame,
            new[]
            {
                new WorldRollbackSnapshotEntry(provider.Key, provider.Export(frame)),
                new WorldRollbackSnapshotEntry(MobaEntitasEntityRollbackProvider.DefaultKey, legacyEntityPayload),
            }, providers);

        provider.ValidateSnapshot(context);
    }
}
