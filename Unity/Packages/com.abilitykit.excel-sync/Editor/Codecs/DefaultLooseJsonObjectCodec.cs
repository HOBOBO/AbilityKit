using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    internal sealed class DefaultLooseJsonObjectCodec : IExcelValueCodec
    {
        public bool TryDecode(object cellValue, Type targetType, ExcelCodecContext context, out object value)
        {
            value = null;
            if (targetType == null)
            {
                return false;
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (nonNullable.IsPrimitive || nonNullable.IsEnum || nonNullable == typeof(string))
            {
                return false;
            }

            if (nonNullable.IsGenericType && nonNullable.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                return false;
            }

            var s = cellValue as string;
            if (s == null)
            {
                return false;
            }

            s = s.Trim();
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            if (!s.StartsWith("{") && s.Contains(":"))
            {
                s = "{" + s + "}";
            }

            if (!(s.StartsWith("{") && s.EndsWith("}")))
            {
                return false;
            }

            try
            {
                var normalized = NormalizeLooseJsonObject(s);
                value = JsonConvert.DeserializeObject(normalized, nonNullable);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        public bool TryEncode(object value, ExcelCodecContext context, out object cellValue)
        {
            cellValue = null;
            if (value == null)
            {
                return true;
            }

            var t = value.GetType();
            if (t.IsPrimitive || t.IsEnum || t == typeof(string))
            {
                return false;
            }

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                return false;
            }

            try
            {
                var json = JsonConvert.SerializeObject(value, Formatting.None);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                cellValue = json;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeLooseJsonObject(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            s = s.Trim();
            s = Regex.Replace(s, "(^|[\\{,]\\s*)([A-Za-z_][A-Za-z0-9_]*)\\s*:\\s*", m => $"{m.Groups[1].Value}\"{m.Groups[2].Value}\":");
            s = s.Replace("'", "\"");
            return s;
        }
    }
}
