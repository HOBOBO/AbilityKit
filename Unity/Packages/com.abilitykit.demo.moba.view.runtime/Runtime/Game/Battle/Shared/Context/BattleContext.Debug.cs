using AbilityKit.Ability.Host.Extensions.FrameSync;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        private readonly ReferenceBindingOwner<BattlePredictionRuntime>
            _predictionRuntimeBinding =
                new ReferenceBindingOwner<BattlePredictionRuntime>();

        public IClientPredictionDriverStats PredictionStats =>
            _predictionRuntimeBinding.Value?.Stats;
        public IClientPredictionReconcileTarget PredictionReconcileTarget =>
            _predictionRuntimeBinding.Value?.ReconcileTarget;
        public IClientPredictionReconcileControl PredictionReconcileControl =>
            _predictionRuntimeBinding.Value?.ReconcileControl;
        public IClientPredictionTuningControl PredictionTuningControl =>
            _predictionRuntimeBinding.Value?.TuningControl;

        internal BattlePredictionRuntime PredictionRuntime =>
            EnsurePredictionRuntime();

        internal void BindPredictionRuntime(BattlePredictionRuntime runtime)
        {
            if (runtime == null)
                throw new System.ArgumentNullException(nameof(runtime));
            if (ReferenceEquals(_predictionRuntimeBinding.Value, runtime)) return;

            ReleasePredictionRuntimeBinding();
            _predictionRuntimeBinding.Bind(runtime);
            runtime.BindContext(this);
        }

        internal void UnbindPredictionRuntime(BattlePredictionRuntime runtime)
        {
            if (!_predictionRuntimeBinding.TryClear(
                    runtime,
                    out var released,
                    out _))
            {
                return;
            }

            released.UnbindContext(this);
        }

        private BattlePredictionRuntime EnsurePredictionRuntime()
        {
            var runtime = _predictionRuntimeBinding.Value;
            if (runtime != null) return runtime;

            runtime = new BattlePredictionRuntime();
            _predictionRuntimeBinding.Bind(runtime, ownsValue: true);
            runtime.BindContext(this);
            return runtime;
        }

        private void ResetPredictionRuntime() =>
            ReleasePredictionRuntimeBinding();

        private void ReleasePredictionRuntimeBinding()
        {
            if (!_predictionRuntimeBinding.Reset(
                    out var runtime,
                    out _))
            {
                return;
            }

            runtime.UnbindContext(this);
        }
    }
}
