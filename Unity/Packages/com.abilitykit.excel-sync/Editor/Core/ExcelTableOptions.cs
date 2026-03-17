namespace AbilityKit.ExcelSync.Editor
{
    public sealed class ExcelTableOptions
    {
        public int HeaderRowIndex { get; set; } = 6;
        public int DataStartRowIndex { get; set; } = 8;
        public string SheetName { get; set; } = "";
        public string PrimaryKeyColumnName { get; set; } = "code";
    }
}
