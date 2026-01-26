using System;
using System.Linq;
using System.Text;

namespace AbilityKit.ProtocolEditor.UI
{
    internal static class CSharpTypeNameUtility
    {
        public static string ToCSharpTypeName(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (type.IsByRef)
            {
                type = type.GetElementType() ?? type;
            }

            if (type.IsArray)
            {
                var element = type.GetElementType() ?? typeof(object);
                var ranks = type.GetArrayRank();
                if (ranks == 1)
                {
                    return ToCSharpTypeName(element) + "[]";
                }

                return ToCSharpTypeName(element) + "[" + new string(',', ranks - 1) + "]";
            }

            if (type == typeof(void)) return "void";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(char)) return "char";
            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";

            if (type.IsGenericType)
            {
                if (type.FullName != null && type.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
                {
                    return ToTupleTypeName(type);
                }

                var genericDef = type.GetGenericTypeDefinition();
                var name = genericDef.FullName ?? genericDef.Name;
                var tickIndex = name.IndexOf('`');
                if (tickIndex >= 0) name = name.Substring(0, tickIndex);

                var args = type.GetGenericArguments().Select(ToCSharpTypeName);
                return name + "<" + string.Join(", ", args) + ">";
            }

            if (type.IsNested)
            {
                return ToCSharpTypeName(type.DeclaringType ?? typeof(object)) + "." + type.Name;
            }

            return type.FullName ?? type.Name;
        }

        private static string ToTupleTypeName(Type tupleType)
        {
            var sb = new StringBuilder();
            sb.Append('(');

            var first = true;
            foreach (var element in FlattenTupleElements(tupleType))
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(ToCSharpTypeName(element));
            }

            sb.Append(')');
            return sb.ToString();
        }

        private static Type[] FlattenTupleElements(Type tupleType)
        {
            var args = tupleType.GetGenericArguments();
            if (args.Length == 8)
            {
                var head = args.Take(7);
                var tail = args[7];

                if (tail.IsGenericType && tail.FullName != null && tail.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
                {
                    return head.Concat(FlattenTupleElements(tail)).ToArray();
                }

                return head.Concat(new[] { tail }).ToArray();
            }

            return args;
        }
    }
}
