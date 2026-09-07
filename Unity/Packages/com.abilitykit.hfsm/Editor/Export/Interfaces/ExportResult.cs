// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

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
}
