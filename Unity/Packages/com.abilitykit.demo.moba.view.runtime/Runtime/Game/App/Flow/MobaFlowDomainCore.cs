using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Demo.Moba.View.Settings;
using AbilityKit.Core.Logging;
using AbilityKit.Game.View.Flow;
using AbilityKit.World.ECS;
using UnityHFSM;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaFlowDomainCore : IMobaFlowActionTarget, IFlowCommandSink, IGameFlowFeatureInstaller
    {
        private readonly IGameFlowRuntimeServices _runtime;
        private readonly IMobaFeatureFactoryProvider _featureFactoryProvider;
        private readonly IBattleSessionFeatureFactory _battleSessionFeatureFactory;
        private readonly ILogSink _log;
        private readonly IPresentationSink _presentationSink;
        private readonly GamePhaseContext _ctx;
        private readonly MobaFlowConfiguration _flowConfig;
        private readonly MobaFlowConditionResolver _conditionResolver;
        private readonly MobaFlowActionExecutor _actionExecutor;
        private readonly MobaFlowSwitchExecutor _switchExecutor;
        private readonly BattleWorldScopeHost _battleWorldScope = new BattleWorldScopeHost();
        private readonly MobaBattleAdvanceDecider _advanceDecider = new MobaBattleAdvanceDecider();
        private readonly FlowConditionContextBuilder _flowConditionContextBuilder;
        private readonly FeatureScheduler _featureScheduler;
        private readonly BattleScopeManager _battleScopeManager;
        private Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> _pendingGatewayConnectionFactory;
        private MobaRootState _activeRoot;
        private MobaBattleState _activeBattle;
        private bool _battleRequested;
        private bool _isStarted;
        private bool _isFaulted;
        private bool _isShutdown;
        private StateMachine<MobaRootState, MobaBattleState, MobaBattleEvent> _battleFsm;

        public MobaFlowDomainCore(
            IGameFlowRuntimeServices runtime,
            IMobaFeatureFactoryProvider featureFactoryProvider,
            ILogSink log,
            IPresentationSink presentationSink = null)
            : this(runtime, featureFactoryProvider, new DefaultBattleSessionFeatureFactory(), log, presentationSink)
        {
        }

        public MobaFlowDomainCore(
            IGameFlowRuntimeServices runtime,
            IMobaFeatureFactoryProvider featureFactoryProvider,
            IBattleSessionFeatureFactory battleSessionFeatureFactory,
            ILogSink log,
            IPresentationSink presentationSink = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _featureFactoryProvider = featureFactoryProvider ?? throw new ArgumentNullException(nameof(featureFactoryProvider));
            _battleSessionFeatureFactory = battleSessionFeatureFactory ?? throw new ArgumentNullException(nameof(battleSessionFeatureFactory));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _presentationSink = presentationSink ?? NullPresentationSink.Instance;

            _ctx = new GamePhaseContext(_runtime.Host, _runtime.Features, _runtime.BattleEntities);
            _flowConfig = MobaFlowConfiguration.CreateDefault();
            _conditionResolver = new MobaFlowConditionResolver();
            _actionExecutor = new MobaFlowActionExecutor();
            _switchExecutor = new MobaFlowSwitchExecutor();
            _battleScopeManager = new BattleScopeManager(
                callbacks: new BattleScopeManager.Callbacks
                {
                    SetBattleRequested = value => _battleRequested = value,
                    EnqueueRootEvent = evt => _rootEvents.Enqueue(evt),
                    TriggerBattleFsm = evt =>
                    {
                        if (_battleFsm != null)
                            _battleFsm.Trigger(evt);
                    },
                    GetActiveBattle = () => _activeBattle,
                    ClearGatewayConnectionFactory = () => _pendingGatewayConnectionFactory = null,
                    GetGatewayConnectionFactory = () => _pendingGatewayConnectionFactory,
                    CreateBattleSessionFeature = (bootstrapper, gatewayConnectionFactory) =>
                        _battleSessionFeatureFactory.Create(bootstrapper, gatewayConnectionFactory)
                },
                battleWorldScope: _battleWorldScope,
                advanceDecider: _advanceDecider,
                log: _log);

            _flowConditionContextBuilder = new FlowConditionContextBuilder(
                callbacks: new FlowConditionContextBuilder.Callbacks
                {
                    GetBattleRequested = () => _battleRequested
                },
                battleWorldScope: _battleWorldScope,
                conditionResolver: _conditionResolver);

            var featurePlanFactory = new MobaFeaturePlanFactory(
                _featureFactoryProvider.CreateFeatureFactoryRegistry(_battleScopeManager.CreateBattleSessionFeature));

            _featureScheduler = new FeatureScheduler(
                log: _log,
                callbacks: new FeatureScheduler.Callbacks
                {
                    FeatureBinderAttach = f => _runtime.FeatureBinder.AttachFeature(f),
                    FeatureBinderDetach = f => _runtime.FeatureBinder.DetachFeature(f),
                    ClearBattleSessionEvents = _battleScopeManager.ClearBattleSessionEvents,
                    ExecuteFlowAction = (actionId, installedCount) =>
                    {
                        var actionCtx = new MobaFlowActionContext(this, installedCount);
                        if (!_actionExecutor.Execute(actionId, in actionCtx))
                            _log.Error($"[MobaFlowDomainCore] Unknown flow action: {actionId}");
                    },
                    ExecuteSwitchFlow = (switchFlowId, installedCount) =>
                    {
                        var actionCtx = new MobaFlowActionContext(this, installedCount);
                        if (!_switchExecutor.Execute(switchFlowId, in actionCtx))
                            _log.Error($"[MobaFlowDomainCore] Unknown switch flow: {switchFlowId}");
                    }
                },
                ctx: _ctx,
                flowConfig: _flowConfig,
                bootFeaturePlan: featurePlanFactory.CreateBootFeaturePlan(),
                battleFeaturePlan: featurePlanFactory.CreateBattleFeaturePlan());

            _flowContext = new FlowContext();
            _rootEvents = new FlowEventQueue<MobaRootEvent>();

            var smBuilder = new FlowStateMachineBuilder(
                _flowConfig,
                _conditionResolver,
                _log,
                _presentationSink,
                new FlowStateMachineBuilder.FlowStateMachineCallbacks
                {
                    OnRootStateEntered = state =>
                    {
                        _activeRoot = state;
                        if (state != MobaRootState.Battle)
                        {
                            _battleScopeManager.EndBattleScope();
                        }
                    },
                    OnBattleStateEntered = state => _log.Info($"[MobaFlowDomainCore] MobaBattleState.{state} entered"),
                    OnBattleStateChanged = state => _activeBattle = state,
                    EnterRootBindings = state => _featureScheduler.RootStateBindings.Enter(state, in _ctx),
                    ExitRootBindings = state => _featureScheduler.RootStateBindings.Exit(state, in _ctx),
                    EnterBattleBindings = state => _featureScheduler.BattleStateBindings.Enter(state, in _ctx),
                    ExitBattleBindings = state => _featureScheduler.BattleStateBindings.Exit(state, in _ctx),
                    EvaluateRootCondition = _flowConditionContextBuilder.EvaluateRootTransitionCondition
                });
            _battleFsm = smBuilder.BuildBattleStateMachine();
            _root = smBuilder.BuildRootStateMachine(_battleFsm);
            _runner = new HfsmFlowRunner<string, MobaRootState, MobaRootEvent>(_flowContext, _root, _rootEvents);
        }

        public LayeredJsonSettingsStore Settings { get; } = new LayeredJsonSettingsStore();

        private readonly FlowContext _flowContext;
        private readonly FlowEventQueue<MobaRootEvent> _rootEvents;
        private readonly StateMachine<string, MobaRootState, MobaRootEvent> _root;
        private readonly HfsmFlowRunner<string, MobaRootState, MobaRootEvent> _runner;

        public MobaRootState CurrentPhase => _activeRoot;
        public MobaBattleState CurrentBattlePhase => _activeBattle;
        public bool IsFaulted => _isFaulted;
        MobaRootState IFlowCommandSink.CurrentRootPhase => _activeRoot;
        MobaBattleState IFlowCommandSink.CurrentBattlePhase => _activeBattle;

        public void Start()
        {
            BeginStart();
            _runtime.LoadPersistentSettings(Settings);
        }

        public void StartWithPersistentSettingsSync()
        {
            BeginStart();
            _runtime.LoadPersistentSettingsSync(Settings);
        }

        public bool TrySaveSettingsOverridesToPersistent()
        {
            return _runtime.TrySaveSettingsOverridesToPersistent(Settings);
        }

        public void Tick(float deltaTime)
        {
            if (!_isStarted || _isFaulted || _isShutdown)
                return;

            try
            {
                _runner.Step(deltaTime);
            }
            catch (Exception ex)
            {
                _isFaulted = true;
                _log.Exception(ex, "[MobaFlowDomainCore] HFSM Step failed");
                return;
            }

            _featureScheduler.Tick(in _ctx, deltaTime);
        }

        public void OnGUI()
        {
            if (!_isStarted || _isFaulted || _isShutdown)
                return;

            _featureScheduler.OnGUI(in _ctx);
        }

        public void Shutdown()
        {
            if (_isShutdown)
                return;

            _isShutdown = true;
            try
            {
                _featureScheduler.ClearFeatures();
            }
            catch (Exception ex)
            {
                _log.Exception(ex, "[MobaFlowDomainCore] Feature shutdown failed");
            }
            finally
            {
                _battleScopeManager.EndBattleScope();
            }
        }

        public void SwitchTo(IGamePhase next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));

            if (next is BattlePhase)
            {
                EnterBattle((IBattleBootstrapper)null);
                return;
            }

            ReturnToBoot();
        }

        public void Attach(IGamePhaseFeature feature) => _featureScheduler.AttachFeature(feature);
        public void Detach(IGamePhaseFeature feature) => _featureScheduler.DetachFeature(feature);
        public int AttachBootFeatures() => _featureScheduler.AttachBootFeatures();
        public int AttachBattleFeatures(IReadOnlyList<string> featureIds = null) => AttachBattleFeatures(featureIds, gatewayConnectionFactory: null);

        public int AttachBattleFeatures(IReadOnlyList<string> featureIds, Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory)
        {
            _pendingGatewayConnectionFactory = gatewayConnectionFactory;
            try
            {
                return _featureScheduler.AttachBattleFeatures(featureIds);
            }
            finally
            {
                _pendingGatewayConnectionFactory = null;
            }
        }

        public void EnterBattle(IBattleBootstrapper bootstrapper) => _battleScopeManager.EnterBattle(bootstrapper);
        public void ReturnToBoot() => _battleScopeManager.ReturnToBoot();
        void IFlowCommandSink.RequestEnterBattle() => EnterBattle((IBattleBootstrapper)null);
        void IFlowCommandSink.RequestBattleEnd()
        {
            if (_battleFsm != null)
            {
                _battleFsm.Trigger(MobaBattleEvent.Ended);
            }
        }
        void IFlowCommandSink.RequestReturnLobby() => ReturnToBoot();

        private void BeginStart()
        {
            if (_isStarted)
                throw new InvalidOperationException("Moba flow domain has already been started.");
            if (_isShutdown)
                throw new InvalidOperationException("Moba flow domain has already been shut down.");

            _isStarted = true;
            _runner.Start();
            _rootEvents.Enqueue(MobaRootEvent.BootCompleted);
        }

        public void ResetBattleSessionRuntimeState() => _battleScopeManager.ResetBattleSessionRuntimeState();
        public void TryAdvanceOnConnectEnter() => _battleScopeManager.TryAdvanceOnConnectEnter();
        public void TryAdvanceOnCreateOrJoinWorldEnter() => _battleScopeManager.TryAdvanceOnCreateOrJoinWorldEnter();
        public void TryAdvanceOnLoadAssetsEnter() => _battleScopeManager.TryAdvanceOnLoadAssetsEnter();
        public void ReturnLobbyAfterBattleEnd() => _battleScopeManager.ReturnLobbyAfterBattleEnd();
    }
}
