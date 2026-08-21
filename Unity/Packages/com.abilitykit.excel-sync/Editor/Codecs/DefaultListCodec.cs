using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    internal sealed class DefaultListCodec : IExcelValueCodec
    {
        public bool TryDecode(object cellValue, Type targetType, ExcelCodecContext context, out object value)
        {
            value = null;
            if (targetType == null)
            {
                return false;
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

            Type elementType;
            bool isArray;
            if (nonNullable.IsArray)
            {
                elementType = nonNullable.GetElementType();
                isArray = true;
            }
            else if (nonNullable.IsGenericType && nonNullable.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = nonNullable.GetGenericArguments()[0];
                isArray = false;
            }
            else
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

            // 复杂元素集合整体按 JSON 数组解析，避免逗号分隔破坏嵌套 JSON（如 {a:1,b:2}）。
            if (!IsSimpleElement(elementType))
            {
                if (s.StartsWith("[") && s.EndsWith("]"))
                {
                    try
                    {
                        var decoded = JsonConvert.DeserializeObject(s, nonNullable);
                        if (decoded != null)
                        {
                            value = decoded;
                            return true;
                        }
                    }
                    catch
                    {
                        // 落到下方按分隔符拆分（尽力而为）。
                    }
                }
            }

            if (s.StartsWith("[") && s.EndsWith("]") && s.Length >= 2)
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }

            if (string.IsNullOrEmpty(s))
            {
                value = isArray ? (object)Array.CreateInstance(elementType, 0) : Activator.CreateInstance(nonNullable);
                return true;
            }

            var seps = context.GetListSeparatorsOrDefault();
            // 检查是否有自定义分隔符
            var sepStr = context.GetCustomParameter("sep");
            if (!string.IsNullOrEmpty(sepStr))
            {
                seps = new[] { sepStr[0] }; // 将字符串转换为字符数组
            }
            var parts = s.Split(seps, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                object ev = null;
                if (elementType == typeof(string))
                {
                    ev = p;
                }
                else if (elementType == typeof(int))
                {
                    int.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv);
                    ev = iv;
                }
                else if (elementType == typeof(long))
                {
                    long.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var lv);
                    ev = lv;
                }
                else if (elementType == typeof(float))
                {
                    float.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv);
                    ev = fv;
                }
                else if (elementType == typeof(double))
                {
                    double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv);
                    ev = dv;
                }
                else if (elementType == typeof(bool))
                {
                    bool.TryParse(p, out var bv);
                    ev = bv;
                }
                else if (elementType.IsEnum)
                {
                    try
                    {
                        ev = Enum.Parse(elementType, p, true);
                    }
                    catch
                    {
                        ev = Activator.CreateInstance(elementType);
                    }
                }
                else
                {
                    ev = p;
                    var ctx = new ExcelCodecContext(context.ColumnName, context.Registry);
                    if (context.Registry != null)
                    {
                        var decoded = false;
                        foreach (var codec in context.Registry.GetCodecs())
                        {
                            if (codec is DefaultListCodec)
                            {
                                continue;
                            }

                            if (codec.TryDecode(p, elementType, ctx, out var obj))
                            {
                                ev = obj;
                                decoded = true;
                                break;
                            }
                        }

                        if (decoded)
                        {
                            list.Add(ev);
                            continue;
                        }
                    }

                    return false;
                }

                list.Add(ev);
            }

            value = isArray ? (object)ToArray(list, elementType) : list;
            return true;
        }

        private static bool IsSimpleElement(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive || t.IsEnum || t == typeof(string);
        }

        private static Array ToArray(IList list, Type elementType)
        {
            var arr = Array.CreateInstance(elementType, list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                arr.SetValue(list[i], i);
            }

            return arr;
        }

        private static Type GetElementType(Type collectionType)
        {
            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }

            if (collectionType.IsGenericType)
            {
                var gd = collectionType.GetGenericTypeDefinition();
                if (gd == typeof(List<>) || gd == typeof(IList<>) || gd == typeof(IEnumerable<>))
                {
                    return collectionType.GetGenericArguments()[0];
                }
            }

            return null;
        }

        public bool TryEncode(object value, ExcelCodecContext context, out object cellValue)
        {
            cellValue = null;
            if (value == null)
            {
                return true;
            }

            if (value is string)
            {
                return false;
            }

            if (value is IEnumerable enumerable)
            {
                if (value is IDictionary)
                {
                    return false;
                }

                // 复杂元素集合整体序列化为 JSON 数组，与 TryDecode 的解析路径对称，
                // 避免逗号分隔在嵌套 JSON（如 {a:1,b:2}）出现时无法正确还原。
                var elementType = GetElementType(value.GetType());
                if (elementType != null && !IsSimpleElement(elementType))
                {
                    try
                    {
                        cellValue = JsonConvert.SerializeObject(value, Formatting.None);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                var sep = context.GetListPreferredSeparatorOrDefault().ToString();
                var parts = new List<string>();
                foreach (var e in enumerable)
                {
                    if (e == null)
                    {
                        continue;
                    }

                    if (e is string es)
                    {
                        parts.Add(es);
                        continue;
                    }

                    if (e is int or long or float or double or bool)
                    {
                        parts.Add(Convert.ToString(e, CultureInfo.InvariantCulture));
                        continue;
                    }

                    if (e.GetType().IsEnum)
                    {
                        parts.Add(Convert.ToInt32(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                        continue;
                    }

                    if (context.Registry != null)
                    {
                        var ctx = new ExcelCodecContext(context.ColumnName, context.Registry);
                        foreach (var codec in context.Registry.GetCodecs())
                        {
                            if (codec is DefaultListCodec)
                            {
                                continue;
                            }

                            if (codec.TryEncode(e, ctx, out var encoded) && encoded != null)
                            {
                                parts.Add(encoded.ToString());
                                goto NEXT;
                            }
                        }
                    }

                    parts.Add(e.ToString());
                NEXT:;
                }

                cellValue = string.Join(sep, parts);
                return true;
            }

            return false;
        }
    }
}
