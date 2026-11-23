using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Client
{
    using SlotValue = AbilityKit.Ability.StateSync.SlotValue;

    /// <summary>
    /// 单实体槽位历史与权威校正接口，不等同于业务世界恢复。
    /// </summary>
    public interface IEntityPredictionState
    {
        /// <summary>
        /// 预测状态所属实体标识。
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// 模块决定是否转发本地输入所用的实体控制标记。
        /// </summary>
        bool IsLocalPlayer { get; }

        /// <summary>
        /// 返回实时可变槽位；如需留存，应使用相应的复制或快照机制。
        /// </summary>
        AbilityKit.Ability.StateSync.Prediction.StateSlots CurrentSlots { get; }

        /// <summary>
        /// 返回当前槽位的预测标记，不是远端确认状态。
        /// </summary>
        bool IsPredicted { get; }

        /// <summary>
        /// 返回实体槽位游标，不保证与模块游标相同。
        /// </summary>
        int CurrentFrame { get; }

        /// <summary>
        /// 同步通知槽位名、旧值和新值；不是跨实体排序流。
        /// </summary>
        event Action<string, object, object> OnSlotChanged;

        /// <summary>
        /// 实际局部历史恢复后，通知回滚前帧和目标帧。
        /// </summary>
        event Action<int, int> OnRollback;

        /// <summary>
        /// 默认实现将非空处理器追加到登记队列，允许重复。
        /// </summary>
        void RegisterHandler(IClientPredictionHandler handler);

        /// <summary>
        /// 以指定帧运行本地槽位预测；默认实现不自动捕获历史或写回业务实体。
        /// </summary>
        void Predict(IInputCommand input, int frame);

        /// <summary>
        /// 应用权威槽位校正；返回是否发生局部历史恢复，不表示哈希验证结果。
        /// </summary>
        bool ApplyServerState(int serverFrame, ServerEntitySnapshot snapshot);

        /// <summary>
        /// 按精确帧尝试恢复槽位；默认实现缺历史、未启用或目标不早于当前帧时无操作。
        /// </summary>
        void RollbackTo(int frame);

        /// <summary>
        /// 以指定帧记下当前槽位；未配置历史时不记录。
        /// </summary>
        void CaptureSnapshot(int frame);

        /// <summary>
        /// 按精确帧查询历史槽位，缺帧时返回空；所有权以存储约定为准。
        /// </summary>
        AbilityKit.Ability.StateSync.Prediction.StateSlots GetSnapshot(int frame);

        /// <summary>
        /// 推进实体内部游标；不运行整世界或帧同步逻辑。
        /// </summary>
        void AdvanceFrame();

        /// <summary>
        /// 返回待发布变化；默认实现提供会随收集和清理变化的实时只读视图。
        /// </summary>
        IReadOnlyList<StateChangeEvent> GetPendingStateChanges();

        /// <summary>
        /// 清空待发布变化，不修改预测槽位或快照存储。
        /// </summary>
        void ClearPendingStateChanges();
    }
}
