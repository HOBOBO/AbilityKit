using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// HFSM 导出管理器
    /// </summary>
    public static class HfsmExportManager
    {
        private static readonly Dictionary<string, IHfsmExporter> _exporters = new Dictionary<string, IHfsmExporter>();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化导出管理器，注册默认导出器
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            // 注册默认导出器
            RegisterExporter(new JsonExporter());
            RegisterExporter(new NewtonsoftJsonExporter());

            _initialized = true;
        }

        /// <summary>
        /// 注册导出器
        /// </summary>
        public static void RegisterExporter(IHfsmExporter exporter)
        {
            if (exporter == null)
                throw new ArgumentNullException(nameof(exporter));

            _exporters[exporter.Name.ToLower()] = exporter;
            Debug.Log($"[HfsmExportManager] Registered exporter: {exporter.Name}");
        }

        /// <summary>
        /// 注销导出器
        /// </summary>
        public static bool UnregisterExporter(string name)
        {
            return _exporters.Remove(name.ToLower());
        }

        /// <summary>
        /// 获取所有已注册的导出器
        /// </summary>
        public static IReadOnlyCollection<IHfsmExporter> GetExporters()
        {
            return _exporters.Values;
        }

        /// <summary>
        /// 获取指定类型的导出器
        /// </summary>
        public static IHfsmExporter GetExporter(string name)
        {
            _exporters.TryGetValue(name.ToLower(), out var exporter);
            return exporter;
        }

        /// <summary>
        /// 检查导出器是否已注册
        /// </summary>
        public static bool HasExporter(string name)
        {
            return _exporters.ContainsKey(name.ToLower());
        }

        /// <summary>
        /// 导出为 JSON（便捷方法）
        /// </summary>
        public static ExportResult ExportToJson(object graph, ExportOptions options = null)
        {
            var exporter = GetExporter("json") as JsonExporter;
            if (exporter == null)
            {
                return ExportResult.Fail("JSON exporter not found");
            }

            return exporter.Export(graph, options ?? ExportOptions.Default);
        }

        /// <summary>
        /// 导出到文件
        /// </summary>
        public static ExportResult ExportToFile(
            object graph,
            string path,
            string format = "json",
            ExportOptions options = null)
        {
            var exporter = GetExporter(format);
            if (exporter == null)
            {
                return ExportResult.Fail($"Unknown export format: {format}. Available formats: {string.Join(", ", GetAvailableFormats())}");
            }

            var result = exporter.Export(graph, options ?? ExportOptions.Default);
            if (!result.success)
            {
                return result;
            }

            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, result.data);
                Debug.Log($"[HfsmExportManager] Exported to: {path} ({result.data.Length} bytes, {result.elapsedMilliseconds}ms)");
                return result;
            }
            catch (Exception e)
            {
                return ExportResult.Fail($"Failed to write file: {e.Message}");
            }
        }

        /// <summary>
        /// 获取可用的导出格式列表
        /// </summary>
        public static IReadOnlyList<string> GetAvailableFormats()
        {
            var formats = new List<string>(_exporters.Count);
            foreach (var kvp in _exporters)
            {
                formats.Add(kvp.Value.Name);
            }
            return formats;
        }

        /// <summary>
        /// 获取导出器描述
        /// </summary>
        public static string GetExporterDescription(string name)
        {
            var exporter = GetExporter(name);
            return exporter?.Description ?? string.Empty;
        }

        /// <summary>
        /// 生成默认导出文件名
        /// </summary>
        public static string GenerateDefaultFileName(string graphName, string format)
        {
            var name = string.IsNullOrEmpty(graphName) ? "HfsmGraph" : graphName;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{name}_{timestamp}.{format}";
        }
    }
}
