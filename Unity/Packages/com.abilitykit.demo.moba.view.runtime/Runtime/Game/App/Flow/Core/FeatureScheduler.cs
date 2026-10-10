using AbilityKit.Game.View.Flow;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using AbilityKit.Ability.Flow;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
    internal interface IFeatureSchedulerHost
    {
        void AttachFeatureBinding(object feature);
        void DetachFeatureBinding(object feature);
        void ClearBattleSessionEvents();
        void ExecuteFlowAction(string actionId, int installedCount);
        void ExecuteSwitchFlow(string switchFlowId, int installedCount);
    }

    /// <summary>
    /// 从 <c>GameFlowDomain</c> 提取的 Feature 调度器（Step 4.7b）。
    /// 负责 PhaseFeatureHost 管理、Plan/Registry 构建、Attach/Detach/Tick/Clear 调度。
    /// 纯逻辑，零 Unity 依赖，可独立测试。
    /// </summary>
    internal sealed class FeatureScheduler
    {
        /// <summary>
        /// FeatureScheduler 需要的回调集合，由 Domain 提供。
        /// </summary>
        internal sealed class Callbacks : IFeatureSchedulerHost
        {
            /// <summary>Feature 挂载到实体绑定器（IFeatureBinder.AttachFeature）。</summary>
            public Action<object> FeatureBinderAttach { get; set; }

            /// <summary>Feature 从实体绑定器卸载（IFeatureBinder.DetachFeature）。</summary>
            public Action<object> FeatureBinderDetach { get; set; }

            /// <summary>清理 BattleSessionFeature 事件订阅（Domain 负责取消订阅 + 置空字段）。</summary>
            public Action ClearBattleSessionEvents { get; set; }

            /// <summary>执行 flow action（Domain 负责 _actionExecutor + MobaFlowActionContext）。</summary>
            public Action<string, int> ExecuteFlowAction { get; set; }

            /// <summary>执行 switch flow action（Domain 负责 _switchExecutor + MobaFlowActionContext）。</summary>
            public Action<string, int> ExecuteSwitchFlow { get; set; }

            void IFeatureSchedulerHost.AttachFeatureBinding(object feature) => FeatureBinderAttach(feature);
            void IFeatureSchedulerHost.DetachFeatureBinding(object feature) => FeatureBinderDetach(feature);
            void IFeatureSchedulerHost.ClearBattleSessionEvents() => ClearBattleSessionEvents();
            void IFeatureSchedulerHost.ExecuteFlowAction(string actionId, int count) => ExecuteFlowAction(actionId, count);
            void IFeatureSchedulerHost.ExecuteSwitchFlow(string actionId, int count) => ExecuteSwitchFlow(actionId, count);
        }

        private readonly ILogSink _log;
        private readonly IFeatureSchedulerHost _callbacks;
        private readonly GamePhaseContext _ctx;
        private readonly MobaFlowConfiguration _flowConfig;
        private readonly List<IGamePhaseFeature> _failedAttachments = new List<IGamePhaseFeature>();
        private readonly List<List<IGamePhaseFeature>> _battleScopes = new List<List<IGamePhaseFeature>>();
        private readonly List<IGamePhaseFeature> _disableAfterTick = new List<IGamePhaseFeature>();
        private readonly HashSet<IGamePhaseFeature> _reportedTickFailures = new HashSet<IGamePhaseFeature>();

        internal readonly PhaseFeatureHost<GamePhaseContext, IGamePhaseFeature> Features;
        internal readonly PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> BootFeaturePlan;
        internal PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> BattleFeaturePlan;
        internal readonly PhaseStateFeatureRegistry<MobaRootState, GamePhaseContext, IGamePhaseFeature> RootStateBindings;
        internal readonly PhaseStateFeatureRegistry<MobaBattleState, GamePhaseContext, IGamePhaseFeature> BattleStateBindings;

        internal FeatureScheduler(
            ILogSink log,
            IFeatureSchedulerHost callbacks,
            GamePhaseContext ctx,
            MobaFlowConfiguration flowConfig,
            PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> bootFeaturePlan,
            PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> battleFeaturePlan,
            int featureCapacity = 16,
            int rootBindingCapacity = 2,
            int battleBindingCapacity = 6)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            _ctx = ctx;
            _flowConfig = flowConfig ?? throw new ArgumentNullException(nameof(flowConfig));

            Features = new PhaseFeatureHost<GamePhaseContext, IGamePhaseFeature>(
                fail: message => _log.Error($"[FeatureScheduler] PhaseFeatureHost: {message}"),
                initialCapacity: featureCapacity,
                attachFeature: AttachFeatureCore,
                detachFeature: DetachFeatureCore,
                tickFeature: TickFeatureCore,
                detachFeatureAsync: DetachFeatureCoreAsync);
            Features.AttachAll(in _ctx);

            BootFeaturePlan = bootFeaturePlan ?? throw new ArgumentNullException(nameof(bootFeaturePlan));
            BattleFeaturePlan = battleFeaturePlan ?? throw new ArgumentNullException(nameof(battleFeaturePlan));
            RootStateBindings = BuildMobaRootStateBindings(rootBindingCapacity);
            BattleStateBindings = BuildMobaBattleStateBindings(battleBindingCapacity);
        }

        // --- Feature 核心回调 ---

        private void AttachFeatureCore(IGamePhaseFeature feature, in GamePhaseContext ctx)
        {
            try { feature.OnAttach(ctx); }
            catch
            {
                // The feature may have published resources before OnAttach failed.
                _failedAttachments.Add(feature);
                throw;
            }
            try
            {
                _callbacks.AttachFeatureBinding(feature);
            }
            catch
            {
                _failedAttachments.Add(feature);
                throw;
            }
        }

        private void DetachFeatureCore(IGamePhaseFeature feature, in GamePhaseContext ctx)
        {
            Exception featureException = null;
            try
            {
                feature.OnDetach(ctx);
            }
            catch (Exception exception)
            {
                featureException = exception;
            }

            try
            {
                _callbacks.DetachFeatureBinding(feature);
            }
            catch (Exception binderException)
            {
                if (featureException != null)
                {
                    throw new AggregateException(
                        "Feature and binder detach both failed.",
                        featureException,
                        binderException);
                }

                throw;
            }

            if (featureException != null)
            {
                ExceptionDispatchInfo.Capture(featureException).Throw();
            }
        }

        private async Task DetachFeatureCoreAsync(
            IGamePhaseFeature feature,
            GamePhaseContext ctx)
        {
            Exception featureException = null;
            try
            {
                if (feature is IAsyncPhaseFeature<GamePhaseContext> asyncFeature)
                {
                    await (asyncFeature.DetachAsync(ctx) ?? Task.CompletedTask);
                }
                else
                {
                    feature.OnDetach(ctx);
                }
            }
            catch (Exception exception)
            {
                featureException = exception;
            }

            try
            {
                _callbacks.DetachFeatureBinding(feature);
            }
            catch (Exception binderException)
            {
                if (featureException != null)
                {
                    throw new AggregateException(
                        "Feature and binder detach both failed.",
                        featureException,
                        binderException);
                }

                throw;
            }

            if (featureException != null)
            {
                ExceptionDispatchInfo.Capture(featureException).Throw();
            }
        }

        private void TickFeatureCore(IGamePhaseFeature feature, in GamePhaseContext ctx, float deltaTime)
        {
            try { feature.Tick(ctx, deltaTime); }
            catch (Exception ex)
            {
                var mode = (feature as IGamePhaseFeatureTickFailurePolicy)?.TickFailureMode ??
                    GamePhaseFeatureTickFailureMode.FaultFlow;
                if (mode == GamePhaseFeatureTickFailureMode.FaultFlow ||
                    (mode == GamePhaseFeatureTickFailureMode.DisableFeature &&
                     feature is IAsyncPhaseFeature<GamePhaseContext>))
                    throw new InvalidOperationException(
                        $"Feature Tick failed: {feature.GetType().FullName}", ex);
                if (_reportedTickFailures.Add(feature))
                    _log.Exception(ex, $"[FeatureScheduler] Feature.Tick failed: feature={feature.GetType().FullName}");
                if (mode == GamePhaseFeatureTickFailureMode.DisableFeature)
                    _disableAfterTick.Add(feature);
            }
        }

        // --- 外部调度 ---

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            Features.Tick(in ctx, deltaTime);
            for (var i = 0; i < _disableAfterTick.Count; i++)
            {
                var feature = _disableAfterTick[i];
                DetachFeature(feature);
                foreach (var scope in _battleScopes) scope.Remove(feature);
            }
            _disableAfterTick.Clear();
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            Features.OnGUI(in ctx);
        }

        public int AttachBootFeatures()
        {
            return BootFeaturePlan.InstallAll(in _ctx, AttachFeature);
        }

        public int AttachBattleFeatures(
            IReadOnlyList<string> featureIds = null)
        {
            return BattleFeaturePlan.InstallByIdsOrAll(
                featureIds,
                in _ctx,
                AttachFeature,
                message => _log.Error($"[FeatureScheduler] {message}"));
        }

        public void AttachFeature(IGamePhaseFeature feature)
        {
            if (feature == null) throw new ArgumentNullException(nameof(feature));
            Features.Add(feature, in _ctx);
        }

        public void DetachFeature(IGamePhaseFeature feature)
        {
            Features.Remove(feature, in _ctx);
        }

        public Task DetachFeatureAsync(IGamePhaseFeature feature)
        {
            return Features.RemoveAsync(feature, _ctx);
        }

        public void ClearFeatures()
        {
            try
            {
                for (var i = _failedAttachments.Count - 1; i >= 0; i--)
                {
                    DetachFeatureCore(_failedAttachments[i], in _ctx);
                    _failedAttachments.RemoveAt(i);
                }
                for (var i = Features.Features.Count - 1; i >= 0; i--)
                    Features.Remove(Features.Features[i], in _ctx);
            }
            catch (Exception ex) { throw new AggregateException("Feature cleanup failed.", ex); }
            CompleteClear();
        }

        internal void ConfigureBattle(MobaFlowConfiguration configuration,
            PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> plan)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            BattleFeaturePlan = plan;
            _battleScopes.Clear();
            foreach (var state in configuration.BattleMachine.States)
                BattleStateBindings.Set(state, BuildBattleStateBinding(state,
                    configuration.BattleFeatureSpecs[state], configuration.BattleRetainedFeatureIds[state]));
        }

        public async Task ClearFeaturesAsync()
        {
            try
            {
                for (var i = _failedAttachments.Count - 1; i >= 0; i--)
                {
                    await DetachFeatureCoreAsync(_failedAttachments[i], _ctx);
                    _failedAttachments.RemoveAt(i);
                }
                for (var i = Features.Features.Count - 1; i >= 0; i--)
                    await Features.RemoveAsync(Features.Features[i], _ctx);
            }
            catch (Exception ex) { throw new AggregateException("Feature cleanup failed.", ex); }
            CompleteClear();
        }

        private void CompleteClear()
        {
            foreach (var scope in _battleScopes) scope.Clear();
            _disableAfterTick.Clear();
            _reportedTickFailures.Clear();
            _callbacks.ClearBattleSessionEvents();
        }

        // --- State Binding 构建 ---

        private PhaseStateFeatureRegistry<MobaRootState, GamePhaseContext, IGamePhaseFeature> BuildMobaRootStateBindings(int capacity)
        {
            return new PhaseStateFeatureRegistry<MobaRootState, GamePhaseContext, IGamePhaseFeature>(
                    message => _log.Error($"[FeatureScheduler] {message}"),
                    initialCapacity: capacity)
                .Add(MobaRootState.Boot, BuildBootStateBinding(_flowConfig.BootFeatures))
                .Add(MobaRootState.Lobby, BuildBootStateBinding(_flowConfig.LobbyFeatures));
        }

        private PhaseStateFeatureRegistry<MobaBattleState, GamePhaseContext, IGamePhaseFeature> BuildMobaBattleStateBindings(int capacity)
        {
            var bindings = new PhaseStateFeatureRegistry<MobaBattleState, GamePhaseContext, IGamePhaseFeature>(
                message => _log.Error($"[FeatureScheduler] {message}"), initialCapacity: capacity);
            foreach (var state in _flowConfig.BattleMachine.States)
                bindings.Add(state, BuildBattleStateBinding(state, _flowConfig.BattleFeatureSpecs[state],
                    _flowConfig.BattleRetainedFeatureIds[state]));
            return bindings;
        }

        private PhaseStateFeatureBinding<GamePhaseContext, IGamePhaseFeature> BuildBootStateBinding(PhaseStateFeatureSpec spec)
        {
            return PhaseStateFeatureBindingFactory.Create<GamePhaseContext, IGamePhaseFeature>(
                spec,
                AttachFeature,
                BootFeaturePlan,
                clear: (in GamePhaseContext ctx) => ClearFeatures(),
                exitAction: ExecuteFlowActionWrapper,
                fail: message => _log.Error($"[FeatureScheduler] {message}"));
        }

        private PhaseStateFeatureBinding<GamePhaseContext, IGamePhaseFeature> BuildBattleStateBinding(
            MobaBattleState state,
            PhaseStateFeatureSpec spec,
            IReadOnlyList<string> retainedFeatureIds)
        {
            var retained = new HashSet<string>(retainedFeatureIds, StringComparer.Ordinal);
            var scopedFeatures = new List<IGamePhaseFeature>();
            var enteredFeatures = new List<IGamePhaseFeature>();
            _battleScopes.Add(scopedFeatures);
            var installIndex = 0;
            return PhaseStateFeatureBindingFactory.Create<GamePhaseContext, IGamePhaseFeature>(
                spec,
                feature =>
                {
                    if (installIndex >= spec.FeatureIds.Count)
                        throw new InvalidOperationException($"Unexpected feature installation in {spec.StateId}.");
                    var featureId = spec.FeatureIds[installIndex++];
                    AttachFeature(feature);
                    enteredFeatures.Add(feature);
                    if (!retained.Contains(featureId)) scopedFeatures.Add(feature);
                },
                BattleFeaturePlan,
                clear: (in GamePhaseContext ctx) => ClearFeatures(),
                beforeEnter: (in GamePhaseContext ctx) =>
                {
                    installIndex = 0;
                    enteredFeatures.Clear();
                },
                onExit: (in GamePhaseContext ctx) =>
                {
                    List<Exception> failures = null;
                    for (var i = scopedFeatures.Count - 1; i >= 0; i--)
                    {
                        try { DetachFeature(scopedFeatures[i]); }
                        catch (Exception ex) { (failures ??= new List<Exception>()).Add(ex); }
                    }
                    scopedFeatures.Clear();
                    enteredFeatures.Clear();
                    if (failures != null)
                        throw new AggregateException($"Failed to detach {spec.StateId} features.", failures);
                },
                enterBeforeAction: ExecuteFlowActionWrapper,
                enterAfterAction: ExecuteFlowActionWrapper,
                exitAction: ExecuteFlowActionWrapper,
                fail: message => _log.Error($"[FeatureScheduler] {message}"),
                switchFlowAction: ExecuteSwitchFlowWrapper,
                onEnterFailure: (in GamePhaseContext ctx) =>
                {
                    List<Exception> failures = null;
                    for (var i = enteredFeatures.Count - 1; i >= 0; i--)
                    {
                        var feature = enteredFeatures[i];
                        // Asynchronous teardown is completed by the domain's fault shutdown.
                        if (feature is IAsyncPhaseFeature<GamePhaseContext>) continue;
                        try { DetachFeature(feature); }
                        catch (Exception ex) { (failures ??= new List<Exception>()).Add(ex); }
                    }
                    enteredFeatures.Clear();
                    scopedFeatures.Clear();
                    if (failures != null)
                        throw new AggregateException($"Failed to roll back {spec.StateId}.", failures);
                });
        }

        // --- Flow Action 包装 ---

        private void ExecuteFlowActionWrapper(in GamePhaseContext ctx, string actionId)
        {
            _callbacks.ExecuteFlowAction(actionId, 0);
        }

        private void ExecuteFlowActionWrapper(in GamePhaseContext ctx, string actionId, int installedCount)
        {
            _callbacks.ExecuteFlowAction(actionId, installedCount);
        }

        private void ExecuteSwitchFlowWrapper(in GamePhaseContext ctx, string switchFlowId, int installedCount)
        {
            _callbacks.ExecuteSwitchFlow(switchFlowId, installedCount);
        }
    }
}

