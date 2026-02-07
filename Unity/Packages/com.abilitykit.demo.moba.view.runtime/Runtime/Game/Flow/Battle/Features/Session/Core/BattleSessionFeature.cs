using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature : IGamePhaseFeature, BattleSessionFeature.ISessionPlanHost, BattleSessionFeature.ISessionReplayHost, BattleSessionFeature.ISessionEventsHost, BattleSessionFeature.ITickLoopHost, BattleSessionFeature.ISessionOrchestratorHost, BattleSessionFeature.INetAdapterContextHost
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DebugForceClientHashMismatch { get; set; }
#endif

        private readonly IBattleBootstrapper _bootstrapper;

        private readonly BattleSessionState _state = new BattleSessionState();
        private readonly BattleSessionHandles _handles = new BattleSessionHandles();

        private readonly SessionOrchestrator _orchestrator;

        private readonly SessionDispatchersController _dispatchers;

        private readonly SessionNetAdapterController _net;

        private readonly SessionReplayController _replayCtrl;

        private readonly SessionPlanController _planCtrl;

        private readonly SessionEventsController _eventsCtrl;

        private readonly TickLoopController _tickLoop;

        float ITickLoopHost.GetFixedDeltaSeconds() => GetFixedDeltaSeconds();
        void ITickLoopHost.TickRemoteDrivenLocalSim(float deltaTime) => TickRemoteDrivenLocalSim(deltaTime);
        void ITickLoopHost.TickConfirmedAuthorityWorldSim(float deltaTime) => TickConfirmedAuthorityWorldSim(deltaTime);
#if UNITY_EDITOR
        private static bool _editorPlayModeHookInstalled;
#endif

        public BattleSessionFeature(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
            _orchestrator = new SessionOrchestrator(_state, _handles, this);
            _dispatchers = new SessionDispatchersController();
            _net = new SessionNetAdapterController();
            _replayCtrl = new SessionReplayController();
            _planCtrl = new SessionPlanController();
            _eventsCtrl = new SessionEventsController();
            _tickLoop = new TickLoopController(_state, _handles, this);
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        private float GetFixedDeltaSeconds() => _orchestrator.GetFixedDeltaSeconds();

        private void StartSession() => _orchestrator.StartSession();

        private void StopSession() => _orchestrator.StopSession();

    }
}
