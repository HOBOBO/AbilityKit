using System;
using System.Collections.Generic;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 数据流管线接口
    /// 定义数据处理流水线的行为
    /// </summary>
    public interface IDataflowPipeline
    {
        /// <summary>
        /// 管线名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 处理器数量
        /// </summary>
        int ProcessorCount { get; }

        /// <summary>
        /// 是否为空
        /// </summary>
        bool IsEmpty { get; }
    }

    /// <summary>
    /// 泛型数据流管线接口
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    public interface IDataflowPipeline<TInput, TOutput> : IDataflowPipeline
    {
        /// <summary>
        /// 执行管线
        /// </summary>
        DataflowResult<TOutput> Execute(TInput input, IDataflowContext context);

        /// <summary>
        /// 添加处理器
        /// </summary>
        IDataflowPipeline<TInput, TOutput> AddProcessor(IDataflowProcessor<TInput, TOutput> processor);

        /// <summary>
        /// 添加多个处理器
        /// </summary>
        IDataflowPipeline<TInput, TOutput> AddProcessors(params IDataflowProcessor<TInput, TOutput>[] processors);

        /// <summary>
        /// 在指定位置插入处理器
        /// </summary>
        IDataflowPipeline<TInput, TOutput> InsertProcessor(int index, IDataflowProcessor<TInput, TOutput> processor);

        /// <summary>
        /// 移除处理器
        /// </summary>
        bool RemoveProcessor(int index);

        /// <summary>
        /// 清空所有处理器
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取处理器
        /// </summary>
        IDataflowProcessor<TInput, TOutput> GetProcessor(int index);
    }

    /// <summary>
    /// 数据流管线
    /// 支持链式处理器的数据处理流水线
    /// </summary>
    /// <typeparam name="TInput">输入数据类型</typeparam>
    /// <typeparam name="TOutput">输出数据类型</typeparam>
    public class DataflowPipeline<TInput, TOutput> : IDataflowPipeline<TInput, TOutput>
    {
        private readonly List<IDataflowProcessor<TInput, TOutput>> _processors = new List<IDataflowProcessor<TInput, TOutput>>();

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public int ProcessorCount => _processors.Count;

        /// <inheritdoc />
        public bool IsEmpty => _processors.Count == 0;

        /// <summary>
        /// 创建数据流管线
        /// </summary>
        public DataflowPipeline(string name = null)
        {
            Name = name ?? typeof(TInput).Name + "->" + typeof(TOutput).Name;
        }

        /// <inheritdoc />
        public DataflowResult<TOutput> Execute(TInput input, IDataflowContext context)
        {
            if (context == null)
            {
                context = new DataflowContext();
            }

            if (context.IsAborted)
            {
                return DataflowResult<TOutput>.Aborted(default, 0);
            }

            var processors = _processors.ToArray();
            if (processors.Length == 0)
            {
                return DataflowResult<TOutput>.Success(default, 0);
            }

            int processedCount = 0;
            int failedProcessorIndex = -1;
            string failedProcessorName = null;
            var lastOutput = default(TOutput);
            var hasOutput = false;
            try
            {
                for (int i = 0; i < processors.Length; i++)
                {
                    if (context.IsAborted)
                    {
                        return DataflowResult<TOutput>.Aborted(
                            hasOutput ? lastOutput : default,
                            processedCount);
                    }

                    var processor = processors[i];
                    failedProcessorIndex = i;
                    failedProcessorName = processor.Name;
                    var output = processor.Process(input, context);
                    lastOutput = output;
                    hasOutput = true;

                    // 仅在类型兼容时把输出回灌为下一处理器输入。
                    // 这样同类型管线仍支持链式处理，DamageRequest -> DamageResult 这类管线也能安全返回结果。
                    if (output is TInput nextInput)
                    {
                        input = nextInput;
                    }

                    processedCount++;

                    if (context.IsAborted)
                    {
                        return DataflowResult<TOutput>.Aborted(lastOutput, processedCount);
                    }
                }

                return DataflowResult<TOutput>.Success(hasOutput ? lastOutput : default, processedCount);
            }
            catch (Exception ex)
            {
                return DataflowResult<TOutput>.Failure(
                    ex,
                    hasOutput ? lastOutput : default,
                    processedCount,
                    failedProcessorIndex,
                    failedProcessorName);
            }
        }

        /// <inheritdoc />
        public IDataflowPipeline<TInput, TOutput> AddProcessor(IDataflowProcessor<TInput, TOutput> processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            _processors.Add(processor);
            return this;
        }

        /// <inheritdoc />
        public IDataflowPipeline<TInput, TOutput> AddProcessors(params IDataflowProcessor<TInput, TOutput>[] processors)
        {
            if (processors == null)
            {
                throw new ArgumentNullException(nameof(processors));
            }

            for (var i = 0; i < processors.Length; i++)
            {
                if (processors[i] == null)
                    throw new ArgumentException("Processor batches cannot contain null entries.", nameof(processors));
            }

            _processors.AddRange(processors);
            return this;
        }

        /// <inheritdoc />
        public IDataflowPipeline<TInput, TOutput> InsertProcessor(int index, IDataflowProcessor<TInput, TOutput> processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            if (index < 0 || index > _processors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _processors.Insert(index, processor);
            return this;
        }

        /// <inheritdoc />
        public bool RemoveProcessor(int index)
        {
            if (index < 0 || index >= _processors.Count)
            {
                return false;
            }

            _processors.RemoveAt(index);
            return true;
        }

        /// <inheritdoc />
        public void Clear()
        {
            _processors.Clear();
        }

        /// <inheritdoc />
        public IDataflowProcessor<TInput, TOutput> GetProcessor(int index)
        {
            if (index < 0 || index >= _processors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _processors[index];
        }

        /// <summary>
        /// 复制管线
        /// </summary>
        public DataflowPipeline<TInput, TOutput> Clone()
        {
            var clone = new DataflowPipeline<TInput, TOutput>(Name);
            foreach (var processor in _processors)
            {
                clone.AddProcessor(processor);
            }
            return clone;
        }
    }

    /// <summary>
    /// 数据流管线构建器
    /// 提供流畅的管线构建接口
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    public class DataflowPipelineBuilder<TInput, TOutput>
    {
        private readonly DataflowPipeline<TInput, TOutput> _pipeline;

        public DataflowPipelineBuilder(string name = null)
        {
            _pipeline = new DataflowPipeline<TInput, TOutput>(name);
        }

        /// <summary>
        /// 添加处理器
        /// </summary>
        public DataflowPipelineBuilder<TInput, TOutput> Add(IDataflowProcessor<TInput, TOutput> processor)
        {
            _pipeline.AddProcessor(processor);
            return this;
        }

        /// <summary>
        /// 添加处理器类型（使用默认构造）
        /// </summary>
        public DataflowPipelineBuilder<TInput, TOutput> Add<TProcessor>() where TProcessor : IDataflowProcessor<TInput, TOutput>, new()
        {
            _pipeline.AddProcessor(new TProcessor());
            return this;
        }

        /// <summary>
        /// 添加处理动作
        /// </summary>
        public DataflowPipelineBuilder<TInput, TOutput> Add(Func<TInput, IDataflowContext, TOutput> process)
        {
            _pipeline.AddProcessor(new LambdaProcessor(process));
            return this;
        }

        /// <summary>
        /// 插入处理器
        /// </summary>
        public DataflowPipelineBuilder<TInput, TOutput> Insert(int index, IDataflowProcessor<TInput, TOutput> processor)
        {
            _pipeline.InsertProcessor(index, processor);
            return this;
        }

        /// <summary>
        /// 构建管线
        /// </summary>
        public DataflowPipeline<TInput, TOutput> Build()
        {
            return _pipeline.Clone();
        }

        /// <summary>
        /// Lambda 处理器
        /// </summary>
        private class LambdaProcessor : IDataflowProcessor<TInput, TOutput>
        {
            private readonly Func<TInput, IDataflowContext, TOutput> _process;

            public string Name => "LambdaProcessor";

            public LambdaProcessor(Func<TInput, IDataflowContext, TOutput> process)
            {
                _process = process ?? throw new ArgumentNullException(nameof(process));
            }

            public TOutput Process(TInput input, IDataflowContext context)
            {
                return _process(input, context);
            }
        }
    }

    /// <summary>
    /// 静态扩展方法
    /// </summary>
    public static class DataflowPipelineExtensions
    {
        /// <summary>
        /// 创建管线构建器
        /// </summary>
        public static DataflowPipelineBuilder<TInput, TOutput> NewPipeline<TInput, TOutput>(string name = null)
        {
            return new DataflowPipelineBuilder<TInput, TOutput>(name);
        }
    }
}
