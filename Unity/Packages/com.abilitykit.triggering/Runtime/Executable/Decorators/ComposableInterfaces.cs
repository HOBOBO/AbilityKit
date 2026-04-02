using System;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 可组合行为接口 (Decorator 模式的核心)
    // ========================================================================

    /// <summary>
    /// 可组合行为接口 - 所有修饰器的基础
    /// 允许通过装饰器模式包装和扩展行为
    /// </summary>
    public interface IComposableExecutable : ISimpleExecutable
    {
        /// <summary>被包装的内部行为</summary>
        ISimpleExecutable Inner { get; set; }

        /// <summary>在执行前调用 (返回 false 则跳过)</summary>
        bool OnBeforeExecute(object ctx);

        /// <summary>在执行后调用</summary>
        void OnAfterExecute(object ctx, ref ExecutionResult result);
    }

    /// <summary>
    /// 可组合行为基类 - 提供默认实现
    /// </summary>
    public abstract class ComposableExecutableBase : IComposableExecutable
    {
        public abstract string Name { get; }
        public abstract ExecutableMetadata Metadata { get; }

        public ISimpleExecutable Inner { get; set; }

        public virtual ExecutionResult Execute(object ctx)
        {
            if (!OnBeforeExecute(ctx))
            {
                return ExecutionResult.Skipped("Decorator condition not met");
            }

            ExecutionResult result = Inner?.Execute(ctx) ?? ExecutionResult.Success();
            OnAfterExecute(ctx, ref result);
            return result;
        }

        public virtual bool OnBeforeExecute(object ctx) => true;

        public virtual void OnAfterExecute(object ctx, ref ExecutionResult result) { }
    }
}
