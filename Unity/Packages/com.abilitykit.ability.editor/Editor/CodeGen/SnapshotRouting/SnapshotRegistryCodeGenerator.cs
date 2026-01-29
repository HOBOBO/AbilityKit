#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.CodeGen.SnapshotRouting
{
    internal static class SnapshotRegistryCodeGenerator
    {
        private const string MenuPath = "Tools/AbilityKit/CodeGen/Generate Snapshot Registries";

        [MenuItem(MenuPath)]
        private static void GenerateMenu()
        {
            GenerateAll();
        }

        public static void GenerateAll()
        {
            var registries = FindRegistries();
            if (registries.Count == 0)
            {
                Debug.Log("[SnapshotRegistryCodeGenerator] No registries found.");
                return;
            }

            var decls = CollectDeclarations();

            int generatedCount = 0;
            foreach (var reg in registries)
            {
                if (!decls.TryGetValue(reg.RegistryId, out var bucket))
                {
                    bucket = new DeclarationBucket();
                }

                var code = EmitRegistryCode(reg, bucket);
                WriteIfChanged(reg.OutputAssetPath, code);
                generatedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[SnapshotRegistryCodeGenerator] Generated {generatedCount} registries.");
        }

        private static List<RegistryTarget> FindRegistries()
        {
            var list = new List<RegistryTarget>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    var attr = type.GetCustomAttribute<SnapshotRegistryAttribute>();
                    if (attr == null) continue;

                    var outputPath = GetGeneratedOutputPath(type);
                    list.Add(new RegistryTarget(attr.RegistryId, type, outputPath));
                }
            }

            return list;
        }

        private static Dictionary<string, DeclarationBucket> CollectDeclarations()
        {
            var dict = new Dictionary<string, DeclarationBucket>(StringComparer.Ordinal);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    for (int m = 0; m < methods.Length; m++)
                    {
                        var mi = methods[m];

                        foreach (var a in mi.GetCustomAttributes<SnapshotDecoderAttribute>())
                        {
                            var bucket = GetBucket(dict, a.RegistryId);
                            bucket.Decoders.Add(new DecoderDecl(a.OpCode, a.PayloadType, mi));
                        }

                        foreach (var a in mi.GetCustomAttributes<SnapshotCmdHandlerAttribute>())
                        {
                            var bucket = GetBucket(dict, a.RegistryId);
                            bucket.CmdHandlers.Add(new CmdHandlerDecl(a.OpCode, a.PayloadType, mi));
                        }

                        foreach (var a in mi.GetCustomAttributes<SnapshotPipelineStageAttribute>())
                        {
                            var bucket = GetBucket(dict, a.RegistryId);
                            bucket.PipelineStages.Add(new PipelineStageDecl(a.OpCode, a.Order, a.PayloadType, mi));
                        }
                    }
                }
            }

            return dict;
        }

        private static DeclarationBucket GetBucket(Dictionary<string, DeclarationBucket> dict, string registryId)
        {
            if (!dict.TryGetValue(registryId, out var bucket))
            {
                bucket = new DeclarationBucket();
                dict.Add(registryId, bucket);
            }
            return bucket;
        }

        private static string EmitRegistryCode(RegistryTarget reg, DeclarationBucket decls)
        {
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using AbilityKit.Ability.Server;");
            sb.AppendLine("using AbilityKit.Ability.Share.Common.SnapshotRouting;");
            sb.AppendLine();

            sb.AppendLine($"namespace {reg.TargetType.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static partial class {reg.TargetType.Name}");
            sb.AppendLine("    {");
            sb.AppendLine("        static partial void RegisterAllGenerated(");
            sb.AppendLine("            ISnapshotDecoderRegistry dispatcherDecoders,");
            sb.AppendLine("            ISnapshotDecoderRegistry pipelineDecoders,");
            sb.AppendLine("            ISnapshotPipelineStageRegistry pipeline,");
            sb.AppendLine("            ISnapshotCmdHandlerRegistry cmd)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (dispatcherDecoders == null) throw new ArgumentNullException(nameof(dispatcherDecoders));");
            sb.AppendLine("            if (pipelineDecoders == null) throw new ArgumentNullException(nameof(pipelineDecoders));");
            sb.AppendLine("            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));");
            sb.AppendLine("            if (cmd == null) throw new ArgumentNullException(nameof(cmd));");
            sb.AppendLine();

            // decoders
            for (int i = 0; i < decls.Decoders.Count; i++)
            {
                var d = decls.Decoders[i];
                var payloadTypeName = GetTypeName(d.PayloadType);
                var methodCall = GetMethodRef(d.Method);

                sb.AppendLine($"            dispatcherDecoders.RegisterDecoder<{payloadTypeName}>({d.OpCode}, {methodCall});");
                sb.AppendLine($"            pipelineDecoders.RegisterDecoder<{payloadTypeName}>({d.OpCode}, {methodCall});");
            }

            sb.AppendLine();

            // cmd handlers
            for (int i = 0; i < decls.CmdHandlers.Count; i++)
            {
                var h = decls.CmdHandlers[i];
                var payloadTypeName = GetTypeName(h.PayloadType);
                var methodCall = GetMethodRef(h.Method);

                sb.AppendLine($"            cmd.RegisterCmdHandler<{payloadTypeName}>({h.OpCode}, {methodCall});");
            }

            sb.AppendLine();

            // pipeline stages
            var stages = decls.PipelineStages.OrderBy(x => x.OpCode).ThenBy(x => x.Order).ToArray();
            for (int i = 0; i < stages.Length; i++)
            {
                var s = stages[i];
                var payloadTypeName = GetTypeName(s.PayloadType);
                var methodCall = GetMethodRef(s.Method);

                sb.AppendLine($"            pipeline.AddPipelineStage<{payloadTypeName}>({s.OpCode}, {s.Order}, {methodCall});");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GetGeneratedOutputPath(Type targetType)
        {
            // Convention:
            // - Generated file sits alongside target registry type under ./Generated
            // - Name: <RegistryClassName>.Generated.cs
            var scriptPath = FindScriptAssetPathByTypeName(targetType.Name);
            if (string.IsNullOrEmpty(scriptPath))
            {
                // fallback: Assets/Scripts/Generated
                return "Assets/Scripts/Generated/" + targetType.Name + ".Generated.cs";
            }

            var dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/") ?? "Assets";
            var genDir = dir + "/Generated";
            EnsureFolder(genDir);
            return genDir + "/" + targetType.Name + ".Generated.cs";
        }

        private static string FindScriptAssetPathByTypeName(string typeName)
        {
            var guids = AssetDatabase.FindAssets(typeName + " t:Script");
            if (guids == null || guids.Length == 0) return null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                if (Path.GetFileNameWithoutExtension(path) == typeName) return path;
            }

            return null;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void WriteIfChanged(string assetPath, string content)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(fullPath))
            {
                var existing = File.ReadAllText(fullPath);
                if (existing == content) return;
            }

            File.WriteAllText(fullPath, content);
        }

        private static string GetTypeName(Type t)
        {
            if (t == null) return "object";

            if (t.IsByRef)
            {
                return GetTypeName(t.GetElementType());
            }

            if (t.IsArray)
            {
                var elem = GetTypeName(t.GetElementType());
                var rank = t.GetArrayRank();
                if (rank <= 1) return elem + "[]";
                return elem + "[" + new string(',', rank - 1) + "]";
            }

            if (t.IsPointer)
            {
                return GetTypeName(t.GetElementType()) + "*";
            }

            if (t.IsGenericParameter)
            {
                return t.Name;
            }

            if (TryGetCSharpKeyword(t, out var keyword))
            {
                return keyword;
            }

            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                var args = t.GetGenericArguments();

                if (def == typeof(Nullable<>))
                {
                    return GetTypeName(args[0]) + "?";
                }

                if (IsValueTupleDefinition(def))
                {
                    return "(" + string.Join(", ", args.Select(GetTypeName)) + ")";
                }

                var defName = GetNonGenericTypeFullName(def);
                var argNames = args.Select(GetTypeName);
                return defName + "<" + string.Join(", ", argNames) + ">";
            }

            return GetNonGenericTypeFullName(t);
        }

        private static bool IsValueTupleDefinition(Type def)
        {
            if (def == null) return false;

            var fullName = def.FullName;
            if (string.IsNullOrEmpty(fullName)) return false;

            return fullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal);
        }

        private static bool TryGetCSharpKeyword(Type t, out string keyword)
        {
            if (t == typeof(void)) { keyword = "void"; return true; }
            if (t == typeof(bool)) { keyword = "bool"; return true; }
            if (t == typeof(byte)) { keyword = "byte"; return true; }
            if (t == typeof(sbyte)) { keyword = "sbyte"; return true; }
            if (t == typeof(short)) { keyword = "short"; return true; }
            if (t == typeof(ushort)) { keyword = "ushort"; return true; }
            if (t == typeof(int)) { keyword = "int"; return true; }
            if (t == typeof(uint)) { keyword = "uint"; return true; }
            if (t == typeof(long)) { keyword = "long"; return true; }
            if (t == typeof(ulong)) { keyword = "ulong"; return true; }
            if (t == typeof(char)) { keyword = "char"; return true; }
            if (t == typeof(float)) { keyword = "float"; return true; }
            if (t == typeof(double)) { keyword = "double"; return true; }
            if (t == typeof(decimal)) { keyword = "decimal"; return true; }
            if (t == typeof(string)) { keyword = "string"; return true; }
            if (t == typeof(object)) { keyword = "object"; return true; }

            keyword = null;
            return false;
        }

        private static string GetNonGenericTypeFullName(Type t)
        {
            if (t == null) return "object";
            if (TryGetCSharpKeyword(t, out var keyword)) return keyword;

            if (t.IsNested && t.DeclaringType != null)
            {
                return GetNonGenericTypeFullName(t.DeclaringType) + "." + t.Name;
            }

            if (!string.IsNullOrEmpty(t.Namespace))
            {
                return t.Namespace + "." + t.Name;
            }

            return t.Name;
        }

        private static string GetMethodRef(MethodInfo mi)
        {
            // We only support static methods.
            return GetNonGenericTypeFullName(mi.DeclaringType) + "." + mi.Name;
        }

        private readonly struct RegistryTarget
        {
            public RegistryTarget(string registryId, Type targetType, string outputAssetPath)
            {
                RegistryId = registryId;
                TargetType = targetType;
                OutputAssetPath = outputAssetPath;
            }

            public string RegistryId { get; }
            public Type TargetType { get; }
            public string OutputAssetPath { get; }
        }

        private sealed class DeclarationBucket
        {
            public readonly List<DecoderDecl> Decoders = new List<DecoderDecl>(16);
            public readonly List<CmdHandlerDecl> CmdHandlers = new List<CmdHandlerDecl>(16);
            public readonly List<PipelineStageDecl> PipelineStages = new List<PipelineStageDecl>(16);
        }

        private readonly struct DecoderDecl
        {
            public DecoderDecl(int opCode, Type payloadType, MethodInfo method)
            {
                OpCode = opCode;
                PayloadType = payloadType;
                Method = method;
            }

            public int OpCode { get; }
            public Type PayloadType { get; }
            public MethodInfo Method { get; }
        }

        private readonly struct CmdHandlerDecl
        {
            public CmdHandlerDecl(int opCode, Type payloadType, MethodInfo method)
            {
                OpCode = opCode;
                PayloadType = payloadType;
                Method = method;
            }

            public int OpCode { get; }
            public Type PayloadType { get; }
            public MethodInfo Method { get; }
        }

        private readonly struct PipelineStageDecl
        {
            public PipelineStageDecl(int opCode, int order, Type payloadType, MethodInfo method)
            {
                OpCode = opCode;
                Order = order;
                PayloadType = payloadType;
                Method = method;
            }

            public int OpCode { get; }
            public int Order { get; }
            public Type PayloadType { get; }
            public MethodInfo Method { get; }
        }
    }
}
#endif
