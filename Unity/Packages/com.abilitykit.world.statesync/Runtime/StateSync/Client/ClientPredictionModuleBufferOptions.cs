using System;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Client
{
    [Flags]
    public enum ClientPredictionModuleBufferFeatures
    {
        None = 0,
        InputBuffer = 1 << 0,
        EntitySnapshotHistory = 1 << 1,
        All = InputBuffer | EntitySnapshotHistory
    }

    /// <summary>
    /// Selects and constructs buffers used by <see cref="ClientPredictionModule"/>.
    /// </summary>
    public sealed class ClientPredictionModuleBufferOptions
    {
        public const int DefaultInputBufferCapacity = 128;
        public const int DefaultEntitySnapshotCapacity = 30;

        private readonly Func<int, int, IInputBuffer<IInputCommand>> _inputBufferFactory;
        private readonly Func<int, int, ISnapshotStore> _entitySnapshotStoreFactory;

        public ClientPredictionModuleBufferOptions(
            ClientPredictionModuleBufferFeatures features,
            int inputBufferCapacity = DefaultInputBufferCapacity,
            int entitySnapshotCapacity = DefaultEntitySnapshotCapacity,
            Func<int, int, IInputBuffer<IInputCommand>> inputBufferFactory = null,
            Func<int, int, ISnapshotStore> entitySnapshotStoreFactory = null)
        {
            if ((features & ~ClientPredictionModuleBufferFeatures.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(features));

            ValidateCapacity(
                features,
                ClientPredictionModuleBufferFeatures.InputBuffer,
                inputBufferCapacity,
                nameof(inputBufferCapacity));
            ValidateCapacity(
                features,
                ClientPredictionModuleBufferFeatures.EntitySnapshotHistory,
                entitySnapshotCapacity,
                nameof(entitySnapshotCapacity));

            if (!Has(features, ClientPredictionModuleBufferFeatures.InputBuffer)
                && inputBufferFactory != null)
            {
                throw new ArgumentException(
                    "An input buffer factory requires InputBuffer.",
                    nameof(inputBufferFactory));
            }

            if (!Has(features, ClientPredictionModuleBufferFeatures.EntitySnapshotHistory)
                && entitySnapshotStoreFactory != null)
            {
                throw new ArgumentException(
                    "An entity snapshot store factory requires EntitySnapshotHistory.",
                    nameof(entitySnapshotStoreFactory));
            }

            Features = features;
            InputBufferCapacity = inputBufferCapacity;
            EntitySnapshotCapacity = entitySnapshotCapacity;
            _inputBufferFactory = inputBufferFactory;
            _entitySnapshotStoreFactory = entitySnapshotStoreFactory;
        }

        public static ClientPredictionModuleBufferOptions Default { get; } =
            new ClientPredictionModuleBufferOptions(ClientPredictionModuleBufferFeatures.All);

        public static ClientPredictionModuleBufferOptions Disabled { get; } =
            new ClientPredictionModuleBufferOptions(
                ClientPredictionModuleBufferFeatures.None,
                inputBufferCapacity: 0,
                entitySnapshotCapacity: 0);

        public static ClientPredictionModuleBufferOptions CreateDefault(
            int inputBufferCapacity,
            int entitySnapshotCapacity)
        {
            return new ClientPredictionModuleBufferOptions(
                ClientPredictionModuleBufferFeatures.All,
                inputBufferCapacity,
                entitySnapshotCapacity);
        }

        /// <summary>
        /// Creates the standard client prediction buffers with resizable circular storage.
        /// Custom factories remain available for mixed or application-specific backends.
        /// </summary>
        public static ClientPredictionModuleBufferOptions CreateRingBacked(
            int inputBufferCapacity,
            int entitySnapshotCapacity)
        {
            return new ClientPredictionModuleBufferOptions(
                ClientPredictionModuleBufferFeatures.All,
                inputBufferCapacity,
                entitySnapshotCapacity,
                (playerId, capacity) => new InputBuffer<IInputCommand>(
                    playerId,
                    new RingFrameIndexedBuffer<IInputCommand>(capacity)),
                (_, capacity) => new DictionarySnapshotStore(
                    new RingFrameIndexedBuffer<StateSlots>(capacity)));
        }

        public ClientPredictionModuleBufferFeatures Features { get; }

        public int InputBufferCapacity { get; }

        public int EntitySnapshotCapacity { get; }

        public bool Has(ClientPredictionModuleBufferFeatures feature)
        {
            return Has(Features, feature);
        }

        internal IInputBuffer<IInputCommand> CreateInputBuffer(int localPlayerId)
        {
            if (!Has(ClientPredictionModuleBufferFeatures.InputBuffer)) return null;

            var buffer = _inputBufferFactory != null
                ? _inputBufferFactory(localPlayerId, InputBufferCapacity)
                : new InputBuffer<IInputCommand>(localPlayerId, InputBufferCapacity);
            return buffer ?? throw new InvalidOperationException("The input buffer factory returned null.");
        }

        internal ISnapshotStore CreateEntitySnapshotStore(int entityId)
        {
            if (!Has(ClientPredictionModuleBufferFeatures.EntitySnapshotHistory)) return null;

            var store = _entitySnapshotStoreFactory != null
                ? _entitySnapshotStoreFactory(entityId, EntitySnapshotCapacity)
                : new DictionarySnapshotStore(EntitySnapshotCapacity);
            return store ?? throw new InvalidOperationException(
                "The entity snapshot store factory returned null.");
        }

        private static bool Has(
            ClientPredictionModuleBufferFeatures features,
            ClientPredictionModuleBufferFeatures feature)
        {
            return (features & feature) == feature;
        }

        private static void ValidateCapacity(
            ClientPredictionModuleBufferFeatures features,
            ClientPredictionModuleBufferFeatures feature,
            int capacity,
            string parameterName)
        {
            if (Has(features, feature) && capacity <= 0)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
