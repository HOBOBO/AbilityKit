namespace AbilityKit.ExcelSync.Editor
{
    public interface ITableReaderWriterFactory
    {
        ITableReader CreateReader(string filePath, ExcelTableOptions options);
        ITableWriter CreateWriter(string filePath, ExcelTableOptions options);
    }
}
