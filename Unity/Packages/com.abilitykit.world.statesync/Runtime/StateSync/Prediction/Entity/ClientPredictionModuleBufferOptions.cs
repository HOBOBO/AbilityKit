using System;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Client
{
    /// <summary>
    /// 选择实体预测模块的输入缓存与槽位历史，两项能力独立启用。
    /// </summary>
    [Flags]
    public enum ClientPredictionModuleBufferFeatures
    {
        /// <summary>
        /// 不创建输入缓冲或实体历史存储。
        /// </summary>
        None = 0,
        /// <summary>
        /// 按本地帧保留输入，供实体模块读取。
        /// </summary>
        InputBuffer = 1 << 0,
        /// <summary>
        /// 为登记实体保留槽位历史，是启用回滚的必要条件。
        /// </summary>
        EntitySnapshotHistory = 1 << 1,
        /// <summary>
        /// 同时开启输入缓冲和实体快照历史。
        /// </summary>
        All = InputBuffer | EntitySnapshotHistory
    }

    /// <summary>
    /// 不可变的实体预测缓冲选项，工厂分别为模块及登记实体提供存储。
    /// 共享或外部管理的存储由宿主协调；模块不承诺并发安全。
    /// </summary>
    public sealed class ClientPredictionModuleBufferOptions
    {
        /// <summary>
        /// 默认输入容量为 128 个保留帧。
        /// </summary>
        public const int DefaultInputBufferCapacity = 128;
        /// <summary>
        /// 默认每实体历史容量为 30 个保留帧。
        /// </summary>
        public const int DefaultEntitySnapshotCapacity = 30;

        private readonly Func<int, int, IInputBuffer<IInputCommand>> _inputBufferFactory;
        private readonly Func<int, int, ISnapshotStore> _entitySnapshotStoreFactory;

        /// <summary>
        /// 创建缓冲选项；启用项要求正容量，禁用项不得提供工厂，未知标志会抛异常。
        /// 工厂分别接收玩家或实体标识及容量；启用后创建时不得返回空，工厂异常向调用方传播。
        /// </summary>
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

        /// <summary>
        /// 共享的默认选项，同时开启两项缓冲，容量分别为 128 和 30。
        /// </summary>
        public static ClientPredictionModuleBufferOptions Default { get; } =
            new ClientPredictionModuleBufferOptions(ClientPredictionModuleBufferFeatures.All);

        /// <summary>
        /// 共享的全禁用选项，两项容量均为 0。
        /// </summary>
        public static ClientPredictionModuleBufferOptions Disabled { get; } =
            new ClientPredictionModuleBufferOptions(
                ClientPredictionModuleBufferFeatures.None,
                inputBufferCapacity: 0,
                entitySnapshotCapacity: 0);

        /// <summary>
        /// 以给定正容量同时开启两项缓冲并使用默认存储。
        /// </summary>
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
        /// 以给定正容量同时开启两项缓冲，并使用环形帧索引存储。
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

        /// <summary>
        /// 返回该选项启用的功能位集合。
        /// </summary>
        public ClientPredictionModuleBufferFeatures Features { get; }

        /// <summary>
        /// 返回传给输入缓冲工厂的容量；禁用输入缓冲时不使用此值。
        /// </summary>
        public int InputBufferCapacity { get; }

        /// <summary>
        /// 返回传给每实体存储工厂的容量；禁用实体历史时不使用此值。
        /// </summary>
        public int EntitySnapshotCapacity { get; }

        /// <summary>
        /// 返回参数中的功能位是否全部包含在配置中；查询零值标志返回真。
        /// </summary>
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
