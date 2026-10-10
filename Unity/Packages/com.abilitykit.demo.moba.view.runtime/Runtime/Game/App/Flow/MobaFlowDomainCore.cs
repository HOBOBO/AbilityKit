using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AbilityKit.Ability.Flow;
using AbilityKit.Demo.Moba.View.Settings;
using AbilityKit.Core.Logging;
using AbilityKit.Game.View.Flow;
using AbilityKit.World.ECS;
using AbilityKit.HFSM;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaFlowDomainCore : IMobaFlowActionTarget, IFlowCommandSink,
        IGameFlowFeatureInstaller, IFeatureSchedulerHost, IBattleScopeHost
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
        private readonly MobaFlowExtensionRegistry _extensions;
        private readonly BattleWorldScopeHost _battleWorldScope = new BattleWorldScopeHost();
        private readonly BattleGameplayPresentationState _gameplayPresentation = new BattleGameplayPresentationState();
        private readonly BattleOutcomeState _battleOutcome = new BattleOutcomeState();
        private readonly MobaBattleAdvanceDecider _advanceDecider = new MobaBattleAdvanceDecider();
        private readonly FlowConditionContextBuilder _flowConditionContextBuilder;
        private readonly FeatureScheduler _featureScheduler;
        private readonly MobaFeaturePlanFactory _featurePlanFactory;
        private readonly MobaFeatureFactoryRegistry _featureFactories;
        private readonly MobaBattleFlowProfile _defaultProfile;
        private MobaBattleFlowComposition _activeComposition;
        private readonly BattleScopeManager _battleScopeManager;
        private readonly BattleFlowExitCoordinator _exitCoordinator;
        private readonly ConcurrentQueue<Action> _battleSignals = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<MobaBattleEvent> _battleEvents = new ConcurrentQueue<MobaBattleEvent>();
        private Task _shutdownTask;
        private Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> _pendingGatewayConnectionFactory;
        private MobaRootState _activeRoot;
        private MobaBattleState _activeBattle;
        private bool _battleRequested;
        private bool _isStarted;
        private bool _isFaulted;
        private bool _isShutdown;
        private MobaBattleEntryState _battleEntryState;
        private MobaBattleExitKind _battleExitKind;
        private string _battleEntryRejection = string.Empty;
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
            IPresentationSink presentationSink = null,
            MobaBattleFlowProfile flowProfile = null,
            MobaFlowExtensionRegistry extensions = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _featureFactoryProvider = featureFactoryProvider ?? throw new ArgumentNullException(nameof(featureFactoryProvider));
            _battleSessionFeatureFactory = battleSessionFeatureFactory ?? throw new ArgumentNullException(nameof(battleSessionFeatureFactory));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _presentationSink = presentationSink ?? NullPresentationSink.Instance;
            _extensions = extensions;

            _ctx = new GamePhaseContext(_runtime.Host, _runtime.Features, _runtime.BattleEntities,
                _gameplayPresentation, _battleOutcome);
            flowProfile ??= MobaBattleFlowProfile.CreateStandard();
            _defaultProfile = flowProfile;
            var composition = flowProfile.Resolve();
            composition.ValidateExtensions(_extensions);
            ValidateSessionModules(composition);
            _activeComposition = composition;
            _flowConfig = MobaFlowConfiguration.CreateResolved(composition);
            _conditionResolver = new MobaFlowConditionResolver();
            _actionExecutor = new MobaFlowActionExecutor();
            _switchExecutor = new MobaFlowSwitchExecutor();
            _battleScopeManager = new BattleScopeManager(
                callbacks: this,
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

            var featureFactories = _featureFactoryProvider.CreateFeatureFactoryRegistry(
                _battleScopeManager.CreateBattleSessionFeature);
            _featureFactories = featureFactories;
            composition.ValidateFactories(featureFactories);
            _featurePlanFactory = new MobaFeaturePlanFactory(featureFactories);

            _featureScheduler = new FeatureScheduler(
                log: _log,
                callbacks: this,
                ctx: _ctx,
                flowConfig: _flowConfig,
                bootFeaturePlan: _featurePlanFactory.CreateBootFeaturePlan(),
                battleFeaturePlan: _featurePlanFactory.CreateBattleFeaturePlan(composition));

            _exitCoordinator = new BattleFlowExitCoordinator(
                _featureScheduler.ClearFeaturesAsync,
                () => _battleFsm?.Trigger(MobaBattleEvent.Ended),
                () =>
                {
                    _battleRequested = false;
                    _pendingGatewayConnectionFactory = null;
                    _rootEvents.Enqueue(MobaRootEvent.ReturnLobby);
                },
                ex =>
                {
                    _isFaulted = true;
                    _battleExitKind = MobaBattleExitKind.CleanupFailed;
                    _log.Exception(ex, "[MobaFlowDomainCore] Battle cleanup failed; battle scope retained for retry");
                    _presentationSink.OnError("Battle cleanup failed: " + ex.Message);
                });

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
                        if (state == MobaRootState.Battle)
                            _battleEntryState = MobaBattleEntryState.Accepted;
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
            _runner = new StateMachineFlowRunner<string, MobaRootState, MobaRootEvent>(_flowContext, _root, _rootEvents);
        }

        public LayeredJsonSettingsStore Settings { get; } = new LayeredJsonSettingsStore();

        private readonly FlowContext _flowContext;
        private readonly FlowEventQueue<MobaRootEvent> _rootEvents;
        private readonly StateMachine<string, MobaRootState, MobaRootEvent> _root;
        private readonly StateMachineFlowRunner<string, MobaRootState, MobaRootEvent> _runner;

        public MobaRootState CurrentPhase => _activeRoot;
        public MobaBattleState CurrentBattlePhase => _activeBattle;
        public MobaFlowStateSnapshot CurrentState => new MobaFlowStateSnapshot(_activeRoot,
            _activeRoot == MobaRootState.Battle ? _activeBattle : (MobaBattleState?)null,
            _exitCoordinator.IsPending, _isFaulted);
        public string CurrentBattleProfileId => _activeComposition.ProfileId;
        public bool IsFaulted => _isFaulted;
        public MobaBattleEntryState BattleEntryState => _battleEntryState;
        public string BattleEntryRejection => _battleEntryRejection;
        public MobaBattleExitKind LastBattleExitKind => _battleExitKind;
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
                DrainBattleSignals();
                if (_exitCoordinator.Poll()) return;
                _runner.Step(deltaTime);
                RejectUnacceptedBattleEntry();
            }
            catch (Exception ex)
            {
                _isFaulted = true;
                _log.Exception(ex, "[MobaFlowDomainCore] HFSM Step failed");
                _ = ObserveShutdownAsync(ShutdownAsync());
                return;
            }

            try { _featureScheduler.Tick(in _ctx, deltaTime); }
            catch (Exception ex)
            {
                _isFaulted = true;
                _log.Exception(ex, "[MobaFlowDomainCore] Feature Tick failed");
                _ = ObserveShutdownAsync(ShutdownAsync());
                return;
            }
            if (_activeBattle == MobaBattleState.InMatch && _battleOutcome.HasAuthoritativeResult &&
                _battleExitKind == MobaBattleExitKind.None)
            {
                _battleExitKind = MobaBattleExitKind.Completed;
                _battleEvents.Enqueue(MobaBattleEvent.Ended);
            }
        }

        public void OnGUI()
        {
            if (!_isStarted || _isFaulted || _isShutdown || _exitCoordinator.IsPending)
                return;

            _featureScheduler.OnGUI(in _ctx);
        }

        public void Shutdown()
        {
            _ = ObserveShutdownAsync(ShutdownAsync());
        }

        public Task ShutdownAsync()
        {
            if (_shutdownTask != null && !_shutdownTask.IsFaulted) return _shutdownTask;
            if (_isShutdown && _shutdownTask == null) return Task.CompletedTask;
            _isShutdown = true;
            _shutdownTask = ShutdownCoreAsync();
            return _shutdownTask;
        }

        private async Task ShutdownCoreAsync()
        {
            try
            {
                try { await _exitCoordinator.PendingTask; }
                catch (Exception ex) { _log.Exception(ex, "[MobaFlowDomainCore] Retrying failed battle cleanup during shutdown"); }
                await _featureScheduler.ClearFeaturesAsync();
                _pendingGatewayConnectionFactory = null;
                _battleScopeManager.EndBattleScope();
            }
            catch (Exception ex)
            {
                _log.Exception(ex, "[MobaFlowDomainCore] Feature shutdown failed");
                throw;
            }
        }

        private async Task ObserveShutdownAsync(Task task)
        {
            try { await task; }
            catch (Exception ex) { _log.Exception(ex, "[MobaFlowDomainCore] Asynchronous shutdown failed"); }
        }

        public void SwitchTo(IGamePhase next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));

            if (next is BattlePhase battle)
            {
                EnterBattle(battle.Bootstrapper, null, battle.GatewayConnectionFactory);
                return;
            }

            ReturnToBoot();
        }

        public void Attach(IGamePhaseFeature feature) => _featureScheduler.AttachFeature(feature);
        public void Detach(IGamePhaseFeature feature) => _featureScheduler.DetachFeature(feature);
        public Task DetachAsync(IGamePhaseFeature feature) => _featureScheduler.DetachFeatureAsync(feature);
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

        public void EnterBattle(IBattleBootstrapper bootstrapper)
            => EnterBattle(bootstrapper, null);

        public void EnterBattle(IBattleBootstrapper bootstrapper, MobaBattleFlowProfile profile)
            => EnterBattle(bootstrapper, profile, null);

        public void EnterBattle(IBattleBootstrapper bootstrapper, MobaBattleFlowProfile profile,
            Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory)
        {
            if (_isShutdown || _isFaulted || _battleRequested || _activeRoot == MobaRootState.Battle ||
                _exitCoordinator.IsPending)
                throw new InvalidOperationException("A battle is already active or still exiting.");
            var selectedProfile = profile ?? _defaultProfile;
            selectedProfile.ValidateHostMode((bootstrapper as IBattleFlowModeProvider)?.FlowHostMode);
            var composition = selectedProfile.Resolve();
            composition.ValidateExtensions(_extensions);
            ValidateSessionModules(composition);
            composition.ValidateFactories(_featureFactories);
            var configuration = MobaFlowConfiguration.CreateResolved(composition);
            var plan = _featurePlanFactory.CreateBattleFeaturePlan(composition);
            _featureScheduler.ConfigureBattle(configuration, plan);
            _activeComposition = composition;
            _gameplayPresentation.Reset();
            _battleOutcome.Reset();
            _pendingGatewayConnectionFactory = gatewayConnectionFactory;
            _battleEntryState = MobaBattleEntryState.Pending;
            _battleEntryRejection = string.Empty;
            _battleExitKind = MobaBattleExitKind.None;
            try { _battleScopeManager.EnterBattle(bootstrapper); }
            catch
            {
                _pendingGatewayConnectionFactory = null;
                _battleRequested = false;
                _battleScopeManager.EndBattleScope();
                _battleEntryState = MobaBattleEntryState.Rejected;
                throw;
            }
        }

        private void ValidateSessionModules(MobaBattleFlowComposition composition)
        {
            var catalog = _battleSessionFeatureFactory as IBattleSessionModuleCatalog;
            var capabilities = _battleSessionFeatureFactory as IBattleSessionModuleCapabilityCatalog;
            foreach (var moduleId in composition.SessionModuleIds)
                if (!(catalog?.ContainsModule(moduleId) ?? SessionSubFeaturePipeline.IsBuiltIn(moduleId)))
                    throw new InvalidOperationException(
                        $"Battle flow profile '{composition.ProfileId}' references unregistered session module: {moduleId}");
            MobaBattleFlowProfile.ValidateSessionCapabilities(composition.SessionModuleIds,
                moduleId => capabilities?.GetCapabilities(moduleId) ??
                    MobaBattleFlowProfile.GetBuiltInModuleCapabilities(moduleId));
        }
        public void ReturnToBoot()
        {
            if (_isShutdown || _isFaulted) return;
            _battleScopeManager.ReturnToBoot();
        }
        void IFlowCommandSink.RequestEnterBattle() => EnterBattle((IBattleBootstrapper)null);
        void IFlowCommandSink.RequestBattleEnd()
        {
            _battleExitKind = MobaBattleExitKind.Requested;
            _battleEvents.Enqueue(MobaBattleEvent.Ended);
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
        public void ReturnLobbyAfterBattleEnd()
        {
            if (!_battleOutcome.HasAuthoritativeResult)
                _battleScopeManager.ReturnLobbyAfterBattleEnd();
        }

        private void DrainBattleSignals()
        {
            var remaining = 256;
            while (remaining-- > 0 && _battleSignals.TryDequeue(out var signal))
                signal();

            remaining = 256;
            while (remaining-- > 0 && _battleEvents.TryDequeue(out var battleEvent))
            {
                if (_activeRoot != MobaRootState.Battle || _exitCoordinator.IsPending) continue;
                if (battleEvent == MobaBattleEvent.Ended && _activeBattle != MobaBattleState.End)
                {
                    if (_battleExitKind == MobaBattleExitKind.SessionFailed ||
                        _battleExitKind == MobaBattleExitKind.Requested)
                        _exitCoordinator.RequestReturnLobby();
                    else
                        _exitCoordinator.RequestEnd();
                }
                else if (battleEvent != MobaBattleEvent.Ended)
                    _battleFsm?.Trigger(battleEvent);
            }
        }

        private void RejectUnacceptedBattleEntry()
        {
            if (_battleEntryState != MobaBattleEntryState.Pending ||
                _activeRoot == MobaRootState.Battle || !_battleSignals.IsEmpty ||
                _rootEvents.Count != 0) return;

            var gates = _flowConditionContextBuilder.BuildFlowConditionContext();
            _battleEntryRejection = $"Battle entry rejected: authenticated={gates.Authenticated}, " +
                $"roomReady={gates.RoomReady}, connectivityReady={gates.ConnectivityReady}, " +
                $"assetsReady={gates.AssetsReady}";
            _battleEntryState = MobaBattleEntryState.Rejected;
            _battleRequested = false;
            _pendingGatewayConnectionFactory = null;
            _battleScopeManager.EndBattleScope();
            _presentationSink.OnError(_battleEntryRejection);
        }

        private void EnqueueRootEvent(MobaRootEvent rootEvent)
        {
            if (rootEvent == MobaRootEvent.ReturnLobby && _activeRoot == MobaRootState.Battle)
            {
                _exitCoordinator.RequestReturnLobby();
                return;
            }
            _rootEvents.Enqueue(rootEvent);
        }

        void IFeatureSchedulerHost.AttachFeatureBinding(object feature) =>
            _runtime.FeatureBinder.AttachFeature(feature);

        void IFeatureSchedulerHost.DetachFeatureBinding(object feature) =>
            _runtime.FeatureBinder.DetachFeature(feature);

        void IFeatureSchedulerHost.ClearBattleSessionEvents() =>
            _battleScopeManager.ClearBattleSessionEvents();

        void IFeatureSchedulerHost.ExecuteFlowAction(string actionId, int installedCount)
        {
            var actionCtx = new MobaFlowActionContext(this, installedCount);
            if (!_actionExecutor.Execute(actionId, in actionCtx) &&
                !(_extensions?.TryExecute(actionId, _ctx, installedCount) ?? false))
                throw new InvalidOperationException("Unknown flow action: " + actionId);
        }

        void IFeatureSchedulerHost.ExecuteSwitchFlow(string switchFlowId, int installedCount)
        {
            var actionCtx = new MobaFlowActionContext(this, installedCount);
            if (!_switchExecutor.Execute(switchFlowId, in actionCtx))
                throw new InvalidOperationException("Unknown switch flow: " + switchFlowId);
        }

        void IBattleScopeHost.SetBattleRequested(bool requested) => _battleRequested = requested;
        void IBattleScopeHost.EnqueueRootEvent(MobaRootEvent evt) =>
            _battleSignals.Enqueue(() => EnqueueRootEvent(evt));
        void IBattleScopeHost.TriggerBattleFsm(MobaBattleEvent evt) => _battleEvents.Enqueue(evt);
        MobaBattleState IBattleScopeHost.GetActiveBattle() => _activeBattle;
        void IBattleScopeHost.ClearGatewayConnectionFactory() => _pendingGatewayConnectionFactory = null;
        Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection>
            IBattleScopeHost.GetGatewayConnectionFactory() => _pendingGatewayConnectionFactory;
        void IBattleScopeHost.DispatchSignal(Action signal) => _battleSignals.Enqueue(signal);
        void IBattleScopeHost.FailBattle(Exception error)
        {
            _battleExitKind = MobaBattleExitKind.SessionFailed;
            _presentationSink.OnError("Battle session failed: " + error?.Message);
            _battleEvents.Enqueue(MobaBattleEvent.Ended);
        }

        IBattleSessionFeature IBattleScopeHost.CreateBattleSessionFeature(IBattleBootstrapper bootstrapper,
            Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory)
        {
            var feature = _battleSessionFeatureFactory.Create(bootstrapper, gatewayConnectionFactory);
            _pendingGatewayConnectionFactory = null;
            if (feature is BattleSessionFeature session)
            {
                var catalog = _battleSessionFeatureFactory as IBattleSessionModuleCapabilityCatalog;
                session.ConfigureSessionModules(
                    _activeComposition.SessionModuleIds,
                    catalog == null ? null : new Func<string, IReadOnlyList<string>>(catalog.GetCapabilities));
            }
            return feature;
        }

    }
}
