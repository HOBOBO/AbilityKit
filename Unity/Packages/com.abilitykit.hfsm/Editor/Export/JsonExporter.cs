using System;
using System.Diagnostics;
using UnityEngine;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// JSON 导出器
    /// </summary>
    public class JsonExporter : IHfsmExporter
    {
        private readonly IHfsmDataExtractor _extractor;
        private readonly IJsonSerializer _serializer;

        public string Name => "JSON";

        public string FileExtension => "json";

        public string Description => "导出为标准 JSON 格式，适用于运行时加载和调试";

        public JsonExporter() : this(new HfsmDataExtractor(), new UnityJsonSerializer())
        {
        }

        public JsonExporter(IHfsmDataExtractor extractor, IJsonSerializer serializer)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public ExportResult Export(object graph, ExportOptions options)
        {
            if (graph == null)
            {
                return ExportResult.Fail("Graph cannot be null");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 提取数据
                var exportedGraph = _extractor.Extract(graph, options);

                // 序列化为 JSON
                string json = _serializer.Serialize(exportedGraph, options?.prettyPrint ?? true);

                stopwatch.Stop();

                return ExportResult.Ok(json, FileExtension, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                return ExportResult.Fail($"Export failed: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 使用 Newtonsoft JSON 的导出器
    /// </summary>
    public class NewtonsoftJsonExporter : IHfsmExporter
    {
        private readonly IHfsmDataExtractor _extractor;
        private readonly NewtonsoftJsonSerializer _serializer;

        public string Name => "Newtonsoft JSON";

        public string FileExtension => "json";

        public string Description => "使用 Newtonsoft JSON 库导出，支持更多特性如小写属性名";

        public NewtonsoftJsonExporter() : this(new HfsmDataExtractor())
        {
        }

        public NewtonsoftJsonExporter(IHfsmDataExtractor extractor)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _serializer = new NewtonsoftJsonSerializer();
        }

        public ExportResult Export(object graph, ExportOptions options)
        {
            if (graph == null)
            {
                return ExportResult.Fail("Graph cannot be null");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var exportedGraph = _extractor.Extract(graph, options);
                string json = _serializer.Serialize(exportedGraph, options?.prettyPrint ?? true);

                stopwatch.Stop();

                return ExportResult.Ok(json, FileExtension, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                return ExportResult.Fail($"Export failed: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
