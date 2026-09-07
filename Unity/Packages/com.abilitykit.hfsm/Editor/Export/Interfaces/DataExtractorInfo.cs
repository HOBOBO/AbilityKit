// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

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
