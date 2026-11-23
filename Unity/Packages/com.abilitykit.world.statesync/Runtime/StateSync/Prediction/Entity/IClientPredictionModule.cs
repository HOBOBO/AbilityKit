using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Client
{
    /// <summary>
    /// 实体级预测和权威槽位校正接口；默认实现不提供待确认输入重演或完整世界回滚。
    /// 由宿主串行驱动；事件在调用线程同步执行，不应重入正在处理的集合。
    /// </summary>
    public interface IClientPredictionModule : IDisposable
    {
        /// <summary>
        /// 返回初始化配置中的本地玩家标识。
        /// </summary>
        int LocalPlayerId { get; }

        /// <summary>
        /// 返回模块本地推进或校正使用的当前帧，不是权威帧同步游标。
        /// </summary>
        int CurrentFrame { get; }

        /// <summary>
        /// 返回默认实现的本地完成游标，由推进更新；不得用于服务端输入确认。
        /// </summary>
        int ConfirmedFrame { get; }

        /// <summary>
        /// 默认实现仅比较当前帧和本地完成游标，不检查待确认输入队列。
        /// </summary>
        bool HasUnconfirmedPrediction { get; }

        /// <summary>
        /// 配置模块并创建所需缓冲；默认实现不接受空配置或重复初始化。
        /// </summary>
        void Initialize(IClientPredictionConfig config);

        /// <summary>
        /// 登记业务可预测实体；默认实现对已登记标识不作替换。
        /// </summary>
        void RegisterEntity(IPredictableEntity entity);

        /// <summary>
        /// 按标识移除模块跟踪，不代表销毁业务实体。
        /// </summary>
        void UnregisterEntity(int entityId);

        /// <summary>
        /// 默认实现把输入交给当前帧的缓冲，不立即预测；缓冲禁用时忽略。
        /// </summary>
        void SubmitInput(IInputCommand input);

        /// <summary>
        /// 按本地帧处理输入、记录实体槽位历史并发布变化；不确认服务器输入。
        /// </summary>
        void Tick(int frame);

        /// <summary>
        /// 按指定帧应用已登记实体的权威快照；默认实现不验证帧顺序或重演待确认输入。
        /// </summary>
        void ApplyServerSnapshot(int serverFrame, ServerEntitySnapshot[] snapshots);

        /// <summary>
        /// 返回已登记实体的实时槽位引用，不是快照；未登记时返回空。
        /// </summary>
        AbilityKit.Ability.StateSync.Prediction.StateSlots GetPredictedSlots(int entityId);

        /// <summary>
        /// 返回模块跟踪的实体预测状态；未登记时返回空。
        /// </summary>
        IEntityPredictionState GetEntityState(int entityId);

        /// <summary>
        /// 推进时同步发布收集到的槽位变化；不承诺跨实体顺序。
        /// </summary>
        event Action<StateChangeEvent> OnStateChanged;

        /// <summary>
        /// 权威快照触发实际局部历史恢复时同步发布。
        /// </summary>
        event Action<RollbackEvent> OnRollback;

        /// <summary>
        /// 每次成功应用已登记实体的快照后，同步通知实体标识和校正帧。
        /// </summary>
        event Action<int, int> OnSnapshotApplied;
    }

    /// <summary>
    /// 提供实体模块初始化使用的玩家和缓冲配置；部分成员为兼容字段。
    /// </summary>
    public interface IClientPredictionConfig
    {
        /// <summary>
        /// 提供本地玩家标识，用于创建输入缓冲。
        /// </summary>
        int LocalPlayerId { get; }

        /// <summary>
        /// 未显式传入缓冲选项时作为每实体历史容量；不会限制预测领先帧数。
        /// </summary>
        int MaxPredictionFrames { get; }

        /// <summary>
        /// 保留的校正阈值配置，默认实体模块不读取或解释该值。
        /// </summary>
        float SnapThreshold { get; }

        /// <summary>
        /// 请求允许局部回滚；可由构造选项覆盖，且要求启用实体历史。
        /// </summary>
        bool EnableRollback { get; }

        /// <summary>
        /// 未显式指定缓冲选项时的输入容量，以保留帧计。
        /// </summary>
        int MaxInputBufferSize { get; }
    }

    /// <summary>
    /// 实体校正兼容对象，业务实体解释其内容；不代表完整世界检查点。
    /// </summary>
    public sealed class ServerEntitySnapshot
    {
        /// <summary>
        /// 快照所属实体标识；用于模块查找和实体状态的标识校验。
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// 调用方提供的帧元数据；默认模块使用方法的校正帧参数，不验证该值。
        /// </summary>
        public int Frame { get; set; }

        /// <summary>
        /// 由业务适配解释的兼容字段；赋值和读取不复制数组，默认模块不解释其内容。
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 由调用方填写的一致性元数据；默认实体模块不验证该值。
        /// </summary>
        public long StateHash { get; set; }
    }

    /// <summary>
    /// 可变的默认实体预测配置，每次读取默认工厂属性都会创建新实例。
    /// </summary>
    public sealed class ClientPredictionConfig : IClientPredictionConfig
    {
        /// <summary>
        /// 本地玩家标识，默认 0。
        /// </summary>
        public int LocalPlayerId { get; set; }
        /// <summary>
        /// 默认实体历史容量 30 帧，不是预测领先上限。
        /// </summary>
        public int MaxPredictionFrames { get; set; } = 30;
        /// <summary>
        /// 兼容阈值默认 0.1；默认实体模块不读取该值，不规定其业务单位。
        /// </summary>
        public float SnapThreshold { get; set; } = 0.1f;
        /// <summary>
        /// 回滚请求默认为真；真正启用仍取决于覆盖值及历史功能。
        /// </summary>
        public bool EnableRollback { get; set; } = true;
        /// <summary>
        /// 默认输入缓冲容量为 128 个保留帧。
        /// </summary>
        public int MaxInputBufferSize { get; set; } = 128;

        /// <summary>
        /// 创建新的独立默认配置实例；修改不会影响其他实例。
        /// </summary>
        public static ClientPredictionConfig Default => new ClientPredictionConfig
        {
            LocalPlayerId = 0,
            MaxPredictionFrames = 30,
            SnapThreshold = 0.1f,
            EnableRollback = true,
            MaxInputBufferSize = 128
        };
    }
}
