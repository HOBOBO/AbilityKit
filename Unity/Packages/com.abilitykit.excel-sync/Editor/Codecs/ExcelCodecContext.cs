using System.Collections.Generic;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    public readonly struct ExcelCodecContext
    {
        private readonly Dictionary<string, string> _customParameters;

        public ExcelCodecContext(string columnName, ExcelCodecRegistry registry, Dictionary<string, string> customParameters = null)
        {
            ColumnName = columnName;
            Registry = registry;
            _customParameters = customParameters ?? new Dictionary<string, string>();
        }

        public string ColumnName { get; }

        public ExcelCodecRegistry Registry { get; }

        public char[] GetListSeparatorsOrDefault() => Registry?.GetListSeparators(ColumnName);

        public char GetListPreferredSeparatorOrDefault() => Registry?.GetListPreferredSeparator(ColumnName) ?? ',';

        public string GetCustomParameter(string key)
        {
            return _customParameters?.TryGetValue(key, out var value) == true ? value : null;
        }

        public static ExcelCodecContext Empty => new ExcelCodecContext(null, ExcelCodecRegistry.Default);
    }
}
