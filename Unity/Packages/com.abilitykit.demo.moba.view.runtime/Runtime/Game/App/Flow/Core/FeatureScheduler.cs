using AbilityKit.Game.View.Flow;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using AbilityKit.Ability.Flow;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
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
        internal sealed class Callbacks
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
        }

        private readonly ILogSink _log;
        private readonly Callbacks _callbacks;
        private readonly GamePhaseContext _ctx;
        private readonly MobaFlowConfiguration _flowConfig;

        internal readonly PhaseFeatureHost<GamePhaseContext, IGamePhaseFeature> Features;
        internal readonly PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> BootFeaturePlan;
        internal readonly PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> BattleFeaturePlan;
        internal readonly PhaseStateFeatureRegistry<MobaRootState, GamePhaseContext, IGamePhaseFeature> RootStateBindings;
        internal readonly PhaseStateFeatureRegistry<MobaBattleState, GamePhaseContext, IGamePhaseFeature> BattleStateBindings;

        internal FeatureScheduler(
            ILogSink log,
            Callbacks callbacks,
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
                tickFeature: TickFeatureCore);
            Features.AttachAll(in _ctx);

            BootFeaturePlan = bootFeaturePlan ?? throw new ArgumentNullException(nameof(bootFeaturePlan));
            BattleFeaturePlan = battleFeaturePlan ?? throw new ArgumentNullException(nameof(battleFeaturePlan));
            RootStateBindings = BuildMobaRootStateBindings(rootBindingCapacity);
            BattleStateBindings = BuildMobaBattleStateBindings(battleBindingCapacity);
        }

        // --- Feature 核心回调 ---

        private void AttachFeatureCore(IGamePhaseFeature feature, in GamePhaseContext ctx)
        {
            feature.OnAttach(ctx);
            try
            {
                _callbacks.FeatureBinderAttach(feature);
            }
            catch (Exception attachException)
            {
                try
                {
                    feature.OnDetach(ctx);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "Feature binder attach failed and feature rollback encountered an error.",
                        attachException,
                        rollbackException);
                }

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
                _callbacks.FeatureBinderDetach(feature);
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
            try
            {
                feature.Tick(ctx, deltaTime);
            }
            catch (Exception ex)
            {
                _log.Exception(ex, $"[FeatureScheduler] Feature.Tick failed: feature={feature?.GetType().FullName}");
            }
        }

        // --- 外部调度 ---

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            Features.Tick(in ctx, deltaTime);
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

        public void ClearFeatures()
        {
            Features.Clear(in _ctx);
            Features.AttachAll(in _ctx);

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
            return new PhaseStateFeatureRegistry<MobaBattleState, GamePhaseContext, IGamePhaseFeature>(
                    message => _log.Error($"[FeatureScheduler] {message}"),
                    initialCapacity: capacity)
                .Add(MobaBattleState.Prepare, BuildBattlePrepareBinding())
                .Add(MobaBattleState.Connect, BuildBattleDebugBinding(_flowConfig.BattleConnectFeatures))
                .Add(MobaBattleState.CreateOrJoinWorld, BuildBattleDebugBinding(_flowConfig.BattleCreateOrJoinWorldFeatures))
                .Add(MobaBattleState.LoadAssets, BuildBattleDebugBinding(_flowConfig.BattleLoadAssetsFeatures))
                .Add(MobaBattleState.InMatch, BuildBattleInMatchBinding())
                .Add(MobaBattleState.End, BuildBattleEndBinding());
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

        private PhaseStateFeatureBinding<GamePhaseContext, IGamePhaseFeature> BuildBattlePrepareBinding()
        {
            return PhaseStateFeatureBindingFactory.Create<GamePhaseContext, IGamePhaseFeature>(
                _flowConfig.BattlePrepareFeatures,
                AttachFeature,
                BattleFeaturePlan,
                clear: (in GamePhaseContext ctx) => ClearFeatures(),
                enterBeforeAction: ExecuteFlowActionWrapper,
                exitAction: ExecuteFlowActionWrapper,
                fail: message => _log.Error($"[FeatureScheduler] {message}"));
        }

        private PhaseStateFeatureBinding<GamePhaseContext, IGamePhaseFeature> BuildBattleDebugBinding(PhaseStateFeatureSpec spec)
        {
            return PhaseStateFeatureBindingFactory.Create<GamePhaseContext, IGamePhaseFeature>(
                spec,
                AttachFeature,
                BattleFeaturePlan,
                exitAction: ExecuteFlowActionWrapper,
                fail: message => _log.Error($"[FeatureScheduler] {message}"),
                switchFlowAction: ExecuteSwitchFlowWrapper);
        }

        private PhaseStateFeatureBinding<GamePhaseContext, IGamePhaseFeature> BuildBattleInMatchBinding()
        {
            return PhaseStateFeatureBindingFactory.Create<GamePhaseContext, IGamePhaseFeature>(
                _flowConfig.BattleInMatchFeatures,
                AttachFeature,
                BattleFeaturePlan,
                exitAction: ExecuteFlowActionWrapper,
                fail: message => _log.Error($"[FeatureScheduler] {message}"));
        }

        private PhaseStateFeatureBinding<GamePhaseContext, IGamePhaseFeature> BuildBattleEndBinding()
        {
            return PhaseStateFeatureBindingFactory.Create<GamePhaseContext, IGamePhaseFeature>(
                _flowConfig.BattleEndFeatures,
                AttachFeature,
                BattleFeaturePlan,
                clear: (in GamePhaseContext ctx) => ClearFeatures(),
                enterAfterAction: ExecuteFlowActionWrapper,
                exitAction: ExecuteFlowActionWrapper,
                fail: message => _log.Error($"[FeatureScheduler] {message}"));
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

