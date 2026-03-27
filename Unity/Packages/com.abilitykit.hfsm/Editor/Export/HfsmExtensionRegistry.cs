// ============================================================================
// HFSM Extension Registry - 扩展点注册系统
// 允许包外代码注册自定义的导出器、数据提取器等扩展点
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityHFSM.Graph.Descriptor;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// HFSM 扩展点注册表
    /// 提供静态方法供包外代码注册各种扩展点
    /// </summary>
    public static class HfsmExtensionRegistry
    {
        private static readonly Dictionary<string, IGraphExporter> _exporters = new Dictionary<string, IGraphExporter>();
        private static readonly Dictionary<string, IGraphDataExtractor> _dataExtractors = new Dictionary<string, IGraphDataExtractor>();
        private static readonly List<IGraphExtension> _extensions = new List<IGraphExtension>();

        private static bool _initialized = false;

        // ========================================================================
        // 初始化
        // ========================================================================

        /// <summary>
        /// 初始化默认扩展点
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            // 注册默认导出器
            RegisterExporter(new JsonGraphExporter());

            // 注册默认数据提取器
            RegisterDataExtractor(new GraphDataExtractor());

            _initialized = true;
        }

        /// <summary>
        /// 确保已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        // ========================================================================
        // 导出器管理
        // ========================================================================

        /// <summary>
        /// 注册导出器
        /// </summary>
        /// <param name="exporter">导出器实例</param>
        /// <exception cref="ArgumentNullException">当 exporter 为 null 时</exception>
        /// <exception cref="InvalidOperationException">当已存在同名导出器时</exception>
        public static void RegisterExporter(IGraphExporter exporter)
        {
            if (exporter == null)
                throw new ArgumentNullException(nameof(exporter));

            if (_exporters.ContainsKey(exporter.Name))
                throw new InvalidOperationException($"Exporter with name '{exporter.Name}' is already registered");

            _exporters[exporter.Name] = exporter;
        }

        /// <summary>
        /// 取消注册导出器
        /// </summary>
        /// <param name="name">导出器名称</param>
        /// <returns>是否成功取消注册</returns>
        public static bool UnregisterExporter(string name)
        {
            return _exporters.Remove(name);
        }

        /// <summary>
        /// 获取导出器
        /// </summary>
        /// <param name="name">导出器名称</param>
        /// <returns>导出器实例，不存在则返回 null</returns>
        public static IGraphExporter GetExporter(string name)
        {
            EnsureInitialized();
            _exporters.TryGetValue(name, out var exporter);
            return exporter;
        }

        /// <summary>
        /// 获取所有已注册的导出器
        /// </summary>
        public static IReadOnlyCollection<IGraphExporter> GetAllExporters()
        {
            EnsureInitialized();
            return _exporters.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取所有导出器信息
        /// </summary>
        public static IReadOnlyList<ExporterInfo> GetExporterInfos()
        {
            EnsureInitialized();
            return _exporters.Values.Select(e => new ExporterInfo(e.Name, e.FileExtension, e.Description, e.GetType())).ToList();
        }

        /// <summary>
        /// 检查是否存在指定导出器
        /// </summary>
        public static bool HasExporter(string name)
        {
            return _exporters.ContainsKey(name);
        }

        // ========================================================================
        // 数据提取器管理
        // ========================================================================

        /// <summary>
        /// 注册数据提取器
        /// </summary>
        /// <param name="extractor">数据提取器实例</param>
        /// <exception cref="ArgumentNullException">当 extractor 为 null 时</exception>
        /// <exception cref="InvalidOperationException">当已存在同名数据提取器时</exception>
        public static void RegisterDataExtractor(IGraphDataExtractor extractor)
        {
            if (extractor == null)
                throw new ArgumentNullException(nameof(extractor));

            if (_dataExtractors.ContainsKey(extractor.Name))
                throw new InvalidOperationException($"Data extractor with name '{extractor.Name}' is already registered");

            _dataExtractors[extractor.Name] = extractor;
        }

        /// <summary>
        /// 取消注册数据提取器
        /// </summary>
        public static bool UnregisterDataExtractor(string name)
        {
            return _dataExtractors.Remove(name);
        }

        /// <summary>
        /// 获取数据提取器
        /// </summary>
        public static IGraphDataExtractor GetDataExtractor(string name)
        {
            _dataExtractors.TryGetValue(name, out var extractor);
            return extractor;
        }

        /// <summary>
        /// 获取所有数据提取器
        /// </summary>
        public static IReadOnlyCollection<IGraphDataExtractor> GetAllDataExtractors()
        {
            return _dataExtractors.Values.ToList().AsReadOnly();
        }

        // ========================================================================
        // 扩展点注册
        // ========================================================================

        /// <summary>
        /// 注册图形扩展
        /// </summary>
        public static void RegisterExtension(IGraphExtension extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            if (!_extensions.Contains(extension))
            {
                _extensions.Add(extension);
            }
        }

        /// <summary>
        /// 取消注册图形扩展
        /// </summary>
        public static bool UnregisterExtension(IGraphExtension extension)
        {
            return _extensions.Remove(extension);
        }

        /// <summary>
        /// 获取所有图形扩展
        /// </summary>
        public static IReadOnlyList<IGraphExtension> GetAllExtensions()
        {
            return _extensions.AsReadOnly();
        }

        // ========================================================================
        // 便捷导出方法
        // ========================================================================

        /// <summary>
        /// 使用默认导出器导出为 JSON
        /// </summary>
        public static ExportResult ExportToJson(IGraphDescriptor graph, ExportOptions options = null)
        {
            var exporter = GetExporter("JSON") as JsonGraphExporter;
            if (exporter == null)
                return ExportResult.Fail("JSON exporter not found");

            return exporter.Export(graph, options ?? ExportOptions.ForRuntime);
        }

        /// <summary>
        /// 使用指定名称的导出器导出
        /// </summary>
        public static ExportResult Export(IGraphDescriptor graph, string exporterName, ExportOptions options = null)
        {
            var exporter = GetExporter(exporterName);
            if (exporter == null)
                return ExportResult.Fail($"Exporter '{exporterName}' not found");

            return exporter.Export(graph, options ?? ExportOptions.Default);
        }

        // ========================================================================
        // 调试和诊断
        // ========================================================================

        /// <summary>
        /// 获取诊断信息
        /// </summary>
        public static string GetDiagnostics()
        {
            var lines = new List<string>
            {
                "=== HFSM Extension Registry Diagnostics ===",
                $"Initialized: {_initialized}",
                $"Exporters: {_exporters.Count}",
                $"Data Extractors: {_dataExtractors.Count}",
                $"Extensions: {_extensions.Count}",
                "",
                "Registered Exporters:"
            };

            foreach (var kvp in _exporters)
            {
                lines.Add($"  - {kvp.Key}: {kvp.Value.GetType().Name}");
            }

            lines.Add("");
            lines.Add("Registered Data Extractors:");

            foreach (var kvp in _dataExtractors)
            {
                lines.Add($"  - {kvp.Key}: {kvp.Value.GetType().Name}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 重置注册表（主要用于测试）
        /// </summary>
        public static void Reset()
        {
            _exporters.Clear();
            _dataExtractors.Clear();
            _extensions.Clear();
            _initialized = false;
        }
    }

    // ========================================================================
    // 图形扩展接口
    // ========================================================================

    /// <summary>
    /// 图形扩展接口 - 提供额外的图形处理能力
    /// </summary>
    public interface IGraphExtension
    {
        /// <summary>
        /// 扩展名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 优先级（数值越小越先执行）
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 在导出前调用
        /// </summary>
        void OnBeforeExport(IGraphDescriptor graph, ExportOptions options);

        /// <summary>
        /// 在导出后调用
        /// </summary>
        void OnAfterExport(IGraphDescriptor graph, ExportOptions options, ExportResult result);
    }
}
