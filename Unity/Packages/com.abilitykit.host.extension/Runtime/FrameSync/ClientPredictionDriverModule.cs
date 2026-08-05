using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    // TODO(v1.0): Extract IClientPredictionDriver interface from this module to unify the
    // three parallel prediction stacks currently in the codebase:
    //   1. This module (host.extension, used by MOBA demo)
    //   2. host.extension/Client/FrameSync/* Generic primitives (used by ConfirmedAuthority path)
    //   3. ShooterClientPredictionRuntimeAdapter + ShooterPackedSnapshotRollbackProvider (shooter demo)
    // See: Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/
    public sealed class ClientPredictionDriverModule : IHostRuntimeModule, IHostRuntimeTickGate, IClientPredictionDriverStats, IClientPredictionTuningControl, IClientPredictionReconcileTarget, IClientPredictionReconcileControl, IClientPredictionBaselineControl
    {
        private const int ReplayWaitTimeoutTicks = 120;

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
                // 防御性处理：如果 worldId 不匹配，则重置所有世界，避免卡在回放状态。
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

            // 同时退出回放模式，避免调试强制不一致开关后卡住。
            ctx.Mode = ReplayMode.Normal;
            ctx.ReplayTo = ctx.PredictedFrame;
            ctx.LastRollbackFrame = new FrameIndex(-1);
            ctx.ReplayWaitTicks = 0;
            ctx.LastReplayWaitTargetFrame = -1;

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
            public FrameTime FrameTime;

            public FrameIndex ConfirmedFrame;
            public FrameIndex PredictedFrame;

            public SortedDictionary<int, List<LocalPlayerInputEvent>> PendingLocalInputs;

            public RollbackCoordinator Rollback;
            public int CaptureCounter;
            public FrameIndex LastProcessedRollbackFrame;

            public InputHistoryRingBuffer AppliedInputs;
            public InputHistoryRingBuffer AuthoritativeInputs;

            public Func<FrameIndex, WorldStateHash> ComputeHash;
            public WorldStateHashRingBuffer PredictedHashes;
            public WorldStateHashRingBuffer AuthoritativeHashes;
            public ClientPredictionReconciler Reconciler;

            public bool ReconcileEnabled;

            public int ReplayWaitTicks;
            public int LastReplayWaitTargetFrame;

            public bool HasBacklogEwma;
            public float BacklogEwma;

            public int BacklogRaw;
            public int PredictionWindow;
            public bool PredictionStalled;
            public long PredictionWindowStallsTotal;

            public int IdealFrameLimit;
            public bool IdealFrameStalled;
            public long IdealFrameStallsTotal;
            public bool IdealFrameCappedWindow;

            public ReplayMode Mode;
            public FrameIndex ReplayTo;
            public FrameIndex LastRollbackFrame;
            public FrameIndex LastMissingAppliedHistoryFrame;
        }

        private readonly Dictionary<WorldId, WorldContext> _contexts = new Dictionary<WorldId, WorldContext>();

        private readonly Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> _resolveRemoteInputs;
        private readonly Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> _resolveLocalInputs;
        private readonly Func<WorldId, int> _resolveIdealFrameLimit;
        private readonly int _inputDelayFrames;

        private readonly int _defaultMaxPredictionAheadFrames;
        private readonly int _defaultMinPredictionWindow;
        private readonly float _defaultBacklogEwmaAlpha;

        private int _tuningMaxPredictionAheadFrames;
        private int _tuningMinPredictionWindow;
        private float _tuningBacklogEwmaAlpha;

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

        private int _currentPredictionWindow;
        private bool _isPredictionStalledByWindow;
        private long _totalPredictionWindowStalls;

        private int _currentIdealFrameLimit;
        private bool _isPredictionStalledByIdealFrame;
        private long _totalIdealFrameStalls;

        private int _currentBacklogRaw;
        private float _currentBacklogEwma;

        private long _totalRollbackCount;
        private long _totalRollbackRestoreFailed;

        private long _totalReplayTimeout;
        private FrameIndex _lastReplayTimeoutFrame;

        private long _totalReconcileAutoDisabledByReplayTimeout;
        private FrameIndex _lastReconcileAutoDisabledByReplayTimeoutFrame;

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
        private bool _shouldRunWorldTick;

        private readonly Action<IWorld> _onWorldCreated;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPreTick;
        private readonly Action<float> _onPostTick;

        private HostRuntime _runtime;
        private HostRuntimeOptions _options;

        public ClientPredictionDriverModule(
            Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> resolveRemoteInputs,
            Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> resolveLocalInputs,
            Func<WorldId, int> resolveIdealFrameLimit = null,
            int inputDelayFrames = 0,
            int maxPredictionAheadFrames = 30,
            int minPredictionWindow = 1,
            float backlogEwmaAlpha = 0.20f,
            bool enableRollback = false,
            int rollbackHistoryFrames = 240,
            int rollbackCaptureEveryNFrames = 1,
            Func<IWorld, RollbackRegistry> buildRollbackRegistry = null,
            Func<IWorld, Func<FrameIndex, WorldStateHash>> buildComputeHash = null)
        {
            _resolveRemoteInputs = resolveRemoteInputs;
            _resolveLocalInputs = resolveLocalInputs;
            _resolveIdealFrameLimit = resolveIdealFrameLimit;

            if (inputDelayFrames < 0) inputDelayFrames = 0;
            _inputDelayFrames = inputDelayFrames;

            if (maxPredictionAheadFrames < 0) maxPredictionAheadFrames = 0;
            _defaultMaxPredictionAheadFrames = maxPredictionAheadFrames;
            _tuningMaxPredictionAheadFrames = maxPredictionAheadFrames;

            if (minPredictionWindow < 0) minPredictionWindow = 0;
            if (_tuningMaxPredictionAheadFrames > 0 && minPredictionWindow > _tuningMaxPredictionAheadFrames) minPredictionWindow = _tuningMaxPredictionAheadFrames;
            _defaultMinPredictionWindow = minPredictionWindow;
            _tuningMinPredictionWindow = minPredictionWindow;

            if (backlogEwmaAlpha < 0f) backlogEwmaAlpha = 0f;
            if (backlogEwmaAlpha > 1f) backlogEwmaAlpha = 1f;
            _defaultBacklogEwmaAlpha = backlogEwmaAlpha;
            _tuningBacklogEwmaAlpha = backlogEwmaAlpha;

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

        public int MaxPredictionAheadFrames => _tuningMaxPredictionAheadFrames;

        public int MinPredictionWindow => _tuningMinPredictionWindow;

        public float BacklogEwmaAlpha => _tuningBacklogEwmaAlpha;

        public int CurrentBacklogRaw => _currentBacklogRaw;

        public float CurrentBacklogEwma => _currentBacklogEwma;

        public int CurrentPredictionWindow => _currentPredictionWindow;

        public bool IsPredictionStalledByWindow => _isPredictionStalledByWindow;

        public long TotalPredictionWindowStalls => _totalPredictionWindowStalls;

        public int CurrentIdealFrameLimit => _currentIdealFrameLimit;

        public bool IsPredictionStalledByIdealFrame => _isPredictionStalledByIdealFrame;

        public long TotalIdealFrameStalls => _totalIdealFrameStalls;

        public bool TryGetIdealFrameStallStats(WorldId worldId, out int idealFrameLimit, out bool stalled, out long stallsTotal)
        {
            idealFrameLimit = 0;
            stalled = false;
            stallsTotal = 0;
            if (!_contexts.TryGetValue(worldId, out var ctx) || ctx == null) return false;
            idealFrameLimit = ctx.IdealFrameLimit;
            stalled = ctx.IdealFrameStalled;
            stallsTotal = ctx.IdealFrameStallsTotal;
            return true;
        }

        void IClientPredictionTuningControl.SetMaxPredictionAheadFrames(int value)
        {
            if (value < 0) value = 0;
            _tuningMaxPredictionAheadFrames = value;
            if (_tuningMaxPredictionAheadFrames > 0 && _tuningMinPredictionWindow > _tuningMaxPredictionAheadFrames)
            {
                _tuningMinPredictionWindow = _tuningMaxPredictionAheadFrames;
            }
        }

        void IClientPredictionTuningControl.SetMinPredictionWindow(int value)
        {
            if (value < 0) value = 0;
            if (_tuningMaxPredictionAheadFrames > 0 && value > _tuningMaxPredictionAheadFrames) value = _tuningMaxPredictionAheadFrames;
            _tuningMinPredictionWindow = value;
        }

        void IClientPredictionTuningControl.SetBacklogEwmaAlpha(float value)
        {
            if (value < 0f) value = 0f;
            if (value > 1f) value = 1f;
            _tuningBacklogEwmaAlpha = value;
        }

        void IClientPredictionTuningControl.ResetDefaults()
        {
            _tuningMaxPredictionAheadFrames = _defaultMaxPredictionAheadFrames;
            _tuningMinPredictionWindow = _defaultMinPredictionWindow;
            _tuningBacklogEwmaAlpha = _defaultBacklogEwmaAlpha;
        }

        public bool IsReplaying => _isReplaying;

        public FrameIndex ReplayToFrame => _replayToFrame;

        public FrameIndex LastRollbackFrame => _lastRollbackFrame;

        public long TotalRollbackCount => _totalRollbackCount;

        public long TotalRollbackRestoreFailed => _totalRollbackRestoreFailed;

        public long TotalReplayTimeout => _totalReplayTimeout;
        public FrameIndex LastReplayTimeoutFrame => _lastReplayTimeoutFrame;

        public long TotalReconcileAutoDisabledByReplayTimeout => _totalReconcileAutoDisabledByReplayTimeout;
        public FrameIndex LastReconcileAutoDisabledByReplayTimeoutFrame => _lastReconcileAutoDisabledByReplayTimeoutFrame;

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

        public bool ShouldRunWorldTick => _shouldRunWorldTick;

        public bool TryGetLocalDelayQueueDepth(WorldId worldId, out int depth)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx?.PendingLocalInputs != null)
            {
                depth = 0;
                foreach (var pending in ctx.PendingLocalInputs)
                {
                    depth += pending.Value?.Count ?? 0;
                }
                return true;
            }

            depth = 0;
            return false;
        }

        public bool TryGetPredictionWindowStats(WorldId worldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                backlogRaw = ctx.BacklogRaw;
                backlogEwma = ctx.BacklogEwma;
                window = ctx.PredictionWindow;
                stalled = ctx.PredictionStalled;
                return true;
            }

            backlogRaw = 0;
            backlogEwma = 0;
            window = 0;
            stalled = false;
            return false;
        }

        public bool TryGetPredictionWindowStats(WorldId worldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled, out long stallsTotal)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                backlogRaw = ctx.BacklogRaw;
                backlogEwma = ctx.BacklogEwma;
                window = ctx.PredictionWindow;
                stalled = ctx.PredictionStalled;
                stallsTotal = ctx.PredictionWindowStallsTotal;
                return true;
            }

            backlogRaw = 0;
            backlogEwma = 0;
            window = 0;
            stalled = false;
            stallsTotal = 0;
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

        public bool TryRebase(WorldId worldId, FrameIndex baselineFrame)
        {
            if (baselineFrame.Value < 0 ||
                !_contexts.TryGetValue(worldId, out var ctx) ||
                ctx == null)
            {
                return false;
            }

            ctx.PendingLocalInputs?.Clear();
            ctx.AppliedInputs?.Clear();
            ctx.AuthoritativeInputs?.Clear();
            ctx.PredictedHashes?.Clear();
            ctx.AuthoritativeHashes?.Clear();
            ctx.Reconciler?.Clear();

            ctx.Rollback?.ClearHistory();
            ctx.ConfirmedFrame = baselineFrame;
            ctx.PredictedFrame = baselineFrame;
            ctx.LastProcessedRollbackFrame = baselineFrame;
            ctx.CaptureCounter = 0;
            ctx.LastMissingAppliedHistoryFrame = new FrameIndex(-1);

            ctx.HasBacklogEwma = false;
            ctx.BacklogEwma = 0f;
            ctx.BacklogRaw = 0;
            ctx.PredictionWindow = 0;
            ctx.PredictionStalled = false;
            ctx.IdealFrameStalled = false;
            ctx.IdealFrameCappedWindow = false;

            ResetReconcileInternal(ctx);
            AlignFrameTimeToBaseline(ctx.FrameTime, baselineFrame);

            if (ctx.Rollback != null && !ctx.Rollback.CaptureAndStore(baselineFrame))
            {
                Log.Warning(
                    $"[ClientPredictionDriverModule] Failed to capture imported prediction baseline. " +
                    $"worldId={worldId.Value}, frame={baselineFrame.Value}");
                return false;
            }

            _lastConsumedConfirmedFrames = 0;
            _lastConsumedPredictedFrames = 0;
            _currentBacklogRaw = 0;
            _currentBacklogEwma = 0f;
            _currentPredictionWindow = 0;
            _isPredictionStalledByWindow = false;
            _isPredictionStalledByIdealFrame = false;
            return true;
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
            runtime.Features.RegisterFeature<IClientPredictionTuningControl>(this);
            runtime.Features.RegisterFeature<IClientPredictionReconcileTarget>(this);
            runtime.Features.RegisterFeature<IClientPredictionReconcileControl>(this);
            runtime.Features.RegisterFeature<IClientPredictionBaselineControl>(this);
            runtime.Features.RegisterFeature<IHostRuntimeTickGate>(this);
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
            runtime.Features.UnregisterFeature<IClientPredictionTuningControl>();
            runtime.Features.UnregisterFeature<IClientPredictionReconcileTarget>();
            runtime.Features.UnregisterFeature<IClientPredictionReconcileControl>();
            runtime.Features.UnregisterFeature<IClientPredictionBaselineControl>();
            runtime.Features.UnregisterFeature<IHostRuntimeTickGate>();

            _contexts.Clear();
            _lastConsumedConfirmedFrames = 0;
            _totalConsumedConfirmedFrames = 0;
            _totalPredictedFrames = 0;

            _currentPredictionWindow = 0;
            _isPredictionStalledByWindow = false;
            _totalPredictionWindowStalls = 0;

            _currentIdealFrameLimit = 0;
            _isPredictionStalledByIdealFrame = false;
            _totalIdealFrameStalls = 0;

            _currentBacklogRaw = 0;
            _currentBacklogEwma = 0;

            _totalReplayTimeout = 0;
            _lastReplayTimeoutFrame = default;

            _totalReconcileAutoDisabledByReplayTimeout = 0;
            _lastReconcileAutoDisabledByReplayTimeoutFrame = default;
            _runtime = null;
            _options = null;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null) return;

            IWorldInputSink sink = null;
            FrameTime frameTime = null;
            if (world.Services != null)
            {
                world.Services.TryResolve<IWorldInputSink>(out sink);
                if (world.Services.TryResolve<IFrameTime>(out var resolvedFrameTime))
                {
                    frameTime = resolvedFrameTime as FrameTime;
                }
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
                FrameTime = frameTime,
                ConfirmedFrame = new FrameIndex(0),
                PredictedFrame = new FrameIndex(0),
                PendingLocalInputs = new SortedDictionary<int, List<LocalPlayerInputEvent>>(),
                Rollback = rollback,
                CaptureCounter = 0,
                LastProcessedRollbackFrame = new FrameIndex(0),
                AppliedInputs = new InputHistoryRingBuffer(_rollbackHistoryFrames),
                AuthoritativeInputs = new InputHistoryRingBuffer(_rollbackHistoryFrames),

                ComputeHash = computeHash,
                PredictedHashes = predictedHashes,
                AuthoritativeHashes = authoritativeHashes,
                Reconciler = reconciler,
                ReconcileEnabled = reconciler != null && computeHash != null,
                Mode = ReplayMode.Normal,
                ReplayTo = new FrameIndex(0),
                LastRollbackFrame = new FrameIndex(-1),
                LastMissingAppliedHistoryFrame = new FrameIndex(-1),
                LastReplayWaitTargetFrame = -1,
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
                // 等该帧预测哈希记录完成后再重试比对。
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

            // 避免回滚风暴（例如调试强制不一致），并避免在回放期间重入。
            if (ctx.Mode == ReplayMode.Replaying) return;
            if (!ShouldRequestReconcileRollback(ctx.LastRollbackFrame, mismatchFrame)) return;

            var rollbackFrame = new FrameIndex(mismatchFrame.Value - 1);
            var ok = TryRestoreState(ctx, rollbackFrame);
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
            ctx.LastProcessedRollbackFrame = rollbackFrame;
            ctx.LastReplayWaitTargetFrame = -1;

            _isReplaying = true;
            _replayToFrame = ctx.ReplayTo;
            _lastRollbackFrame = rollbackFrame;
        }

        private static bool ShouldRequestReconcileRollback(
            FrameIndex lastRollbackFrame,
            FrameIndex mismatchFrame)
        {
            var rollbackFrame = new FrameIndex(mismatchFrame.Value - 1);
            return lastRollbackFrame.Value < rollbackFrame.Value;
        }

        private static bool TryRestoreState(WorldContext ctx, FrameIndex frame)
        {
            if (ctx == null || ctx.Rollback == null) return false;
            return ctx.Rollback.TryRestore(frame);
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _contexts.Remove(worldId);
        }

        private void OnPreTick(float deltaTime)
        {
            if (_runtime == null) return;

            _shouldRunWorldTick = false;
            _lastConsumedConfirmedFrames = 0;
            _lastConsumedPredictedFrames = 0;

            _isPredictionStalledByWindow = false;
            _isPredictionStalledByIdealFrame = false;

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
                if (ctx == null) continue;
                if (ctx.World == null || ctx.InputSink == null) continue;

                _lastConsumedConfirmedFrames = 0;
                _lastConsumedPredictedFrames = 0;

                var remote = _resolveRemoteInputs != null ? _resolveRemoteInputs(worldId) : null;
                var local = _resolveLocalInputs != null ? _resolveLocalInputs(worldId) : null;

                // 每次 HostRuntime.Tick 只提交一次。

                // 从时间同步计算理想帧上限（如果可用）。
                var ideal = _resolveIdealFrameLimit != null ? _resolveIdealFrameLimit(worldId) : 0;
                _currentIdealFrameLimit = ideal;
                ctx.IdealFrameLimit = ideal;
                ctx.IdealFrameStalled = false;
                ctx.IdealFrameCappedWindow = false;

                // 计算自适应预测窗口。
                // - 硬上限：_maxPredictionAheadFrames
                // - 动态窗口：基于权威积压帧数（TargetFrame - ConfirmedFrame）
                // - 通过 EWMA 平滑积压帧数，避免窗口抖动。
                var rawBacklog = remote != null ? (remote.TargetFrame - ctx.ConfirmedFrame.Value) : 0;
                if (rawBacklog < 0) rawBacklog = 0;

                ctx.BacklogRaw = rawBacklog;

                if (!ctx.HasBacklogEwma)
                {
                    ctx.HasBacklogEwma = true;
                    ctx.BacklogEwma = rawBacklog;
                }
                else
                {
                    ctx.BacklogEwma = (_tuningBacklogEwmaAlpha * rawBacklog) + ((1f - _tuningBacklogEwmaAlpha) * ctx.BacklogEwma);
                }

                _currentBacklogRaw = rawBacklog;
                _currentBacklogEwma = ctx.BacklogEwma;

                var smoothedBacklogInt = (int)MathF.Round(ctx.BacklogEwma);
                if (smoothedBacklogInt < 0) smoothedBacklogInt = 0;

                var window = smoothedBacklogInt + _inputDelayFrames;
                if (window < _tuningMinPredictionWindow) window = _tuningMinPredictionWindow;
                if (_tuningMaxPredictionAheadFrames > 0 && window > _tuningMaxPredictionAheadFrames) window = _tuningMaxPredictionAheadFrames;
                if (_tuningMaxPredictionAheadFrames == 0) window = 0;
                _currentPredictionWindow = window;

                ctx.PredictionWindow = window;
                ctx.PredictionStalled = false;

                if (_currentIdealFrameLimit > 0)
                {
                    var maxAheadByIdeal = _currentIdealFrameLimit - ctx.ConfirmedFrame.Value;
                    if (maxAheadByIdeal < 0) maxAheadByIdeal = 0;

                    if (maxAheadByIdeal < _currentPredictionWindow)
                    {
                        ctx.IdealFrameCappedWindow = true;
                        _currentPredictionWindow = maxAheadByIdeal;
                        ctx.PredictionWindow = _currentPredictionWindow;

                        if (_currentPredictionWindow == 0 && window > 0)
                        {
                            _isPredictionStalledByIdealFrame = true;
                            _totalIdealFrameStalls++;
                            ctx.IdealFrameStalled = true;
                            ctx.IdealFrameStallsTotal++;
                        }
                    }
                }

                // 步骤 0：始终入队一个本地批次（可为空），确保延迟队列向前推进。
                if (local != null)
                {
                    var evts = Array.Empty<LocalPlayerInputEvent>();
                    if (local.TryDequeue(out var dequeued) && dequeued != null)
                    {
                        evts = dequeued;
                    }

                    ScheduleLocalInputs(ctx, evts);
                }

                // 步骤 1（优先）：如果下一确认帧有权威输入，则应用它。
                if (remote != null && ctx.Mode == ReplayMode.Normal)
                {
                    var nextConfirmed = ctx.ConfirmedFrame.Value + 1;
                    if (nextConfirmed <= remote.TargetFrame)
                    {
                        var frame = new FrameIndex(nextConfirmed);

                        // 先窥探输入，用于判断是否需要回滚。
                        PlayerInputCommand[] authInputs;
                        if (!remote.TryGet(nextConfirmed, out authInputs) || authInputs == null)
                        {
                            authInputs = Array.Empty<PlayerInputCommand>();
                        }

                        var confirmsAlreadyPredictedFrame = false;
                        if (_enableRollback && ctx.Rollback != null && ctx.PredictedFrame.Value >= frame.Value)
                        {
                            if (!ctx.AppliedInputs.TryGet(frame, out var appliedAtFrame) || appliedAtFrame == null)
                            {
                                if (ctx.LastMissingAppliedHistoryFrame.Value != frame.Value)
                                {
                                    ctx.LastMissingAppliedHistoryFrame = frame;
                                    _totalRollbackRestoreFailed++;
                                    Log.Warning(
                                        $"[ClientPredictionDriverModule] Authoritative frame is outside applied input history; " +
                                        $"stopping before stale submission. worldId={worldId.Value}, frame={frame.Value}, " +
                                        $"predicted={ctx.PredictedFrame.Value}, historyCapacity={ctx.AppliedInputs.Capacity}");
                                }

                                continue;
                            }

                            if (!InputsEqual(appliedAtFrame, authInputs))
                            {
                                var rollbackFrame = new FrameIndex(frame.Value - 1);
                                var ok = TryRestoreState(ctx, rollbackFrame);
                                if (ok)
                                {
                                    _totalRollbackCount++;
                                    ctx.Mode = ReplayMode.Replaying;
                                    ctx.ReplayTo = ctx.PredictedFrame;
                                    ctx.ConfirmedFrame = rollbackFrame;
                                    ctx.PredictedFrame = rollbackFrame;
                                    ctx.LastRollbackFrame = rollbackFrame;
                                    ctx.LastProcessedRollbackFrame = rollbackFrame;
                                    ctx.LastReplayWaitTargetFrame = -1;

                                    _isReplaying = true;
                                    _replayToFrame = ctx.ReplayTo;
                                    _lastRollbackFrame = rollbackFrame;
                                }
                                else
                                {
                                    _totalRollbackRestoreFailed++;
                                }
                            }
                            else
                            {
                                confirmsAlreadyPredictedFrame = true;
                            }
                        }

                        if (ctx.Mode == ReplayMode.Replaying)
                        {
                            // 继续落入下方回放逻辑。
                        }
                        else
                        {
                            // 消费并应用权威输入。
                            if (!remote.TryConsume(nextConfirmed, out var consumed) || consumed == null)
                            {
                                consumed = Array.Empty<PlayerInputCommand>();
                            }
                            if (consumed.Length > 0)
                            {
                                Log.Info($"[ClientPredictionDriverModule] Consuming authoritative inputs. worldId={worldId.Value}, frame={nextConfirmed}, count={consumed.Length}, firstOpCode={consumed[0].OpCode}");
                            }

                            ctx.AuthoritativeInputs.Store(frame, consumed);

                            ctx.ConfirmedFrame = frame;
                            _lastConsumedConfirmedFrames = 1;
                            _totalConsumedConfirmedFrames++;

                            if (confirmsAlreadyPredictedFrame)
                            {
                                // The world has already simulated this exact input. Use this host tick
                                // to prepare the next predicted frame instead of executing it twice.
                            }
                            else
                            {
                                AlignFrameTime(ctx.FrameTime, frame, deltaTime);
                                ctx.InputSink.Submit(frame, consumed);
                                _shouldRunWorldTick = true;
                                ctx.AppliedInputs.Store(frame, consumed);

                                if (ctx.PredictedFrame.Value < frame.Value)
                                {
                                    ctx.PredictedFrame = frame;
                                }

                                continue;
                            }
                        }
                    }
                }

                // 回放：确定性重新模拟，直到 ReplayTo。
                if (ctx.Mode == ReplayMode.Replaying)
                {
                    _isReplaying = true;
                    _replayToFrame = ctx.ReplayTo;
                    _lastRollbackFrame = ctx.LastRollbackFrame;

                    var next = new FrameIndex(ctx.PredictedFrame.Value + 1);

                    if (next.Value > ctx.ReplayTo.Value)
                    {
                        ctx.Mode = ReplayMode.Normal;
                        ctx.ReplayWaitTicks = 0;
                        ctx.LastReplayWaitTargetFrame = -1;
                        _isReplaying = false;
                        continue;
                    }

                    // 方案 A（收敛优先）：只有该帧权威输入可用时才回放。
                    // 否则退出回放并返回 Normal 预测，避免来回拉扯。
                    PlayerInputCommand[] inputs = null;
                    if (ctx.AuthoritativeInputs.TryGet(next, out var auth) && auth != null)
                    {
                        inputs = auth;
                        if (ctx.ConfirmedFrame.Value < next.Value)
                        {
                            ctx.ConfirmedFrame = next;
                        }
                    }
                    else if (remote != null && next.Value <= remote.TargetFrame)
                    {
                        if (!remote.TryConsume(next.Value, out var consumed) || consumed == null)
                        {
                            consumed = Array.Empty<PlayerInputCommand>();
                        }
                        if (consumed.Length > 0)
                        {
                            Log.Info($"[ClientPredictionDriverModule] Consuming replay authoritative inputs. worldId={worldId.Value}, frame={next.Value}, count={consumed.Length}, firstOpCode={consumed[0].OpCode}");
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
                        // 该帧尚无权威输入，暂停回放（不提交预测输入）。
                        var replayWaitTargetFrame = remote != null ? remote.TargetFrame : -1;
                        if (ctx.LastReplayWaitTargetFrame != replayWaitTargetFrame)
                        {
                            ctx.LastReplayWaitTargetFrame = replayWaitTargetFrame;
                            ctx.ReplayWaitTicks++;
                        }
                        if (ctx.ReplayWaitTicks >= ReplayWaitTimeoutTicks)
                        {
                            Log.Warning($"[ClientPredictionDriverModule] Replay wait timeout. worldId={worldId} rollbackFrame={ctx.LastRollbackFrame.Value} predicted={ctx.PredictedFrame.Value} replayTo={ctx.ReplayTo.Value} targetFrame={(remote != null ? remote.TargetFrame : -1)}. Disabling reconcile and exiting replay.");
                            _totalReplayTimeout++;
                            _lastReplayTimeoutFrame = next;
                            _totalReconcileAutoDisabledByReplayTimeout++;
                            _lastReconcileAutoDisabledByReplayTimeoutFrame = next;
                            ctx.ReconcileEnabled = false;
                            ctx.Mode = ReplayMode.Normal;
                            ctx.ReplayWaitTicks = 0;
                            ctx.LastReplayWaitTargetFrame = -1;
                        }
                        continue;
                    }

                    ctx.ReplayWaitTicks = 0;
                    ctx.LastReplayWaitTargetFrame = -1;

                    AlignFrameTime(ctx.FrameTime, next, deltaTime);
                    ctx.InputSink.Submit(next, inputs);
                    _shouldRunWorldTick = true;
                    ctx.AppliedInputs.Store(next, inputs);
                    ctx.PredictedFrame = next;
                    _lastConsumedPredictedFrames = 1;
                    _totalPredictedFrames++;
                    continue;
                }

                // 步骤 2（方案 A：响应优先）：使用当前 tick 本地输入执行一次预测步。
                {
                    if (_currentPredictionWindow == 0)
                    {
                        if (ctx.IdealFrameCappedWindow)
                        {
                            _isPredictionStalledByIdealFrame = true;
                        }
                        continue;
                    }

                    var next = new FrameIndex(ctx.PredictedFrame.Value + 1);

                    var ahead = ctx.PredictedFrame.Value - ctx.ConfirmedFrame.Value;
                    if (ahead >= _currentPredictionWindow)
                    {
                        if (ctx.IdealFrameCappedWindow)
                        {
                            _isPredictionStalledByIdealFrame = true;
                            _totalIdealFrameStalls++;
                            ctx.IdealFrameStalled = true;
                            ctx.IdealFrameStallsTotal++;
                        }
                        else
                        {
                            _isPredictionStalledByWindow = true;
                            _totalPredictionWindowStalls++;
                            ctx.PredictionStalled = true;
                            ctx.PredictionWindowStallsTotal++;
                        }
                        continue;
                    }

                    var localBatch = TakeLocalInputs(ctx, next);
                    PlayerInputCommand[] predictedInputs;
                    if (localBatch.Length == 0)
                    {
                        predictedInputs = Array.Empty<PlayerInputCommand>();
                    }
                    else
                    {
                        predictedInputs = new PlayerInputCommand[localBatch.Length];
                        for (int i = 0; i < localBatch.Length; i++)
                        {
                            var e = localBatch[i];
                            predictedInputs[i] = new PlayerInputCommand(next, e.PlayerId, e.OpCode, e.Payload ?? Array.Empty<byte>());
                        }
                    }

                    AlignFrameTime(ctx.FrameTime, next, deltaTime);
                    ctx.InputSink.Submit(next, predictedInputs);
                    _shouldRunWorldTick = true;
                    ctx.PredictedFrame = next;
                    ctx.AppliedInputs.Store(next, predictedInputs);
                    _lastConsumedPredictedFrames = 1;
                    _totalPredictedFrames++;
                }
            }
        }

        private void ScheduleLocalInputs(WorldContext ctx, LocalPlayerInputEvent[] events)
        {
            if (ctx == null || events == null || events.Length == 0) return;
            ctx.PendingLocalInputs ??= new SortedDictionary<int, List<LocalPlayerInputEvent>>();

            var droppedStaleInput = false;
            for (var i = 0; i < events.Length; i++)
            {
                var input = events[i];
                var targetFrame = input.Frame.Value > 0
                    ? input.Frame.Value
                    : ctx.PredictedFrame.Value + 1;
                if (targetFrame <= ctx.PredictedFrame.Value)
                {
                    if (input.CanRetargetIfStale)
                    {
                        targetFrame = ctx.PredictedFrame.Value + 1;
                    }
                    else
                    {
                        droppedStaleInput = true;
                        continue;
                    }
                }

                if (!ctx.PendingLocalInputs.TryGetValue(targetFrame, out var frameInputs))
                {
                    frameInputs = new List<LocalPlayerInputEvent>(2);
                    ctx.PendingLocalInputs[targetFrame] = frameInputs;
                }

                var replaced = false;
                if (input.CanRetargetIfStale)
                {
                    for (var pendingIndex = 0; pendingIndex < frameInputs.Count; pendingIndex++)
                    {
                        var pending = frameInputs[pendingIndex];
                        if (pending.CanRetargetIfStale &&
                            pending.OpCode == input.OpCode &&
                            pending.PlayerId.Value == input.PlayerId.Value)
                        {
                            frameInputs[pendingIndex] = input;
                            replaced = true;
                            break;
                        }
                    }
                }

                if (!replaced)
                {
                    frameInputs.Add(input);
                }
            }

            if (droppedStaleInput)
            {
                _totalLocalDelayQueueDroppedBatches++;
            }

            while (ctx.PendingLocalInputs.Count > _maxLocalDelayQueueDepth)
            {
                var firstKey = int.MaxValue;
                foreach (var pending in ctx.PendingLocalInputs)
                {
                    firstKey = pending.Key;
                    break;
                }
                if (firstKey == int.MaxValue) break;
                ctx.PendingLocalInputs.Remove(firstKey);
                _totalLocalDelayQueueDroppedBatches++;
            }
        }

        private static LocalPlayerInputEvent[] TakeLocalInputs(WorldContext ctx, FrameIndex frame)
        {
            if (ctx?.PendingLocalInputs == null ||
                !ctx.PendingLocalInputs.TryGetValue(frame.Value, out var inputs) ||
                inputs == null ||
                inputs.Count == 0)
            {
                return Array.Empty<LocalPlayerInputEvent>();
            }

            ctx.PendingLocalInputs.Remove(frame.Value);
            return inputs.ToArray();
        }

        private static void AlignFrameTime(
            FrameTime frameTime,
            FrameIndex targetFrame,
            float fallbackDeltaTime)
        {
            if (frameTime == null || frameTime.Frame.Value == targetFrame.Value) return;

            var fixedDelta = frameTime.FrameToTime(new FrameIndex(1));
            if (fixedDelta <= 0f)
            {
                fixedDelta = fallbackDeltaTime;
            }
            if (fixedDelta <= 0f) return;

            var previousFrame = new FrameIndex(targetFrame.Value - 1);
            frameTime.Reset(
                previousFrame,
                previousFrame.Value * fixedDelta,
                fixedDelta);
            frameTime.StepTo(targetFrame, fixedDelta);
        }

        private static void AlignFrameTimeToBaseline(FrameTime frameTime, FrameIndex baselineFrame)
        {
            if (frameTime == null) return;

            var fixedDelta = frameTime.FrameToTime(new FrameIndex(1));
            if (fixedDelta <= 0f) return;

            frameTime.Reset(
                baselineFrame,
                baselineFrame.Value * fixedDelta,
                fixedDelta);
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
            if (_runtime == null || !_shouldRunWorldTick) return;

            foreach (var kv in _contexts)
            {
                var ctx = kv.Value;
                if (ctx == null) continue;
                if (!_enableRollback || ctx.Rollback == null) continue;
                if (!ShouldProcessRollbackFrame(ctx.PredictedFrame, ctx.LastProcessedRollbackFrame)) continue;

                ctx.LastProcessedRollbackFrame = ctx.PredictedFrame;
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

                        // 如果该帧权威哈希已提前到达，现在执行比对。
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

        private static bool ShouldProcessRollbackFrame(
            FrameIndex predictedFrame,
            FrameIndex lastProcessedFrame)
        {
            return predictedFrame.Value != lastProcessedFrame.Value;
        }
    }
}
