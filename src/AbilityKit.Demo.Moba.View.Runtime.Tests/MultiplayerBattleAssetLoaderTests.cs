using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MultiplayerBattleAssetLoaderTests
{
    [Fact]
    public async Task OlderGenerationCompletingLate_CannotReplaceCurrentLease()
    {
        var service = new DeferredLoadService();
        var loader = new MultiplayerBattleAssetLoader(
            service,
            dependencyProvider: new FixedDependencyProvider());
        using var oldCancellation = new CancellationTokenSource();

        var oldLoad = loader.LoadAsync(Snapshot(7), progress: null!, oldCancellation.Token);
        var newLoad = loader.LoadAsync(Snapshot(8), progress: null!, CancellationToken.None);

        var currentLease = service.Complete(8);
        await newLoad;
        oldCancellation.Cancel();
        var staleLease = service.Complete(7);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldLoad);
        Assert.False(staleLease.IsActive);
        Assert.True(currentLease.IsActive);

        loader.Release();
        Assert.False(currentLease.IsActive);
        Assert.Contains(
            service.LastManifest!.Entries,
            entry => entry.AssetKey == "presentation:test-prefab");
    }

    [Fact]
    public async Task TakeLease_TransfersOwnershipOutOfMultiplayerLoader()
    {
        var service = new DeferredLoadService();
        var loader = new MultiplayerBattleAssetLoader(
            service,
            dependencyProvider: new FixedDependencyProvider());
        var load = loader.LoadAsync(Snapshot(9), progress: null!, CancellationToken.None);
        var lease = service.Complete(9);
        await load;

        var transferred = loader.TakeLease();

        Assert.Same(lease, transferred);
        Assert.Null(loader.TakeLease());
        loader.Release();
        Assert.True(lease.IsActive);

        transferred!.Dispose();
        Assert.False(lease.IsActive);
    }

    [Fact]
    public async Task LoadAsync_WithMainThreadDispatcher_PostsBeforeLoadingAssets()
    {
        var service = new DeferredLoadService();
        var dispatcher = new ManualDispatcher();
        var loader = new MultiplayerBattleAssetLoader(
            service,
            dependencyProvider: new FixedDependencyProvider(),
            mainThreadDispatcher: dispatcher);

        var load = loader.LoadAsync(Snapshot(10), progress: null!, CancellationToken.None);

        Assert.Equal(1, dispatcher.PostCalls);
        Assert.Null(service.LastManifest);

        dispatcher.RunPending();
        var lease = service.Complete(10);
        await load;

        Assert.Same(lease, loader.TakeLease());
    }

    private static MultiplayerRoomSnapshot Snapshot(long generation)
    {
        return new MultiplayerRoomSnapshot
        {
            RoomId = "room-1",
            Phase = MultiplayerRoomPhase.Loading,
            LaunchGeneration = generation,
            LaunchManifestVersion = 3,
            LaunchManifestHash = $"manifest-{generation}"
        };
    }

    private sealed class DeferredLoadService : IBattleAssetLoadService
    {
        private readonly Dictionary<long, TaskCompletionSource<BattleAssetLoadResult>> _pending = new();
        public BattleAssetManifest? LastManifest { get; private set; }

        public Task<BattleAssetLoadResult> LoadAsync(
            BattleAssetManifest manifest,
            IProgress<BattleAssetLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastManifest = manifest;
            var completion = new TaskCompletionSource<BattleAssetLoadResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(manifest.LaunchGeneration, completion);
            return completion.Task;
        }

        public TestLease Complete(long generation)
        {
            var lease = new TestLease(generation);
            _pending[generation].SetResult(new BattleAssetLoadResult(
                true,
                generation,
                3,
                $"manifest-{generation}",
                Array.Empty<BattleAssetLoadError>(),
                lease));
            return lease;
        }
    }

    private sealed class FixedDependencyProvider : IBattleAssetDependencyProvider
    {
        public IReadOnlyList<BattleAssetEntry> ResolveDependencies(IBattleAssetManifestSource source)
        {
            return new[]
            {
                new BattleAssetEntry(
                    "presentation/test",
                    "presentation:test-prefab",
                    BattleAssetKind.Presentation)
            };
        }
    }

    private sealed class TestLease : IBattleAssetLease
    {
        public TestLease(long generation)
        {
            LaunchGeneration = generation;
            IsActive = true;
        }

        public bool IsActive { get; private set; }
        public long LaunchGeneration { get; }

        public void Dispose()
        {
            IsActive = false;
        }
    }

    private sealed class ManualDispatcher : IDispatcher
    {
        private Action? _pending;

        public int PostCalls { get; private set; }

        public void Post(Action action)
        {
            PostCalls++;
            _pending = action;
        }

        public void RunPending()
        {
            var pending = _pending ?? throw new InvalidOperationException("No action was posted.");
            _pending = null;
            pending();
        }
    }
}
