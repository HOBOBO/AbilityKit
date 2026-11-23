using System;
using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Prediction
{
    /// <summary>
    /// 标记需要创建的局部预测历史；启用单一历史不等于启用回滚重演。
    /// </summary>
    [Flags]
    public enum PredictionCoordinatorBufferFeatures
    {
        /// <summary>
        /// 不创建预测快照或输入历史。
        /// </summary>
        None = 0,
        /// <summary>
        /// 保留用于同帧权威比较的预测槽位快照。
        /// </summary>
        PredictedStateHistory = 1 << 0,
        /// <summary>
        /// 保留各预测帧的输入引用和同帧顺序。
        /// </summary>
        InputHistory = 1 << 1,

        /// <summary>
        /// 同时创建预测快照与输入历史，满足重演所需的历史配置。
        /// </summary>
        All = PredictedStateHistory | InputHistory
    }

    /// <summary>
    /// 选择历史能力、初始容量和创建策略；选项本身不持有运行中的历史。
    /// </summary>
    public sealed class PredictionCoordinatorBufferOptions
    {
        /// <summary>
        /// 各历史的默认容量为三十个帧位置，不是毫秒。
        /// </summary>
        public const int DefaultCapacity = 30;

        private readonly Func<int, ISnapshotStore> _snapshotStoreFactory;
        private readonly Func<int, IInputHistory> _inputHistoryFactory;

        /// <summary>
        /// 设置按帧计的初始容量和可选后端工厂。启用项容量必须为正，禁止给禁用项提供工厂。
        /// 工厂须为每个协调器提供可独立使用的非空实例；默认使用稀疏帧后端。
        /// </summary>
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

        /// <summary>
        /// 启用两类历史，各使用默认容量和默认稀疏帧后端。
        /// </summary>
        public static PredictionCoordinatorBufferOptions Default { get; } =
            new PredictionCoordinatorBufferOptions(PredictionCoordinatorBufferFeatures.All);

        /// <summary>
        /// 禁用两类历史，配置容量均为零。
        /// </summary>
        public static PredictionCoordinatorBufferOptions Disabled { get; } =
            new PredictionCoordinatorBufferOptions(
                PredictionCoordinatorBufferFeatures.None,
                predictedStateHistoryCapacity: 0,
                inputHistoryCapacity: 0);

        /// <summary>
        /// 为两类历史分别使用可调整容量的环形帧后端；默认各保留三十个帧位置。
        /// </summary>
        public static PredictionCoordinatorBufferOptions CreateRingBacked(
            int predictedStateHistoryCapacity = DefaultCapacity,
            int inputHistoryCapacity = DefaultCapacity)
        {
            return new PredictionCoordinatorBufferOptions(
                PredictionCoordinatorBufferFeatures.All,
                predictedStateHistoryCapacity,
                inputHistoryCapacity,
                capacity => new DictionarySnapshotStore(
                    new RingFrameIndexedBuffer<StateSlots>(capacity)),
                capacity => new InputHistory(
                    new RingFrameIndexedBuffer<List<IInputCommand>>(capacity)));
        }

        /// <summary>
        /// 返回构造时配置的历史能力位集合。
        /// </summary>
        public PredictionCoordinatorBufferFeatures Features { get; }

        /// <summary>
        /// 返回预测快照后端的初始容量配置；禁用时不创建存储。
        /// </summary>
        public int PredictedStateHistoryCapacity { get; }

        /// <summary>
        /// 返回输入历史后端的初始容量配置；禁用时不创建存储。
        /// </summary>
        public int InputHistoryCapacity { get; }

        /// <summary>
        /// 判断给定掩码的所有位是否已配置；零掩码始终匹配。
        /// </summary>
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
