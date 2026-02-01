using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public sealed class ClientPredictionDriverModule : IHostRuntimeModule, IClientPredictionDriverStats, IClientPredictionReconcileTarget, IClientPredictionReconcileControl
    {
        private enum ReplayMode
        {
            Normal = 0,
            Replaying = 1,
        }

        public void ResetReconcile(WorldId worldId)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                ResetReconcileInternal(ctx);
            }
            else
            {
                // Be defensive: if worldId mismatch occurs, reset all worlds to avoid being stuck in replay.
                foreach (var kv in _contexts)
                {
                    if (kv.Value != null) ResetReconcileInternal(kv.Value);
                }
            }
        }

        private void ResetReconcileInternal(WorldContext ctx)
        {
            ctx.PredictedHashes?.Clear();
            ctx.AuthoritativeHashes?.Clear();
            ctx.Reconciler?.Clear();

            // Also exit replay mode to avoid getting stuck after debug mismatch toggles.
            ctx.Mode = ReplayMode.Normal;
            ctx.ReplayTo = ctx.PredictedFrame;
            ctx.LastRollbackFrame = new FrameIndex(0);

            _isReplaying = false;
            _replayToFrame = default;
            _lastRollbackFrame = default;

            _lastReconcileComparedFrame = default;
            _lastReconcileMismatchFrame = default;
            _lastReconcilePredictedHash = default;
            _lastReconcileAuthoritativeHash = default;
        }

        private sealed class WorldContext
        {
            public IWorld World;
            public IWorldInputSink InputSink;

            public FrameIndex ConfirmedFrame;
            public FrameIndex PredictedFrame;

            public Queue<LocalPlayerInputEvent[]> LocalDelayQueue;

            public RollbackCoordinator Rollback;
            public int CaptureCounter;

            public InputHistoryRingBuffer AppliedInputs;
            public InputHistoryRingBuffer AuthoritativeInputs;

            public Func<FrameIndex, WorldStateHash> ComputeHash;
            public WorldStateHashRingBuffer PredictedHashes;
            public WorldStateHashRingBuffer AuthoritativeHashes;
            public ClientPredictionReconciler Reconciler;

            public bool ReconcileEnabled;

            public ReplayMode Mode;
            public FrameIndex ReplayTo;
            public FrameIndex LastRollbackFrame;
        }

        private readonly Dictionary<WorldId, WorldContext> _contexts = new Dictionary<WorldId, WorldContext>();

        private readonly Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> _resolveRemoteInputs;
        private readonly Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> _resolveLocalInputs;

        private readonly int _inputDelayFrames;

        private readonly int _maxLocalDelayQueueDepth;

        private readonly bool _enableRollback;
        private readonly int _rollbackHistoryFrames;
        private readonly int _rollbackCaptureEveryNFrames;
        private readonly Func<IWorld, RollbackRegistry> _buildRollbackRegistry;

        private readonly Func<IWorld, Func<FrameIndex, WorldStateHash>> _buildComputeHash;

        private int _lastConsumedConfirmedFrames;
        private int _lastConsumedPredictedFrames;
        private long _totalConsumedConfirmedFrames;
        private long _totalPredictedFrames;

        private long _totalLocalDelayQueueDroppedBatches;

        private long _totalRollbackCount;
        private long _totalRollbackRestoreFailed;

        private long _totalReconcileMismatch;
        private long _totalPredictedHashRecorded;
        private long _totalAuthoritativeHashSkippedNoPredictedHash;
        private FrameIndex _lastReconcileMismatchFrame;
        private FrameIndex _lastReconcileComparedFrame;
        private WorldStateHash _lastReconcilePredictedHash;
        private WorldStateHash _lastReconcileAuthoritativeHash;

        private long _totalAuthoritativeHashReceived;
        private FrameIndex _lastAuthoritativeHashFrame;
        private WorldStateHash _lastAuthoritativeHash;

        private long _totalAuthoritativeHashIgnoredNoReconciler;

        private bool _isReplaying;
        private FrameIndex _replayToFrame;
        private FrameIndex _lastRollbackFrame;

        private readonly Action<IWorld> _onWorldCreated;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPreTick;
        private readonly Action<float> _onPostTick;

        private HostRuntime _runtime;
        private HostRuntimeOptions _options;

        public ClientPredictionDriverModule(
            Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> resolveRemoteInputs,
            Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> resolveLocalInputs,
            int inputDelayFrames = 0,
            bool enableRollback = false,
            int rollbackHistoryFrames = 240,
            int rollbackCaptureEveryNFrames = 1,
            Func<IWorld, RollbackRegistry> buildRollbackRegistry = null,
            Func<IWorld, Func<FrameIndex, WorldStateHash>> buildComputeHash = null)
        {
            _resolveRemoteInputs = resolveRemoteInputs;
            _resolveLocalInputs = resolveLocalInputs;

            if (inputDelayFrames < 0) inputDelayFrames = 0;
            _inputDelayFrames = inputDelayFrames;

            _maxLocalDelayQueueDepth = 2048;

            _enableRollback = enableRollback;
            _rollbackHistoryFrames = rollbackHistoryFrames <= 0 ? 240 : rollbackHistoryFrames;
            _rollbackCaptureEveryNFrames = rollbackCaptureEveryNFrames <= 0 ? 1 : rollbackCaptureEveryNFrames;
            _buildRollbackRegistry = buildRollbackRegistry;

            _buildComputeHash = buildComputeHash;

            _onWorldCreated = OnWorldCreated;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPreTick = OnPreTick;
            _onPostTick = OnPostTick;
        }

        public int InputDelayFrames => _inputDelayFrames;

        public bool IsReplaying => _isReplaying;

        public FrameIndex ReplayToFrame => _replayToFrame;

        public FrameIndex LastRollbackFrame => _lastRollbackFrame;

        public long TotalRollbackCount => _totalRollbackCount;

        public long TotalRollbackRestoreFailed => _totalRollbackRestoreFailed;

        public long TotalReconcileMismatch => _totalReconcileMismatch;

        public long TotalPredictedHashRecorded => _totalPredictedHashRecorded;

        public long TotalAuthoritativeHashSkippedNoPredictedHash => _totalAuthoritativeHashSkippedNoPredictedHash;

        public FrameIndex LastReconcileComparedFrame => _lastReconcileComparedFrame;

        public FrameIndex LastReconcileMismatchFrame => _lastReconcileMismatchFrame;

        public WorldStateHash LastReconcilePredictedHash => _lastReconcilePredictedHash;

        public WorldStateHash LastReconcileAuthoritativeHash => _lastReconcileAuthoritativeHash;

        public long TotalAuthoritativeHashReceived => _totalAuthoritativeHashReceived;

        public FrameIndex LastAuthoritativeHashFrame => _lastAuthoritativeHashFrame;

        public WorldStateHash LastAuthoritativeHash => _lastAuthoritativeHash;

        public long TotalAuthoritativeHashIgnoredNoReconciler => _totalAuthoritativeHashIgnoredNoReconciler;

        public int LastConsumedConfirmedFrames => _lastConsumedConfirmedFrames;

        public int LastConsumedPredictedFrames => _lastConsumedPredictedFrames;

        public long TotalConsumedConfirmedFrames => _totalConsumedConfirmedFrames;

        public long TotalPredictedFrames => _totalPredictedFrames;

        public long TotalLocalDelayQueueDroppedBatches => _totalLocalDelayQueueDroppedBatches;

        public bool TryGetLocalDelayQueueDepth(WorldId worldId, out int depth)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null && ctx.LocalDelayQueue != null)
            {
                depth = ctx.LocalDelayQueue.Count;
                return true;
            }

            depth = 0;
            return false;
        }

        public bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                confirmed = ctx.ConfirmedFrame;
                predicted = ctx.PredictedFrame;
                return true;
            }

            confirmed = default;
            predicted = default;
            return false;
        }

        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;
            _options = options;

            options.WorldCreated.Add(_onWorldCreated);
            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PreTick.Add(_onPreTick);
            options.PostTick.Add(_onPostTick);

            runtime.Features.RegisterFeature<IClientPredictionDriverStats>(this);
            runtime.Features.RegisterFeature<IClientPredictionReconcileTarget>(this);
            runtime.Features.RegisterFeature<IClientPredictionReconcileControl>(this);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldCreated.Remove(_onWorldCreated);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PreTick.Remove(_onPreTick);
            options.PostTick.Remove(_onPostTick);

            runtime.Features.UnregisterFeature<IClientPredictionDriverStats>();
            runtime.Features.UnregisterFeature<IClientPredictionReconcileTarget>();
            runtime.Features.UnregisterFeature<IClientPredictionReconcileControl>();

            _contexts.Clear();
            _lastConsumedConfirmedFrames = 0;
            _totalConsumedConfirmedFrames = 0;
            _totalPredictedFrames = 0;
            _runtime = null;
            _options = null;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null) return;

            IWorldInputSink sink = null;
            if (world.Services != null)
            {
                world.Services.TryResolve<IWorldInputSink>(out sink);
            }

            RollbackCoordinator rollback = null;
            if (_enableRollback)
            {
                var reg = _buildRollbackRegistry != null ? _buildRollbackRegistry(world) : new RollbackRegistry();
                rollback = new RollbackCoordinator(reg, new RollbackSnapshotRingBuffer(_rollbackHistoryFrames));
                rollback.CaptureAndStore(new FrameIndex(0));
            }

            Func<FrameIndex, WorldStateHash> computeHash = null;
            ClientPredictionReconciler reconciler = null;
            WorldStateHashRingBuffer predictedHashes = null;
            WorldStateHashRingBuffer authoritativeHashes = null;
            if (_buildComputeHash != null)
            {
                computeHash = _buildComputeHash(world);
                if (computeHash != null)
                {
                    predictedHashes = new WorldStateHashRingBuffer(_rollbackHistoryFrames);
                    authoritativeHashes = new WorldStateHashRingBuffer(_rollbackHistoryFrames);
                    reconciler = new ClientPredictionReconciler(predictedHashes);
                    reconciler.OnRollbackRequested += frame => RequestReconcileRollback(world.Id, frame);
                }
            }

            _contexts[world.Id] = new WorldContext
            {
                World = world,
                InputSink = sink,
                ConfirmedFrame = new FrameIndex(0),
                PredictedFrame = new FrameIndex(0),
                LocalDelayQueue = new Queue<LocalPlayerInputEvent[]>(_inputDelayFrames + 2),
                Rollback = rollback,
                CaptureCounter = 0,
                AppliedInputs = new InputHistoryRingBuffer(_rollbackHistoryFrames),
                AuthoritativeInputs = new InputHistoryRingBuffer(_rollbackHistoryFrames),

                ComputeHash = computeHash,
                PredictedHashes = predictedHashes,
                AuthoritativeHashes = authoritativeHashes,
                Reconciler = reconciler,
                ReconcileEnabled = reconciler != null && computeHash != null,
                Mode = ReplayMode.Normal,
                ReplayTo = new FrameIndex(0),
                LastRollbackFrame = new FrameIndex(0),
            };
        }

        public void SetReconcileEnabled(WorldId worldId, bool enabled)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                ctx.ReconcileEnabled = enabled;
            }
            else
            {
                foreach (var kv in _contexts)
                {
                    if (kv.Value != null) kv.Value.ReconcileEnabled = enabled;
                }
            }
        }

        public bool TryGetReconcileEnabled(WorldId worldId, out bool enabled)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                enabled = ctx.ReconcileEnabled;
                return true;
            }

            enabled = false;
            return false;
        }

        public void OnAuthoritativeStateHash(WorldId worldId, FrameIndex frame, WorldStateHash hash)
        {
            _totalAuthoritativeHashReceived++;
            _lastAuthoritativeHashFrame = frame;
            _lastAuthoritativeHash = hash;

            if (!_contexts.TryGetValue(worldId, out var ctx) || ctx == null) return;
            if (ctx.Reconciler == null)
            {
                _totalAuthoritativeHashIgnoredNoReconciler++;
                return;
            }

            if (!ctx.ReconcileEnabled) return;

            if (ctx.AuthoritativeHashes != null)
            {
                ctx.AuthoritativeHashes.Store(frame, hash);
            }

            _lastReconcileComparedFrame = frame;

            if (ctx.PredictedHashes == null || !ctx.PredictedHashes.TryGet(frame, out var predictedAtFrame))
            {
                _totalAuthoritativeHashSkippedNoPredictedHash++;
                // We'll retry comparison when predicted hash for this frame is recorded.
                return;
            }
            else
            {
                _lastReconcilePredictedHash = predictedAtFrame;
            }

            if (!ctx.Reconciler.OnAuthoritativeHash(frame, hash)) return;

            _totalReconcileMismatch++;
            _lastReconcileMismatchFrame = frame;
            _lastReconcileAuthoritativeHash = hash;
        }

        private void RequestReconcileRollback(WorldId worldId, FrameIndex mismatchFrame)
        {
            if (!_contexts.TryGetValue(worldId, out var ctx) || ctx == null) return;
            if (!_enableRollback || ctx.Rollback == null) return;

            // Avoid rollback storms (e.g. debug forced mismatch) and avoid re-entering while replaying.
            if (ctx.Mode == ReplayMode.Replaying) return;
            if (ctx.LastRollbackFrame.Value >= mismatchFrame.Value) return;

            var rollbackFrame = new FrameIndex(mismatchFrame.Value - 1);
            var ok = ctx.Rollback.TryRestore(rollbackFrame);
            if (!ok)
            {
                _totalRollbackRestoreFailed++;
                return;
            }

            _totalRollbackCount++;
            ctx.Mode = ReplayMode.Replaying;
            ctx.ReplayTo = ctx.PredictedFrame;
            ctx.ConfirmedFrame = rollbackFrame;
            ctx.PredictedFrame = rollbackFrame;
            ctx.LastRollbackFrame = rollbackFrame;

            _isReplaying = true;
            _replayToFrame = ctx.ReplayTo;
            _lastRollbackFrame = rollbackFrame;
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _contexts.Remove(worldId);
        }

        private void OnPreTick(float deltaTime)
        {
            if (_runtime == null) return;

            _lastConsumedConfirmedFrames = 0;
            _lastConsumedPredictedFrames = 0;

            _isReplaying = false;
            _replayToFrame = default;
            _lastRollbackFrame = default;

            _lastReconcileMismatchFrame = default;
            _lastReconcilePredictedHash = default;
            _lastReconcileAuthoritativeHash = default;

            foreach (var kv in _contexts)
            {
                var worldId = kv.Key;
                var ctx = kv.Value;
                if (ctx?.World == null || ctx.InputSink == null) continue;

                _lastConsumedConfirmedFrames = 0;
                _lastConsumedPredictedFrames = 0;

                var remote = _resolveRemoteInputs != null ? _resolveRemoteInputs(worldId) : null;
                var local = _resolveLocalInputs != null ? _resolveLocalInputs(worldId) : null;

                // Exactly one Submit per HostRuntime.Tick.

                // Step 0: always enqueue one local batch (may be empty) so delay queue advances.
                if (local != null)
                {
                    var evts = Array.Empty<LocalPlayerInputEvent>();
                    if (local.TryDequeue(out var dequeued) && dequeued != null)
                    {
                        evts = dequeued;
                    }

                    ctx.LocalDelayQueue ??= new Queue<LocalPlayerInputEvent[]>(_inputDelayFrames + 2);
                    ctx.LocalDelayQueue.Enqueue(evts);

                    while (ctx.LocalDelayQueue.Count > _maxLocalDelayQueueDepth)
                    {
                        ctx.LocalDelayQueue.Dequeue();
                        _totalLocalDelayQueueDroppedBatches++;
                    }
                }

                // Step 1 (preferred): apply authoritative input for next confirmed frame if available.
                if (remote != null && ctx.Mode == ReplayMode.Normal)
                {
                    var nextConfirmed = ctx.ConfirmedFrame.Value + 1;
                    if (nextConfirmed <= remote.TargetFrame)
                    {
                        var frame = new FrameIndex(nextConfirmed);

                        // Peek first: decide whether to rollback.
                        PlayerInputCommand[] authInputs;
                        if (!remote.TryGet(nextConfirmed, out authInputs) || authInputs == null)
                        {
                            authInputs = Array.Empty<PlayerInputCommand>();
                        }

                        if (_enableRollback && ctx.Rollback != null && ctx.PredictedFrame.Value >= frame.Value)
                        {
                            if (ctx.AppliedInputs.TryGet(frame, out var appliedAtFrame) && appliedAtFrame != null)
                            {
                                if (!InputsEqual(appliedAtFrame, authInputs))
                                {
                                    var rollbackFrame = new FrameIndex(frame.Value - 1);
                                    var ok = ctx.Rollback.TryRestore(rollbackFrame);
                                    if (ok)
                                    {
                                        _totalRollbackCount++;
                                        ctx.Mode = ReplayMode.Replaying;
                                        ctx.ReplayTo = ctx.PredictedFrame;
                                        ctx.ConfirmedFrame = rollbackFrame;
                                        ctx.PredictedFrame = rollbackFrame;
                                        ctx.LastRollbackFrame = rollbackFrame;

                                        _isReplaying = true;
                                        _replayToFrame = ctx.ReplayTo;
                                        _lastRollbackFrame = rollbackFrame;
                                    }
                                    else
                                    {
                                        _totalRollbackRestoreFailed++;
                                    }
                                }
                            }
                        }

                        if (ctx.Mode == ReplayMode.Replaying)
                        {
                            // fall through to replay logic below
                        }
                        else
                        {
                            // Consume and apply authoritative.
                            if (!remote.TryConsume(nextConfirmed, out var consumed) || consumed == null)
                            {
                                consumed = Array.Empty<PlayerInputCommand>();
                            }

                            ctx.InputSink.Submit(frame, consumed);
                            ctx.AuthoritativeInputs.Store(frame, consumed);
                            ctx.AppliedInputs.Store(frame, consumed);

                            ctx.ConfirmedFrame = frame;
                            _lastConsumedConfirmedFrames = 1;
                            _totalConsumedConfirmedFrames++;

                            if (ctx.PredictedFrame.Value < frame.Value)
                            {
                                ctx.PredictedFrame = frame;
                            }

                            continue;
                        }
                    }
                }

                // Replay: deterministic re-sim until ReplayTo.
                if (ctx.Mode == ReplayMode.Replaying)
                {
                    var next = new FrameIndex(ctx.PredictedFrame.Value + 1);

                    if (next.Value > ctx.ReplayTo.Value)
                    {
                        ctx.Mode = ReplayMode.Normal;
                        continue;
                    }

                    // Plan A (convergence-first): only replay when authoritative input for this frame is available.
                    // Otherwise, exit replay and return to Normal prediction to avoid tug-of-war.
                    PlayerInputCommand[] inputs = null;
                    if (ctx.AuthoritativeInputs.TryGet(next, out var auth) && auth != null)
                    {
                        inputs = auth;
                    }
                    else if (remote != null && next.Value <= remote.TargetFrame)
                    {
                        if (!remote.TryConsume(next.Value, out var consumed) || consumed == null)
                        {
                            consumed = Array.Empty<PlayerInputCommand>();
                        }
                        inputs = consumed;
                        ctx.AuthoritativeInputs.Store(next, consumed);
                        if (ctx.ConfirmedFrame.Value < next.Value)
                        {
                            ctx.ConfirmedFrame = next;
                        }
                    }
                    else
                    {
                        // No authoritative input yet for this frame -> pause replay.
                        ctx.Mode = ReplayMode.Normal;
                        continue;
                    }

                    ctx.InputSink.Submit(next, inputs);
                    ctx.AppliedInputs.Store(next, inputs);
                    ctx.PredictedFrame = next;
                    _lastConsumedPredictedFrames = 1;
                    _totalPredictedFrames++;
                    continue;
                }

                // Step 2: otherwise, do one predicted step when delay satisfied.
                if (ctx.LocalDelayQueue != null && (ctx.LocalDelayQueue.Count - _inputDelayFrames) > 0)
                {
                    var delayed = ctx.LocalDelayQueue.Dequeue() ?? Array.Empty<LocalPlayerInputEvent>();
                    var next = new FrameIndex(ctx.PredictedFrame.Value + 1);

                    PlayerInputCommand[] predictedInputs;
                    if (delayed.Length == 0)
                    {
                        predictedInputs = Array.Empty<PlayerInputCommand>();
                    }
                    else
                    {
                        predictedInputs = new PlayerInputCommand[delayed.Length];
                        for (int i = 0; i < delayed.Length; i++)
                        {
                            var e = delayed[i];
                            predictedInputs[i] = new PlayerInputCommand(next, e.PlayerId, e.OpCode, e.Payload ?? Array.Empty<byte>());
                        }
                    }

                    ctx.InputSink.Submit(next, predictedInputs);
                    ctx.PredictedFrame = next;
                    ctx.AppliedInputs.Store(next, predictedInputs);
                    _lastConsumedPredictedFrames = 1;
                    _totalPredictedFrames++;
                }
            }
        }

        private static bool InputsEqual(PlayerInputCommand[] a, PlayerInputCommand[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null) a = Array.Empty<PlayerInputCommand>();
            if (b == null) b = Array.Empty<PlayerInputCommand>();
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i];
                var y = b[i];
                if (x.OpCode != y.OpCode) return false;
                if (x.Player.Value != y.Player.Value) return false;

                var xp = x.Payload ?? Array.Empty<byte>();
                var yp = y.Payload ?? Array.Empty<byte>();
                if (xp.Length != yp.Length) return false;
                for (int j = 0; j < xp.Length; j++)
                {
                    if (xp[j] != yp[j]) return false;
                }
            }

            return true;
        }

        private void OnPostTick(float deltaTime)
        {
            if (_runtime == null) return;

            foreach (var kv in _contexts)
            {
                var ctx = kv.Value;
                if (ctx == null) continue;
                if (!_enableRollback || ctx.Rollback == null) continue;

                ctx.CaptureCounter++;
                if (ctx.CaptureCounter % _rollbackCaptureEveryNFrames != 0) continue;

                try
                {
                    ctx.Rollback.CaptureAndStore(ctx.PredictedFrame);

                    if (ctx.ComputeHash != null && ctx.Reconciler != null)
                    {
                        if (!ctx.ReconcileEnabled) continue;
                        var hash = ctx.ComputeHash(ctx.PredictedFrame);
                        ctx.Reconciler.RecordPredictedHash(ctx.PredictedFrame, hash);
                        _totalPredictedHashRecorded++;

                        // If authoritative hash for this frame arrived earlier, compare now.
                        if (ctx.AuthoritativeHashes != null && ctx.AuthoritativeHashes.TryGet(ctx.PredictedFrame, out var authAtFrame))
                        {
                            _lastReconcileComparedFrame = ctx.PredictedFrame;
                            _lastReconcilePredictedHash = hash;
                            if (ctx.Reconciler.OnAuthoritativeHash(ctx.PredictedFrame, authAtFrame))
                            {
                                _totalReconcileMismatch++;
                                _lastReconcileMismatchFrame = ctx.PredictedFrame;
                                _lastReconcileAuthoritativeHash = authAtFrame;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }
        }
    }
}
