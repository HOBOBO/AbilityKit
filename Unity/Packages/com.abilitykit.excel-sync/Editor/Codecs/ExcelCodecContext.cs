using System;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    public readonly struct ExcelCodecContext
    {
        public ExcelCodecContext(string columnName, ExcelCodecRegistry registry)
        {
            ColumnName = columnName;
            Registry = registry;
        }

        public string ColumnName { get; }

        public ExcelCodecRegistry Registry { get; }

        public char[] GetListSeparatorsOrDefault() => Registry?.GetListSeparators(ColumnName);

        public char GetListPreferredSeparatorOrDefault() => Registry?.GetListPreferredSeparator(ColumnName) ?? ',';

        public static ExcelCodecContext Empty => new ExcelCodecContext(null, ExcelCodecRegistry.Default);
    }
}
