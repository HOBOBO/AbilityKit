using AbilityKit.Ability.Host.Extensions.FrameSync;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        private BattlePredictionRuntime _predictionRuntime;

        public IClientPredictionDriverStats PredictionStats =>
            _predictionRuntime?.Stats;
        public IClientPredictionReconcileTarget PredictionReconcileTarget =>
            _predictionRuntime?.ReconcileTarget;
        public IClientPredictionReconcileControl PredictionReconcileControl =>
            _predictionRuntime?.ReconcileControl;
        public IClientPredictionTuningControl PredictionTuningControl =>
            _predictionRuntime?.TuningControl;

        internal BattlePredictionRuntime PredictionRuntime =>
            EnsurePredictionRuntime();

        internal void BindPredictionRuntime(BattlePredictionRuntime runtime)
        {
            if (runtime == null)
                throw new System.ArgumentNullException(nameof(runtime));
            if (ReferenceEquals(_predictionRuntime, runtime)) return;

            ReleasePredictionRuntimeBinding();
            _predictionRuntime = runtime;
            runtime.BindContext(this);
        }

        internal void UnbindPredictionRuntime(BattlePredictionRuntime runtime)
        {
            if (!ReferenceEquals(_predictionRuntime, runtime)) return;

            runtime.UnbindContext(this);
            _predictionRuntime = null;
        }

        private BattlePredictionRuntime EnsurePredictionRuntime()
        {
            if (_predictionRuntime != null) return _predictionRuntime;

            _predictionRuntime = new BattlePredictionRuntime();
            return _predictionRuntime;
        }

        private void ResetPredictionRuntime() =>
            ReleasePredictionRuntimeBinding();

        private void ReleasePredictionRuntimeBinding()
        {
            var runtime = _predictionRuntime;
            _predictionRuntime = null;
            runtime?.UnbindContext(this);
        }
    }
}
