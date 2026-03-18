using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
            if (!nonNullable.IsGenericType || nonNullable.GetGenericTypeDefinition() != typeof(List<>))
            {
                return false;
            }

            var elementType = nonNullable.GetGenericArguments()[0];

            var s = cellValue?.ToString() ?? string.Empty;
            s = s.Trim();

            if (string.IsNullOrEmpty(s) || string.Equals(s, "null", StringComparison.OrdinalIgnoreCase))
            {
                value = null;
                return true;
            }

            if (s.StartsWith("[") && s.EndsWith("]") && s.Length >= 2)
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }

            if (string.IsNullOrEmpty(s))
            {
                value = Activator.CreateInstance(nonNullable);
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

            var list = (IList)Activator.CreateInstance(nonNullable);
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

            value = list;
            return true;
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
