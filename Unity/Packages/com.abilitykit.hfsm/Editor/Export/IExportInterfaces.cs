using System;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// 导出选项
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
            includeConditions = true
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
            prettyPrint = true
        };
    }

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

    /// <summary>
    /// 导出器接口
    /// </summary>
    public interface IHfsmExporter
    {
        string Name { get; }
        string FileExtension { get; }
        string Description { get; }
        ExportResult Export(object graph, ExportOptions options);
    }

    /// <summary>
    /// 数据提取器接口
    /// </summary>
    public interface IHfsmDataExtractor
    {
        ExportedGraph Extract(object graph, ExportOptions options);
    }
}
