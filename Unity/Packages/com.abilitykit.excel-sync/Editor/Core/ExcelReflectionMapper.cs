using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AbilityKit.ExcelSync.Editor.Codecs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.ExcelSync.Editor
{
    internal static class ExcelReflectionMapper
    {
        public sealed class ColumnBinding
        {
            public string ColumnName;
            public int ColumnIndex;
            public MemberInfo Member;
            public Type ValueType;
            public Dictionary<string, string> CustomParameters;
        }

        public static IReadOnlyList<ColumnBinding> BuildBindings(Type targetType, IReadOnlyList<string> headers)
        {
            var headerToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var h = headers[i]?.Trim();
                if (string.IsNullOrWhiteSpace(h))
                {
                    continue;
                }

                if (!headerToIndex.ContainsKey(h))
                {
                    headerToIndex.Add(h, i);
                }
            }

            var members = new List<(MemberInfo member, string name, int order)>();

            foreach (var f in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var n = attr?.Name ?? f.Name;
                var o = attr?.Order ?? int.MaxValue;
                members.Add((f, n, o));
            }

            foreach (var p in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var n = attr?.Name ?? p.Name;
                var o = attr?.Order ?? int.MaxValue;
                members.Add((p, n, o));
            }

            var ordered = members.OrderBy(x => x.order).ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase).ToList();
            var result = new List<ColumnBinding>();
            foreach (var m in ordered)
            {
                if (!headerToIndex.TryGetValue(m.name, out var idx))
                {
                    continue;
                }

                var vt = GetMemberType(m.member);
                var attr = m.member.GetCustomAttribute<ExcelColumnAttribute>();
                result.Add(new ColumnBinding
                {
                    ColumnName = m.name,
                    ColumnIndex = idx,
                    Member = m.member,
                    ValueType = vt,
                    CustomParameters = attr?.CustomParameters ?? new Dictionary<string, string>()
                });
            }

            return result;
        }

        public static IReadOnlyList<string> BuildHeaders(Type targetType)
        {
            var members = new List<(MemberInfo member, string name, int order)>();

            foreach (var f in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                members.Add((f, attr?.Name ?? f.Name, attr?.Order ?? int.MaxValue));
            }

            foreach (var p in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                members.Add((p, attr?.Name ?? p.Name, attr?.Order ?? int.MaxValue));
            }

            return members
                .OrderBy(x => x.order)
                .ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.name)
                .ToList();
        }

        public static void SetValue(object target, MemberInfo member, object value)
        {
            if (member is FieldInfo f)
            {
                f.SetValue(target, value);
                return;
            }

            if (member is PropertyInfo p)
            {
                p.SetValue(target, value);
            }
        }

        public static object GetValue(object target, MemberInfo member)
        {
            if (member is FieldInfo f)
            {
                return f.GetValue(target);
            }

            if (member is PropertyInfo p)
            {
                return p.GetValue(target);
            }

            return null;
        }

        public static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo f)
            {
                return f.FieldType;
            }

            if (member is PropertyInfo p)
            {
                return p.PropertyType;
            }

            return typeof(object);
        }

         public static object ConvertCellValue(object cellValue, Type targetType)
         {
             return ConvertCellValue(cellValue, targetType, null, ExcelCodecRegistry.Default);
         }

         public static object ConvertCellValue(object cellValue, Type targetType, string columnName)
         {
             return ConvertCellValue(cellValue, targetType, columnName, ExcelCodecRegistry.Default);
         }

         public static object ConvertCellValue(object cellValue, Type targetType, string columnName, ExcelCodecRegistry registry)
        {
            return ConvertCellValue(cellValue, targetType, columnName, registry, null);
        }

        public static object ConvertCellValue(object cellValue, Type targetType, string columnName, ExcelCodecRegistry registry, Dictionary<string, string> customParameters)
        {
            if (targetType == null)
            {
                return null;
            }

            if (cellValue == null)
            {
                return GetDefault(targetType);
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var rawString = cellValue.ToString();
            if (string.IsNullOrEmpty(rawString))
            {
                return GetDefault(targetType);
            }

            object normalizedCellValue = cellValue;
            if (cellValue is string s0 && nonNullable != typeof(string))
            {
                normalizedCellValue = NormalizeNumberLikeString(s0);
            }

            registry ??= ExcelCodecRegistry.Default;
            var ctx = new ExcelCodecContext(columnName, registry, customParameters);
            foreach (var codec in registry.GetCodecs())
            {
                if (codec.TryDecode(normalizedCellValue, targetType, ctx, out var v))
                {
                    return v;
                }
            }

             // Fallback: keep legacy behavior for JToken -> custom types
             if (normalizedCellValue is JToken customToken)
             {
                 try
                 {
                     var tokenJson = customToken.ToString(Formatting.None);
                     if (!string.IsNullOrWhiteSpace(tokenJson))
                     {
                         var obj = JsonConvert.DeserializeObject(tokenJson, nonNullable);
                         if (obj != null)
                         {
                             return obj;
                         }
                     }
                 }
                 catch
                 {
                     // ignored
                 }
             }

             return Convert.ChangeType(normalizedCellValue, nonNullable, CultureInfo.InvariantCulture);
         }

        private static string NormalizeNumberLikeString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            // Common Excel / copy-paste artifacts
            s = s.Trim();
            s = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
            s = s.Replace('，', ',');

            while (s.Contains("  "))
            {
                s = s.Replace("  ", " ");
            }

            return s.Trim();
        }

         public static object FormatCellValue(object value)
         {
             return FormatCellValue(value, null, ExcelCodecRegistry.Default);
         }

         public static object FormatCellValue(object value, string columnName)
         {
             return FormatCellValue(value, columnName, ExcelCodecRegistry.Default);
         }

         public static object FormatCellValue(object value, string columnName, ExcelCodecRegistry registry)
         {
             if (value == null)
             {
                 return null;
             }

             registry ??= ExcelCodecRegistry.Default;
             var ctx = new ExcelCodecContext(columnName, registry);
             foreach (var codec in registry.GetCodecs())
             {
                 if (codec.TryEncode(value, ctx, out var encoded))
                 {
                     return encoded;
                 }
             }

             // Fallback: keep legacy behavior for string json-like editing
             if (value is string str)
             {
                 var trimmed = str.Trim();

                 if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
                 {
                     trimmed = trimmed.Substring(1, trimmed.Length - 2);
                     trimmed = trimmed.Replace("\\\"", "\"");
                 }
                 if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                 {
                     try
                     {
                         var normalized = NormalizeLooseJsonObject(trimmed);
                         var parsed = JObject.Parse(normalized);
                         return FormatLooseJsonObject(parsed);
                     }
                     catch
                     {
                         return str;
                     }
                 }

                 if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && trimmed.Contains(":"))
                 {
                     try
                     {
                         var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
                         var asObject = "{" + inner + "}";
                         asObject = asObject.Replace("\\\"", "\"");
                         var normalized = NormalizeLooseJsonObject(asObject);
                         var parsed = JObject.Parse(normalized);
                         return FormatLooseJsonObject(parsed);
                     }
                     catch
                     {
                         return str;
                     }
                 }
             }

             return value;
         }

        private static string NormalizeLooseJsonObject(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            s = s.Trim();

            // Support object-literal style like: {x:200,y:430}
            // Convert unquoted identifier keys into valid JSON: {"x":200,"y":430}
            // Only transforms keys that are simple identifiers.
            s = Regex.Replace(
                s,
                "(^|[\\{,]\\s*)([A-Za-z_][A-Za-z0-9_]*)\\s*:\\s*",
                m => $"{m.Groups[1].Value}\"{m.Groups[2].Value}\":");

            // Also accept single quotes (common when pasted)
            s = s.Replace("'", "\"");
            return s;
        }

        private static string FormatLooseJsonObject(JObject jo)
        {
            if (jo == null)
            {
                return string.Empty;
            }

            var props = jo.Properties().ToList();
            if (props.Count == 0)
            {
                return "{}";
            }

            var parts = new List<string>(props.Count);
            foreach (var p in props)
            {
                var key = p.Name;
                var token = p.Value;

                var valueStr = string.Empty;
                if (token == null || token.Type == JTokenType.Null)
                {
                    valueStr = "null";
                }
                else if (token.Type == JTokenType.String)
                {
                    valueStr = JsonConvert.ToString(token.Value<string>());
                }
                else
                {
                    valueStr = token.ToString(Formatting.None);
                }

                parts.Add($"{key}:{valueStr}");
            }

            return "{" + string.Join(",", parts) + "}";
        }

        private static object GetDefault(Type t)
        {
            if (Nullable.GetUnderlyingType(t) != null)
            {
                return null;
            }

            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
    }
}
