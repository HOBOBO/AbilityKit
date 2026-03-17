using System;
using System.Collections.Generic;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    public sealed class ExcelCodecRegistry
    {
        private static ExcelCodecRegistry s_default;
        public static ExcelCodecRegistry Default => s_default ??= CreateDefault();

        private readonly List<IExcelValueCodec> codecs = new List<IExcelValueCodec>();

        private readonly Dictionary<string, char[]> listSeparatorsByColumn = new Dictionary<string, char[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, char> listPreferredSeparatorByColumn = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);

        public void Register(IExcelValueCodec codec)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }
            codecs.Add(codec);
        }

        public IReadOnlyList<IExcelValueCodec> GetCodecs() => codecs;

        public void SetListSeparators(string columnName, params char[] separators)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            if (separators == null || separators.Length == 0)
            {
                listSeparatorsByColumn.Remove(columnName);
                return;
            }

            listSeparatorsByColumn[columnName] = separators;
        }

        public void SetListPreferredSeparator(string columnName, char separator)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            listPreferredSeparatorByColumn[columnName] = separator;
        }

        public char[] GetListSeparators(string columnName)
        {
            if (!string.IsNullOrWhiteSpace(columnName) && listSeparatorsByColumn.TryGetValue(columnName, out var seps) && seps != null && seps.Length > 0)
            {
                return seps;
            }

            return new[] { ',', ';', '|', ' ' };
        }

        public char GetListPreferredSeparator(string columnName)
        {
            if (!string.IsNullOrWhiteSpace(columnName) && listPreferredSeparatorByColumn.TryGetValue(columnName, out var sep))
            {
                return sep;
            }

            return ',';
        }

        private static ExcelCodecRegistry CreateDefault()
        {
            var r = new ExcelCodecRegistry();
            r.Register(new DefaultPrimitiveCodec());
            r.Register(new DefaultJObjectLooseCodec());
            r.Register(new DefaultListCodec());
            r.Register(new DefaultLooseJsonObjectCodec());
            return r;
        }
    }
}
