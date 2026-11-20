using System.Text;
using AbilityKit.Entitas.CodeGen.Model;

namespace AbilityKit.Entitas.CodeGen.Templating;

/// <summary>
/// 组件逐字段序列化模板：生成 {Ctx}ComponentSerialization.Write/Restore（BinaryWriter/BinaryReader）。
/// 覆盖集由 pin 策略决定；有限纯数据图逐字段展开，运行时引用仍由其业务 Provider 恢复。
/// 托管字典需显式 EntitasBlob，形状由 ModelBuilder 校验，不支持任意对象图。
/// </summary>
public static class SerializationTemplates
{
    private static readonly Dictionary<string, string> PrimitiveReaders = new(StringComparer.Ordinal)
    {
        ["bool"] = "ReadBoolean", ["byte"] = "ReadByte", ["sbyte"] = "ReadSByte", ["char"] = "ReadChar",
        ["decimal"] = "ReadDecimal", ["double"] = "ReadDouble", ["float"] = "ReadSingle", ["int"] = "ReadInt32",
        ["uint"] = "ReadUInt32", ["long"] = "ReadInt64", ["ulong"] = "ReadUInt64", ["short"] = "ReadInt16",
        ["ushort"] = "ReadUInt16", ["string"] = "ReadString",
    };

    /// <summary>框架定点/向量类型：生成器内置展开规则（写 raw/分量，读构造回来），字段形状由框架契约固定。</summary>
    public static readonly HashSet<string> KnownLeafTypes = new(StringComparer.Ordinal)
    {
        "AbilityKit.Core.Mathematics.Vec3",
        "AbilityKit.Core.Mathematics.Quat",
        "AbilityKit.Core.Mathematics.Transform3",
        "AbilityKit.Deterministic.Fixed64",
        "AbilityKit.Attributes.Core.AttributeId",
    };

    private static bool WriteKnownLeaf(string access, string t, StringBuilder sb, string indent)
    {
        if (t == "AbilityKit.Core.Mathematics.Vec3")
        {
            sb.Append(indent).Append("w.Write(").Append(access).Append(".X);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Y);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Z);\n");
            return true;
        }
        if (t == "AbilityKit.Core.Mathematics.Quat")
        {
            sb.Append(indent).Append("w.Write(").Append(access).Append(".X);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Y);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Z);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".W);\n");
            return true;
        }
        if (t == "AbilityKit.Core.Mathematics.Transform3")
        {
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Position.X);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Position.Y);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Position.Z);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Rotation.X);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Rotation.Y);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Rotation.Z);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Rotation.W);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Scale.X);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Scale.Y);\n");
            sb.Append(indent).Append("w.Write(").Append(access).Append(".Scale.Z);\n");
            return true;
        }
        if (t == "AbilityKit.Deterministic.Fixed64")
        {
            sb.Append(indent).Append("w.Write(").Append(access).Append(".RawValue);\n");
            return true;
        }
        if (t == "AbilityKit.Attributes.Core.AttributeId")
        {
            sb.Append(indent).Append("w.Write(").Append(access).Append(".RawId);\n");
            return true;
        }
        return false;
    }

    private static string? ReadKnownLeaf(string t)
    {
        return t switch
        {
            "AbilityKit.Core.Mathematics.Vec3" =>
                "new AbilityKit.Core.Mathematics.Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle())",
            "AbilityKit.Core.Mathematics.Quat" =>
                "new AbilityKit.Core.Mathematics.Quat(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle())",
            "AbilityKit.Core.Mathematics.Transform3" =>
                "new AbilityKit.Core.Mathematics.Transform3(new AbilityKit.Core.Mathematics.Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), new AbilityKit.Core.Mathematics.Quat(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), new AbilityKit.Core.Mathematics.Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()))",
            "AbilityKit.Deterministic.Fixed64" =>
                "AbilityKit.Deterministic.Fixed64.FromRaw(r.ReadInt64())",
            "AbilityKit.Attributes.Core.AttributeId" =>
                "AbilityKit.Attributes.Core.AttributeId.FromRaw(r.ReadInt32())",
            _ => null,
        };
    }

    public static string ComponentSerialization(string ctx, List<PinnedComponent> entries,
        Dictionary<string, PlainTypeInfo> plainTypes, string header)
    {
        var serializable = entries.Where(e => e.Serialize && e.Definition is not null).ToList();

        var sb = new StringBuilder();
        sb.Append(header);
        sb.Append("\npublic static class ").Append(ctx).Append("ComponentSerialization {\n\n");

        AppendWrite(ctx, serializable, plainTypes, sb);
        sb.Append('\n');
        AppendRestore(ctx, serializable, plainTypes, sb);

        sb.Append("\n    private static bool ReadPresence(System.IO.BinaryReader r) {\n");
        sb.Append("        var value = r.ReadByte();\n        if (value > 1) throw new System.IO.InvalidDataException(\"Invalid presence marker.\");\n        return value == 1;\n    }\n");
        sb.Append("\n    private static int ReadCollectionCount(System.IO.BinaryReader r) {\n");
        sb.Append("        var count = r.ReadInt32();\n        if (count < -1 || count > r.BaseStream.Length - r.BaseStream.Position) throw new System.IO.InvalidDataException(\"Invalid collection count.\");\n        return count;\n    }\n");

        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendWrite(string ctx, List<PinnedComponent> entries,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb)
    {
        sb.Append("    public static void Write(global::").Append(ctx).Append("Entity e, System.IO.BinaryWriter w) {\n");
        foreach (var entry in entries)
        {
            var def = entry.Definition!;
            var prop = Templates.LowerFirst(def.ComponentName);
            if (def.IsFlag)
            {
                sb.Append("        w.Write(e.is").Append(def.ComponentName).Append(");\n");
                continue;
            }

            sb.Append("        w.Write(e.has").Append(def.ComponentName).Append(");\n");
            sb.Append("        if (e.has").Append(def.ComponentName).Append(") {\n");
            sb.Append("            var p = e.").Append(prop).Append(";\n");
            AppendFieldWrites("p", def.Fields, plainTypes, sb, indent: "            ");
            sb.Append("        }\n");
        }
        sb.Append("    }\n");
    }

    private static void AppendFieldWrites(string access, List<FieldInfo> fields,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb, string indent)
    {
        foreach (var f in fields)
        {
            AppendSingleWrite(access, f, plainTypes, sb, indent);
        }
    }

    private static void AppendSingleWrite(string access, FieldInfo f,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb, string indent)
    {
        AppendValueWrite($"{access}.{f.Name}", f.TypeFqn, plainTypes, sb, indent, f.Name);
    }

    /// <summary>
    /// 只解析已解析的标准 Dictionary 形状；其它泛型仍由模型层拒绝。
    /// </summary>
    public static bool TryDictionary(string type, out string key, out string value)
    {
        const string prefix = "System.Collections.Generic.Dictionary<";
        key = value = "";
        if (!type.StartsWith(prefix, StringComparison.Ordinal) || !type.EndsWith('>')) return false;
        var arguments = type[prefix.Length..^1];
        var depth = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == '<') depth++;
            if (arguments[i] == '>') depth--;
            if (arguments[i] != ',' || depth != 0) continue;
            key = arguments[..i].Trim();
            value = arguments[(i + 1)..].Trim();
            return true;
        }
        return false;
    }

    private static void AppendValueWrite(string expr, string t,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb, string indent, string path)
    {
        if (t == "string")
        {
            sb.AppendLine($"{indent}w.Write({expr} != null);");
            sb.AppendLine($"{indent}if ({expr} != null) w.Write({expr});");
            return;
        }

        if (PrimitiveReaders.ContainsKey(t))
        {
            sb.Append(indent).Append("w.Write(").Append(expr).Append(");\n");
            return;
        }

        if (WriteKnownLeaf(expr, t, sb, indent)) return;

        if (t.EndsWith("[]", StringComparison.Ordinal))
        {
            var elem = t[..^2];
            sb.Append(indent).Append("w.Write(").Append(expr).Append(" == null ? -1 : ").Append(expr).Append(".Length);\n");
            sb.Append(indent).Append("if (").Append(expr).Append(" != null) {\n");
            var index = "index" + path;
            sb.AppendLine($"{indent}    for (var {index} = 0; {index} < {expr}.Length; {index}++) {{");
            AppendValueWrite($"{expr}[{index}]", elem, plainTypes, sb, indent + "        ", path + "Item");
            sb.Append(indent).Append("    }\n");
            sb.Append(indent).Append("}\n");
            return;
        }

        if (TryDictionary(t, out var key, out var value))
        {
            sb.AppendLine($"{indent}w.Write({expr} == null ? -1 : {expr}.Count);");
            sb.AppendLine($"{indent}if ({expr} != null) {{");
            sb.AppendLine($"{indent}    if ({expr}.GetType() != typeof({t}) || !object.ReferenceEquals({expr}.Comparer, System.Collections.Generic.EqualityComparer<{key}>.Default)) throw new System.IO.InvalidDataException(\"Unsupported dictionary runtime shape or comparer.\");");
            sb.AppendLine($"{indent}    var keys{path} = new System.Collections.Generic.List<{key}>({expr}.Keys);");
            sb.AppendLine($"{indent}    keys{path}.Sort();");
            sb.AppendLine($"{indent}    foreach (var key{path} in keys{path}) {{");
            AppendValueWrite("key" + path, key, plainTypes, sb, indent + "        ", path + "Key");
            AppendValueWrite($"{expr}[key{path}]", value, plainTypes, sb, indent + "        ", path + "Value");
            sb.AppendLine($"{indent}    }}\n{indent}}}");
            return;
        }

        if (plainTypes.TryGetValue(t, out var info) && !info.IsEnum)
        {
            if (info.Kind == "class")
            {
                sb.AppendLine($"{indent}w.Write({expr} != null);");
                sb.AppendLine($"{indent}if ({expr} != null) {{");
                indent += "    ";
            }
            foreach (var field in info.Fields)
                AppendValueWrite(expr + "." + field.Name, field.TypeFqn, plainTypes, sb, indent, path + field.Name);
            if (info.Kind == "class") sb.AppendLine(indent[..^4] + "}");
            return;
        }

        sb.AppendLine($"{indent}if (!System.Enum.IsDefined(typeof({t}), {expr})) throw new System.IO.InvalidDataException(\"Unknown enum value.\");");
        sb.Append(indent).Append("w.Write((int)(").Append(expr).Append("));\n");
    }

    private static void AppendRestore(string ctx, List<PinnedComponent> entries,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb)
    {
        sb.Append("    public static void Restore(global::").Append(ctx).Append("Entity e, System.IO.BinaryReader r) {\n");
        foreach (var entry in entries)
        {
            var def = entry.Definition!;
            if (def.IsFlag)
            {
                sb.Append("        if (r.ReadBoolean()) {\n");
                sb.Append("            e.is").Append(def.ComponentName).Append(" = true;\n");
                sb.Append("        } else {\n");
                sb.Append("            e.is").Append(def.ComponentName).Append(" = false;\n");
                sb.Append("        }\n");
                continue;
            }

            sb.Append("        if (r.ReadBoolean()) {\n");
            var locals = new List<string>();
            foreach (var f in def.Fields)
            {
                AppendSingleRestore(f, plainTypes, sb, "            ", locals);
            }
            sb.Append("            e.Replace").Append(def.ComponentName)
              .Append('(').Append(string.Join(", ", locals)).Append(");\n");
            sb.Append("        } else if (e.has").Append(def.ComponentName).Append(") {\n");
            sb.Append("            e.Remove").Append(def.ComponentName).Append("();\n");
            sb.Append("        }\n");
        }
        sb.Append("    }\n");
    }

    private static void AppendSingleRestore(FieldInfo f, Dictionary<string, PlainTypeInfo> plainTypes,
        StringBuilder sb, string indent, List<string> locals)
    {
        var local = Templates.LowerFirst(f.Name);
        AppendValueRestore(local, f.TypeFqn, plainTypes, sb, indent);
        locals.Add(local);
    }

    private static void AppendValueRestore(string local, string t,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb, string indent)
    {
        if (t == "string")
        {
            sb.AppendLine($"{indent}var {local} = ReadPresence(r) ? r.ReadString() : null;");
            return;
        }
        if (PrimitiveReaders.TryGetValue(t, out var reader))
        {
            sb.AppendLine($"{indent}var {local} = r.{reader}();");
            return;
        }
        var knownLeaf = ReadKnownLeaf(t);
        if (knownLeaf is not null)
        {
            sb.AppendLine($"{indent}var {local} = {knownLeaf};");
            return;
        }
        if (t.EndsWith("[]", StringComparison.Ordinal))
        {
            var elem = t[..^2];
            var count = local + "Count";
            var index = local + "Index";
            sb.AppendLine($"{indent}var {count} = ReadCollectionCount(r);");
            sb.AppendLine($"{indent}var {local} = {count} == -1 ? null : new {elem}[{count}];");
            sb.AppendLine($"{indent}for (var {index} = 0; {index} < {count}; {index}++) {{");
            AppendValueRestore(local + "Item", elem, plainTypes, sb, indent + "    ");
            sb.AppendLine($"{indent}    {local}[{index}] = {local}Item;\n{indent}}}");
            return;
        }
        if (TryDictionary(t, out var key, out var value))
        {
            sb.AppendLine($"{indent}var {local}Count = ReadCollectionCount(r);");
            sb.AppendLine($"{indent}var {local} = {local}Count == -1 ? null : new {t}();");
            sb.AppendLine($"{indent}for (var {local}Index = 0; {local}Index < {local}Count; {local}Index++) {{");
            AppendValueRestore(local + "Key", key, plainTypes, sb, indent + "    ");
            sb.AppendLine($"{indent}    if ({local}.ContainsKey({local}Key)) throw new System.IO.InvalidDataException(\"Duplicate dictionary key.\");");
            AppendValueRestore(local + "Value", value, plainTypes, sb, indent + "    ");
            sb.AppendLine($"{indent}    {local}.Add({local}Key, {local}Value);\n{indent}}}");
            return;
        }
        if (plainTypes.TryGetValue(t, out var single) && !single.IsEnum)
        {
            var nullable = single.Kind == "class";
            sb.AppendLine($"{indent}var {local} = {(nullable ? "ReadPresence(r) ? new " + t + "() : null" : "new " + t + "()")};");
            if (nullable) sb.AppendLine($"{indent}if ({local} != null) {{");
            var inner = nullable ? indent + "    " : indent;
            foreach (var field in single.Fields)
            {
                var nested = local + field.Name;
                AppendValueRestore(nested, field.TypeFqn, plainTypes, sb, inner);
                sb.AppendLine($"{inner}{local}.{field.Name} = {nested};");
            }
            if (nullable) sb.AppendLine(indent + "}");
            return;
        }
        if (plainTypes.TryGetValue(t, out var info) && info.IsEnum)
        {
            sb.AppendLine($"{indent}var {local} = ({t})r.ReadInt32();");
            sb.AppendLine($"{indent}if (!System.Enum.IsDefined(typeof({t}), {local})) throw new System.IO.InvalidDataException(\"Unknown enum value.\");");
            return;
        }
        throw new InvalidOperationException($"序列化恢复生成遇到未支持字段类型（校验阶段应已拦截）: {t}");
    }
}
