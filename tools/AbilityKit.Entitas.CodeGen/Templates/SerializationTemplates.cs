using System.Text;
using AbilityKit.Entitas.CodeGen.Model;

namespace AbilityKit.Entitas.CodeGen.Templating;

/// <summary>
/// 组件逐字段序列化模板：生成 {Ctx}ComponentSerialization.Write/Restore（BinaryWriter/BinaryReader）。
/// 覆盖集由 pin 的 serialize:true 决定；类型支持面 = 基元/枚举/平值数据类及其数组（校验在 ModelBuilder 拦截）。
/// 与回滚体系的关系：平值组件走本生成物，blob 组件（Buffs/Motion 等）走各自专属 provider。
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
        "AbilityKit.Deterministic.Fixed64",
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
        if (t == "AbilityKit.Deterministic.Fixed64")
        {
            sb.Append(indent).Append("w.Write(").Append(access).Append(".RawValue);\n");
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
            "AbilityKit.Deterministic.Fixed64" =>
                "AbilityKit.Deterministic.Fixed64.FromRaw(r.ReadInt64())",
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
        var t = f.TypeFqn;
        var expr = $"{access}.{f.Name}";

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
            sb.Append(indent).Append("    for (var i = 0; i < ").Append(expr).Append(".Length; i++) {\n");
            AppendFieldWrites($"{expr}[i]", plainTypes[elem].Fields, plainTypes, sb, indent + "        ");
            sb.Append(indent).Append("    }\n");
            sb.Append(indent).Append("}\n");
            return;
        }

        if (plainTypes.TryGetValue(t, out var info) && !info.IsEnum)
        {
            AppendFieldWrites(expr, info.Fields, plainTypes, sb, indent);
            return;
        }

        // 枚举（或校验阶段漏放的未支持类型——校验先行，这里只会是枚举）
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
        var t = f.TypeFqn;
        var local = Templates.LowerFirst(f.Name);

        if (PrimitiveReaders.TryGetValue(t, out var reader))
        {
            sb.Append(indent).Append("var ").Append(local).Append(" = r.").Append(reader).Append("();\n");
            locals.Add(local);
            return;
        }

        var knownLeaf = ReadKnownLeaf(t);
        if (knownLeaf is not null)
        {
            sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(knownLeaf).Append(";\n");
            locals.Add(local);
            return;
        }

        if (t.EndsWith("[]", StringComparison.Ordinal))
        {
            var elem = t[..^2];
            var count = local + "Count";
            sb.Append(indent).Append("var ").Append(count).Append(" = r.ReadInt32();\n");
            sb.Append(indent).Append("var ").Append(local).Append(" = ").Append(count).Append(" < 0 ? null : new ")
              .Append(elem).Append('[').Append(count).Append("];\n");
            sb.Append(indent).Append("for (var i = 0; i < ").Append(count).Append("; i++) {\n");
            sb.Append(indent).Append("    ").Append(local).Append("[i] = new ").Append(elem).Append("();\n");
            foreach (var ef in plainTypes[elem].Fields)
            {
                AppendElementRestore($"{local}[i]", ef, plainTypes, sb, indent + "    ");
            }
            sb.Append(indent).Append("}\n");
            locals.Add(local);
            return;
        }

        if (plainTypes.TryGetValue(t, out var single) && !single.IsEnum)
        {
            sb.Append(indent).Append("var ").Append(local).Append(" = new ").Append(t).Append("();\n");
            foreach (var ef in single.Fields)
            {
                AppendElementRestore(local, ef, plainTypes, sb, indent);
            }
            locals.Add(local);
            return;
        }

        // 枚举
        sb.Append(indent).Append("var ").Append(local).Append(" = (").Append(t).Append(")r.ReadInt32();\n");
        locals.Add(local);
    }

    private static void AppendElementRestore(string target, FieldInfo f,
        Dictionary<string, PlainTypeInfo> plainTypes, StringBuilder sb, string indent)
    {
        var t = f.TypeFqn;
        if (PrimitiveReaders.TryGetValue(t, out var reader))
        {
            sb.Append(indent).Append(target).Append('.').Append(f.Name).Append(" = r.").Append(reader).Append("();\n");
            return;
        }

        var knownLeaf = ReadKnownLeaf(t);
        if (knownLeaf is not null)
        {
            sb.Append(indent).Append(target).Append('.').Append(f.Name).Append(" = ").Append(knownLeaf).Append(";\n");
            return;
        }

        if (plainTypes.TryGetValue(t, out var info) && info.IsEnum)
        {
            sb.Append(indent).Append(target).Append('.').Append(f.Name).Append(" = (").Append(t).Append(")r.ReadInt32();\n");
            return;
        }

        throw new InvalidOperationException($"序列化恢复生成遇到未支持字段类型（校验阶段应已拦截）: {t} {f.Name}");
    }
}
