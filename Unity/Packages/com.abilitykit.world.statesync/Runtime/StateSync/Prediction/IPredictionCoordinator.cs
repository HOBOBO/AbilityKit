using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 预测协调器接口
    /// 通用接口，不依赖具体输入类型
    ///
    /// 【适用场景】
    /// 此接口适合以下类型的游戏：
    /// - FPS / 动作游戏：使用 ECS 风格，按系统（移动、技能）划分 Handler
    /// - Handler 是全局的，针对某一类数据（如所有角色的位置）进行处理
    /// - 共享的 StateSlots 用于所有 Handler
    ///
    /// 【不适用场景】
    /// - MOBA / ARPG：实体数量多，每个实体需要独立的预测逻辑
    ///   这种情况下建议使用 IClientPredictionModule
    ///
    /// 【与 IClientPredictionModule 的区别】
    /// - IClientPredictionModule：每个实体有独立的 StateSlots 和 Handler
    /// - IPredictionCoordinator：所有 Handler 共享同一个 StateSlots
    ///
    /// 【使用方式】
    /// 1. 实现 IPredictionHandler 接口定义 Handler（如 MovementHandler, CooldownHandler）
    /// 2. 注册 Handler 到 PredictionCoordinator
    /// 3. 每帧调用 ProcessInput 或 ProcessInputs 执行并记录本地预测
    /// 4. 调用 ApplyServerSnapshot 应用服务器校正，如有冲突自动回滚
    /// </summary>
    public interface IPredictionCoordinator
    {
        int LocalPlayerId { get; }
        int CurrentPredictedFrame { get; }
        int ServerConfirmedFrame { get; }
        bool NeedsRollback { get; }

        void ProcessInput(IInputCommand input);
        void ProcessInputs(IReadOnlyList<IInputCommand> inputs);
        void ApplyServerSnapshot(int serverFrame, int objectId, StateSync.Prediction.StateSlots serverSlots);
        void Reset();
    }
}
