using AbilityKit.Ability.Host.Extensions.FrameSync;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattlePredictionRuntime
    {
        private BattleContext _context;

        internal BattleContext Context => _context;
        internal IClientPredictionDriverStats Stats { get; private set; }
        internal IClientPredictionReconcileTarget ReconcileTarget { get; private set; }
        internal IClientPredictionReconcileControl ReconcileControl { get; private set; }
        internal IClientPredictionTuningControl TuningControl { get; private set; }

        internal void BindContext(BattleContext context)
        {
            if (context == null)
                throw new System.ArgumentNullException(nameof(context));
            if (ReferenceEquals(_context, context)) return;

            Clear();
            _context = context;
        }

        internal void UnbindContext(BattleContext context)
        {
            if (context != null && !ReferenceEquals(_context, context)) return;

            _context = null;
            Clear();
        }

        internal void Bind(
            IClientPredictionDriverStats stats,
            IClientPredictionReconcileTarget reconcileTarget,
            IClientPredictionReconcileControl reconcileControl,
            IClientPredictionTuningControl tuningControl)
        {
            Stats = stats;
            ReconcileTarget = reconcileTarget;
            ReconcileControl = reconcileControl;
            TuningControl = tuningControl;
        }

        internal void Clear()
        {
            Stats = null;
            ReconcileTarget = null;
            ReconcileControl = null;
            TuningControl = null;
        }
    }
}
