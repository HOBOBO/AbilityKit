using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionSimulationHost
    {
        BattleStartPlan Plan { get; }
        BattleContext Context { get; }
        IBattleViewFeatureHost Flow { get; }
        bool HasLogicSession { get; }
        float FixedDeltaSeconds { get; }
        int ResolveIdealFrameLimit(WorldId worldId);
        void DestroyEntityTree(IEntity root);
    }

    /// <summary>
    /// Owns session-level simulation lifecycle orchestration and its dynamic inputs.
    /// </summary>
    internal sealed class SessionSimulationController
    {
        private readonly BattleSimulationRuntime _simulation;
        private readonly BattleSessionDiagnostics _diagnostics;
        private readonly ISessionSimulationHost _host;

        internal SessionSimulationController(
            BattleSimulationRuntime simulation,
            BattleSessionDiagnostics diagnostics,
            ISessionSimulationHost host)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal void StartRemoteDriven()
        {
            _simulation.StartRemoteDriven(
                _host.Plan,
                _host.Context,
                _host.FixedDeltaSeconds,
                _host.ResolveIdealFrameLimit,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                () => _diagnostics.ShouldForceClientHashMismatch);
#else
                () => false);
#endif
        }

        internal void StartConfirmedAuthority()
        {
            _simulation.StartConfirmedAuthority(
                _host.Plan,
                _host.Context,
                _host.Flow,
                _host.HasLogicSession,
                _host.FixedDeltaSeconds,
                _host.ResolveIdealFrameLimit,
                _host.DestroyEntityTree);
        }

        internal void DestroyWorlds() => _simulation.DestroyBattleWorlds(_host.Plan);

        internal void DisposeConfirmedView() =>
            _simulation.DisposeConfirmedView(_host.Flow, _host.DestroyEntityTree);

        internal void DisposeRemoteDrivenWorld() => _simulation.DisposeRemoteDrivenWorld();

        internal void DisposeConfirmedWorld() => _simulation.DisposeConfirmedWorld(_host.Context);
    }
}
