using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns session-level simulation lifecycle orchestration and its dynamic inputs.
    /// </summary>
    internal sealed class SessionSimulationController
    {
        private readonly BattleSimulationRuntime _simulation;
        private readonly BattleSessionDiagnostics _diagnostics;
        private readonly Func<BattleStartPlan> _getPlan;
        private readonly Func<BattleContext> _getContext;
        private readonly Func<GameFlowDomain> _getFlow;
        private readonly Func<bool> _hasLogicSession;
        private readonly Func<float> _getFixedDeltaSeconds;
        private readonly Func<WorldId, int> _resolveIdealFrameLimit;
        private readonly Action<IEntity> _destroyEntityTree;

        internal SessionSimulationController(
            BattleSimulationRuntime simulation,
            BattleSessionDiagnostics diagnostics,
            Func<BattleStartPlan> getPlan,
            Func<BattleContext> getContext,
            Func<GameFlowDomain> getFlow,
            Func<bool> hasLogicSession,
            Func<float> getFixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Action<IEntity> destroyEntityTree)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _getPlan = getPlan ?? throw new ArgumentNullException(nameof(getPlan));
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _getFlow = getFlow ?? throw new ArgumentNullException(nameof(getFlow));
            _hasLogicSession = hasLogicSession ?? throw new ArgumentNullException(nameof(hasLogicSession));
            _getFixedDeltaSeconds = getFixedDeltaSeconds ??
                throw new ArgumentNullException(nameof(getFixedDeltaSeconds));
            _resolveIdealFrameLimit = resolveIdealFrameLimit ??
                throw new ArgumentNullException(nameof(resolveIdealFrameLimit));
            _destroyEntityTree = destroyEntityTree ??
                throw new ArgumentNullException(nameof(destroyEntityTree));
        }

        internal void StartRemoteDriven()
        {
            _simulation.StartRemoteDriven(
                _getPlan(),
                _getContext(),
                _getFixedDeltaSeconds(),
                _resolveIdealFrameLimit,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                () => _diagnostics.ShouldForceClientHashMismatch);
#else
                () => false);
#endif
        }

        internal void StartConfirmedAuthority()
        {
            _simulation.StartConfirmedAuthority(
                _getPlan(),
                _getContext(),
                _getFlow(),
                _hasLogicSession(),
                _getFixedDeltaSeconds(),
                _resolveIdealFrameLimit,
                _destroyEntityTree);
        }

        internal void DestroyWorlds() => _simulation.DestroyBattleWorlds(_getPlan());

        internal void DisposeConfirmedView() =>
            _simulation.DisposeConfirmedView(_getFlow(), _destroyEntityTree);

        internal void DisposeRemoteDrivenWorld() => _simulation.DisposeRemoteDrivenWorld();

        internal void DisposeConfirmedWorld() => _simulation.DisposeConfirmedWorld(_getContext());
    }
}
