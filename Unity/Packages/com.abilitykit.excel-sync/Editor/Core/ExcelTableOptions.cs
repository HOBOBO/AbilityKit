namespace AbilityKit.ExcelSync.Editor
{
    public sealed class ExcelTableOptions
    {
        public int HeaderRowIndex { get; set; } = 6;
        public int DataStartRowIndex { get; set; } = 8;
        public string SheetName { get; set; } = "";
        public string PrimaryKeyColumnName { get; set; } = "code";

        /// <summary>
        /// 写出 Luban 布局的标记列：A 列写 ##var / ##type / ##，字段行、类型行与数据行整体右移一列。
        /// 仅影响"新写文件"（Bootstrap）；读取与三方合并无需开关——标记列在读取时自然成为 headers[0]，
        /// 字段绑定按列索引自洽，不受影响。
        /// </summary>
        public bool LubanMarkers { get; set; } = false;
    }
}
