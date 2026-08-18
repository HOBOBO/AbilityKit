using System;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    [Flags]
    public enum ClientPredictionDriverBufferFeatures
    {
        None = 0,
        AppliedInputHistory = 1 << 0,
        AuthoritativeInputHistory = 1 << 1,
        PredictedStateHashHistory = 1 << 2,
        AuthoritativeStateHashHistory = 1 << 3,
        RollbackSnapshots = 1 << 4,
        All = AppliedInputHistory
            | AuthoritativeInputHistory
            | PredictedStateHashHistory
            | AuthoritativeStateHashHistory
            | RollbackSnapshots
    }

    /// <summary>
    /// Selects the prediction histories assembled by ClientPredictionDriverModule.
    /// </summary>
    public sealed class ClientPredictionDriverBufferOptions
    {
        public const int DefaultCapacity = 240;

        public ClientPredictionDriverBufferOptions(
            ClientPredictionDriverBufferFeatures features,
            int inputHistoryCapacity = DefaultCapacity,
            int stateHashHistoryCapacity = DefaultCapacity,
            int rollbackSnapshotCapacity = DefaultCapacity)
        {
            if ((features & ~ClientPredictionDriverBufferFeatures.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(features));
            }

            ValidateCapacity(
                features,
                ClientPredictionDriverBufferFeatures.AppliedInputHistory,
                inputHistoryCapacity,
                nameof(inputHistoryCapacity));
            ValidateCapacity(
                features,
                ClientPredictionDriverBufferFeatures.AuthoritativeInputHistory,
                inputHistoryCapacity,
                nameof(inputHistoryCapacity));
            ValidateCapacity(
                features,
                ClientPredictionDriverBufferFeatures.PredictedStateHashHistory,
                stateHashHistoryCapacity,
                nameof(stateHashHistoryCapacity));
            ValidateCapacity(
                features,
                ClientPredictionDriverBufferFeatures.AuthoritativeStateHashHistory,
                stateHashHistoryCapacity,
                nameof(stateHashHistoryCapacity));
            ValidateCapacity(
                features,
                ClientPredictionDriverBufferFeatures.RollbackSnapshots,
                rollbackSnapshotCapacity,
                nameof(rollbackSnapshotCapacity));

            Features = features;
            InputHistoryCapacity = inputHistoryCapacity;
            StateHashHistoryCapacity = stateHashHistoryCapacity;
            RollbackSnapshotCapacity = rollbackSnapshotCapacity;
        }

        public static ClientPredictionDriverBufferOptions Default { get; } =
            new ClientPredictionDriverBufferOptions(ClientPredictionDriverBufferFeatures.All);

        public static ClientPredictionDriverBufferOptions Disabled { get; } =
            new ClientPredictionDriverBufferOptions(
                ClientPredictionDriverBufferFeatures.None,
                inputHistoryCapacity: 0,
                stateHashHistoryCapacity: 0,
                rollbackSnapshotCapacity: 0);

        public static ClientPredictionDriverBufferOptions CreateDefault(
            bool enableRollback,
            bool enableStateHashReconciliation,
            int capacity = DefaultCapacity)
        {
            if (!enableRollback)
            {
                return Disabled;
            }

            var features = ClientPredictionDriverBufferFeatures.AppliedInputHistory
                | ClientPredictionDriverBufferFeatures.AuthoritativeInputHistory
                | ClientPredictionDriverBufferFeatures.RollbackSnapshots;
            if (enableStateHashReconciliation)
            {
                features |= ClientPredictionDriverBufferFeatures.PredictedStateHashHistory
                    | ClientPredictionDriverBufferFeatures.AuthoritativeStateHashHistory;
            }

            if (capacity <= 0)
            {
                capacity = DefaultCapacity;
            }

            return new ClientPredictionDriverBufferOptions(
                features,
                inputHistoryCapacity: capacity,
                stateHashHistoryCapacity: capacity,
                rollbackSnapshotCapacity: capacity);
        }

        public ClientPredictionDriverBufferFeatures Features { get; }

        public int InputHistoryCapacity { get; }

        public int StateHashHistoryCapacity { get; }

        public int RollbackSnapshotCapacity { get; }

        public bool Has(ClientPredictionDriverBufferFeatures feature)
        {
            return (Features & feature) == feature;
        }

        private static void ValidateCapacity(
            ClientPredictionDriverBufferFeatures features,
            ClientPredictionDriverBufferFeatures feature,
            int capacity,
            string parameterName)
        {
            if ((features & feature) != 0 && capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
