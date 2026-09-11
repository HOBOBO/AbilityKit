#nullable enable

using System;

namespace AbilityKit.Samples.Logic
{
    /// <summary>
    /// 示例标记属性
    /// 标记在 SampleBase 子类上，用于自动注册到 SampleRunner
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SampleAttribute : Attribute
    {
        /// <summary>
        /// 示例优先级（越小越靠前）
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 示例标签
        /// </summary>
        public string[] Tags { get; }

        /// <summary>
        /// 标记一个示例类
        /// </summary>
        /// <param name="priority">优先级（越小越靠前）</param>
        /// <param name="tags">标签</param>
        public SampleAttribute(int priority = 100, params string[] tags)
        {
            Priority = priority;
            Tags = tags;
        }
    }
}
