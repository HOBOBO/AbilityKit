using System;

namespace AbilityKit.Ability.StateSync.Prediction
{
    [Flags]
    public enum PredictionCoordinatorBufferFeatures
    {
        None = 0,
        PredictedStateHistory = 1 << 0,
        InputHistory = 1 << 1,
        All = PredictedStateHistory | InputHistory
    }

    /// <summary>
    /// Selects and constructs the histories used by <see cref="PredictionCoordinator"/>.
    /// Rollback replay is available only when both histories are enabled.
    /// </summary>
    public sealed class PredictionCoordinatorBufferOptions
    {
        public const int DefaultCapacity = 30;

        private readonly Func<int, ISnapshotStore> _snapshotStoreFactory;
        private readonly Func<int, IInputHistory> _inputHistoryFactory;

        public PredictionCoordinatorBufferOptions(
            PredictionCoordinatorBufferFeatures features,
            int predictedStateHistoryCapacity = DefaultCapacity,
            int inputHistoryCapacity = DefaultCapacity,
            Func<int, ISnapshotStore> snapshotStoreFactory = null,
            Func<int, IInputHistory> inputHistoryFactory = null)
        {
            if ((features & ~PredictionCoordinatorBufferFeatures.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(features));

            ValidateCapacity(
                features,
                PredictionCoordinatorBufferFeatures.PredictedStateHistory,
                predictedStateHistoryCapacity,
                nameof(predictedStateHistoryCapacity));
            ValidateCapacity(
                features,
                PredictionCoordinatorBufferFeatures.InputHistory,
                inputHistoryCapacity,
                nameof(inputHistoryCapacity));

            if (!Has(features, PredictionCoordinatorBufferFeatures.PredictedStateHistory)
                && snapshotStoreFactory != null)
            {
                throw new ArgumentException(
                    "A snapshot store factory requires PredictedStateHistory.",
                    nameof(snapshotStoreFactory));
            }

            if (!Has(features, PredictionCoordinatorBufferFeatures.InputHistory)
                && inputHistoryFactory != null)
            {
                throw new ArgumentException(
                    "An input history factory requires InputHistory.",
                    nameof(inputHistoryFactory));
            }

            Features = features;
            PredictedStateHistoryCapacity = predictedStateHistoryCapacity;
            InputHistoryCapacity = inputHistoryCapacity;
            _snapshotStoreFactory = snapshotStoreFactory;
            _inputHistoryFactory = inputHistoryFactory;
        }

        public static PredictionCoordinatorBufferOptions Default { get; } =
            new PredictionCoordinatorBufferOptions(PredictionCoordinatorBufferFeatures.All);

        public static PredictionCoordinatorBufferOptions Disabled { get; } =
            new PredictionCoordinatorBufferOptions(
                PredictionCoordinatorBufferFeatures.None,
                predictedStateHistoryCapacity: 0,
                inputHistoryCapacity: 0);

        public PredictionCoordinatorBufferFeatures Features { get; }

        public int PredictedStateHistoryCapacity { get; }

        public int InputHistoryCapacity { get; }

        public bool Has(PredictionCoordinatorBufferFeatures feature)
        {
            return Has(Features, feature);
        }

        internal ISnapshotStore CreateSnapshotStore()
        {
            if (!Has(PredictionCoordinatorBufferFeatures.PredictedStateHistory)) return null;

            var store = _snapshotStoreFactory != null
                ? _snapshotStoreFactory(PredictedStateHistoryCapacity)
                : new DictionarySnapshotStore(PredictedStateHistoryCapacity);
            return store ?? throw new InvalidOperationException("The snapshot store factory returned null.");
        }

        internal IInputHistory CreateInputHistory()
        {
            if (!Has(PredictionCoordinatorBufferFeatures.InputHistory)) return null;

            var history = _inputHistoryFactory != null
                ? _inputHistoryFactory(InputHistoryCapacity)
                : new InputHistory(InputHistoryCapacity);
            return history ?? throw new InvalidOperationException("The input history factory returned null.");
        }

        private static bool Has(
            PredictionCoordinatorBufferFeatures features,
            PredictionCoordinatorBufferFeatures feature)
        {
            return (features & feature) == feature;
        }

        private static void ValidateCapacity(
            PredictionCoordinatorBufferFeatures features,
            PredictionCoordinatorBufferFeatures feature,
            int capacity,
            string parameterName)
        {
            if (Has(features, feature) && capacity <= 0)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
