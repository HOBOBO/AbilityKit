using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Shared.Assets;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleAssetManifestResolverTests
{
    private static ClientRoomSnapshotAssetSource NewSnapshot(params ClientRoomPlayer[] players)
    {
        return new ClientRoomSnapshotAssetSource(new ClientRoomSnapshot
        {
            RoomId = "room-1",
            Phase = ClientRoomPhase.Loading,
            LaunchGeneration = 7L,
            LaunchManifestVersion = 3,
            LaunchManifestHash = "hash-abc",
            Players = players
        });
    }

    private static ClientRoomPlayer NewPlayer(int heroId, params int[] skillIds)
    {
        return new ClientRoomPlayer
        {
            AccountId = "acc-" + heroId,
            HeroId = heroId,
            SkillIds = skillIds
        };
    }

    [Fact]
    public void SameSnapshot_ProducesSameManifest_Deterministic()
    {
        var snapshot = NewSnapshot(NewPlayer(1001, 100101), NewPlayer(1002, 100201));

        var m1 = BattleAssetManifestResolver.Resolve(snapshot);
        var m2 = BattleAssetManifestResolver.Resolve(snapshot);

        Assert.Equal(m1.Entries.Count, m2.Entries.Count);
        for (var i = 0; i < m1.Entries.Count; i++)
        {
            Assert.Equal(m1.Entries[i], m2.Entries[i]);
        }
        Assert.Equal(3, m1.ManifestVersion);
        Assert.Equal("hash-abc", m1.ManifestHash);
        Assert.Equal(7L, m1.LaunchGeneration);
    }

    [Fact]
    public void DifferentHeroId_ProducesDifferentEntries()
    {
        var m1 = BattleAssetManifestResolver.Resolve(NewSnapshot(NewPlayer(1001)));
        var m2 = BattleAssetManifestResolver.Resolve(NewSnapshot(NewPlayer(1002)));

        Assert.NotEqual(m1.Entries, m2.Entries);
        Assert.Contains(m1.Entries, e => e.AssetKey == "character:1001");
        Assert.Contains(m2.Entries, e => e.AssetKey == "character:1002");
    }

    [Fact]
    public void Entries_AreSortedByAssetKey()
    {
        var manifest = BattleAssetManifestResolver.Resolve(
            NewSnapshot(NewPlayer(2002), NewPlayer(1001)));

        var keys = manifest.Entries.Select(e => e.AssetKey).ToList();
        var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, keys);
    }

    [Fact]
    public void EmptyPlayers_StillContainsFixedConfigEntries()
    {
        var manifest = BattleAssetManifestResolver.Resolve(NewSnapshot());

        Assert.Contains(manifest.Entries, e => e.AssetKey == "config:skills");
        Assert.Contains(manifest.Entries, e => e.AssetKey == "config:characters");
        Assert.Contains(manifest.Entries, e => e.AssetKey == "config:projectiles");
        Assert.Contains(manifest.Entries, e => e.AssetKey == "map:classic");
    }

    [Fact]
    public void DuplicateHeroIds_AreDeduplicated()
    {
        var manifest = BattleAssetManifestResolver.Resolve(
            NewSnapshot(NewPlayer(1001), NewPlayer(1001)));

        var characterCount = manifest.Entries.Count(e => e.AssetKey == "character:1001");
        Assert.Equal(1, characterCount);
    }
}

public sealed class BattleAssetLoadServiceTests
{
    private sealed class MockAssetSource : IBattleAssetSource
    {
        private readonly HashSet<string> _existing;

        public MockAssetSource(params string[] existing)
        {
            _existing = new HashSet<string>(existing);
        }

        public bool TryLoad(string path, out object asset)
        {
            if (_existing.Contains(path))
            {
                asset = new object();
                return true;
            }

            asset = null!;
            return false;
        }
    }

    private sealed class CollectingProgress : IProgress<BattleAssetLoadProgress>
    {
        public List<BattleAssetLoadProgress> Reports { get; } = new();

        public void Report(BattleAssetLoadProgress value)
        {
            Reports.Add(value);
        }
    }

    private static BattleAssetManifest NewManifest(params BattleAssetEntry[] entries)
    {
        return new BattleAssetManifest(3, "hash-abc", 7L, entries);
    }

    private static BattleAssetEntry Entry(string key, string path)
    {
        return new BattleAssetEntry(path, key, BattleAssetKind.Generic);
    }

    [Fact]
    public async Task AllAssetsExist_ReturnsSuccess()
    {
        var source = new MockAssetSource("a", "b", "c");
        var service = new BattleAssetLoadService(source);
        var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"), Entry("k3", "c"));

        var result = await service.LoadAsync(manifest);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Lease);
        Assert.True(result.Lease.IsActive);
    }

    [Fact]
    public async Task MissingAsset_ReturnsFailureWithError()
    {
        var source = new MockAssetSource("a", "c");
        var service = new BattleAssetLoadService(source);
        var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"), Entry("k3", "c"));

        var result = await service.LoadAsync(manifest);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal("b", result.Errors[0].AssetPath);
        Assert.Equal("AssetNotFound", result.Errors[0].Reason);
        Assert.Null(result.Lease);
    }

    [Fact]
    public async Task CancellationToken_ReturnsFailureWithCancelledReason()
    {
        var source = new MockAssetSource("a", "b");
        var service = new BattleAssetLoadService(source);
        var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.LoadAsync(manifest, null, cts.Token);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Reason == "Cancelled");
    }

    [Fact]
    public async Task RepeatLoadSameManifest_IsIdempotentSuccess()
    {
        var source = new MockAssetSource("a", "b");
        var service = new BattleAssetLoadService(source);
        var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"));

        var r1 = await service.LoadAsync(manifest);
        var r2 = await service.LoadAsync(manifest);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
    }

    [Fact]
    public async Task ProgressCallback_IncrementsCorrectly()
    {
        var source = new MockAssetSource("a", "b", "c");
        var service = new BattleAssetLoadService(source);
        var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"), Entry("k3", "c"));
        var progress = new CollectingProgress();

        await service.LoadAsync(manifest, progress);

        Assert.True(progress.Reports.Count >= manifest.Entries.Count);
        var last = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(3, last.LoadedCount);
        Assert.Equal(3, last.TotalCount);
        Assert.Equal(1f, last.Progress01, 3);
    }
}

public sealed class BattleAssetLeaseTests
{
    [Fact]
    public void Dispose_MarksLeaseInactive()
    {
        var lease = new BattleAssetLease(7L, new[] { "a", "b" });

        Assert.True(lease.IsActive);
        Assert.Equal(7L, lease.LaunchGeneration);

        lease.Dispose();

        Assert.False(lease.IsActive);
    }
}
