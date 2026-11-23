using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 驱动局部槽位预测并接收宿主已准入的权威状态。
    /// 处理器共享协调器槽位；选型依据状态所有权与重演要求，而非游戏类型。
    /// </summary>
    public interface IPredictionCoordinator
    {
        /// <summary>
        /// 返回协调器的本地目标标识。
        /// </summary>
        int LocalPlayerId { get; }
        /// <summary>
        /// 返回局部预测进度，不代表完整世界进度。
        /// </summary>
        int CurrentPredictedFrame { get; }
        /// <summary>
        /// 返回协调器已处理的权威状态帧，不表示输入已终结。
        /// </summary>
        int ServerConfirmedFrame { get; }
        /// <summary>
        /// 兼容属性：表示本地预测领先权威状态帧，不表示冲突或强制回滚。
        /// </summary>
        bool NeedsRollback { get; }

        /// <summary>
        /// 推进一个本地帧并执行单个命令。
        /// </summary>
        void ProcessInput(IInputCommand input);
        /// <summary>
        /// 推进一个本地帧，按提交顺序执行同帧批次。
        /// </summary>
        void ProcessInputs(IReadOnlyList<IInputCommand> inputs);
        /// <summary>
        /// 处理指定目标和权威帧的槽位；准入、基线验证及输入终结由宿主管理。
        /// </summary>
        void ApplyServerSnapshot(int serverFrame, int objectId, StateSync.Prediction.StateSlots serverSlots);
        /// <summary>
        /// 清除当前预测状态与历史进度，不要求重新装配处理器。
        /// </summary>
        void Reset();
    }
}
