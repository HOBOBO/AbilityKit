// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

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
}
