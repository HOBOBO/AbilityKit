using System;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 数据流处理器接口
    /// 处理器负责处理输入数据并产生输出数据
    /// </summary>
    /// <typeparam name="TInput">输入数据类型</typeparam>
    /// <typeparam name="TOutput">输出数据类型</typeparam>
    public interface IDataflowProcessor<in TInput, TOutput>
    {
        /// <summary>
        /// 处理器名称（用于调试）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 处理数据
        /// </summary>
        /// <param name="input">输入数据</param>
        /// <param name="context">数据流上下文</param>
        /// <returns>处理后的输出数据</returns>
        TOutput Process(TInput input, IDataflowContext context);
    }

    /// <summary>
    /// 数据流处理器基类
    /// 提供通用的处理器实现模板
    /// </summary>
    /// <typeparam name="TInput">输入数据类型</typeparam>
    /// <typeparam name="TOutput">输出数据类型</typeparam>
    public abstract class DataflowProcessor<TInput, TOutput> : IDataflowProcessor<TInput, TOutput>
    {
        public virtual string Name => GetType().Name;

        public virtual TOutput Process(TInput input, IDataflowContext context)
        {
            OnBeforeProcess(input, context);
            var result = OnProcess(input, context);
            OnAfterProcess(input, context, result);
            return result;
        }

        /// <summary>
        /// 处理前的钩子方法
        /// </summary>
        protected virtual void OnBeforeProcess(TInput input, IDataflowContext context) { }

        /// <summary>
        /// 核心处理逻辑（子类必须实现）
        /// </summary>
        protected abstract TOutput OnProcess(TInput input, IDataflowContext context);

        /// <summary>
        /// 处理后的钩子方法
        /// </summary>
        protected virtual void OnAfterProcess(TInput input, IDataflowContext context, TOutput result) { }
    }

    /// <summary>
    /// 输入输出同类型的处理器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public abstract class DataflowProcessor<T> : DataflowProcessor<T, T>
    {
        protected override T OnProcess(T input, IDataflowContext context)
        {
            return input;
        }
    }
}
