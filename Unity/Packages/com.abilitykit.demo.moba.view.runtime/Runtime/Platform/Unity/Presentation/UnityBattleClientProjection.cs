using System;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    internal sealed class UnityBattleClientProjection : IBattleClientProjection
    {
        private readonly Func<BattleContext> _getContext;
        private readonly Func<bool> _predictionEnabled;
        private readonly BattleRemoteInterpolationApplier.Session _session =
            new BattleRemoteInterpolationApplier.Session();

        public UnityBattleClientProjection(
            Func<BattleContext> getContext,
            Func<bool> predictionEnabled)
        {
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _predictionEnabled = predictionEnabled ??
                throw new ArgumentNullException(nameof(predictionEnabled));
        }

        public void Apply(in GatewayStateSyncSnapshot snapshot)
        {
            var context = _getContext();
            if (context == null) return;
            var localActorId = BattleRemoteInterpolationApplier.ResolveExcludedLocalActorId(
                _predictionEnabled(), context.LocalActorId);
            _session.Apply(context, in snapshot, localActorId);
        }

        public void Reset() => _session.Reset();
    }
}
