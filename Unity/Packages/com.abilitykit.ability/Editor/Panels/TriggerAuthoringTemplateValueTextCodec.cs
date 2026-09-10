#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Panels
{
    internal static class TriggerAuthoringTemplateValueTextCodec
    {
        public static string Format(TriggerValueRefData value)
        {
            if (value == null) return string.Empty;
            switch (value.Source)
            {
                case TriggerValueSource.Payload: return "payload:" + (value.Path ?? string.Empty);
                case TriggerValueSource.Context: return "context:" + (value.Path ?? string.Empty);
                case TriggerValueSource.LocalBlackboard: return "local:" + (value.Path ?? string.Empty);
                case TriggerValueSource.GlobalBlackboard: return "global:" + (value.Path ?? string.Empty);
                case TriggerValueSource.Expression: return "expr:" + (value.Expression ?? string.Empty);
                case TriggerValueSource.TemplateParameter: return "template:" + (value.Path ?? string.Empty);
                case TriggerValueSource.Constant: return FormatConstant(value);
                default: return string.Empty;
            }
        }

        public static bool TryParse(
            string text,
            TriggerAuthoringTemplateParameterData parameter,
            out TriggerValueRefData value,
            out string error)
        {
            value = null;
            error = null;
            if (parameter == null)
            {
                error = "参数定义为空。";
                return false;
            }

            text = text ?? string.Empty;
            var source = TriggerValueSource.Constant;
            var content = text;
            if (TryReadPrefix(text, "payload:", out content)) source = TriggerValueSource.Payload;
            else if (TryReadPrefix(text, "context:", out content)) source = TriggerValueSource.Context;
            else if (TryReadPrefix(text, "local:", out content)) source = TriggerValueSource.LocalBlackboard;
            else if (TryReadPrefix(text, "global:", out content)) source = TriggerValueSource.GlobalBlackboard;
            else if (TryReadPrefix(text, "expr:", out content)) source = TriggerValueSource.Expression;
            else if (TryReadPrefix(text, "const:", out content)) source = TriggerValueSource.Constant;

            var mask = (TriggerTemplateValueSourceMask)(1 << (int)source);
            if ((parameter.AllowedSources & mask) == 0)
            {
                error = "参数“" + parameter.Name + "”不允许使用" + SourceLabel(source) + "。";
                return false;
            }

            value = new TriggerValueRefData { Source = source, Type = parameter.Type };
            if (source == TriggerValueSource.Expression)
            {
                value.Expression = content;
                return RequireContent(content, out error);
            }
            if (source != TriggerValueSource.Constant)
            {
                value.Path = content;
                return RequireContent(content, out error);
            }
            return TryParseConstant(content, parameter.Type, value, out error);
        }

        private static string FormatConstant(TriggerValueRefData value)
        {
            switch (value.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    return value.IntegerValue.ToString(CultureInfo.InvariantCulture);
                case TriggerValueType.Number:
                    return value.NumberValue.ToString("R", CultureInfo.InvariantCulture);
                case TriggerValueType.Boolean:
                    return value.BooleanValue ? "true" : "false";
                case TriggerValueType.String:
                    return "const:" + (value.StringValue ?? string.Empty);
                case TriggerValueType.IntegerList:
                    return "list:" + string.Join(",", value.IntegerListValue ?? new List<long>());
                case TriggerValueType.Vector3:
                    var vector = value.Vector3Value ?? new TriggerVector3Data();
                    return "vec3:" + vector.X.ToString("R", CultureInfo.InvariantCulture) + "," +
                           vector.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
                           vector.Z.ToString("R", CultureInfo.InvariantCulture);
                default: return "<复杂值>";
            }
        }

        private static bool TryParseConstant(
            string text,
            TriggerValueType type,
            TriggerValueRefData value,
            out string error)
        {
            error = null;
            switch (type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    {
                        value.IntegerValue = integer;
                        return true;
                    }
                    error = "需要整数。";
                    return false;
                case TriggerValueType.Number:
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    {
                        value.NumberValue = number;
                        return true;
                    }
                    error = "需要使用英文小数点的数值。";
                    return false;
                case TriggerValueType.Boolean:
                    if (TryParseBoolean(text, out var boolean))
                    {
                        value.BooleanValue = boolean;
                        return true;
                    }
                    error = "需要 true、false、1、0、是或否。";
                    return false;
                case TriggerValueType.String:
                    value.StringValue = text;
                    return true;
                case TriggerValueType.IntegerList:
                    return TryParseIntegerList(text, value, out error);
                case TriggerValueType.Vector3:
                    return TryParseVector(text, value, out error);
                default:
                    error = "复杂对象暂不支持通过参数矩阵粘贴，请在规则编辑中修改。";
                    return false;
            }
        }

        private static bool TryParseIntegerList(string text, TriggerValueRefData value, out string error)
        {
            if (TryReadPrefix(text, "list:", out var content)) text = content;
            value.IntegerListValue = new List<long>();
            var parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (long.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var item))
                    value.IntegerListValue.Add(item);
                else
                {
                    error = "整数列表需要使用英文逗号分隔。";
                    return false;
                }
            }
            error = null;
            return true;
        }

        private static bool TryParseVector(string text, TriggerValueRefData value, out string error)
        {
            if (TryReadPrefix(text, "vec3:", out var content)) text = content;
            var parts = text.Split(',');
            if (parts.Length == 3 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                value.Vector3Value = new TriggerVector3Data { X = x, Y = y, Z = z };
                error = null;
                return true;
            }
            error = "三维向量需要写为 vec3:x,y,z。";
            return false;
        }

        private static bool TryParseBoolean(string text, out bool value)
        {
            if (bool.TryParse(text, out value)) return true;
            if (text == "1" || text == "是") { value = true; return true; }
            if (text == "0" || text == "否") { value = false; return true; }
            return false;
        }

        private static bool TryReadPrefix(string text, string prefix, out string content)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                content = text.Substring(prefix.Length);
                return true;
            }
            content = text;
            return false;
        }

        private static bool RequireContent(string content, out string error)
        {
            error = string.IsNullOrWhiteSpace(content) ? "引用路径或表达式不能为空。" : null;
            return error == null;
        }

        private static string SourceLabel(TriggerValueSource source)
        {
            switch (source)
            {
                case TriggerValueSource.Payload: return "事件参数";
                case TriggerValueSource.Context: return "运行上下文";
                case TriggerValueSource.LocalBlackboard: return "局部变量";
                case TriggerValueSource.GlobalBlackboard: return "全局变量";
                case TriggerValueSource.Expression: return "表达式";
                default: return "常量";
            }
        }
    }
}
#endif
