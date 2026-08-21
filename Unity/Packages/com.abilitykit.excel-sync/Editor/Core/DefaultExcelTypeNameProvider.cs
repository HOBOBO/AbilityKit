using System;
using System.Collections.Generic;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// 缺省类型命名方案（中性、自描述，供 Luban 或自研管线共同理解）：
    /// int/long/float/double/bool/string/enum 直接映射；T[] 与 List&lt;T&gt; 映射为 "list,&lt;元素类型&gt;"；
    /// 无法静态描述的嵌套对象/结构（如 ContinuousModifierDTO[]）映射为 "json"，下游自行按 JSON 反序列化。
    /// 若项目有自己的类型命名约定（如 Luban 的 "(list#sep=|),int"），实现 IExcelTypeNameProvider 替换即可。
    /// </summary>
    public sealed class DefaultExcelTypeNameProvider : IExcelTypeNameProvider
    {
        public static readonly DefaultExcelTypeNameProvider Instance = new DefaultExcelTypeNameProvider();

        public string GetTypeName(Type memberType)
        {
            if (memberType == null)
            {
                return "string";
            }

            var nonNullable = Nullable.GetUnderlyingType(memberType) ?? memberType;

            var elementType = TryGetElementType(nonNullable);
            if (elementType != null)
            {
                return "list," + GetTypeName(elementType);
            }

            if (nonNullable == typeof(string)) return "string";
            if (nonNullable == typeof(int)) return "int";
            if (nonNullable == typeof(long)) return "long";
            if (nonNullable == typeof(float)) return "float";
            if (nonNullable == typeof(double)) return "double";
            if (nonNullable == typeof(bool)) return "bool";
            if (nonNullable.IsEnum) return "int";

            return "json";
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
