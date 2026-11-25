using System;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionInputExecution;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionLocalInputQueue;
using static AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionSettledTimeline;
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
    /// <summary>
    /// 在宿主帧内推进客户端预测、确认及可选回滚；每个上下文只适用于登记时绑定的世界实例。
    /// </summary>
    public sealed class ClientPredictionDriverModule : IHostRuntimeModule, IHostRuntimeTickGate, IClientPredictionDriverStats, IClientPredictionTuningControl, IClientPredictionReconcileTarget, IClientPredictionReconcileControl, IClientPredictionBaselineControl, IClientPredictionRestoreFloor
    {

        /// <summary>
        /// 重置指定世界的对账；未找到记录时仅对仍登记的上下文执行兜底重置。
        /// </summary>
        public void ResetReconcile(WorldId worldId)
        {
            if (_contexts.TryGetValue(worldId, out var ctx))
            {
                if (IsRegistered(ctx.Bindings.World)) ResetReconcileInternal(ctx);
            }
            else
            {
                // 防御性处理：如果 worldId 不匹配，则重置所有世界，避免卡在回放状态。
                foreach (var kv in _contexts)
                {
                    if (IsRegistered(kv.Value.Bindings.World)) ResetReconcileInternal(kv.Value);
                }
            }
        }

        private void ResetReconcileInternal(ClientPredictionWorldContext ctx)
        {
            if (ctx.Bindings.SettledTimeline != null) return;
            EndInputReplay(ctx);
            ctx.Reconciliation.PredictedHashes?.Clear();
            ctx.Reconciliation.AuthoritativeHashes?.Clear();
            ctx.Reconciliation.ComparedAuthoritativeHashes?.Clear();
            ctx.Bindings.Reconciler?.Clear();

            // 同时退出回放模式，避免调试强制不一致开关后卡住。
            ctx.Replay.Mode = ClientPredictionReplayMode.Normal;
            ctx.Replay.ReplayTo = ctx.Progress.PredictedFrame;
            ctx.Replay.LastRollbackFrame = new FrameIndex(-1);
            ctx.Replay.ReplayWaitTicks = 0;
            ctx.Replay.LastReplayWaitTargetFrame = -1;

            _state.IsReplaying = false;
            _state.ReplayToFrame = default;
            _state.LastRollbackFrame = default;

            _state.LastReconcileComparedFrame = default;
            _state.LastReconcileMismatchFrame = default;
            _state.LastReconcilePredictedHash = default;
            _state.LastReconcileAuthoritativeHash = default;
        }

        private readonly Dictionary<WorldId, ClientPredictionWorldContext> _contexts = new Dictionary<WorldId, ClientPredictionWorldContext>();
        private readonly List<WorldId> _detachedContexts = new List<WorldId>();

        private readonly Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> _resolveRemoteInputs;
        private readonly bool _useSettledExecutionInputs;
        private readonly Func<WorldId, IConsumableRemoteFrameSource<WorldSettledInputFrame>> _resolveSettledFrames;
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
        private readonly ClientPredictionDriverBufferOptions _bufferOptions;
        private readonly int _rollbackHistoryFrames;
        private readonly int _rollbackCaptureEveryNFrames;
        private readonly Func<IWorld, RollbackRegistry> _buildRollbackRegistry;

        private readonly Func<IWorld, Func<FrameIndex, WorldStateHash>> _buildComputeHash;


        private readonly ClientPredictionDriverState _state = new ClientPredictionDriverState();

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
            Func<IWorld, Func<FrameIndex, WorldStateHash>> buildComputeHash = null,
            ClientPredictionDriverBufferOptions bufferOptions = null,
            bool useSettledExecutionInputs = false,
            Func<WorldId, IConsumableRemoteFrameSource<WorldSettledInputFrame>> resolveSettledFrames = null)
        {
            if (resolveSettledFrames != null && resolveRemoteInputs != null)
                throw new ArgumentException("A settled timeline cannot consume legacy remote input.", nameof(resolveRemoteInputs));
            _useSettledExecutionInputs = useSettledExecutionInputs || resolveSettledFrames != null;
            if (_useSettledExecutionInputs && buildComputeHash != null)
                throw new ArgumentException("Legacy hash reconciliation is not a settled execution proof.", nameof(buildComputeHash));
            _resolveSettledFrames = resolveSettledFrames;
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

            _rollbackHistoryFrames = rollbackHistoryFrames <= 0 ? 240 : rollbackHistoryFrames;
            _rollbackCaptureEveryNFrames = rollbackCaptureEveryNFrames <= 0 ? 1 : rollbackCaptureEveryNFrames;
            _buildRollbackRegistry = buildRollbackRegistry;

            _buildComputeHash = buildComputeHash;
            _bufferOptions = bufferOptions ?? ClientPredictionDriverBufferOptions.CreateDefault(
                enableRollback,
                buildComputeHash != null,
                _rollbackHistoryFrames);
            _enableRollback = enableRollback
                && _bufferOptions.Has(ClientPredictionDriverBufferFeatures.RollbackSnapshots);

            _onWorldCreated = OnWorldCreated;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPreTick = OnPreTick;
            _onPostTick = OnPostTick;
        }

        public int InputDelayFrames => _inputDelayFrames;

        public int MaxPredictionAheadFrames => _tuningMaxPredictionAheadFrames;

        public int MinPredictionWindow => _tuningMinPredictionWindow;

        public float BacklogEwmaAlpha => _tuningBacklogEwmaAlpha;

        public ClientPredictionDriverBufferOptions BufferOptions => _bufferOptions;

        public int CurrentBacklogRaw => _state.CurrentBacklogRaw;

        public float CurrentBacklogEwma => _state.CurrentBacklogEwma;

        public int CurrentPredictionWindow => _state.CurrentPredictionWindow;

        public bool IsPredictionStalledByWindow => _state.IsPredictionStalledByWindow;

        public long TotalPredictionWindowStalls => _state.TotalPredictionWindowStalls;

        public int CurrentIdealFrameLimit => _state.CurrentIdealFrameLimit;

        public bool IsPredictionStalledByIdealFrame => _state.IsPredictionStalledByIdealFrame;

        public long TotalIdealFrameStalls => _state.TotalIdealFrameStalls;

        /// <summary>
        /// 查询当前登记世界的理想帧上限与停滞统计。
        /// </summary>
        public bool TryGetIdealFrameStallStats(WorldId worldId, out int idealFrameLimit, out bool stalled, out long stallsTotal)
        {
            idealFrameLimit = 0;
            stalled = false;
            stallsTotal = 0;
            if (!TryGetActiveContext(worldId, out var ctx)) return false;
            idealFrameLimit = ctx.Window.IdealFrameLimit;
            stalled = ctx.Window.IdealFrameStalled;
            stallsTotal = ctx.Window.IdealFrameStallsTotal;
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

        public bool IsReplaying => _state.IsReplaying;

        public FrameIndex ReplayToFrame => _state.ReplayToFrame;

        public FrameIndex LastRollbackFrame => _state.LastRollbackFrame;

        public long TotalRollbackCount => _state.TotalRollbackCount;

        public long TotalRollbackRestoreFailed => _state.TotalRollbackRestoreFailed;

        public long TotalReplayTimeout => _state.TotalReplayTimeout;
        public FrameIndex LastReplayTimeoutFrame => _state.LastReplayTimeoutFrame;

        public long TotalReconcileAutoDisabledByReplayTimeout => _state.TotalReconcileAutoDisabledByReplayTimeout;
        public FrameIndex LastReconcileAutoDisabledByReplayTimeoutFrame => _state.LastReconcileAutoDisabledByReplayTimeoutFrame;

        public long TotalReconcileMismatch => _state.TotalReconcileMismatch;

        public long TotalPredictedHashRecorded => _state.TotalPredictedHashRecorded;

        public long TotalAuthoritativeHashSkippedNoPredictedHash => _state.TotalAuthoritativeHashSkippedNoPredictedHash;

        public FrameIndex LastReconcileComparedFrame => _state.LastReconcileComparedFrame;

        public FrameIndex LastReconcileMismatchFrame => _state.LastReconcileMismatchFrame;

        public WorldStateHash LastReconcilePredictedHash => _state.LastReconcilePredictedHash;

        public WorldStateHash LastReconcileAuthoritativeHash => _state.LastReconcileAuthoritativeHash;

        public long TotalAuthoritativeHashReceived => _state.TotalAuthoritativeHashReceived;

        public FrameIndex LastAuthoritativeHashFrame => _state.LastAuthoritativeHashFrame;

        public WorldStateHash LastAuthoritativeHash => _state.LastAuthoritativeHash;

        public long TotalAuthoritativeHashIgnoredNoReconciler => _state.TotalAuthoritativeHashIgnoredNoReconciler;

        public int LastConsumedConfirmedFrames => _state.LastConsumedConfirmedFrames;

        public int LastConsumedPredictedFrames => _state.LastConsumedPredictedFrames;

        public long TotalConsumedConfirmedFrames => _state.TotalConsumedConfirmedFrames;

        public long TotalPredictedFrames => _state.TotalPredictedFrames;

        public long TotalLocalDelayQueueDroppedBatches => _state.TotalLocalDelayQueueDroppedBatches;

        public bool ShouldRunWorldTick => _state.ShouldRunWorldTick;

        /// <summary>
        /// 查询当前登记世界尚未提交的本地输入批次数。
        /// </summary>
        public bool TryGetLocalDelayQueueDepth(WorldId worldId, out int depth)
        {
            if (TryGetActiveContext(worldId, out var ctx) && ctx.Inputs.PendingLocalInputs != null)
            {
                depth = 0;
                foreach (var pending in ctx.Inputs.PendingLocalInputs)
                {
                    depth += pending.Value?.Count ?? 0;
                }
                return true;
            }

            depth = 0;
            return false;
        }

        /// <summary>
        /// 查询当前登记世界的输入积压、预测窗口和停滞状态。
        /// </summary>
        public bool TryGetPredictionWindowStats(WorldId worldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled)
        {
            if (TryGetActiveContext(worldId, out var ctx))
            {
                backlogRaw = ctx.Window.BacklogRaw;
                backlogEwma = ctx.Window.BacklogEwma;
                window = ctx.Window.PredictionWindow;
                stalled = ctx.Window.PredictionStalled;
                return true;
            }

            backlogRaw = 0;
            backlogEwma = 0;
            window = 0;
            stalled = false;
            return false;
        }

        /// <summary>
        /// 查询当前登记世界的预测窗口状态，并返回该实例的累计停滞次数。
        /// </summary>
        public bool TryGetPredictionWindowStats(WorldId worldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled, out long stallsTotal)
        {
            if (TryGetActiveContext(worldId, out var ctx))
            {
                backlogRaw = ctx.Window.BacklogRaw;
                backlogEwma = ctx.Window.BacklogEwma;
                window = ctx.Window.PredictionWindow;
                stalled = ctx.Window.PredictionStalled;
                stallsTotal = ctx.Window.PredictionWindowStallsTotal;
                return true;
            }

            backlogRaw = 0;
            backlogEwma = 0;
            window = 0;
            stalled = false;
            stallsTotal = 0;
            return false;
        }

        /// <summary>
        /// 查询当前登记世界的确认帧和预测帧。
        /// </summary>
        public bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted)
        {
            if (TryGetActiveContext(worldId, out var ctx))
            {
                confirmed = ctx.Progress.ConfirmedFrame;
                predicted = ctx.Progress.PredictedFrame;
                return true;
            }

            confirmed = default;
            predicted = default;
            return false;
        }

        /// <summary>
        /// 读取已提交的恢复门槛。未启用回滚或尚无有效权威锚点时返回 false。
        /// </summary>
        public bool TryGetRestoreFloor(WorldId worldId, out FrameIndex frame)
        {
            frame = default;
            return TryGetActiveContext(worldId, out var ctx) && ctx.Bindings.Rollback != null &&
                   ctx.Bindings.Rollback.TryGetRestoreFloor(out frame);
        }

        /// <summary>
        /// 在宿主帧结束后提交已由权威适配器验真的结算锚点。
        /// 调用方保证锚点属于当前战局、摘要匹配并已排空在途旧纠偏。
        /// 迟到锚点须对应当前分支仍保有完整快照、且已消费权威输入的帧。
        /// 不接受未来帧或纠偏重演期间的提交。
        /// </summary>
        public bool TryCommitVerifiedRestoreAnchor(WorldId worldId, FrameIndex frame)
        {
            if (!TryGetActiveContext(worldId, out var ctx) || ctx.Bindings.Rollback == null || ctx.Progress.RollbackFaulted ||
                ctx.Replay.Mode != ClientPredictionReplayMode.Normal || ctx.Replay.InputReplayActive ||
                frame.Value > ctx.Progress.LastProcessedRollbackFrame.Value || frame.Value > ctx.Progress.PredictedFrame.Value ||
                (frame.Value < ctx.Progress.PredictedFrame.Value && frame.Value > ctx.Progress.ConfirmedFrame.Value))
                return false;

            if (ctx.Bindings.World?.Services?.TryResolve<IWorldFrameGuard>(out var guard) == true && !guard.CanCommit)
                return false;

            return ctx.Bindings.Rollback.TryAdvanceRestoreFloor(frame);
        }

        /// <summary>
        /// 用已导入的权威基线重建预测历史。生效的不可回滚边界不会随历史重置。
        /// </summary>
        public bool TryRebase(WorldId worldId, FrameIndex baselineFrame)
        {
            if (baselineFrame.Value < 0 ||
                !TryGetActiveContext(worldId, out var ctx) || ctx.Bindings.SettledTimeline != null)
            {
                return false;
            }

            if (ctx.Bindings.Rollback != null && ctx.Bindings.Rollback.TryGetRestoreFloor(out var floor) &&
                baselineFrame.Value < floor.Value)
                return false;

            ctx.Inputs.PendingLocalInputs?.Clear();
            ctx.Inputs.AppliedInputs?.Clear();
            ctx.Inputs.AuthoritativeInputs?.Clear();
            ctx.Reconciliation.PredictedHashes?.Clear();
            ctx.Reconciliation.AuthoritativeHashes?.Clear();
            ctx.Bindings.Reconciler?.Clear();

            ctx.Bindings.Rollback?.ClearHistory();
            ctx.Progress.ConfirmedFrame = baselineFrame;
            ctx.Progress.PredictedFrame = baselineFrame;
            ctx.Progress.LastProcessedRollbackFrame = baselineFrame;
            ctx.Progress.CaptureCounter = 0;
            ctx.Replay.LastMissingAppliedHistoryFrame = new FrameIndex(-1);

            ctx.Window.HasBacklogEwma = false;
            ctx.Window.BacklogEwma = 0f;
            ctx.Window.BacklogRaw = 0;
            ctx.Window.PredictionWindow = 0;
            ctx.Window.PredictionStalled = false;
            ctx.Window.IdealFrameStalled = false;
            ctx.Window.IdealFrameCappedWindow = false;

            ResetReconcileInternal(ctx);
            AlignFrameTimeToBaseline(ctx.Bindings.FrameTime, baselineFrame);

            if (ctx.Bindings.Rollback != null && !ctx.Bindings.Rollback.TryCaptureAndStore(baselineFrame, out var captureResult))
            {
                ctx.Progress.RollbackFaulted = true;
                Log.Warning(
                    $"[ClientPredictionDriverModule] Failed to capture imported prediction baseline. " +
                    $"worldId={worldId.Value}, frame={baselineFrame.Value}, result={captureResult}");
                return false;
            }
            ctx.Progress.RollbackFaulted = false;

            _state.LastConsumedConfirmedFrames = 0;
            _state.LastConsumedPredictedFrames = 0;
            _state.CurrentBacklogRaw = 0;
            _state.CurrentBacklogEwma = 0f;
            _state.CurrentPredictionWindow = 0;
            _state.IsPredictionStalledByWindow = false;
            _state.IsPredictionStalledByIdealFrame = false;
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
            runtime.Features.RegisterFeature<IClientPredictionRestoreFloor>(this);
            runtime.Features.RegisterFeature<IHostRuntimeTickGate>(this);
        }

        /// <summary>
        /// 解绑宿主回调和功能，结束存活世界的重演并丢弃本次安装的预测上下文。
        /// </summary>
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
            runtime.Features.UnregisterFeature<IClientPredictionRestoreFloor>();
            runtime.Features.UnregisterFeature<IHostRuntimeTickGate>();

            foreach (var entry in _contexts)
            {
                if (IsRegistered(entry.Value.Bindings.World)) EndInputReplay(entry.Value);
            }
            _contexts.Clear();
            _detachedContexts.Clear();
            _state.LastConsumedConfirmedFrames = 0;
            _state.TotalConsumedConfirmedFrames = 0;
            _state.TotalPredictedFrames = 0;

            _state.CurrentPredictionWindow = 0;
            _state.IsPredictionStalledByWindow = false;
            _state.TotalPredictionWindowStalls = 0;

            _state.CurrentIdealFrameLimit = 0;
            _state.IsPredictionStalledByIdealFrame = false;
            _state.TotalIdealFrameStalls = 0;

            _state.CurrentBacklogRaw = 0;
            _state.CurrentBacklogEwma = 0;

            _state.TotalReplayTimeout = 0;
            _state.LastReplayTimeoutFrame = default;

            _state.TotalReconcileAutoDisabledByReplayTimeout = 0;
            _state.LastReconcileAutoDisabledByReplayTimeoutFrame = default;
            _runtime = null;
            _options = null;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null || !IsRegistered(world)) return;

            IWorldInputSink sink = null;
            IFrameTimeAligner frameTime = null;
            if (world.Services != null)
            {
                world.Services.TryResolve<IWorldInputSink>(out sink);
                if (world.Services.TryResolve<IFrameTime>(out var resolvedFrameTime))
                {
                    frameTime = resolvedFrameTime as IFrameTimeAligner;
                }
            }

            RollbackCoordinator rollback = null;
            if (_enableRollback)
            {
                var reg = _buildRollbackRegistry != null ? _buildRollbackRegistry(world) : new RollbackRegistry();
                rollback = new RollbackCoordinator(
                    reg,
                    new RollbackSnapshotRingBuffer(_bufferOptions.RollbackSnapshotCapacity));
                if (!rollback.TryCaptureAndStore(new FrameIndex(0), out var initialCapture))
                    throw new InvalidOperationException($"Initial rollback capture failed: {initialCapture}", initialCapture.Exception);
            }

            Func<FrameIndex, WorldStateHash> computeHash = null;
            ClientPredictionReconciler reconciler = null;
            WorldStateHashRingBuffer predictedHashes = null;
            WorldStateHashRingBuffer authoritativeHashes = null;
            WorldStateHashRingBuffer comparedAuthoritativeHashes = null;
            if (_buildComputeHash != null)
            {
                computeHash = _buildComputeHash(world);
                if (computeHash != null && _bufferOptions.Has(ClientPredictionDriverBufferFeatures.PredictedStateHashHistory))
                {
                    predictedHashes = new WorldStateHashRingBuffer(_bufferOptions.StateHashHistoryCapacity);
                    comparedAuthoritativeHashes = new WorldStateHashRingBuffer(
                        _bufferOptions.StateHashHistoryCapacity);
                    if (_bufferOptions.Has(ClientPredictionDriverBufferFeatures.AuthoritativeStateHashHistory))
                    {
                        authoritativeHashes = new WorldStateHashRingBuffer(_bufferOptions.StateHashHistoryCapacity);
                    }

                    reconciler = new ClientPredictionReconciler(predictedHashes);
                    reconciler.OnRollbackRequested += frame => RequestReconcileRollback(world.Id, frame);
                }
            }

            if (_useSettledExecutionInputs && (sink is not IWorldSettledInputSink || rollback == null ||
                !_bufferOptions.Has(ClientPredictionDriverBufferFeatures.AppliedInputHistory) ||
                _rollbackCaptureEveryNFrames != 1))
                throw new InvalidOperationException("Settled execution requires a settled input sink, per-frame rollback capture, and applied input history.");

            (sink as IWorldClientInputSink)?.BindClientInput();
            var timeline = _resolveSettledFrames != null
                ? sink as IWorldSettledTimelineSink ?? throw new InvalidOperationException("Missing settled timeline sink.")
                : null;
            timeline?.BindSettledTimeline();
            if (!IsRegistered(world)) return;
            _contexts[world.Id] = new ClientPredictionWorldContext
            {
                Bindings =
                {
                    World = world,
                    InputSink = sink,
                    SettledInputSink = _useSettledExecutionInputs ? sink as IWorldSettledInputSink : null,
                    SettledTimeline = timeline,
                    FrameTime = frameTime,
                    Rollback = rollback,
                    ComputeHash = computeHash,
                    Reconciler = reconciler,
                },
                Progress =
                {
                    ConfirmedFrame = new FrameIndex(0),
                    PredictedFrame = new FrameIndex(0),
                    CaptureCounter = 0,
                    LastProcessedRollbackFrame = new FrameIndex(0),
                },
                Inputs =
                {
                    RawPredictionInputs = timeline != null ? new InputHistoryRingBuffer(_bufferOptions.InputHistoryCapacity) : null,
                    PendingLocalInputs = new SortedDictionary<int, List<LocalPlayerInputEvent>>(),
                    AppliedInputs = _bufferOptions.Has(ClientPredictionDriverBufferFeatures.AppliedInputHistory)
                        ? new InputHistoryRingBuffer(_bufferOptions.InputHistoryCapacity)
                        : null,
                    AuthoritativeInputs = _bufferOptions.Has(ClientPredictionDriverBufferFeatures.AuthoritativeInputHistory)
                        ? new InputHistoryRingBuffer(_bufferOptions.InputHistoryCapacity)
                        : null,
                },
                Reconciliation =
                {
                    PredictedHashes = predictedHashes,
                    AuthoritativeHashes = authoritativeHashes,
                    ComparedAuthoritativeHashes = comparedAuthoritativeHashes,
                    ReconcileEnabled = reconciler != null && computeHash != null && rollback != null,
                },
                Replay =
                {
                    Mode = ClientPredictionReplayMode.Normal,
                    ReplayTo = new FrameIndex(0),
                    LastRollbackFrame = new FrameIndex(-1),
                    LastMissingAppliedHistoryFrame = new FrameIndex(-1),
                    LastReplayWaitTargetFrame = -1,
                },
            };
        }

        /// <summary>
        /// 设置指定世界的对账开关；未找到记录时沿用对当前登记世界的统一设置。
        /// </summary>
        public void SetReconcileEnabled(WorldId worldId, bool enabled)
        {
            if (_contexts.TryGetValue(worldId, out var ctx))
            {
                if (IsRegistered(ctx.Bindings.World)) ctx.Reconciliation.ReconcileEnabled = enabled;
            }
            else
            {
                foreach (var kv in _contexts)
                {
                    if (IsRegistered(kv.Value.Bindings.World)) kv.Value.Reconciliation.ReconcileEnabled = enabled;
                }
            }
        }

        /// <summary>
        /// 查询当前登记世界的对账开关。
        /// </summary>
        public bool TryGetReconcileEnabled(WorldId worldId, out bool enabled)
        {
            if (TryGetActiveContext(worldId, out var ctx))
            {
                enabled = ctx.Reconciliation.ReconcileEnabled;
                return true;
            }

            enabled = false;
            return false;
        }

        /// <summary>
        /// 记录收到的权威哈希；只对当前登记且启用对账的世界执行比较。
        /// </summary>
        public void OnAuthoritativeStateHash(WorldId worldId, FrameIndex frame, WorldStateHash hash)
        {
            _state.TotalAuthoritativeHashReceived++;
            _state.LastAuthoritativeHashFrame = frame;
            _state.LastAuthoritativeHash = hash;

            if (!TryGetActiveContext(worldId, out var ctx)) return;
            if (ctx.Bindings.Reconciler == null)
            {
                _state.TotalAuthoritativeHashIgnoredNoReconciler++;
                return;
            }

            if (!ctx.Reconciliation.ReconcileEnabled) return;

            if (ctx.Reconciliation.AuthoritativeHashes != null)
            {
                ctx.Reconciliation.AuthoritativeHashes.Store(frame, hash);
            }

            _state.LastReconcileComparedFrame = frame;

            if (ctx.Reconciliation.PredictedHashes == null || !ctx.Reconciliation.PredictedHashes.TryGet(frame, out var predictedAtFrame))
            {
                _state.TotalAuthoritativeHashSkippedNoPredictedHash++;
                // 等该帧预测哈希记录完成后再重试比对。
                return;
            }
            else
            {
                _state.LastReconcilePredictedHash = predictedAtFrame;
            }

            MarkAuthoritativeHashCompared(ctx, frame, hash);
            if (!ctx.Bindings.Reconciler.OnAuthoritativeHash(frame, hash)) return;

            _state.TotalReconcileMismatch++;
            _state.LastReconcileMismatchFrame = frame;
            _state.LastReconcileAuthoritativeHash = hash;
        }

        private void RequestReconcileRollback(WorldId worldId, FrameIndex mismatchFrame)
        {
            if (!TryGetActiveContext(worldId, out var ctx)) return;
            if (!_enableRollback || ctx.Bindings.Rollback == null || ctx.Progress.RollbackFaulted) return;

            // 避免回滚风暴（例如调试强制不一致），并避免在回放期间重入。
            if (ctx.Replay.Mode == ClientPredictionReplayMode.Replaying) return;
            if (!ShouldRequestReconcileRollback(ctx.Replay.LastRollbackFrame, mismatchFrame)) return;

            var rollbackFrame = new FrameIndex(mismatchFrame.Value - 1);
            var ok = TryRestoreState(ctx, rollbackFrame);
            if (!ok)
            {
                _state.TotalRollbackRestoreFailed++;
                return;
            }

            _state.TotalRollbackCount++;
            ctx.Replay.Mode = ClientPredictionReplayMode.Replaying;
            ctx.Replay.ReplayTo = ctx.Progress.PredictedFrame;
            ctx.Progress.ConfirmedFrame = rollbackFrame;
            ctx.Progress.PredictedFrame = rollbackFrame;
            ctx.Replay.LastRollbackFrame = rollbackFrame;
            ctx.Progress.LastProcessedRollbackFrame = rollbackFrame;
            ctx.Replay.LastReplayWaitTargetFrame = -1;
            BeginInputReplay(ctx, rollbackFrame, ctx.Replay.ReplayTo);

            _state.IsReplaying = true;
            _state.ReplayToFrame = ctx.Replay.ReplayTo;
            _state.LastRollbackFrame = rollbackFrame;
        }

        private static bool ShouldRequestReconcileRollback(
            FrameIndex lastRollbackFrame,
            FrameIndex mismatchFrame)
        {
            var rollbackFrame = new FrameIndex(mismatchFrame.Value - 1);
            return lastRollbackFrame.Value < rollbackFrame.Value;
        }


        private void OnWorldDestroyed(WorldId worldId)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && !IsRegistered(ctx.Bindings.World))
            {
                _contexts.Remove(worldId);
                EndInputReplay(ctx);
            }
        }

        private bool IsRegistered(IWorld world)
        {
            return _runtime != null && _runtime.TryGetWorld(world.Id, out var current) && ReferenceEquals(current, world);
        }

        private bool TryGetActiveContext(WorldId worldId, out ClientPredictionWorldContext context)
        {
            if (_contexts.TryGetValue(worldId, out var candidate) && IsRegistered(candidate.Bindings.World))
            {
                context = candidate;
                return true;
            }

            context = null!;
            return false;
        }

        private void PruneDetachedContexts()
        {
            foreach (var entry in _contexts)
            {
                if (!IsRegistered(entry.Value.Bindings.World)) _detachedContexts.Add(entry.Key);
            }

            foreach (var worldId in _detachedContexts) _contexts.Remove(worldId);
            _detachedContexts.Clear();
        }

        private void OnPreTick(float deltaTime)
        {
            if (_runtime == null) return;

            PruneDetachedContexts();
            _state.ShouldRunWorldTick = false;
            _state.LastConsumedConfirmedFrames = 0;
            _state.LastConsumedPredictedFrames = 0;

            _state.IsPredictionStalledByWindow = false;
            _state.IsPredictionStalledByIdealFrame = false;

            _state.IsReplaying = false;
            _state.ReplayToFrame = default;
            _state.LastRollbackFrame = default;

            _state.LastReconcileMismatchFrame = default;
            _state.LastReconcilePredictedHash = default;
            _state.LastReconcileAuthoritativeHash = default;
            foreach (var kv in _contexts)
            {
                var worldId = kv.Key;
                var ctx = kv.Value;
                if (!IsRegistered(ctx.Bindings.World) || ctx.Bindings.InputSink == null) continue;
                if (!ClientPredictionTickCoordinator.Prepare(ctx, _state)) continue;

                var remote = _resolveRemoteInputs != null ? _resolveRemoteInputs(worldId) : null;
                var timelineSource = _resolveSettledFrames?.Invoke(worldId);
                var local = _resolveLocalInputs != null ? _resolveLocalInputs(worldId) : null;
                var ideal = _resolveIdealFrameLimit != null ? _resolveIdealFrameLimit(worldId) : 0;
                var settings = new ClientPredictionTickSettings(_inputDelayFrames, _tuningMinPredictionWindow,
                    _tuningMaxPredictionAheadFrames, _tuningBacklogEwmaAlpha, _enableRollback, _maxLocalDelayQueueDepth);
                ClientPredictionTickCoordinator.Advance(ctx, _state, remote, timelineSource, local, ideal, settings, deltaTime);
            }
        }

        private static void AlignFrameTime(IFrameTimeAligner frameTime, FrameIndex targetFrame, float fallbackDeltaTime)
        {
            ClientPredictionInputExecution.AlignFrameTime(frameTime, targetFrame, fallbackDeltaTime);
        }

        private static void AlignFrameTimeToBaseline(IFrameTimeAligner frameTime, FrameIndex baselineFrame)
        {
            ClientPredictionInputExecution.AlignFrameTimeToBaseline(frameTime, baselineFrame);
        }

        private void OnPostTick(float deltaTime)
        {
            if (_runtime == null || !_state.ShouldRunWorldTick) return;

            foreach (var kv in _contexts)
            {
                var ctx = kv.Value;
                if (!IsRegistered(ctx.Bindings.World)) continue;
                if (ctx.Bindings.World?.Services?.TryResolve<IWorldFrameGuard>(out var postGuard) == true && !postGuard.CanCommit)
                {
                    ctx.Bindings.PendingSettlement = null;
                    ClearPendingTimelineFrame(ctx);
                    ctx.Progress.PredictedFrame = ctx.Progress.PredictedBeforeTick;
                    ctx.Progress.ConfirmedFrame = ctx.Progress.ConfirmedBeforeTick;
                    ctx.Progress.RollbackFaulted = true;
                    continue;
                }
                if (!_enableRollback || ctx.Bindings.Rollback == null) continue;
                var captureFrame = ctx.Inputs.PendingSettledFrame?.Frame ?? ctx.Progress.PredictedFrame;
                if (!ShouldProcessRollbackFrame(captureFrame, ctx.Progress.LastProcessedRollbackFrame)) continue;

                ctx.Progress.LastProcessedRollbackFrame = captureFrame;
                ctx.Progress.CaptureCounter++;
                if (ctx.Progress.CaptureCounter % _rollbackCaptureEveryNFrames != 0) continue;

                try
                {
                    if (!ctx.Bindings.Rollback.TryCaptureAndStore(captureFrame, out var captureResult))
                    {
                        ctx.Bindings.PendingSettlement = null;
                        ClearPendingTimelineFrame(ctx);
                        if (ctx.Bindings.SettledInputSink != null) ctx.Progress.PredictedFrame = ctx.Progress.PredictedBeforeTick;
                        ctx.Progress.RollbackFaulted = true;
                        Log.Warning($"[ClientPredictionDriverModule] Rollback capture failed; world tick paused. result={captureResult}");
                        continue;
                    }

                    if (ctx.Bindings.PendingSettlement != null)
                    {
                        var source = ctx.Bindings.PendingSettlement;
                        ctx.Bindings.PendingSettlement = null;
                        var frame = ctx.Progress.PredictedFrame;
                        if (!ctx.Inputs.AppliedInputs.TryGet(frame, out var simulated) ||
                            !source.TryGet(frame.Value, out var available) || available == null ||
                            !InputsEqual(simulated, available) ||
                            !source.TryConsume(frame.Value, out var committed) || committed == null ||
                            !InputsEqual(simulated, committed))
                        {
                            ctx.Progress.RollbackFaulted = true;
                            ctx.Progress.PredictedFrame = ctx.Progress.PredictedBeforeTick;
                            continue;
                        }
                        ctx.Inputs.AuthoritativeInputs?.Store(frame, committed);
                        ctx.Progress.ConfirmedFrame = frame;
                        _state.LastConsumedConfirmedFrames = 1;
                        _state.TotalConsumedConfirmedFrames++;
                    }

                    if (ctx.Inputs.PendingSettledFrame != null)
                    {
                        CommitTimelineFrame(ctx, _state, ctx.Bindings.PendingSettledSource, ctx.Inputs.PendingSettledFrame, ctx.Bindings.PendingSettledUpdate);
                        ClearPendingTimelineFrame(ctx);
                    }

                    if (ctx.Bindings.ComputeHash != null && ctx.Bindings.Reconciler != null)
                    {
                        if (!ctx.Reconciliation.ReconcileEnabled) continue;
                        var hash = ctx.Bindings.ComputeHash(ctx.Progress.PredictedFrame);
                        ctx.Bindings.Reconciler.RecordPredictedHash(ctx.Progress.PredictedFrame, hash);
                        _state.TotalPredictedHashRecorded++;

                        // 如果该帧权威哈希已提前到达，现在执行比对。
                        if (ctx.Reconciliation.AuthoritativeHashes != null && ctx.Reconciliation.AuthoritativeHashes.TryGet(ctx.Progress.PredictedFrame, out var authAtFrame))
                        {
                            if (WasAuthoritativeHashCompared(ctx, ctx.Progress.PredictedFrame, authAtFrame))
                                continue;

                            _state.LastReconcileComparedFrame = ctx.Progress.PredictedFrame;
                            _state.LastReconcilePredictedHash = hash;
                            MarkAuthoritativeHashCompared(ctx, ctx.Progress.PredictedFrame, authAtFrame);
                            if (ctx.Bindings.Reconciler.OnAuthoritativeHash(ctx.Progress.PredictedFrame, authAtFrame))
                            {
                                _state.TotalReconcileMismatch++;
                                _state.LastReconcileMismatchFrame = ctx.Progress.PredictedFrame;
                                _state.LastReconcileAuthoritativeHash = authAtFrame;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ctx.Inputs.PendingSettledFrame != null)
                    {
                        ctx.Progress.PredictedFrame = ctx.Progress.PredictedBeforeTick;
                        ClearPendingTimelineFrame(ctx);
                    }
                    ctx.Progress.RollbackFaulted = true;
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

        private static bool WasAuthoritativeHashCompared(
            ClientPredictionWorldContext ctx,
            FrameIndex frame,
            WorldStateHash hash)
        {
            return ctx.Reconciliation.ComparedAuthoritativeHashes != null &&
                   ctx.Reconciliation.ComparedAuthoritativeHashes.TryGet(frame, out var compared) &&
                   compared.Value == hash.Value;
        }

        private static void MarkAuthoritativeHashCompared(
            ClientPredictionWorldContext ctx,
            FrameIndex frame,
            WorldStateHash hash)
        {
            ctx.Reconciliation.ComparedAuthoritativeHashes?.Store(frame, hash);
        }
    }
}
