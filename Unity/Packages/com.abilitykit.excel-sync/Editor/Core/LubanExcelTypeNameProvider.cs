using System;
using System.Collections.Generic;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// Luban 类型行命名方案：输出 Luban 认得的类型串。
    /// - 基础类型：int / long / float / bool / string（enum 取 int）
    /// - 列表：Luban 语法 (list#sep=,),T
    /// - 嵌套结构（对象或对象数组）：Luban 无 json 内置类型，退化为 string（单元格放原始 JSON 文本）
    /// </summary>
    public sealed class LubanExcelTypeNameProvider : IExcelTypeNameProvider
    {
        public static readonly LubanExcelTypeNameProvider Instance = new LubanExcelTypeNameProvider();

        public string GetTypeName(Type memberType)
        {
            if (memberType == null)
            {
                return StringType;
            }

            var nonNullable = Nullable.GetUnderlyingType(memberType) ?? memberType;

            var elementType = TryGetElementType(nonNullable);
            if (elementType != null)
            {
                // 复杂元素列表 → string（整段 JSON）；基础元素 → (list#sep=,),T
                // 列表元素不带可空标记：空列表由空单元格表达。
                return IsSimple(elementType)
                    ? "(list#sep=,)," + GetTypeName(elementType).TrimEnd('?')
                    : StringType;
            }

            if (!IsSimple(nonNullable))
            {
                return StringType;   // 嵌套对象：以 JSON 文本存 string
            }

            if (nonNullable == typeof(string)) return StringType;
            if (nonNullable == typeof(int)) return "int";
            if (nonNullable == typeof(long)) return "long";
            if (nonNullable == typeof(float)) return "float";
            if (nonNullable == typeof(double)) return "float";
            if (nonNullable == typeof(bool)) return "bool";
            if (nonNullable.IsEnum) return "int";
            return StringType;
        }

        /// <summary>
        /// Luban 对 string 不接受空单元格（int 空可默认 0，string 无默认值），
        /// 因此统一用可空 string?，使空值能在 Excel ↔ 数据间往返。
        /// </summary>
        private const string StringType = "string";

        private static bool IsSimple(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);
        }

        private static Type TryGetElementType(Type t)
        {
            if (t.IsArray)
            {
                return t.GetElementType();
            }

            if (t.IsGenericType)
            {
                var gd = t.GetGenericTypeDefinition();
                if (gd == typeof(List<>) || gd == typeof(IList<>))
                {
                    return t.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
