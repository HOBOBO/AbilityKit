#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 客户端控制器。
    /// 本地玩家使用权威 pose、输入确认和有界未确认输入重放；远端 actor 只进入服务器时间线插值，
    /// 不导入本地模拟，也不触发整世界回滚。
    /// </summary>
    public sealed class ShooterClientAuthoritativeInterpolationSyncController : IShooterClientSyncController, IShooterClientFrameSyncCapability, IShooterClientInputCapability, IInterpolationDiagnosticsProvider
    {
        private const int MaxPendingInputs = 128;
        private const int MaxReplayFrames = 120;
        private const float PositionQuantizationScale = 1000f;
        private const float SmallErrorTolerance = 0.05f;
        private const float SnapErrorThreshold = 0.5f;
        private const float MaxCorrectionPerSnapshot = 0.25f;

        private readonly IShooterBattleRuntimePort _runtime;
        private readonly ShooterClientSyncCore _core;
        private readonly ShooterPresentationFacade _presentation;
        private readonly ShooterGatewaySnapshotDecoder _decoder;
        private readonly RemoteInterpolationPlayback<ShooterRemoteSnapshotSample> _playback;
        private readonly ShooterRemoteSnapshotProjector _projector = new ShooterRemoteSnapshotProjector();
        private readonly NetworkSyncModel _syncModel;
        private readonly float _fixedDeltaTime;
        private readonly object _pendingInputLock = new object();
        private readonly List<PendingLocalInput> _pendingInputs = new List<PendingLocalInput>(MaxPendingInputs);
        private ShooterPlayerCommand _lastPredictedCommand;
        private ShooterStateSyncPredictionState _predictionState = ShooterStateSyncPredictionState.Empty;
        private SyncReconciliationReport _localReconciliationReport = SyncReconciliationReport.None;
        private ulong _authorityWorldId;
        private int _lastAuthorityFrame = -1;
        private int _lastGatewaySnapshotFrame = -1;
        private long _nextSyntheticSubmissionId;

        public ShooterClientAuthoritativeInterpolationSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway)
            : this(runtime, presentation, tickRate, decoder, gateway, InterpolationConfig.Default)
        {
        }

        public ShooterClientAuthoritativeInterpolationSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig config)
            : this(runtime, presentation, tickRate, decoder, gateway, config, NetworkSyncModel.AuthoritativeInterpolation)
        {
        }

        public ShooterClientAuthoritativeInterpolationSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig config,
            NetworkSyncModel syncModel,
            ShooterClientPredictionBufferOptions? predictionBufferOptions = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _core = new ShooterClientSyncCore(_runtime, presentation, tickRate, decoder, gateway, predictionBufferOptions);
            _decoder = decoder ?? new ShooterGatewaySnapshotDecoder();
            _playback = new RemoteInterpolationPlayback<ShooterRemoteSnapshotSample>(config);
            _syncModel = syncModel;
            _fixedDeltaTime = 1f / Math.Max(1, tickRate);
        }

        public NetworkSyncModel SyncModel => _syncModel;

        public bool IsStarted => _core.IsStarted;

        public int CurrentFrame => _core.CurrentFrame;

        public int GatewayInputFrame => _lastGatewaySnapshotFrame >= 0 ? _lastGatewaySnapshotFrame : CurrentFrame;

        public ShooterClientFrameSyncController FrameSync => _core.FrameSync;

        public ShooterClientInputCoordinator InputCoordinator => _core.InputCoordinator;

        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _core.FrameworkSnapshotPipelineDiagnostics;

        public ShooterClientReconciliationResult LastReconciliationResult => _core.LastReconciliationResult;

        public bool NeedsFullSnapshotResync => _core.NeedsFullSnapshotResync;

        public ShooterClientRecoveryState RecoveryState => _core.RecoveryState;

        public AbilityKit.Network.Runtime.Sync.FastReconnectPhase FastReconnectPhase => _core.FastReconnectPhase;

        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents => _core.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _core.LastResyncReason;

        public int LastResyncClientFrame => _core.LastResyncClientFrame;

        public int LastResyncAuthoritativeFrame => _core.LastResyncAuthoritativeFrame;

        public uint LastResyncClientStateHash => _core.LastResyncClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _core.LastResyncAuthoritativeStateHash;

        public bool HasGateway => _core.HasGateway;

        /// <summary>当前为插值缓冲的远端权威快照数量。</summary>
        public int BufferedRemoteSnapshotCount => _playback.BufferedSampleCount;

        /// <summary>当前延迟远端播放时间，单位为时间线 tick。</summary>
        public long RemotePlaybackTicks => _playback.PlaybackTicks;

        /// <summary>当前本地估算的权威服务器时间，单位为时间线 tick。</summary>
        public long EstimatedServerTicks => _playback.EstimatedServerTicks;

        /// <summary>是否已经向表现层发布过至少一帧远端插值结果。</summary>
        public bool HasPublishedRemoteFrame => _playback.HasPublished;

        /// <summary>
        /// 最近一次发布尝试是否发现延迟播放时间已经超过最新缓冲快照
        /// <see cref="InterpolationConfig.MaxExtrapolationTicks"/> 以上。
        /// 表示远端缓冲已经饥饿（例如快照停止到达），播放会保持最后一个权威姿态，而不是继续外推。
        /// </summary>
        public bool IsRemotePlaybackStarved => _playback.IsStarved;

        public ShooterStateSyncPredictionState PredictionState => _predictionState;

        public bool StartGame(in ShooterStartGamePayload startGame)
        {
            ResetAuthoritativeState();
            return _core.StartGame(in startGame);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            var command = new ShooterPlayerCommand(playerId, moveX, moveY, aimX, aimY, fire);
            return SubmitLocalInput(in command);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            var result = _core.SubmitLocalInput(in command);
            if (command.PlayerId > 0 && result.AcceptedInputs > 0)
            {
                _lastPredictedCommand = command;
                RecordPendingInput(in result);
                RefreshPredictedPose(command.PlayerId, CurrentFrame, EstimatedServerTicks);
            }

            return result;
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var local = SubmitLocalInput(in command).WithRequestedFrame(context.Frame);
            return await SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            RecordPendingInput(in local);
            MarkGatewayStarted(in local);
            var result = await _core.SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken).ConfigureAwait(false);
            BindGatewayResult(in result.Local, in result.Remote);
            return result;
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            var result = _core.Tick(deltaTime);
            RefreshPredictedPose(_lastPredictedCommand.PlayerId, result.Frame, EstimatedServerTicks);
            _playback.Advance(deltaTime);
            PublishInterpolatedRemoteFrame();
            return result;
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            return _core.CatchUpToFrame(targetFrame);
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            return _core.TryEnterCatchUp(authoritativeFrame);
        }

        /// <summary>
        /// 应用权威快照。本地玩家只做局部 pose 纠偏；远端玩家进入延迟插值缓冲。
        /// </summary>
        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            if (!_decoder.IsSnapshotPush(opCode))
            {
                return ShooterSnapshotApplyResult.Ignored;
            }

            var snapshot = _decoder.Decode(payload);
            if (snapshot.PureStateSnapshot.HasValue)
            {
                var pureStateResult = _presentation.ApplyPureStateGatewaySnapshot(in snapshot);
                if (pureStateResult == ShooterPureStateSnapshotApplyResult.AppliedFullBaseline
                    || pureStateResult == ShooterPureStateSnapshotApplyResult.AppliedDelta)
                {
                    ObserveGatewaySnapshotFrame(snapshot.Frame);
                    ReconcileControlledPlayer(in snapshot, forceAuthorityReset: false);
                }

                return ShooterSnapshotApplyResults.FromPureStateResult(pureStateResult);
            }

            return BufferRemoteSnapshot(in snapshot);
        }

        /// <summary>
        /// 为延迟插值缓冲一个已经解码的网关快照。
        /// </summary>
        public ShooterSnapshotApplyResult BufferRemoteSnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var worldChanged = snapshot.WorldId != 0
                && _authorityWorldId != 0
                && snapshot.WorldId != _authorityWorldId;
            if (worldChanged)
            {
                ResetAuthoritativeState();
            }

            var sample = new ShooterRemoteSnapshotSample(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTicks,
                snapshot.Actors,
                _presentation.ControlledPlayerId);

            if (!_playback.Observe(sample))
            {
                return ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
            }

            ObserveGatewaySnapshotFrame(snapshot.Frame);
            ReconcileControlledPlayer(in snapshot, worldChanged);
            ObserveAuthoritativeProjectileActions(in snapshot);
            return ShooterSnapshotApplyResult.AppliedActorSnapshot;
        }

        private void MarkGatewayStarted(in ShooterClientInputSubmitResult local)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    if (local.SubmissionId > 0 && _pendingInputs[i].SubmissionId == local.SubmissionId)
                    {
                        _pendingInputs[i].GatewayStarted = true;
                        return;
                    }
                }
            }
        }

        private void ResetAuthoritativeState()
        {
            lock (_pendingInputLock)
            {
                _pendingInputs.Clear();
            }

            _authorityWorldId = 0;
            _lastAuthorityFrame = -1;
            _lastGatewaySnapshotFrame = -1;
            _localReconciliationReport = SyncReconciliationReport.None;
            _predictionState = ShooterStateSyncPredictionState.Empty;
            _lastPredictedCommand = default;
            _playback.Reset();
        }

        private void ObserveGatewaySnapshotFrame(int frame)
        {
            if (frame >= 0 && frame > _lastGatewaySnapshotFrame)
            {
                _lastGatewaySnapshotFrame = frame;
            }
        }

        private void RecordPendingInput(in ShooterClientInputSubmitResult result)
        {
            if (result.AcceptedInputs <= 0 || result.Packet.Command.PlayerId <= 0)
            {
                return;
            }

            lock (_pendingInputLock)
            {
                var submissionId = result.SubmissionId > 0
                    ? result.SubmissionId
                    : Interlocked.Increment(ref _nextSyntheticSubmissionId);
                for (var i = 0; i < _pendingInputs.Count; i++)
                {
                    if (_pendingInputs[i].SubmissionId == submissionId)
                    {
                        _pendingInputs[i].RequestedFrame = result.RequestedFrame;
                        return;
                    }
                }

                if (_pendingInputs.Count >= MaxPendingInputs)
                {
                    _pendingInputs.RemoveAt(0);
                }

                _pendingInputs.Add(new PendingLocalInput(
                    submissionId,
                    result.RequestedFrame,
                    in result.Packet.Command));
            }
        }

        private void BindGatewayResult(
            in ShooterClientInputSubmitResult local,
            in ShooterGatewayBattleInputResult remote)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    var pending = _pendingInputs[i];
                    if (local.SubmissionId > 0 && pending.SubmissionId != local.SubmissionId)
                    {
                        continue;
                    }

                    if (local.SubmissionId <= 0
                        && (pending.Command.PlayerId != local.Packet.Command.PlayerId
                            || pending.RequestedFrame != local.RequestedFrame))
                    {
                        continue;
                    }

                    pending.GatewayCompleted = true;
                    pending.CommandSequence = remote.CommandSequence;
                    pending.AcceptedFrame = remote.AcceptedFrame;
                    if (!remote.Success || remote.ShouldResync)
                    {
                        _pendingInputs.RemoveAt(i);
                    }

                    return;
                }
            }
        }

        private void ReconcileControlledPlayer(
            in ShooterGatewaySnapshot snapshot,
            bool forceAuthorityReset)
        {
            var playerId = _presentation.ControlledPlayerId;
            if (playerId <= 0 || snapshot.Frame < 0 || !_runtime.TryGetPlayer(playerId, out var current))
            {
                return;
            }

            var worldChanged = forceAuthorityReset
                || (snapshot.WorldId != 0
                    && _authorityWorldId != 0
                    && snapshot.WorldId != _authorityWorldId);
            if (worldChanged && !forceAuthorityReset)
            {
                ResetAuthoritativeState();
            }

            if (!worldChanged && snapshot.Frame <= _lastAuthorityFrame)
            {
                return;
            }

            if (!TryExtractAuthoritativePlayer(in snapshot, playerId, in current, out var target))
            {
                return;
            }

            _authorityWorldId = snapshot.WorldId != 0 ? snapshot.WorldId : _authorityWorldId;
            _lastAuthorityFrame = snapshot.Frame;
            var acknowledgedSequence = ResolveAcknowledgedSequence(in snapshot, playerId);
            var replayCount = ReplayPendingInputs(
                ref target,
                playerId,
                snapshot.Frame,
                acknowledgedSequence);

            var deltaX = target.X - current.X;
            var deltaY = target.Y - current.Y;
            var error = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var forceSnap = worldChanged
                || snapshot.IsFullSnapshot
                || error >= SnapErrorThreshold
                || (snapshot.PackedSnapshot?.SnapshotFlags & ShooterPackedSnapshotFlags.AuthorityOverride) != 0;
            if (!forceSnap && error <= SmallErrorTolerance)
            {
                target.X = current.X;
                target.Y = current.Y;
            }
            else if (!forceSnap)
            {
                var scale = Math.Min(1f, MaxCorrectionPerSnapshot / error);
                target.X = current.X + deltaX * scale;
                target.Y = current.Y + deltaY * scale;
            }

            if (PlayersEqual(in current, in target))
            {
                return;
            }

            _runtime.SetPlayer(in target);
            RefreshPredictedPose(playerId, CurrentFrame, snapshot.ServerTicks);
            _localReconciliationReport = new SyncReconciliationReport(
                SyncReconciliationReason.LocalAuthorityCorrection,
                SyncRecoveryState.Normal,
                needsFullSnapshot: false,
                clientFrame: CurrentFrame,
                authoritativeFrame: snapshot.Frame,
                clientStateHash: 0u,
                authoritativeStateHash: ResolveAuthoritativeStateHash(in snapshot),
                replayTicks: replayCount);
        }

        private int ReplayPendingInputs(
            ref ShooterSveltoPlayerComponent player,
            int playerId,
            int authorityFrame,
            ulong acknowledgedSequence)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    var pending = _pendingInputs[i];
                    var explicitlyAcknowledged = acknowledgedSequence > 0
                        && pending.CommandSequence > 0
                        && pending.CommandSequence <= acknowledgedSequence;
                    var legacyFrameAcknowledged = acknowledgedSequence == 0
                        && pending.GatewayCompleted
                        && pending.AcceptedFrame <= authorityFrame;
                    if (explicitlyAcknowledged || legacyFrameAcknowledged)
                    {
                        _pendingInputs.RemoveAt(i);
                    }
                }

                var replayed = 0;
                for (var i = 0; i < _pendingInputs.Count && replayed < MaxReplayFrames; i++)
                {
                    var pending = _pendingInputs[i];
                    if (pending.Command.PlayerId != playerId)
                    {
                        continue;
                    }

                    var command = pending.Command;
                    ApplyPredictedPoseInput(ref player, in command);
                    replayed++;
                }

                return replayed;
            }
        }

        private void ApplyPredictedPoseInput(
            ref ShooterSveltoPlayerComponent player,
            in ShooterPlayerCommand source)
        {
            var moveX = source.MoveX;
            var moveY = source.MoveY;
            if (ShooterBattleMath.Normalize(ref moveX, ref moveY) > 0f)
            {
                player.X += moveX * ShooterBattleTuning.PlayerSpeed * _fixedDeltaTime;
                player.Y += moveY * ShooterBattleTuning.PlayerSpeed * _fixedDeltaTime;
            }

            var aimX = source.AimX;
            var aimY = source.AimY;
            if (ShooterBattleMath.Normalize(ref aimX, ref aimY) > 0f)
            {
                player.AimX = aimX;
                player.AimY = aimY;
            }
        }

        private static bool TryExtractAuthoritativePlayer(
            in ShooterGatewaySnapshot snapshot,
            int playerId,
            in ShooterSveltoPlayerComponent current,
            out ShooterSveltoPlayerComponent player)
        {
            player = current;
            if (snapshot.PackedSnapshot.HasValue)
            {
                var found = false;
                var chunks = snapshot.PackedSnapshot.Value.ComponentChunks;
                for (var chunkIndex = 0; chunkIndex < SafeLength(chunks); chunkIndex++)
                {
                    var chunk = chunks[chunkIndex];
                    if (chunk.EntityKind != ShooterPackedEntityKinds.Player)
                    {
                        continue;
                    }

                    var count = Math.Min(chunk.Count, SafeLength(chunk.EntityIds));
                    for (var i = 0; i < count; i++)
                    {
                        if (chunk.EntityIds[i] != playerId)
                        {
                            continue;
                        }

                        if (chunk.ComponentKind == ShooterPackedComponentKinds.Transform
                            && i < SafeLength(chunk.ValueX)
                            && i < SafeLength(chunk.ValueY)
                            && i < SafeLength(chunk.ValueZ)
                            && i < SafeLength(chunk.ValueW))
                        {
                            player.X = chunk.ValueX[i];
                            player.Y = chunk.ValueY[i];
                            player.AimX = chunk.ValueZ[i];
                            player.AimY = chunk.ValueW[i];
                            found = true;
                        }
                        else if (chunk.ComponentKind == ShooterPackedComponentKinds.Health
                            && i < SafeLength(chunk.IntValues))
                        {
                            player.Hp = chunk.IntValues[i];
                            found = true;
                        }
                        else if (chunk.ComponentKind == ShooterPackedComponentKinds.Score
                            && i < SafeLength(chunk.IntValues))
                        {
                            player.Score = chunk.IntValues[i];
                            found = true;
                        }
                        else if (chunk.ComponentKind == ShooterPackedComponentKinds.EntityLifecycle
                            && i < SafeLength(chunk.Flags))
                        {
                            player.Alive = (chunk.Flags[i] & ShooterPackedEntityFlags.Alive) != 0;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            if (snapshot.PureStateSnapshot.HasValue)
            {
                var entities = snapshot.PureStateSnapshot.Value.Entities;
                for (var i = 0; i < SafeLength(entities); i++)
                {
                    var entity = entities[i];
                    if (entity.EntityId != playerId
                        || entity.EntityKind != ShooterPackedEntityKinds.Player
                        || entity.DeltaKind == ShooterPureStateDeltaKinds.Despawn)
                    {
                        continue;
                    }

                    player.X = entity.QuantizedX / PositionQuantizationScale;
                    player.Y = entity.QuantizedY / PositionQuantizationScale;
                    player.AimX = entity.QuantizedVelocityX / PositionQuantizationScale;
                    player.AimY = entity.QuantizedVelocityY / PositionQuantizationScale;
                    player.Hp = entity.Hp;
                    player.Score = entity.Score;
                    player.Alive = (entity.Flags & ShooterPureStateEntityFlags.Alive) != 0;
                    return true;
                }
            }

            var actors = snapshot.Actors;
            for (var i = 0; i < actors.Count; i++)
            {
                if (actors[i].ActorId != playerId)
                {
                    continue;
                }

                player.X = actors[i].X;
                player.Y = actors[i].Y;
                player.Hp = (int)actors[i].Hp;
                return true;
            }

            return false;
        }

        private static bool PlayersEqual(
            in ShooterSveltoPlayerComponent left,
            in ShooterSveltoPlayerComponent right)
        {
            return left.X == right.X
                && left.Y == right.Y
                && left.AimX == right.AimX
                && left.AimY == right.AimY
                && left.Hp == right.Hp
                && left.Score == right.Score
                && left.Alive == right.Alive;
        }

        private static ulong ResolveAcknowledgedSequence(in ShooterGatewaySnapshot snapshot, int playerId)
        {
            var acknowledgements = snapshot.PackedSnapshot?.AcknowledgedCommands
                ?? snapshot.PureStateSnapshot?.AcknowledgedCommands
                ?? Array.Empty<ShooterCommandAcknowledgement>();
            for (var i = 0; i < acknowledgements.Length; i++)
            {
                if (acknowledgements[i].PlayerId == playerId)
                {
                    return acknowledgements[i].CommandSequence;
                }
            }

            return 0;
        }

        private static uint ResolveAuthoritativeStateHash(in ShooterGatewaySnapshot snapshot)
        {
            return snapshot.PackedSnapshot?.StateHash
                ?? snapshot.PureStateSnapshot?.StateHash
                ?? 0u;
        }

        private sealed class PendingLocalInput
        {
            public PendingLocalInput(long submissionId, int requestedFrame, in ShooterPlayerCommand command)
            {
                SubmissionId = submissionId;
                RequestedFrame = requestedFrame;
                AcceptedFrame = requestedFrame;
                Command = command;
            }

            public long SubmissionId { get; }
            public int RequestedFrame { get; set; }
            public int AcceptedFrame { get; set; }
            public ShooterPlayerCommand Command { get; }
            public bool GatewayStarted { get; set; }
            public bool GatewayCompleted { get; set; }
            public ulong CommandSequence { get; set; }
        }

        private void RefreshPredictedPose(int playerId, int frame, long serverTicks)
        {
            if (playerId <= 0 || !_runtime.TryGetPlayer(playerId, out var player))
            {
                return;
            }

            _predictionState = _predictionState.WithPredictedPose(
                player.PlayerId,
                player.X,
                player.Y,
                player.AimX,
                player.AimY,
                frame,
                serverTicks);
        }

        private void ObserveAuthoritativeProjectileActions(in ShooterGatewaySnapshot snapshot)
        {
            if (!snapshot.PackedSnapshot.HasValue)
            {
                return;
            }

            var chunks = snapshot.PackedSnapshot.Value.ComponentChunks;
            if (chunks == null)
            {
                return;
            }

            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                if (chunk.ComponentKind != ShooterPackedComponentKinds.EntityLifecycle || chunk.EntityKind != ShooterPackedEntityKinds.Projectile)
                {
                    continue;
                }

                var count = Math.Min(chunk.Count, Math.Min(SafeLength(chunk.EntityIds), SafeLength(chunk.OwnerIds)));
                for (int i = 0; i < count; i++)
                {
                    var ownerPlayerId = chunk.OwnerIds[i];
                    if (ownerPlayerId <= 0)
                    {
                        continue;
                    }

                    SetPredictedAction(
                        ownerPlayerId,
                        ShooterStateSyncPredictedAction.Fire,
                        snapshot.Frame,
                        snapshot.ServerTicks,
                        CurrentFrame,
                        needsCatchUp: true,
                        ownerPlayerId,
                        0,
                        chunk.EntityIds[i]);
                }
            }
        }

        private void SetPredictedAction(
            int playerId,
            ShooterStateSyncPredictedAction action,
            int sourceFrame,
            long sourceServerTicks,
            int playbackFrame,
            bool needsCatchUp,
            int sourcePlayerId,
            int targetPlayerId,
            int bulletId)
        {
            if (playerId <= 0 || action == ShooterStateSyncPredictedAction.None)
            {
                return;
            }

            _predictionState = _predictionState.WithAction(
                playerId,
                action,
                sourceFrame,
                sourceServerTicks,
                playbackFrame,
                needsCatchUp,
                Math.Max(0, playbackFrame - sourceFrame),
                sourcePlayerId,
                targetPlayerId,
                bulletId);
        }

        private static int SafeLength<T>(T[]? values)
        {
            return values?.Length ?? 0;
        }

        private void PublishInterpolatedRemoteFrame()
        {
            // 框架播放层负责缓冲、时间线以及外推/饥饿策略；
            // Shooter 控制器只提供“投影 + 应用到表现层”这一半循环。
            if (!_playback.TrySample(out var interpolation))
            {
                return;
            }

            var projected = _projector.Project(in interpolation);
            _presentation.ApplyInterpolatedGatewaySnapshot(in projected);
        }

        /// <summary>
        /// 采集当前插值播放健康状态，用于诊断与 smoke 输出。
        /// </summary>
        public InterpolationDiagnostics GetInterpolationDiagnostics()
        {
            return _playback.GetDiagnostics();
        }

        // --- IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> ---
        // 显式框架契约接口，映射到现有示例行为。

        SyncTickResult IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.Tick(float deltaSeconds)
        {
            return ShooterClientSyncStrategyMapping.ToSyncTickResult(Tick(deltaSeconds));
        }

        void IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.SubmitInput(in ShooterPlayerCommand input)
        {
            SubmitLocalInput(in input);
        }

        void IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.ObserveRemote(in ShooterRemoteSnapshotSample sample)
        {
            // 对权威插值来说，观察远端样本会写入延迟播放缓冲
            // （与 BufferRemoteSnapshot/ApplyGatewayPush 使用同一路径），绝不会进入本地模拟。
            _playback.Observe(sample);
        }

        SyncReconciliationReport IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.GetReconciliationReport()
        {
            return _localReconciliationReport.DidReconcile
                ? _localReconciliationReport
                : ShooterClientSyncStrategyMapping.ToReconciliationReport(this);
        }
    }

    public enum ShooterStateSyncPredictedAction
    {
        None = 0,
        Fire = 1,
        Hit = 2
    }

    public readonly struct ShooterStateSyncPredictionState
    {
        public static readonly ShooterStateSyncPredictionState Empty = new ShooterStateSyncPredictionState(
            0,
            false,
            0f,
            0f,
            0f,
            0f,
            0,
            0L,
            0,
            ShooterStateSyncPredictedAction.None,
            0,
            0L,
            0,
            false,
            0,
            0,
            0,
            0);

        public readonly int PlayerId;
        public readonly bool HasPredictedPose;
        public readonly float PredictedX;
        public readonly float PredictedY;
        public readonly float PredictedAimX;
        public readonly float PredictedAimY;
        public readonly int PredictedFrame;
        public readonly long PredictedServerTicks;
        public readonly int ActionPlayerId;
        public readonly ShooterStateSyncPredictedAction Action;
        public readonly int ActionSourceFrame;
        public readonly long ActionSourceServerTicks;
        public readonly int ActionPlaybackFrame;
        public readonly bool NeedsActionCatchUp;
        public readonly int ActionCatchUpFrames;
        public readonly int ActionSourcePlayerId;
        public readonly int ActionTargetPlayerId;
        public readonly int ActionBulletId;

        public ShooterStateSyncPredictionState(
            int playerId,
            bool hasPredictedPose,
            float predictedX,
            float predictedY,
            float predictedAimX,
            float predictedAimY,
            int predictedFrame,
            long predictedServerTicks,
            int actionPlayerId,
            ShooterStateSyncPredictedAction action,
            int actionSourceFrame,
            long actionSourceServerTicks,
            int actionPlaybackFrame,
            bool needsActionCatchUp,
            int actionCatchUpFrames,
            int actionSourcePlayerId,
            int actionTargetPlayerId,
            int actionBulletId)
        {
            PlayerId = playerId;
            HasPredictedPose = hasPredictedPose;
            PredictedX = predictedX;
            PredictedY = predictedY;
            PredictedAimX = predictedAimX;
            PredictedAimY = predictedAimY;
            PredictedFrame = predictedFrame;
            PredictedServerTicks = predictedServerTicks;
            ActionPlayerId = actionPlayerId;
            Action = action;
            ActionSourceFrame = actionSourceFrame;
            ActionSourceServerTicks = actionSourceServerTicks;
            ActionPlaybackFrame = actionPlaybackFrame;
            NeedsActionCatchUp = needsActionCatchUp;
            ActionCatchUpFrames = actionCatchUpFrames;
            ActionSourcePlayerId = actionSourcePlayerId;
            ActionTargetPlayerId = actionTargetPlayerId;
            ActionBulletId = actionBulletId;
        }

        public ShooterStateSyncPredictionState WithPredictedPose(
            int playerId,
            float x,
            float y,
            float aimX,
            float aimY,
            int frame,
            long serverTicks)
        {
            return new ShooterStateSyncPredictionState(
                playerId,
                true,
                x,
                y,
                aimX,
                aimY,
                frame,
                serverTicks,
                ActionPlayerId,
                Action,
                ActionSourceFrame,
                ActionSourceServerTicks,
                ActionPlaybackFrame,
                NeedsActionCatchUp,
                ActionCatchUpFrames,
                ActionSourcePlayerId,
                ActionTargetPlayerId,
                ActionBulletId);
        }

        public ShooterStateSyncPredictionState WithAction(
            int playerId,
            ShooterStateSyncPredictedAction action,
            int sourceFrame,
            long sourceServerTicks,
            int playbackFrame,
            bool needsCatchUp,
            int catchUpFrames,
            int sourcePlayerId,
            int targetPlayerId,
            int bulletId)
        {
            return new ShooterStateSyncPredictionState(
                PlayerId,
                HasPredictedPose,
                PredictedX,
                PredictedY,
                PredictedAimX,
                PredictedAimY,
                PredictedFrame,
                PredictedServerTicks,
                playerId,
                action,
                sourceFrame,
                sourceServerTicks,
                playbackFrame,
                needsCatchUp,
                catchUpFrames,
                sourcePlayerId,
                targetPlayerId,
                bulletId);
        }
    }
}
