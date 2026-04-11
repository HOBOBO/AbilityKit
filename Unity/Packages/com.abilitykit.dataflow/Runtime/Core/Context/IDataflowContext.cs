using System;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 数据流上下文接口
    /// 提供处理器之间的数据共享能力
    /// 使用强类型槽位（DataflowSlot）来保证类型安全
    /// </summary>
    public interface IDataflowContext
    {
        /// <summary>
        /// 获取上下文数据（使用槽位获取，推荐方式）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="slot">数据槽位</param>
        /// <returns>数据值，如果未设置则返回槽位默认值</returns>
        T GetData<T>(DataflowSlot<T> slot);

        /// <summary>
        /// 获取上下文数据（使用槽位获取，带显式默认值）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="slot">数据槽位</param>
        /// <param name="defaultValue">默认值（当槽位未设置时使用）</param>
        /// <returns>数据值</returns>
        T GetData<T>(DataflowSlot<T> slot, T defaultValue);

        /// <summary>
        /// 设置上下文数据（使用槽位设置，推荐方式）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="slot">数据槽位</param>
        /// <param name="value">要设置的值</param>
        void SetData<T>(DataflowSlot<T> slot, T value);

        /// <summary>
        /// 尝试获取上下文数据（使用槽位获取）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="slot">数据槽位</param>
        /// <param name="value">输出数据值</param>
        /// <returns>是否成功获取</returns>
        bool TryGetData<T>(DataflowSlot<T> slot, out T value);

        /// <summary>
        /// 检查是否包含指定槽位的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="slot">数据槽位</param>
        /// <returns>是否包含数据</returns>
        bool ContainsData<T>(DataflowSlot<T> slot);

        /// <summary>
        /// 获取数据流请求的源对象（如技能、Buff等）
        /// </summary>
        object Source { get; }

        /// <summary>
        /// 设置数据流请求的源对象
        /// </summary>
        void SetSource(object source);

        /// <summary>
        /// 是否被中断
        /// </summary>
        bool IsAborted { get; set; }

        /// <summary>
        /// 中断数据流执行
        /// </summary>
        void Abort();

        /// <summary>
        /// 清除所有上下文数据
        /// </summary>
        void Clear();
    }
}
