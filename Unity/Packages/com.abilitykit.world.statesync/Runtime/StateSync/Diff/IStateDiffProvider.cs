using System;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 提供框架差分与序列化配对接口；兼容格式不等同于会话基线验证或业务重建。
    /// </summary>
    public interface IStateDiffProvider
    {
        /// <summary>
        /// 以调用方提供的前态计算差分；默认实现以前态为空选择全量记录。
        /// </summary>
        IStateDiff ComputeDiff<TState>(TState current, TState previous) where TState : class;
        /// <summary>
        /// 使用匹配的提供器配置及正确基线重建目标类型；默认实现不会验证基线身份。
        /// </summary>
        TState ApplyDiff<TState>(TState baseState, IStateDiff diff) where TState : class;
        /// <summary>
        /// 将状态转换为与本提供器匹配的表示，支持的状态类型由实现决定。
        /// </summary>
        byte[] SerializeState<TState>(TState state) where TState : class;
        /// <summary>
        /// 恢复与本提供器及写入类型匹配的状态，不承诺跨版本兼容。
        /// </summary>
        TState DeserializeState<TState>(byte[] data) where TState : class;
    }
}
