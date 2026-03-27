// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;

namespace UnityHFSM.Editor.Export
{
    // ========================================================================
    // 导出选项
    // ========================================================================

    /// <summary>
    /// 导出选项 - 控制导出的内容和格式
    /// </summary>
    [Serializable]
    public class ExportOptions
    {
        /// <summary>
        /// 是否包含编辑器元数据（位置、大小等）
        /// </summary>
        public bool includeEditorMetadata = false;

        /// <summary>
        /// 是否美化输出（格式化 JSON）
        /// </summary>
        public bool prettyPrint = true;

        /// <summary>
        /// 是否包含节点 ID（用于调试和引用追踪）
        /// </summary>
        public bool includeNodeIds = true;

        /// <summary>
        /// 是否包含行为树详情
        /// </summary>
        public bool includeBehaviors = true;

        /// <summary>
        /// 是否包含转换条件详情
        /// </summary>
        public bool includeConditions = true;

        /// <summary>
        /// 是否包含参数默认值
        /// </summary>
        public bool includeParameterDefaults = true;

        /// <summary>
        /// 导出格式版本
        /// </summary>
        public string version = "1.0";

        /// <summary>
        /// 导出目标平台
        /// </summary>
        public string targetPlatform = "Generic";

        /// <summary>
        /// 创建默认选项
        /// </summary>
        public static ExportOptions Default => new ExportOptions();

        /// <summary>
        /// 创建用于运行时导出的选项
        /// </summary>
        public static ExportOptions ForRuntime => new ExportOptions
        {
            includeEditorMetadata = false,
            includeNodeIds = true,
            includeBehaviors = true,
            includeConditions = true,
            includeParameterDefaults = true,
            prettyPrint = false
        };

        /// <summary>
        /// 创建用于存档/备份的选项
        /// </summary>
        public static ExportOptions ForArchive => new ExportOptions
        {
            includeEditorMetadata = true,
            includeNodeIds = true,
            includeBehaviors = true,
            includeConditions = true,
            includeParameterDefaults = true,
            prettyPrint = true
        };
    }

    // ========================================================================
    // 导出结果
    // ========================================================================

    /// <summary>
    /// 导出结果
    /// </summary>
    [Serializable]
    public class ExportResult
    {
        public bool success;
        public string data;
        public string fileExtension;
        public string errorMessage;
        public long elapsedMilliseconds;

        public static ExportResult Ok(string data, string extension, long elapsedMs = 0) => new ExportResult
        {
            success = true,
            data = data,
            fileExtension = extension,
            elapsedMilliseconds = elapsedMs
        };

        public static ExportResult Fail(string error) => new ExportResult
        {
            success = false,
            errorMessage = error
        };
    }

    // ========================================================================
    // 导出器接口
    // ========================================================================

    /// <summary>
    /// 导出器接口 - 定义如何将图数据导出为特定格式
    /// 包外可以通过实现此接口来添加自定义导出格式
    /// </summary>
    public interface IGraphExporter
    {
        /// <summary>
        /// 导出器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 文件扩展名
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// 导出器描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 导出图数据为特定格式
        /// </summary>
        /// <param name="graph">图描述器</param>
        /// <param name="options">导出选项</param>
        /// <returns>导出结果</returns>
        ExportResult Export(Graph.Descriptor.IGraphDescriptor graph, ExportOptions options);
    }

    // ========================================================================
    // 数据提取器接口
    // ========================================================================

    /// <summary>
    /// 数据提取器接口 - 定义如何从图描述器提取数据到可序列化 DTO
    /// 包外可以通过实现此接口来自定义数据提取逻辑
    /// </summary>
    public interface IGraphDataExtractor
    {
        /// <summary>
        /// 提取器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 从图描述器提取数据
        /// </summary>
        /// <param name="graph">图描述器</param>
        /// <param name="options">导出选项</param>
        /// <returns>导出的图数据</returns>
        ExportGraphData Extract(Graph.Descriptor.IGraphDescriptor graph, ExportOptions options);
    }

    // ========================================================================
    // 序列化器接口
    // ========================================================================

    /// <summary>
    /// JSON 序列化器接口 - 定义如何序列化对象为 JSON
    /// </summary>
    public interface IJsonSerializer
    {
        /// <summary>
        /// 序列化器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        string Serialize<T>(T obj, bool prettyPrint = false) where T : class;

        /// <summary>
        /// 从 JSON 字符串反序列化对象
        /// </summary>
        T Deserialize<T>(string json) where T : class;
    }

    // ========================================================================
    // 导出器信息
    // ========================================================================

    /// <summary>
    /// 导出器信息 - 用于在编辑器 UI 中显示可用导出器
    /// </summary>
    public struct ExporterInfo
    {
        public readonly string Name;
        public readonly string FileExtension;
        public readonly string Description;
        public readonly Type ExporterType;

        public ExporterInfo(string name, string fileExtension, string description, Type exporterType)
        {
            Name = name;
            FileExtension = fileExtension;
            Description = description;
            ExporterType = exporterType;
        }
    }

    /// <summary>
    /// 数据提取器信息
    /// </summary>
    public struct DataExtractorInfo
    {
        public readonly string Name;
        public readonly Type ExtractorType;

        public DataExtractorInfo(string name, Type extractorType)
        {
            Name = name;
            ExtractorType = extractorType;
        }
    }
}
