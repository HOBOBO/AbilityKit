using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MultiplayerBattleAssetLoaderTests
{
    [Fact]
    public async Task OlderGenerationCompletingLate_CannotReplaceCurrentLease()
    {
        var service = new DeferredLoadService();
        var loader = new MultiplayerBattleAssetLoader(service);
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

        public Task<BattleAssetLoadResult> LoadAsync(
            BattleAssetManifest manifest,
            IProgress<BattleAssetLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
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
}
