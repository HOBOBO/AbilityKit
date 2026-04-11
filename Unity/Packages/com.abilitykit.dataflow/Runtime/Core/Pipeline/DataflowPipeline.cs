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

            if (_processors.Count == 0)
            {
                return DataflowResult<TOutput>.Success(default, 0);
            }

            int processedCount = 0;
            try
            {
                for (int i = 0; i < _processors.Count; i++)
                {
                    // 检查是否被中断
                    if (context.IsAborted)
                    {
                        return DataflowResult<TOutput>.Aborted(default, processedCount);
                    }

                    // 执行处理器
                    var processor = _processors[i];
                    var output = processor.Process(input, context);

                    // 更新输入为输出（支持链式处理）
                    if (output != null)
                    {
                        input = (TInput)(object)output;
                    }

                    processedCount++;
                }

                return DataflowResult<TOutput>.Success((TOutput)(object)input, processedCount);
            }
            catch (Exception ex)
            {
                return DataflowResult<TOutput>.Failure(ex, default, processedCount);
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
            foreach (var processor in processors)
            {
                AddProcessor(processor);
            }
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
            return _pipeline;
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
