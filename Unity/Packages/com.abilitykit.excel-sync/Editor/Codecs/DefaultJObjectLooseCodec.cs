using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    internal sealed class DefaultJObjectLooseCodec : IExcelValueCodec
    {
        public bool TryDecode(object cellValue, Type targetType, ExcelCodecContext context, out object value)
        {
            value = null;
            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (nonNullable != typeof(JObject))
            {
                return false;
            }

            var s = cellValue?.ToString() ?? string.Empty;
            s = s.Trim();
            if (string.IsNullOrEmpty(s) || string.Equals(s, "null", StringComparison.OrdinalIgnoreCase))
            {
                value = null;
                return true;
            }

            value = JObject.Parse(NormalizeLooseJsonObject(s));
            return true;
        }

        public bool TryEncode(object value, ExcelCodecContext context, out object cellValue)
        {
            cellValue = null;
            if (value == null)
            {
                return true;
            }

            if (value is JObject jo)
            {
                cellValue = FormatLooseJsonObject(jo);
                return true;
            }

            if (value is JToken token)
            {
                if (token.Type == JTokenType.Object)
                {
                    cellValue = FormatLooseJsonObject((JObject)token);
                    return true;
                }

                cellValue = token.ToString(Newtonsoft.Json.Formatting.None);
                return true;
            }

            return false;
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

        private static string FormatLooseJsonObject(JObject jo)
        {
            if (jo == null)
            {
                return string.Empty;
            }

            var props = jo.Properties();
            var parts = new System.Collections.Generic.List<string>();
            foreach (var p in props)
            {
                var token = p.Value;
                string valueStr;
                if (token == null || token.Type == JTokenType.Null)
                {
                    valueStr = "null";
                }
                else if (token.Type == JTokenType.String)
                {
                    valueStr = Newtonsoft.Json.JsonConvert.ToString(token.Value<string>());
                }
                else
                {
                    valueStr = token.ToString(Newtonsoft.Json.Formatting.None);
                }

                parts.Add($"{p.Name}:{valueStr}");
            }

            return "{" + string.Join(",", parts) + "}";
        }
    }
}
