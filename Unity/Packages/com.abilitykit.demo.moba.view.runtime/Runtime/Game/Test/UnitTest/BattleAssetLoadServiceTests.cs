using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleAssetManifestResolverTests
    {
        private static IBattleAssetManifestSource NewSnapshot(params ClientRoomPlayer[] players)
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

        [Test]
        public void SameSnapshot_ProducesSameManifest_Deterministic()
        {
            var snapshot = NewSnapshot(NewPlayer(1001, 100101), NewPlayer(1002, 100201));

            var m1 = BattleAssetManifestResolver.Resolve(snapshot);
            var m2 = BattleAssetManifestResolver.Resolve(snapshot);

            Assert.AreEqual(m1.Entries.Count, m2.Entries.Count);
            for (var i = 0; i < m1.Entries.Count; i++)
            {
                Assert.AreEqual(m1.Entries[i], m2.Entries[i], "Entry " + i + " differs");
            }
            Assert.AreEqual(3, m1.ManifestVersion);
            Assert.AreEqual("hash-abc", m1.ManifestHash);
            Assert.AreEqual(7L, m1.LaunchGeneration);
        }

        [Test]
        public void DifferentHeroId_ProducesDifferentEntries()
        {
            var m1 = BattleAssetManifestResolver.Resolve(NewSnapshot(NewPlayer(1001)));
            var m2 = BattleAssetManifestResolver.Resolve(NewSnapshot(NewPlayer(1002)));

            CollectionAssert.AreNotEqual(m1.Entries, m2.Entries);
            Assert.IsTrue(m1.Entries.Any(e => e.AssetKey == "character:1001"));
            Assert.IsTrue(m2.Entries.Any(e => e.AssetKey == "character:1002"));
        }

        [Test]
        public void Entries_AreSortedByAssetKey()
        {
            // 故意乱序输入 hero id
            var manifest = BattleAssetManifestResolver.Resolve(
                NewSnapshot(NewPlayer(2002), NewPlayer(1001)));

            var keys = manifest.Entries.Select(e => e.AssetKey).ToList();
            var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            CollectionAssert.AreEqual(sorted, keys);
        }

        [Test]
        public void EmptyPlayers_StillContainsFixedConfigEntries()
        {
            var manifest = BattleAssetManifestResolver.Resolve(NewSnapshot());

            Assert.IsTrue(manifest.Entries.Any(e => e.AssetKey == "config:skills"));
            Assert.IsTrue(manifest.Entries.Any(e => e.AssetKey == "config:characters"));
            Assert.IsTrue(manifest.Entries.Any(e => e.AssetKey == "config:projectiles"));
            Assert.IsTrue(manifest.Entries.Any(e => e.AssetKey == "map:classic"));
        }

        [Test]
        public void DuplicateHeroIds_AreDeduplicated()
        {
            var manifest = BattleAssetManifestResolver.Resolve(
                NewSnapshot(NewPlayer(1001), NewPlayer(1001)));

            var characterCount = manifest.Entries.Count(e => e.AssetKey == "character:1001");
            Assert.AreEqual(1, characterCount);
        }

        [Test]
        public void DependencyProvider_EntriesAreMergedAndSorted()
        {
            var provider = new FixedDependencyProvider(
                new BattleAssetEntry("effect/b", "presentation:vfx-prefab:2", BattleAssetKind.Presentation),
                new BattleAssetEntry("character/a", "presentation:model-prefab:1", BattleAssetKind.Presentation));

            var manifest = BattleAssetManifestResolver.Resolve(NewSnapshot(), provider);
            var keys = manifest.Entries.Select(entry => entry.AssetKey).ToArray();

            CollectionAssert.Contains(keys, "presentation:model-prefab:1");
            CollectionAssert.Contains(keys, "presentation:vfx-prefab:2");
            CollectionAssert.AreEqual(keys.OrderBy(key => key, StringComparer.Ordinal), keys);
        }

        [Test]
        public void DependencyProvider_ConflictingKeysAreRejected()
        {
            var provider = new FixedDependencyProvider(
                new BattleAssetEntry("effect/a", "presentation:vfx-prefab:1", BattleAssetKind.Presentation),
                new BattleAssetEntry("effect/b", "presentation:vfx-prefab:1", BattleAssetKind.Presentation));

            Assert.Throws<InvalidOperationException>(
                () => BattleAssetManifestResolver.Resolve(NewSnapshot(), provider));
        }

        [Test]
        public void ResourcesDependencyProvider_ResolvesExistingConcreteAssets()
        {
            var dependencies = ResourcesBattleAssetDependencyProvider.Default.ResolveDependencies(NewSnapshot());

            Assert.AreEqual(9, dependencies.Count(entry =>
                entry.AssetKey.StartsWith("presentation:model-prefab:", StringComparison.Ordinal)));
            Assert.AreEqual(23, dependencies.Count(entry =>
                entry.AssetKey.StartsWith("presentation:vfx-prefab:", StringComparison.Ordinal)));
            Assert.IsTrue(dependencies.Any(entry => entry.AssetPath == "character/character1"));
            Assert.IsTrue(dependencies.Any(entry => entry.AssetPath == "effect/bullet_1"));
            Assert.IsTrue(dependencies.All(entry =>
                UnityEngine.Resources.Load<UnityEngine.Object>(entry.AssetPath) != null),
                "Every expanded presentation dependency must resolve from Resources.");
        }

        private sealed class FixedDependencyProvider : IBattleAssetDependencyProvider
        {
            private readonly IReadOnlyList<BattleAssetEntry> _entries;

            public FixedDependencyProvider(params BattleAssetEntry[] entries)
            {
                _entries = entries;
            }

            public IReadOnlyList<BattleAssetEntry> ResolveDependencies(IBattleAssetManifestSource source)
            {
                return _entries;
            }
        }
    }

    public sealed class BattleAssetLoadServiceTests
    {
        private sealed class MockAssetSource : IBattleAssetSource, IBattleAssetReleaseSource
        {
            private readonly HashSet<string> _existing;
            public readonly List<object> Released = new List<object>();

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

                asset = null;
                return false;
            }

            public void Release(object asset)
            {
                Released.Add(asset);
            }
        }

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public ImmediateProgress(Action<T> report)
            {
                _report = report ?? throw new ArgumentNullException(nameof(report));
            }

            public void Report(T value)
            {
                _report(value);
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

        [Test]
        public void AllAssetsExist_ReturnsSuccess()
        {
            var source = new MockAssetSource("a", "b", "c");
            var service = new BattleAssetLoadService(source);
            var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"), Entry("k3", "c"));

            var result = service.LoadAsync(manifest).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.IsNotNull(result.Lease);
            Assert.IsTrue(result.Lease.IsActive);
        }

        [Test]
        public void MissingAsset_ReturnsFailureWithError()
        {
            var source = new MockAssetSource("a", "c");
            var service = new BattleAssetLoadService(source);
            var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"), Entry("k3", "c"));

            var result = service.LoadAsync(manifest).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual("b", result.Errors[0].AssetPath);
            Assert.AreEqual("AssetNotFound", result.Errors[0].Reason);
            Assert.IsNull(result.Lease);
            Assert.AreEqual(2, source.Released.Count);
        }

        [Test]
        public void CancellationToken_ReturnsFailureWithCancelledReason()
        {
            var source = new MockAssetSource("a", "b");
            var service = new BattleAssetLoadService(source);
            var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = service.LoadAsync(manifest, null, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Any(e => e.Reason == "Cancelled"));
        }

        [Test]
        public void RepeatLoadSameManifest_IsIdempotentSuccess()
        {
            var source = new MockAssetSource("a", "b");
            var service = new BattleAssetLoadService(source);
            var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"));

            var r1 = service.LoadAsync(manifest).GetAwaiter().GetResult();
            var r2 = service.LoadAsync(manifest).GetAwaiter().GetResult();

            Assert.IsTrue(r1.Success);
            Assert.IsTrue(r2.Success);
        }

        [Test]
        public void ProgressCallback_IncrementsCorrectly()
        {
            var source = new MockAssetSource("a", "b", "c");
            var service = new BattleAssetLoadService(source);
            var manifest = NewManifest(Entry("k1", "a"), Entry("k2", "b"), Entry("k3", "c"));
            var reports = new List<BattleAssetLoadProgress>();
            var progress = new ImmediateProgress<BattleAssetLoadProgress>(p => reports.Add(p));

            service.LoadAsync(manifest, progress).GetAwaiter().GetResult();

            // 至少报告了每个条目的进度 + 最终完成
            Assert.GreaterOrEqual(reports.Count, manifest.Entries.Count);
            var last = reports[reports.Count - 1];
            Assert.AreEqual(3, last.LoadedCount);
            Assert.AreEqual(3, last.TotalCount);
            Assert.AreEqual(1f, last.Progress01, 0.0001f);
        }

        [Test]
        public void LeaseDispose_ReleasesEveryLoadedAssetExactlyOnce()
        {
            var source = new MockAssetSource("a", "b");
            var service = new BattleAssetLoadService(source);
            var result = service.LoadAsync(NewManifest(Entry("k1", "a"), Entry("k2", "b")))
                .GetAwaiter().GetResult();

            result.Lease.Dispose();
            result.Lease.Dispose();

            Assert.AreEqual(2, source.Released.Count);
            Assert.IsFalse(result.Lease.IsActive);
        }

        [Test]
        public void SuccessfulLease_ExposesLoadedAssetsByPathUntilDisposed()
        {
            var source = new MockAssetSource("a");
            var service = new BattleAssetLoadService(source);
            var result = service.LoadAsync(NewManifest(Entry("k1", "a")))
                .GetAwaiter().GetResult();
            var lookup = result.Lease as IBattleAssetLookup;

            Assert.IsNotNull(lookup);
            Assert.IsTrue(lookup.TryGetAsset("a", out var loaded));
            Assert.IsNotNull(loaded);
            Assert.IsFalse(lookup.TryGetAsset("missing", out _));

            result.Lease.Dispose();

            Assert.IsFalse(lookup.TryGetAsset("a", out _));
        }
    }

    public sealed class BattleAssetLeaseTests
    {
        [Test]
        public void Dispose_MarksLeaseInactive()
        {
            var lease = new BattleAssetLease(7L, new[] { "a", "b" });

            Assert.IsTrue(lease.IsActive);
            Assert.AreEqual(7L, lease.LaunchGeneration);

            lease.Dispose();

            Assert.IsFalse(lease.IsActive);
        }
    }

    public sealed class BattleAssetLoadCoordinatorTests
    {
        [Test]
        public async Task CancelThenRetry_StaleCompletionCannotConsumeRetryCallbackOrLease()
        {
            var service = new DeferredLoadService();
            var coordinator = new BattleAssetLoadCoordinator(service, CreateManifest);
            var firstCallbackCount = 0;
            var secondCompletion = new TaskCompletionSource<bool>();

            coordinator.StartLoading(success =>
            {
                firstCallbackCount++;
                Assert.IsFalse(success);
            });
            coordinator.Cancel();
            coordinator.StartLoading(success => secondCompletion.TrySetResult(success));

            var staleLease = new BattleAssetLease(1, new[] { "stale" });
            service.Complete(0, Success(staleLease));
            await Task.Yield();

            Assert.AreEqual(1, firstCallbackCount);
            Assert.IsTrue(coordinator.IsLoading);
            Assert.IsFalse(staleLease.IsActive);
            Assert.IsFalse(secondCompletion.Task.IsCompleted);

            var currentLease = new BattleAssetLease(1, new[] { "current" });
            service.Complete(1, Success(currentLease));
            Assert.IsTrue(await secondCompletion.Task);
            Assert.IsFalse(coordinator.IsLoading);
            Assert.IsTrue(currentLease.IsActive);

            coordinator.ReleaseLease();
            Assert.IsFalse(currentLease.IsActive);
        }

        [Test]
        public void ManifestProviderFailure_DoesNotLeaveCoordinatorLoading()
        {
            var coordinator = new BattleAssetLoadCoordinator(
                new DeferredLoadService(),
                () => throw new InvalidOperationException("manifest failed"));

            Assert.Throws<InvalidOperationException>(() => coordinator.StartLoading(_ => { }));
            Assert.IsFalse(coordinator.IsLoading);
        }

        [Test]
        public void ManifestProviderCannotBeReenteredBySecondStart()
        {
            BattleAssetLoadCoordinator coordinator = null;
            var nestedRejected = false;
            var service = new DeferredLoadService();
            coordinator = new BattleAssetLoadCoordinator(
                service,
                () =>
                {
                    nestedRejected = Assert.Throws<InvalidOperationException>(
                        () => coordinator.StartLoading(_ => { })) != null;
                    return CreateManifest();
                });

            coordinator.StartLoading(_ => { });

            Assert.IsTrue(nestedRejected);
            Assert.IsTrue(coordinator.IsLoading);
            coordinator.Cancel();
            service.Complete(0, Failure());
        }

        [Test]
        public async Task CancelledOperationKeepsTokenSourceAliveUntilLoadCompletes()
        {
            var service = new DeferredLoadService();
            var coordinator = new BattleAssetLoadCoordinator(service, CreateManifest);
            coordinator.StartLoading(_ => { });

            coordinator.Cancel();

            Assert.DoesNotThrow(() => service.RegisterCancellation(0));
            service.Complete(0, Failure());
            await Task.Yield();
        }

        [Test]
        public async Task FailedResultLease_IsDisposedInsteadOfLeaked()
        {
            var service = new DeferredLoadService();
            var coordinator = new BattleAssetLoadCoordinator(service, CreateManifest);
            var completion = new TaskCompletionSource<bool>();
            coordinator.StartLoading(success => completion.TrySetResult(success));
            var invalidLease = new BattleAssetLease(1, new[] { "invalid" });

            service.Complete(0, new BattleAssetLoadResult(
                false,
                1,
                1,
                "hash",
                Array.Empty<BattleAssetLoadError>(),
                invalidLease));

            Assert.IsFalse(await completion.Task);
            Assert.IsFalse(invalidLease.IsActive);
        }

        [Test]
        public async Task FailedResult_IsRetainedForStructuredDiagnostics()
        {
            var service = new DeferredLoadService();
            var coordinator = new BattleAssetLoadCoordinator(service, CreateManifest);
            var completion = new TaskCompletionSource<bool>();
            coordinator.StartLoading(success => completion.TrySetResult(success));
            var error = new BattleAssetLoadError("missing/path", "missing:key", "AssetNotFound");

            service.Complete(0, new BattleAssetLoadResult(
                false,
                1,
                1,
                "hash",
                new[] { error }));

            Assert.IsFalse(await completion.Task);
            Assert.IsNotNull(coordinator.LastResult);
            Assert.AreEqual(1, coordinator.LastResult.Errors.Count);
            Assert.AreEqual("missing:key", coordinator.LastResult.Errors[0].AssetKey);
        }

        [Test]
        public async Task TakeLease_TransfersSuccessfulLeaseOwnership()
        {
            var service = new DeferredLoadService();
            var coordinator = new BattleAssetLoadCoordinator(service, CreateManifest);
            var completion = new TaskCompletionSource<bool>();
            coordinator.StartLoading(success => completion.TrySetResult(success));
            var lease = new BattleAssetLease(1, new[] { "battle" });

            service.Complete(0, Success(lease));

            Assert.IsTrue(await completion.Task);
            Assert.AreSame(lease, coordinator.TakeLease());
            Assert.IsNull(coordinator.TakeLease());
            coordinator.ReleaseLease();
            Assert.IsTrue(lease.IsActive);

            lease.Dispose();
            Assert.IsFalse(lease.IsActive);
        }

        private static BattleAssetManifest CreateManifest()
        {
            return new BattleAssetManifest(1, "hash", 1, Array.Empty<BattleAssetEntry>());
        }

        private static BattleAssetLoadResult Success(IBattleAssetLease lease)
        {
            return new BattleAssetLoadResult(
                true,
                1,
                1,
                "hash",
                Array.Empty<BattleAssetLoadError>(),
                lease);
        }

        private static BattleAssetLoadResult Failure()
        {
            return new BattleAssetLoadResult(
                false,
                1,
                1,
                "hash",
                Array.Empty<BattleAssetLoadError>(),
                null);
        }

        private sealed class DeferredLoadService : IBattleAssetLoadService
        {
            private readonly List<TaskCompletionSource<BattleAssetLoadResult>> _loads =
                new List<TaskCompletionSource<BattleAssetLoadResult>>();
            private readonly List<CancellationToken> _tokens = new List<CancellationToken>();

            public Task<BattleAssetLoadResult> LoadAsync(
                BattleAssetManifest manifest,
                IProgress<BattleAssetLoadProgress> progress = null,
                CancellationToken cancellationToken = default)
            {
                var completion = new TaskCompletionSource<BattleAssetLoadResult>();
                _loads.Add(completion);
                _tokens.Add(cancellationToken);
                return completion.Task;
            }

            public void Complete(int index, BattleAssetLoadResult result)
            {
                _loads[index].SetResult(result);
            }

            public void RegisterCancellation(int index)
            {
                _tokens[index].Register(() => { }).Dispose();
            }
        }
    }
}
