#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 示例接口 - 纯逻辑层定义
    /// </summary>
    public interface ISample
    {
        /// <summary>
        /// 示例标题
        /// </summary>
        string Title { get; }

        /// <summary>
        /// 示例描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 所属分类
        /// </summary>
        SampleCategory Category { get; }

        /// <summary>
        /// 运行示例
        /// </summary>
        void Run();
    }
}
