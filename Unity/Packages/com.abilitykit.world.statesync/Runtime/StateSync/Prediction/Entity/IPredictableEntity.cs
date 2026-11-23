using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Client
{
    using StateSlots = AbilityKit.Ability.StateSync.Prediction.StateSlots;

    /// <summary>
    /// 由业务层实现的实体适配，连接业务状态与实体预测槽位；默认模块不拥有实体生命周期。
    /// </summary>
    public interface IPredictableEntity
    {
        /// <summary>
        /// 用于登记和校正匹配的标识，在登记有效期内应稳定且唯一。
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// 构造实体预测状态时读取的本地控制标记，后续修改不会刷新既有状态。
        /// </summary>
        bool IsLocalPlayer { get; }

        /// <summary>
        /// 供业务诊断或工厂选择的类型描述；默认模块不据此分派。
        /// </summary>
        string EntityType { get; }

        /// <summary>
        /// 返回按业务要求排序的处理器列表；默认模块只在登记时读取，空列表或空返回值都不登记。
        /// </summary>
        IReadOnlyList<IClientPredictionHandler> GetPredictionHandlers();

        /// <summary>
        /// 提供可复制的当前业务槽位；初始可为空，应用权威快照后不得为空。
        /// </summary>
        StateSlots GetStateSlots();

        /// <summary>
        /// 保留给业务调用方的预测状态写回入口；默认实体模块的回滚和推进均不调用它。
        /// </summary>
        void RestoreFromPredictedState(StateSlots slots);

        /// <summary>
        /// 解释并应用权威实体快照；成功后应能读到对应槽位，回调异常由调用方处理。
        /// </summary>
        void ApplyServerState(ServerEntitySnapshot snapshot);
    }

    /// <summary>
    /// 业务实现的实体级槽位处理器；同步执行，由宿主管理实例与副作用。
    /// </summary>
    public interface IClientPredictionHandler
    {
        /// <summary>
        /// 业务提供的处理器名称；默认模块不按名称排序或去重。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 预测策略；默认实体状态在本地预测中仅据此跳过禁用策略。
        /// </summary>
        PredictionStrategy Strategy { get; }

        /// <summary>
        /// 声明处理器所需槽位模式，默认实体模块不自动校验或补齐。
        /// </summary>
        IReadOnlyList<string> RequiredSlots { get; }

        /// <summary>
        /// 以输入和帧更新给定的实时预测槽位；异常向调用方传播。
        /// </summary>
        void PredictLocal(IInputCommand input, StateSlots slots, int frame);

        /// <summary>
        /// 提供可由宿主调用的预测验证逻辑；默认实体校正不通过此方法决定回滚。
        /// </summary>
        PredictionResult Validate(StateSlots predicted, ServerEntitySnapshot server);

        /// <summary>
        /// 按登记顺序将权威槽位合并到当前槽位；默认实体状态对全部登记处理器调用此方法。
        /// </summary>
        void ApplyServerState(StateSlots server, StateSlots current);
    }

    /// <summary>
    /// 供业务装配选择并创建处理器的兼容扩展点；默认模块直接向实体取处理器，不自动调用工厂。
    /// </summary>
    public interface IPredictionHandlerFactory
    {
        /// <summary>
        /// 由业务工厂判断是否支持传入的实体。
        /// </summary>
        bool CanHandle(IPredictableEntity entity);

        /// <summary>
        /// 为适用实体创建处理器；实例生命周期由使用工厂的宿主管理。
        /// </summary>
        IClientPredictionHandler CreateHandler(IPredictableEntity entity);
    }
}
