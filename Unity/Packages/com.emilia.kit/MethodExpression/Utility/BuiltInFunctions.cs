using System;
using System.Collections;

namespace Emilia.Expressions
{
    /// <summary>
    /// 内置函数库
    /// </summary>
    public static class BuiltInFunctions
    {
        public static void RegisterAll(ExpressionConfig config)
        {
            // 算术
            config.RegisterFunction("add", (args, ctx) => {
                if (args[0] is string || args[1] is string) return string.Concat(args[0]?.ToString() ?? "", args[1]?.ToString() ?? "");
                return ToDouble(args[0]) + ToDouble(args[1]);
            });
            config.RegisterFunction("sub", (args, ctx) => ToDouble(args[0]) - ToDouble(args[1]));
            config.RegisterFunction("mul", (args, ctx) => ToDouble(args[0]) * ToDouble(args[1]));
            config.RegisterFunction("div", (args, ctx) => {
                double divisor = ToDouble(args[1]);
                if (Math.Abs(divisor) < double.Epsilon) throw new ExpressionEvaluateException("除数不能为零");
                return ToDouble(args[0]) / divisor;
            });
            config.RegisterFunction("mod", (args, ctx) => ToDouble(args[0]) % ToDouble(args[1]));
            config.RegisterFunction("neg", (args, ctx) => -ToDouble(args[0]));

            // 比较
            config.RegisterFunction("eq", (args, ctx) => {
                if (args[0] == null && args[1] == null) return true;
                if (args[0] == null || args[1] == null) return false;
                if (IsNumeric(args[0]) && IsNumeric(args[1])) return Math.Abs(ToDouble(args[0]) - ToDouble(args[1])) < double.Epsilon;
                return args[0].Equals(args[1]);
            });
            config.RegisterFunction("ne", (args, ctx) => {
                if (args[0] == null && args[1] == null) return false;
                if (args[0] == null || args[1] == null) return true;
                if (IsNumeric(args[0]) && IsNumeric(args[1])) return Math.Abs(ToDouble(args[0]) - ToDouble(args[1])) >= double.Epsilon;
                return ! args[0].Equals(args[1]);
            });
            config.RegisterFunction("lt", (args, ctx) => ToDouble(args[0]) < ToDouble(args[1]));
            config.RegisterFunction("le", (args, ctx) => ToDouble(args[0]) <= ToDouble(args[1]));
            config.RegisterFunction("gt", (args, ctx) => ToDouble(args[0]) > ToDouble(args[1]));
            config.RegisterFunction("ge", (args, ctx) => ToDouble(args[0]) >= ToDouble(args[1]));

            // 逻辑
            config.RegisterFunction("and", (args, ctx) => ToBool(args[0]) && ToBool(args[1]));
            config.RegisterFunction("or", (args, ctx) => ToBool(args[0]) || ToBool(args[1]));
            config.RegisterFunction("not", (args, ctx) => ! ToBool(args[0]));
            config.RegisterFunction("if", (args, ctx) => ToBool(args[0]) ? args[1] : args[2]);

            // 数学
            config.RegisterFunction("abs", (args, ctx) => Math.Abs(ToDouble(args[0])));
            config.RegisterFunction("floor", (args, ctx) => Math.Floor(ToDouble(args[0])));
            config.RegisterFunction("ceil", (args, ctx) => Math.Ceiling(ToDouble(args[0])));
            config.RegisterFunction("round", (args, ctx) => {
                double value = ToDouble(args[0]);
                if (args.Length > 1) return Math.Round(value, Convert.ToInt32(args[1]));
                return Math.Round(value);
            });
            config.RegisterFunction("sqrt", (args, ctx) => Math.Sqrt(ToDouble(args[0])));
            config.RegisterFunction("pow", (args, ctx) => Math.Pow(ToDouble(args[0]), ToDouble(args[1])));
            config.RegisterFunction("min", (args, ctx) => {
                double min = ToDouble(args[0]);
                for (int i = 1; i < args.Length; i++)
                {
                    double v = ToDouble(args[i]);
                    if (v < min) min = v;
                }
                return min;
            });
            config.RegisterFunction("max", (args, ctx) => {
                double max = ToDouble(args[0]);
                for (int i = 1; i < args.Length; i++)
                {
                    double v = ToDouble(args[i]);
                    if (v > max) max = v;
                }
                return max;
            });
            config.RegisterFunction("clamp", (args, ctx) => {
                double value = ToDouble(args[0]);
                double min = ToDouble(args[1]);
                double max = ToDouble(args[2]);
                return value < min ? min : value > max ? max : value;
            });
            config.RegisterFunction("lerp", (args, ctx) => {
                double a = ToDouble(args[0]);
                double b = ToDouble(args[1]);
                double t = ToDouble(args[2]);
                return a + (b - a) * t;
            });

            // 字符串
            config.RegisterFunction("len", (args, ctx) => {
                object arg = args[0];
                if (arg is string s) return s.Length;
                if (arg is ICollection c) return c.Count;
                if (arg is Array arr) return arr.Length;
                return 0;
            });
            config.RegisterFunction("str", (args, ctx) => args[0]?.ToString() ?? "");

            // 类型
            config.RegisterFunction("int", (args, ctx) => Convert.ToInt64(ToDouble(args[0])));
            config.RegisterFunction("float", (args, ctx) => (float) ToDouble(args[0]));
            config.RegisterFunction("double", (args, ctx) => ToDouble(args[0]));
            config.RegisterFunction("bool", (args, ctx) => ToBool(args[0]));
            config.RegisterFunction("isnull", (args, ctx) => args[0] == null);

            // 集合
            config.RegisterFunction("index", (args, ctx) => {
                object collection = args[0];
                if (collection == null) throw new ExpressionEvaluateException("集合为null");

                int idx = Convert.ToInt32(args[1]);

                if (collection is Array arr)
                {
                    if (idx < 0 || idx >= arr.Length) throw new ExpressionEvaluateException($"索引越界: {idx}");
                    return arr.GetValue(idx);
                }
                if (collection is IList list)
                {
                    if (idx < 0 || idx >= list.Count) throw new ExpressionEvaluateException($"索引越界: {idx}");
                    return list[idx];
                }
                if (collection is string str)
                {
                    if (idx < 0 || idx >= str.Length) throw new ExpressionEvaluateException($"索引越界: {idx}");
                    return str[idx];
                }

                throw new ExpressionEvaluateException($"类型 {collection.GetType().Name} 不支持索引访问");
            });

            // 常量
            config.RegisterConstant("PI", Math.PI);
            config.RegisterConstant("E", Math.E);
        }

        private static bool IsNumeric(object value) =>
            value is sbyte || value is byte || value is short || value is ushort ||
            value is int || value is uint || value is long || value is ulong ||
            value is float || value is double || value is decimal;

        private static double ToDouble(object value)
        {
            if (value == null) return 0;
            switch (value)
            {
                case double d:
                    return d;
                case float f:
                    return f;
                case long l:
                    return l;
                case int i:
                    return i;
                case short s:
                    return s;
                case byte b:
                    return b;
                case decimal m:
                    return (double) m;
                case bool bl:
                    return bl ? 1 : 0;
                default:
                    if (double.TryParse(value.ToString(), out double result)) return result;
                    return 0;
            }
        }

        private static bool ToBool(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is string s) return ! string.IsNullOrEmpty(s) && s.ToLower() != "false" && s != "0";
            return ToDouble(value) != 0;
        }
    }
}