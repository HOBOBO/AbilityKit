using System;
using System.Globalization;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    internal sealed class DefaultPrimitiveCodec : IExcelValueCodec
    {
        public bool TryDecode(object cellValue, Type targetType, ExcelCodecContext context, out object value)
        {
            value = null;
            if (targetType == null)
            {
                return false;
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (nonNullable == typeof(string))
            {
                value = cellValue?.ToString() ?? string.Empty;
                return true;
            }

            if (cellValue == null)
            {
                return true;
            }

            var s = cellValue.ToString();
            if (string.IsNullOrEmpty(s))
            {
                return true;
            }

            if (nonNullable.IsEnum)
            {
                if (cellValue is double d)
                {
                    value = Enum.ToObject(nonNullable, (int)d);
                    return true;
                }

                value = Enum.Parse(nonNullable, s, true);
                return true;
            }

            if (nonNullable == typeof(bool))
            {
                if (cellValue is bool b)
                {
                    value = b;
                    return true;
                }

                if (bool.TryParse(s, out var bv))
                {
                    value = bv;
                    return true;
                }

                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi))
                {
                    value = bi != 0;
                    return true;
                }

                value = false;
                return true;
            }

            if (nonNullable == typeof(int))
            {
                if (cellValue is double d)
                {
                    value = (int)d;
                    return true;
                }

                if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv))
                {
                    value = iv;
                    return true;
                }

                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var id))
                {
                    value = (int)id;
                    return true;
                }

                value = int.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable == typeof(long))
            {
                if (cellValue is double d)
                {
                    value = (long)d;
                    return true;
                }

                if (long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var lv))
                {
                    value = lv;
                    return true;
                }

                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ld))
                {
                    value = (long)ld;
                    return true;
                }

                value = long.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable == typeof(float))
            {
                if (cellValue is double d)
                {
                    value = (float)d;
                    return true;
                }

                value = float.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable == typeof(double))
            {
                if (cellValue is double d)
                {
                    value = d;
                    return true;
                }

                value = double.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }

        public bool TryEncode(object value, ExcelCodecContext context, out object cellValue)
        {
            cellValue = null;
            if (value == null)
            {
                return true;
            }

            var t = value.GetType();
            if (t == typeof(string) || t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(bool))
            {
                cellValue = value;
                return true;
            }

            if (t.IsEnum)
            {
                cellValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }
    }
}
