using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 从 <c>GameFlowDomain</c> 提取的 Battle 作用域管理器。
    /// 负责 per-battle 的 BattleWorldScope 管理、BattleSessionFeature 创建、事件处理和 session 推进逻辑。
    /// 纯逻辑，零 Unity 依赖，可独立测试。
    /// </summary>
    internal sealed class BattleScopeManager
    {
        /// <summary>
        /// BattleScopeManager 需要的回调集合，由 Domain 提供。
        /// </summary>
        internal sealed class Callbacks
        {
            /// <summary>设置 _battleRequested 标志。</summary>
            public Action<bool> SetBattleRequested { get; set; }

            /// <summary>入队 Root 事件，用于触发 HFSM 根状态转移。</summary>
            public Action<MobaRootEvent> EnqueueRootEvent { get; set; }

            /// <summary>触发 Battle 状态机事件，内部做 null 守卫。</summary>
            public Action<MobaBattleEvent> TriggerBattleFsm { get; set; }

            /// <summary>获取当前活跃 Battle 状态。</summary>
            public Func<MobaBattleState> GetActiveBattle { get; set; }

            /// <summary>清除瞬态 gateway 连接工厂。</summary>
            public Action ClearGatewayConnectionFactory { get; set; }

            /// <summary>获取瞬态 gateway 连接工厂，仅在 AttachBattleFeatures 期间有效。</summary>
            public Func<Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection>> GetGatewayConnectionFactory { get; set; }

            /// <summary>创建 Battle session feature，由 runtime 层提供具体实现。</summary>
            public Func<IBattleBootstrapper, Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection>, IBattleSessionFeature> CreateBattleSessionFeature { get; set; }
        }

        private readonly Callbacks _callbacks;
        private readonly BattleWorldScopeHost _battleWorldScope;
        private readonly MobaBattleAdvanceDecider _advanceDecider;
        private readonly ILogSink _log;

        private IBattleSessionFeature _battleSessionFeature;
        private Action _sessionStartedHandler;
        private Action _firstFrameReceivedHandler;
        private Action<Exception> _sessionFailedHandler;
        private Action _assetsLoadCompletedHandler;

        internal BattleScopeManager(
            Callbacks callbacks,
            BattleWorldScopeHost battleWorldScope,
            MobaBattleAdvanceDecider advanceDecider,
            ILogSink log)
        {
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            _battleWorldScope = battleWorldScope ?? throw new ArgumentNullException(nameof(battleWorldScope));
            _advanceDecider = advanceDecider ?? throw new ArgumentNullException(nameof(advanceDecider));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        // --- Battle 作用域生命周期 ---

        public void EnterBattle(IBattleBootstrapper bootstrapper)
        {
            _callbacks.SetBattleRequested(true);
            // Per-battle inputs are seeded before the root transition is evaluated.
            // A bootstrapper may override the module's local/default gate policy by implementing
            // IFlowGateProvider; seeded services remain caller-owned.
            if (bootstrapper != null)
            {
                _battleWorldScope.BeginBattle(seeder =>
                {
                    seeder.Seed<IBattleBootstrapper>(bootstrapper);
                    if (bootstrapper is IFlowGateProvider gates)
                    {
                        seeder.Seed<IFlowGateProvider>(gates);
                    }
                });
            }
            else
            {
                _battleWorldScope.BeginBattle();
            }
            _callbacks.EnqueueRootEvent(MobaRootEvent.EnterBattle);
        }

        public void ReturnToBoot()
        {
            _callbacks.SetBattleRequested(false);
            _callbacks.ClearGatewayConnectionFactory();
            _callbacks.EnqueueRootEvent(MobaRootEvent.ReturnLobby);
        }

        internal void EndBattleScope()
        {
            _battleWorldScope.EndBattle();
        }

        // --- BattleSessionFeature 工厂 ---

        internal IBattleSessionFeature CreateBattleSessionFeature()
        {
            ClearBattleSessionEvents();

            // bootstrapper 从 per-battle scope 取回；取不到则传 null，与迁移前 _pendingBootstrapper 为 null 行为等价。
            _battleWorldScope.TryResolve<IBattleBootstrapper>(out var bootstrapper);
            var generation = _battleWorldScope.ScopeGeneration;
            _battleSessionFeature = _callbacks.CreateBattleSessionFeature(bootstrapper, _callbacks.GetGatewayConnectionFactory());
            _sessionStartedHandler = () => OnBattleSessionStarted(generation);
            _firstFrameReceivedHandler = () => OnBattleFirstFrameReceived(generation);
            _sessionFailedHandler = ex => OnBattleSessionFailed(generation, ex);
            _assetsLoadCompletedHandler = () => OnBattleAssetsLoadCompleted(generation);
            _battleSessionFeature.SessionStarted += _sessionStartedHandler;
            _battleSessionFeature.FirstFrameReceived += _firstFrameReceivedHandler;
            _battleSessionFeature.SessionFailed += _sessionFailedHandler;
            _battleSessionFeature.AssetsLoadCompleted += _assetsLoadCompletedHandler;
            return _battleSessionFeature;
        }

        internal void ClearBattleSessionEvents()
        {
            if (_battleSessionFeature != null)
            {
                if (_sessionStartedHandler != null)
                    _battleSessionFeature.SessionStarted -= _sessionStartedHandler;
                if (_firstFrameReceivedHandler != null)
                    _battleSessionFeature.FirstFrameReceived -= _firstFrameReceivedHandler;
                if (_sessionFailedHandler != null)
                    _battleSessionFeature.SessionFailed -= _sessionFailedHandler;
                if (_assetsLoadCompletedHandler != null)
                    _battleSessionFeature.AssetsLoadCompleted -= _assetsLoadCompletedHandler;
            }

            _battleSessionFeature = null;
            _sessionStartedHandler = null;
            _firstFrameReceivedHandler = null;
            _sessionFailedHandler = null;
            _assetsLoadCompletedHandler = null;
        }

        // --- Session 事件处理 ---

        internal void OnBattleSessionStarted()
        {
            OnBattleSessionStarted(_battleWorldScope.ScopeGeneration);
        }

        private void OnBattleSessionStarted(int generation)
        {
            if (!IsCurrentBattleGeneration(generation, nameof(IBattleSessionFeature.SessionStarted))) return;

            _battleWorldScope.Resolve<IBattleRuntimeState>().SessionStarted = true;
            _log.Info($"[BattleScopeManager] SessionStarted, activeBattle={_callbacks.GetActiveBattle()}");
            var next = _advanceDecider.OnSessionStarted(_callbacks.GetActiveBattle());
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        internal void OnBattleFirstFrameReceived()
        {
            OnBattleFirstFrameReceived(_battleWorldScope.ScopeGeneration);
        }

        private void OnBattleFirstFrameReceived(int generation)
        {
            if (!IsCurrentBattleGeneration(generation, nameof(IBattleSessionFeature.FirstFrameReceived))) return;

            _battleWorldScope.Resolve<IBattleRuntimeState>().FirstFrameReceived = true;
            _log.Info($"[BattleScopeManager] FirstFrameReceived, activeBattle={_callbacks.GetActiveBattle()}");
            var next = _advanceDecider.OnFirstFrameReceived(_callbacks.GetActiveBattle());
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        internal void OnBattleSessionFailed(Exception ex)
        {
            OnBattleSessionFailed(_battleWorldScope.ScopeGeneration, ex);
        }

        private void OnBattleSessionFailed(int generation, Exception ex)
        {
            if (!IsCurrentBattleGeneration(generation, nameof(IBattleSessionFeature.SessionFailed))) return;

            // 当前契约中 runtime state 仅跟踪 session start / first frame
            _log.Error($"[BattleScopeManager] Battle session failed: {ex}");
            var next = _advanceDecider.OnSessionFailed(_callbacks.GetActiveBattle());
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        /// <summary>
        /// 阶段 7a：真实资源加载完成（manifest barrier）。
        /// 由 BattleSessionFeature.AssetsLoadCompleted 事件或 <see cref="NotifyAssetsLoadCompleted"/> 触发，
        /// 仅在 LoadAssets 状态推进为 AssetsLoadCompleted。
        /// </summary>
        internal void OnBattleAssetsLoadCompleted()
        {
            OnBattleAssetsLoadCompleted(_battleWorldScope.ScopeGeneration);
        }

        private void OnBattleAssetsLoadCompleted(int generation)
        {
            if (!IsCurrentBattleGeneration(generation, nameof(IBattleSessionFeature.AssetsLoadCompleted))) return;

            _battleWorldScope.Resolve<IBattleRuntimeState>().AssetsLoadCompleted = true;
            _log.Info($"[BattleScopeManager] AssetsLoadCompleted, activeBattle={_callbacks.GetActiveBattle()}");
            var next = _advanceDecider.OnAssetsLoadCompleted(_callbacks.GetActiveBattle());
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        /// <summary>
        /// 阶段 7a：供外部（资源加载协调器）在 manifest barrier 通过后调用，
        /// 推进 LoadAssets → InMatch。首帧不再代表资源加载完成。
        /// </summary>
        internal void NotifyAssetsLoadCompleted()
        {
            OnBattleAssetsLoadCompleted();
        }

        // --- 抽取的推进判断 ---

        internal void TryAdvanceOnConnectEnter()
        {
            var state = _battleWorldScope.Resolve<IBattleRuntimeState>();
            var next = _advanceDecider.OnStateEntered(MobaBattleState.Connect, state.SessionStarted, state.FirstFrameReceived);
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        internal void TryAdvanceOnCreateOrJoinWorldEnter()
        {
            var state = _battleWorldScope.Resolve<IBattleRuntimeState>();
            var next = _advanceDecider.OnStateEntered(MobaBattleState.CreateOrJoinWorld, state.SessionStarted, state.FirstFrameReceived);
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        internal void TryAdvanceOnLoadAssetsEnter()
        {
            var state = _battleWorldScope.Resolve<IBattleRuntimeState>();
            var next = _advanceDecider.OnStateEntered(
                MobaBattleState.LoadAssets,
                state.SessionStarted,
                state.FirstFrameReceived,
                state.AssetsLoadCompleted);
            if (next.HasValue) _callbacks.TriggerBattleFsm(next.Value);
        }

        internal void ResetBattleSessionRuntimeState()
        {
            _battleWorldScope.Resolve<IBattleRuntimeState>().Reset();
        }

        internal void ReturnLobbyAfterBattleEnd()
        {
            _callbacks.SetBattleRequested(false);
            _callbacks.ClearGatewayConnectionFactory();
            _callbacks.EnqueueRootEvent(MobaRootEvent.ReturnLobby);
        }

        private bool IsCurrentBattleGeneration(int generation, string eventName)
        {
            if (_battleWorldScope.HasActiveScope &&
                _battleWorldScope.ScopeGeneration == generation)
            {
                return true;
            }

            _log.Info(
                $"[BattleScopeManager] Ignored stale {eventName}: " +
                $"eventGeneration={generation}, currentGeneration={_battleWorldScope.ScopeGeneration}, " +
                $"hasActiveScope={_battleWorldScope.HasActiveScope}");
            return false;
        }
    }
}


