#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Ability.Core.Pipeline
{
    /// <summary>
    /// 管线执行时的上下文数据接口。
    /// 用于在不同阶段之间传递数据和状态。
    /// </summary>
    public interface IPipelineContext
    {
        /// <summary>
        /// 上下文的唯一标识符。
        /// </summary>
        int ContextId { get; }

        /// <summary>
        /// 获取上下文中存储的值。
        /// </summary>
        T? GetData<T>(string key);

        /// <summary>
        /// 设置上下文中存储的值。
        /// </summary>
        void SetData<T>(string key, T value);

        /// <summary>
        /// 检查是否存在指定键的值。
        /// </summary>
        bool HasData(string key);

        /// <summary>
        /// 移除指定键的值。
        /// </summary>
        void RemoveData(string key);
    }
}
