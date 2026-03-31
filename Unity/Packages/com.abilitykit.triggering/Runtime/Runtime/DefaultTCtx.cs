using System;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 默认轻量级上下文（当不需要上下文数据时使用）
    /// 提供空实现，避免每次都要定义一个空的 TCtx
    /// </summary>
    public readonly struct DefaultTCtx
    {
        /// <summary>
        /// 全局共享实例
        /// </summary>
        public static DefaultTCtx Instance => default;
    }

    /// <summary>
    /// 空上下文接口实现（用于不需要上下文的场景）
    /// </summary>
    public sealed class DefaultContextSource : ITriggerContextSource<DefaultTCtx>
    {
        public static DefaultContextSource Instance { get; } = new DefaultContextSource();

        private DefaultContextSource() { }

        public DefaultTCtx GetContext() => default;
    }
}
