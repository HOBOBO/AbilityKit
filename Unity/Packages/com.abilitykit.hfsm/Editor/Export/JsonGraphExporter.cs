// ============================================================================
// Graph Exporter - 导出器实现
// ============================================================================

using System;
using System.Diagnostics;
using UnityHFSM.Graph.Descriptor;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// JSON 导出器 - 使用描述器接口
    /// </summary>
    public class JsonGraphExporter : IGraphExporter
    {
        private readonly IGraphDataExtractor _extractor;
        private readonly IJsonSerializer _serializer;

        public string Name => "JSON";
        public string FileExtension => "json";
        public string Description => "导出为标准 JSON 格式，适用于运行时加载和调试";

        public JsonGraphExporter() : this(new GraphDataExtractor(), new UnityJsonSerializer())
        {
        }

        public JsonGraphExporter(IGraphDataExtractor extractor, IJsonSerializer serializer)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public ExportResult Export(IGraphDescriptor graph, ExportOptions options)
        {
            if (graph == null)
                return ExportResult.Fail("Graph is null");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var opts = options ?? ExportOptions.Default;
                var data = _extractor.Extract(graph, opts);
                var json = _serializer.Serialize(data, opts.prettyPrint);

                stopwatch.Stop();
                return ExportResult.Ok(json, FileExtension, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ExportResult.Fail($"Export failed: {ex.Message}");
            }
        }
    }
}
