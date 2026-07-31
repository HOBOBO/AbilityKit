#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.View.Loading;

namespace AbilityKit.Game.Flow
{
    internal sealed class MultiplayerBattleAssetLoader : IMultiplayerBattleAssetLoader
    {
        private const string ValidateStepType = "moba.manifest.validate";
        private const string AssetsStepType = "moba.assets.load";

        private static readonly ClientLoadingPipelineDefinition DefaultPipeline =
            new ClientLoadingPipelineDefinition(new[]
            {
                new ClientLoadingStepDefinition("manifest", ValidateStepType, 5),
                new ClientLoadingStepDefinition("battle-assets", AssetsStepType, 95)
            });

        private readonly IBattleAssetLoadService _loadService;
        private readonly ClientLoadingPipelineDefinition _pipelineDefinition;
        private readonly object _gate = new object();
        private IBattleAssetLease? _lease;
        private long _loadVersion;

        public MultiplayerBattleAssetLoader(
            IBattleAssetLoadService loadService,
            ClientLoadingPipelineDefinition pipelineDefinition = null)
        {
            _loadService = loadService ?? throw new ArgumentNullException(nameof(loadService));
            _pipelineDefinition = pipelineDefinition ?? DefaultPipeline;
        }

        public async Task LoadAsync(
            MultiplayerRoomSnapshot snapshot,
            IProgress<MultiplayerAssetLoadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            long loadVersion;
            lock (_gate)
            {
                loadVersion = ++_loadVersion;
            }

            var manifest = BattleAssetManifestResolver.Resolve(new SnapshotAssetSource(snapshot));
            BattleAssetLoadResult result = null;
            var registry = new ClientLoadingStepRegistry()
                .Register(ValidateStepType, _ => new DelegateClientLoadingStep((stepProgress, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    ValidateManifest(snapshot, manifest);
                    stepProgress.Report(1f);
                    return Task.CompletedTask;
                }))
                .Register(AssetsStepType, _ => new DelegateClientLoadingStep(async (stepProgress, ct) =>
                {
                    result = await _loadService.LoadAsync(
                        manifest,
                        new AssetStepProgressAdapter(stepProgress, progress),
                        ct).ConfigureAwait(false);
                }));
            var pipeline = new ClientLoadingPipeline(_pipelineDefinition, registry);
            await pipeline.ExecuteAsync(
                progress == null ? null : new PipelineProgressAdapter(progress),
                cancellationToken).ConfigureAwait(false);

            if (result == null || !result.Success || result.Lease == null || !result.Lease.IsActive)
            {
                result?.Lease?.Dispose();
                throw new InvalidOperationException(BuildFailureMessage(result));
            }

            if (result.LaunchGeneration != snapshot.LaunchGeneration ||
                result.ManifestVersion != snapshot.LaunchManifestVersion ||
                !string.Equals(result.ManifestHash, snapshot.LaunchManifestHash, StringComparison.Ordinal))
            {
                result.Lease.Dispose();
                throw new InvalidOperationException("Loaded assets do not match the authoritative launch manifest.");
            }

            IBattleAssetLease? previousLease = null;
            var accepted = false;
            lock (_gate)
            {
                if (loadVersion == _loadVersion && !cancellationToken.IsCancellationRequested)
                {
                    previousLease = _lease;
                    _lease = result.Lease;
                    accepted = true;
                }
            }

            if (!accepted)
            {
                result.Lease.Dispose();
                throw new OperationCanceledException(
                    "Battle asset load was superseded by a newer launch generation.",
                    cancellationToken);
            }

            previousLease?.Dispose();
        }

        public void Release()
        {
            IBattleAssetLease? lease;
            lock (_gate)
            {
                _loadVersion++;
                lease = _lease;
                _lease = null;
            }

            lease?.Dispose();
        }

        private static string BuildFailureMessage(BattleAssetLoadResult? result)
        {
            if (result?.Errors == null || result.Errors.Count == 0)
            {
                return "Battle asset loading failed.";
            }

            var first = result.Errors[0];
            return $"Battle asset loading failed: {first.AssetKey} ({first.Reason}).";
        }

        private static void ValidateManifest(
            MultiplayerRoomSnapshot snapshot,
            BattleAssetManifest manifest)
        {
            if (snapshot.LaunchGeneration <= 0 ||
                snapshot.LaunchManifestVersion <= 0 ||
                string.IsNullOrWhiteSpace(snapshot.LaunchManifestHash))
            {
                throw new InvalidOperationException("The authoritative battle manifest is incomplete.");
            }

            if (manifest.LaunchGeneration != snapshot.LaunchGeneration ||
                manifest.ManifestVersion != snapshot.LaunchManifestVersion ||
                !string.Equals(manifest.ManifestHash, snapshot.LaunchManifestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Resolved battle assets do not match the authoritative launch manifest.");
            }
        }

        private sealed class AssetStepProgressAdapter : IProgress<BattleAssetLoadProgress>
        {
            private readonly IProgress<float> _target;
            private readonly IProgress<MultiplayerAssetLoadProgress> _overallTarget;

            public AssetStepProgressAdapter(
                IProgress<float> target,
                IProgress<MultiplayerAssetLoadProgress> overallTarget)
            {
                _target = target;
                _overallTarget = overallTarget;
            }

            public void Report(BattleAssetLoadProgress value)
            {
                _target.Report(value.Progress01);
                _overallTarget?.Report(new MultiplayerAssetLoadProgress(
                    5 + (int)Math.Round(value.Progress01 * 95f),
                    value.LoadedCount,
                    value.TotalCount,
                    value.CurrentAssetKey));
            }
        }

        private sealed class PipelineProgressAdapter : IProgress<ClientLoadingProgress>
        {
            private readonly IProgress<MultiplayerAssetLoadProgress> _target;

            public PipelineProgressAdapter(IProgress<MultiplayerAssetLoadProgress> target)
            {
                _target = target;
            }

            public void Report(ClientLoadingProgress value)
            {
                _target.Report(new MultiplayerAssetLoadProgress(
                    value.OverallProgress,
                    0,
                    100,
                    value.StageId));
            }
        }

        private sealed class SnapshotAssetSource : IBattleAssetManifestSource
        {
            private readonly MultiplayerRoomSnapshot _snapshot;

            public SnapshotAssetSource(MultiplayerRoomSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public IReadOnlyList<IBattleAssetManifestPlayer> Players
            {
                get
                {
                    var players = _snapshot.Players;
                    if (players == null || players.Count == 0)
                    {
                        return Array.Empty<IBattleAssetManifestPlayer>();
                    }

                    var result = new IBattleAssetManifestPlayer[players.Count];
                    for (var i = 0; i < players.Count; i++)
                    {
                        result[i] = new PlayerAssetSource(players[i]);
                    }

                    return result;
                }
            }

            public int LaunchManifestVersion => _snapshot.LaunchManifestVersion;
            public string LaunchManifestHash => _snapshot.LaunchManifestHash;
            public long LaunchGeneration => _snapshot.LaunchGeneration;
        }

        private sealed class PlayerAssetSource : IBattleAssetManifestPlayer
        {
            private readonly MultiplayerRoomPlayerSnapshot _player;

            public PlayerAssetSource(MultiplayerRoomPlayerSnapshot player)
            {
                _player = player;
            }

            public int HeroId => _player.HeroId;
            public int BasicAttackSkillId => _player.BasicAttackSkillId;
            public IReadOnlyList<int> SkillIds => _player.SkillIds;
        }
    }
}
