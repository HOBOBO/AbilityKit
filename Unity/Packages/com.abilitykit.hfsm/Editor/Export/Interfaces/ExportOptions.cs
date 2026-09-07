// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

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
}
