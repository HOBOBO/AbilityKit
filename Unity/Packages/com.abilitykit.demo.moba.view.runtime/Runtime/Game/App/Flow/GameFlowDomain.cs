using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.Flow;
using AbilityKit.Demo.Moba.View.Settings;
using AbilityKit.Core.Logging;
using AbilityKit.Game;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class GameFlowDomain : IMobaFlowActionTarget, IFlowCommandSink,
        IGameFlowFeatureInstaller, IBattleViewFeatureHost
    {
        private readonly MobaFlowDomainCore _core;

        public GameFlowDomain(IGameHost entry)
            : this(entry, rootOverride: default, presentationSink: null)
        {
        }

        public GameFlowDomain(IGameHost entry, IEntity rootOverride)
            : this(entry, rootOverride, presentationSink: null)
        {
        }

        public GameFlowDomain(IGameHost entry, IPresentationSink presentationSink)
            : this(entry, rootOverride: default, presentationSink: presentationSink)
        {
        }

        public GameFlowDomain(IGameHost entry, IEntity rootOverride, IPresentationSink presentationSink)
            : this(
                entry,
                rootOverride,
                log: Log.Sink,
                featureBinder: null,
                presentationSink: presentationSink)
        {
        }

        public GameFlowDomain(IGameHost entry, IEntity rootOverride, IPresentationSink presentationSink,
            MobaFlowExtensionRegistry extensions)
            : this(new UnityGameFlowRuntimeServices(entry, rootOverride, null),
                new UnityMobaFeatureFactoryProvider(), new DefaultBattleSessionFeatureFactory(),
                Log.Sink, presentationSink, null, extensions)
        {
        }

        public GameFlowDomain(
            IGameHost entry,
            IEntity rootOverride,
            ILogSink log,
            IFeatureBinder featureBinder = null,
            IPresentationSink presentationSink = null)
            : this(
                new UnityGameFlowRuntimeServices(entry, rootOverride, featureBinder),
                new UnityMobaFeatureFactoryProvider(),
                log,
                presentationSink)
        {
        }

        public GameFlowDomain(
            IGameFlowRuntimeServices runtime,
            IMobaFeatureFactoryProvider featureFactoryProvider,
            ILogSink log,
            IPresentationSink presentationSink = null)
            : this(runtime, featureFactoryProvider, new DefaultBattleSessionFeatureFactory(), log, presentationSink)
        {
        }

        public GameFlowDomain(
            IGameFlowRuntimeServices runtime,
            IMobaFeatureFactoryProvider featureFactoryProvider,
            IBattleSessionFeatureFactory battleSessionFeatureFactory,
            ILogSink log,
            IPresentationSink presentationSink = null,
            MobaBattleFlowProfile flowProfile = null,
            MobaFlowExtensionRegistry extensions = null)
        {
            _core = new MobaFlowDomainCore(runtime, featureFactoryProvider, battleSessionFeatureFactory,
                log, presentationSink, flowProfile ?? MobaBattleFlowProfileCatalog.Default.DefaultProfile,
                extensions);
        }

        public LayeredJsonSettingsStore Settings => _core.Settings;

        public MobaRootState CurrentPhase => _core.CurrentPhase;
        public MobaBattleState CurrentBattlePhase => _core.CurrentBattlePhase;
        public MobaFlowStateSnapshot CurrentState => _core.CurrentState;
        public string CurrentBattleProfileId => _core.CurrentBattleProfileId;
        public bool IsFaulted => _core.IsFaulted;
        public MobaBattleEntryState BattleEntryState => _core.BattleEntryState;
        public string BattleEntryRejection => _core.BattleEntryRejection;
        public MobaBattleExitKind LastBattleExitKind => _core.LastBattleExitKind;
        MobaRootState IFlowCommandSink.CurrentRootPhase => ((IFlowCommandSink)_core).CurrentRootPhase;
        MobaBattleState IFlowCommandSink.CurrentBattlePhase => ((IFlowCommandSink)_core).CurrentBattlePhase;

        public void Start() => _core.Start();
        public void StartWithPersistentSettingsSync() => _core.StartWithPersistentSettingsSync();
        public bool TrySaveSettingsOverridesToPersistent() => _core.TrySaveSettingsOverridesToPersistent();
        public void Tick(float deltaTime) => _core.Tick(deltaTime);
        public void OnGUI() => _core.OnGUI();
        public void Shutdown() => _core.Shutdown();
        public Task ShutdownAsync() => _core.ShutdownAsync();
        public void SwitchTo(IGamePhase next)
        {
            if (next is BattlePhase battle)
                EnterBattle(battle.Bootstrapper, battle.GatewayConnectionFactory);
            else
                _core.SwitchTo(next);
        }

        public void Attach(IGamePhaseFeature feature) => _core.Attach(feature);
        public void Detach(IGamePhaseFeature feature) => _core.Detach(feature);
        public Task DetachAsync(IGamePhaseFeature feature) => _core.DetachAsync(feature);
        public int AttachBootFeatures() => _core.AttachBootFeatures();
        [Obsolete("Use EnterBattle with a battle flow profile.")]
        public int AttachBattleFeatures(IReadOnlyList<string> featureIds = null) => _core.AttachBattleFeatures(featureIds);
        [Obsolete("Use EnterBattle with a battle flow profile.")]
        public int AttachBattleFeatures(IReadOnlyList<string> featureIds, Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory) =>
            _core.AttachBattleFeatures(featureIds, gatewayConnectionFactory);

        public void EnterBattle(IBattleBootstrapper bootstrapper)
            => EnterBattle(bootstrapper, null);

        public void EnterBattle(IBattleBootstrapper bootstrapper,
            Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory)
        {
            var profileId = (bootstrapper as IBattleFlowProfileProvider)?.FlowProfileId;
            var profile = string.IsNullOrWhiteSpace(profileId)
                ? null
                : MobaBattleFlowProfileCatalog.Default.Get(profileId);
            _core.EnterBattle(bootstrapper, profile, gatewayConnectionFactory);
        }
        public void ReturnToBoot() => _core.ReturnToBoot();
        void IFlowCommandSink.RequestEnterBattle() => ((IFlowCommandSink)_core).RequestEnterBattle();
        void IFlowCommandSink.RequestBattleEnd() => ((IFlowCommandSink)_core).RequestBattleEnd();
        void IFlowCommandSink.RequestReturnLobby() => ((IFlowCommandSink)_core).RequestReturnLobby();

        public void ResetBattleSessionRuntimeState() => _core.ResetBattleSessionRuntimeState();
        public void TryAdvanceOnConnectEnter() => _core.TryAdvanceOnConnectEnter();
        public void TryAdvanceOnCreateOrJoinWorldEnter() => _core.TryAdvanceOnCreateOrJoinWorldEnter();
        public void TryAdvanceOnLoadAssetsEnter() => _core.TryAdvanceOnLoadAssetsEnter();
        public void ReturnLobbyAfterBattleEnd() => _core.ReturnLobbyAfterBattleEnd();
    }
}
