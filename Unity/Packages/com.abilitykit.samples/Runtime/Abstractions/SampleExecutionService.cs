#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 为通过按钮、列表或菜单管理示例的宿主运行目录项。
    /// </summary>
    public sealed class SampleExecutionService
    {
        private readonly SampleCatalog _catalog;
        private readonly Func<ExecutionMode, ISampleEnvironment> _environmentFactory;
        private readonly IConfigProvider? _config;
        private readonly IResourceProvider? _resources;

        /// <summary>
        /// 创建执行服务。
        /// </summary>
        public SampleExecutionService(
            SampleCatalog catalog,
            Func<ExecutionMode, ISampleEnvironment> environmentFactory,
            IConfigProvider? config = null,
            IResourceProvider? resources = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _environmentFactory = environmentFactory ?? throw new ArgumentNullException(nameof(environmentFactory));
            _config = config;
            _resources = resources;
        }

        /// <summary>
        /// 根据宿主无关的请求运行示例。
        /// </summary>
        public IReadOnlyList<SampleExecutionResult> Run(SampleRunRequest request, ILogger output)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            var results = new List<SampleExecutionResult>();
            switch (request.SelectionKind)
            {
                case SampleRunSelectionKind.Index:
                    if (!request.Index.HasValue)
                        throw new ArgumentException("Index request must include an index.", nameof(request));
                    results.Add(RunByIndex(request.Index.Value, output, request.Options));
                    break;
                case SampleRunSelectionKind.Id:
                    results.Add(RunById(request.Id, output, request.Options));
                    break;
                case SampleRunSelectionKind.All:
                    foreach (var entry in _catalog.Entries)
                    {
                        results.Add(Run(entry, output, request.Options));
                    }
                    break;
                default:
                    throw new ArgumentException("Sample run request did not select a sample.", nameof(request));
            }

            return results;
        }

        /// <summary>
        /// 按显示索引运行示例。
        /// </summary>
        public SampleExecutionResult RunByIndex(int index, ILogger output, SampleRunOptions? options = null)
        {
            if (!_catalog.TryGetByIndex(index, out var entry))
                throw new ArgumentOutOfRangeException(nameof(index), $"Sample index not found: {index}");

            return Run(entry, output, options);
        }

        /// <summary>
        /// 按稳定 ID 运行示例。
        /// </summary>
        public SampleExecutionResult RunById(string id, ILogger output, SampleRunOptions? options = null)
        {
            if (!_catalog.TryGetById(id, out var entry))
                throw new ArgumentException($"Sample id not found: {id}", nameof(id));

            return Run(entry, output, options);
        }

        /// <summary>
        /// 按稳定 ID 启动示例，并返回由宿主驱动的运行句柄。
        /// </summary>
        public SampleRunHandle StartById(string id, ILogger output, SampleRunOptions? options = null)
        {
            if (!_catalog.TryGetById(id, out var entry))
                throw new ArgumentException($"Sample id not found: {id}", nameof(id));

            return Start(entry, output, options);
        }

        /// <summary>
        /// 按显示索引启动示例，并返回由宿主驱动的运行句柄。
        /// </summary>
        public SampleRunHandle StartByIndex(int index, ILogger output, SampleRunOptions? options = null)
        {
            if (!_catalog.TryGetByIndex(index, out var entry))
                throw new ArgumentOutOfRangeException(nameof(index), $"Sample index not found: {index}");

            return Start(entry, output, options);
        }

        /// <summary>
        /// 使用宿主提供的输出和选项运行目录项。
        /// </summary>
        public SampleExecutionResult Run(SampleCatalogEntry entry, ILogger output, SampleRunOptions? options = null)
        {
            using var handle = Start(entry, output, options);
            return handle.Result ?? new SampleExecutionResult(entry, succeeded: true);
        }

        /// <summary>
        /// 启动目录项，并保留其环境供宿主驱动 Tick。
        /// </summary>
        public SampleRunHandle Start(SampleCatalogEntry entry, ILogger output, SampleRunOptions? options = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            var runOptions = options ?? new SampleRunOptions();
            var environment = _environmentFactory(runOptions.ExecutionMode);
            var context = new SampleRuntimeContext(
                output,
                environment,
                runOptions.HostKind,
                _config,
                _resources,
                runOptions.OutputDirectory,
                runOptions.HostCapabilities,
                runOptions.Inputs);

            try
            {
                var sample = entry.CreateSample();
                if (sample is SampleBase sampleBase)
                {
                    sampleBase.Initialize(context);
                }

                sample.Run();
                output.Flush();
                return new SampleRunHandle(entry, sample, environment, output, new SampleExecutionResult(entry, succeeded: true));
            }
            catch (Exception ex)
            {
                output.Error(ex.Message);
                output.Flush();
                return new SampleRunHandle(entry, null, environment, output, new SampleExecutionResult(entry, succeeded: false, ex.Message, ex));
            }
        }
    }
}
