using System;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 验证阶段处理器
    /// 用于验证输入数据的合法性
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    public abstract class ValidatorProcessor<TInput> : DataflowProcessor<TInput, TInput>
    {
        /// <summary>
        /// 验证失败时的默认输出
        /// </summary>
        protected virtual TInput DefaultOnFail => default;

        protected sealed override TInput OnProcess(TInput input, IDataflowContext context)
        {
            if (!Validate(input, context))
            {
                // 验证失败时输出默认值并中断
                context.Abort();
                return DefaultOnFail;
            }
            return input;
        }

        /// <summary>
        /// 验证逻辑
        /// </summary>
        protected abstract bool Validate(TInput input, IDataflowContext context);
    }

    /// <summary>
    /// 条件处理器
    /// 根据条件决定是否执行后续处理
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    public abstract class ConditionalProcessor<TInput, TOutput> : DataflowProcessor<TInput, TOutput>
    {
        protected sealed override TOutput OnProcess(TInput input, IDataflowContext context)
        {
            if (ShouldProcess(input, context))
            {
                return OnProcessWhenTrue(input, context);
            }
            return OnProcessWhenFalse(input, context);
        }

        /// <summary>
        /// 是否应该处理
        /// </summary>
        protected abstract bool ShouldProcess(TInput input, IDataflowContext context);

        /// <summary>
        /// 条件为真时的处理逻辑
        /// </summary>
        protected abstract TOutput OnProcessWhenTrue(TInput input, IDataflowContext context);

        /// <summary>
        /// 条件为假时的处理逻辑
        /// </summary>
        protected virtual TOutput OnProcessWhenFalse(TInput input, IDataflowContext context)
        {
            return default;
        }
    }

    /// <summary>
    /// 中断处理器
    /// 当满足条件时中断管线执行
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    public abstract class InterruptProcessor<TInput, TOutput> : DataflowProcessor<TInput, TOutput>
    {
        protected sealed override TOutput OnProcess(TInput input, IDataflowContext context)
        {
            if (ShouldInterrupt(input, context))
            {
                context.Abort();
            }
            return Transform(input, context);
        }

        /// <summary>
        /// 是否应该中断
        /// </summary>
        protected abstract bool ShouldInterrupt(TInput input, IDataflowContext context);

        /// <summary>
        /// 转换数据
        /// </summary>
        protected abstract TOutput Transform(TInput input, IDataflowContext context);
    }

    /// <summary>
    /// 组合处理器
    /// 将多个处理器组合成一个
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    public class CompositeProcessor<TInput, TOutput> : DataflowProcessor<TInput, TOutput>
    {
        private readonly IDataflowProcessor<TInput, TOutput>[] _processors;

        public CompositeProcessor(params IDataflowProcessor<TInput, TOutput>[] processors)
        {
            _processors = processors ?? throw new ArgumentNullException(nameof(processors));
        }

        protected override TOutput OnProcess(TInput input, IDataflowContext context)
        {
            TOutput current = default;
            bool first = true;
            foreach (var processor in _processors)
            {
                if (context.IsAborted)
                {
                    break;
                }
                if (first)
                {
                    current = processor.Process(input, context);
                    first = false;
                }
                else
                {
                    current = processor.Process(input, context);
                }
            }
            return current;
        }
    }

    /// <summary>
    /// 日志处理器
    /// 用于调试，输出管线执行信息
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    public class LoggingProcessor<TInput> : DataflowProcessor<TInput, TInput>
    {
        public string Message { get; set; }
        public bool LogInput { get; set; } = true;
        public bool LogOutput { get; set; } = true;

        public LoggingProcessor(string message = null)
        {
            Message = message;
        }

        protected override TInput OnProcess(TInput input, IDataflowContext context)
        {
            if (LogInput)
            {
                Log($"[Dataflow] {Message ?? Name} - Input: {input}");
            }

            if (LogOutput)
            {
                Log($"[Dataflow] {Message ?? Name} - Output: {input}");
            }

            return input;
        }

        protected virtual void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
