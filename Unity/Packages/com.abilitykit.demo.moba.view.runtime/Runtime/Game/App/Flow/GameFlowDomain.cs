using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Core.Configuration;
using AbilityKit.Core.Logging;
using AbilityKit.Game;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class GameFlowDomain : IMobaFlowActionTarget, IFlowCommandSink, IGameFlowFeatureInstaller
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
            IPresentationSink presentationSink = null)
        {
            _core = new MobaFlowDomainCore(runtime, featureFactoryProvider, battleSessionFeatureFactory, log, presentationSink);
        }

        public LayeredJsonSettingsStore Settings => _core.Settings;

        public MobaRootState CurrentPhase => _core.CurrentPhase;
        public MobaBattleState CurrentBattlePhase => _core.CurrentBattlePhase;
        public bool IsFaulted => _core.IsFaulted;
        MobaRootState IFlowCommandSink.CurrentRootPhase => ((IFlowCommandSink)_core).CurrentRootPhase;
        MobaBattleState IFlowCommandSink.CurrentBattlePhase => ((IFlowCommandSink)_core).CurrentBattlePhase;

        public void Start() => _core.Start();
        public void StartWithPersistentSettingsSync() => _core.StartWithPersistentSettingsSync();
        public bool TrySaveSettingsOverridesToPersistent() => _core.TrySaveSettingsOverridesToPersistent();
        public void Tick(float deltaTime) => _core.Tick(deltaTime);
        public void OnGUI() => _core.OnGUI();
        public void Shutdown() => _core.Shutdown();
        public void SwitchTo(IGamePhase next) => _core.SwitchTo(next);

        public void Attach(IGamePhaseFeature feature) => _core.Attach(feature);
        public void Detach(IGamePhaseFeature feature) => _core.Detach(feature);
        public int AttachBootFeatures() => _core.AttachBootFeatures();
        public int AttachBattleFeatures(IReadOnlyList<string> featureIds = null) => _core.AttachBattleFeatures(featureIds);
        public int AttachBattleFeatures(IReadOnlyList<string> featureIds, Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory) =>
            _core.AttachBattleFeatures(featureIds, gatewayConnectionFactory);

        public void EnterBattle(IBattleBootstrapper bootstrapper) => _core.EnterBattle(bootstrapper);
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
