using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AbilityKit.ExcelSync.Editor.Codecs;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    public static class ScriptableObjectExcelSync
    {
        [Serializable]
        private sealed class ExcelSoSyncConflict
        {
            public string Key;
            public string Column;
            public string Address;
            public string Base;
            public string Local;
            public string Remote;
        }

        private static string GetBaselineAssetPath(ScriptableObject targetAsset)
        {
            var targetPath = AssetDatabase.GetAssetPath(targetAsset);
            if (string.IsNullOrEmpty(targetPath))
            {
                return string.Empty;
            }
            return targetPath + ".excelBaseline.asset";
        }

        private static ExcelSoSyncBaselineAsset GetOrCreateBaselineAsset(ScriptableObject targetAsset)
        {
            var path = GetBaselineAssetPath(targetAsset);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var a = AssetDatabase.LoadAssetAtPath<ExcelSoSyncBaselineAsset>(path);
            if (a != null)
            {
                return a;
            }

            a = ScriptableObject.CreateInstance<ExcelSoSyncBaselineAsset>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static string NormalizeCellString(object v, string columnName, ExcelCodecRegistry registry)
        {
            var formatted = ExcelReflectionMapper.FormatCellValue(v, columnName, registry ?? ExcelCodecRegistry.Default);
            return formatted == null ? string.Empty : formatted.ToString();
        }

        private static string ToAbsolutePathFromAssetsPath(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(assetsPath))
            {
                return assetsPath;
            }

            if (!assetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(assetsPath);
            }

            var rel = assetsPath.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, rel);
        }

        public static void ValidateSchemaConsistency(ScriptableObject targetAsset, string runtimeRowTypeName)
        {
            if (targetAsset == null)
            {
                throw new ArgumentNullException(nameof(targetAsset));
            }

            if (string.IsNullOrWhiteSpace(runtimeRowTypeName))
            {
                return;
            }

            var dataListMember = FindDataListMember(targetAsset.GetType());
            if (dataListMember == null)
            {
                throw new InvalidOperationException($"Cannot find DataList(List<T>) on {targetAsset.GetType().Name}");
            }

            var editorRowType = GetListElementType(dataListMember);
            if (editorRowType == null)
            {
                throw new InvalidOperationException($"DataList on {targetAsset.GetType().Name} is not List<T>");
            }

            var runtimeRowType = ResolveTypeByName(runtimeRowTypeName);
            if (runtimeRowType == null)
            {
                throw new InvalidOperationException($"Cannot resolve runtime row type '{runtimeRowTypeName}'. Please provide a fully-qualified type name.");
            }

            ValidateTypeSchemaConsistency(editorRowType, runtimeRowType);
        }

        private static Type ResolveTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            typeName = typeName.Trim();

            var t = Type.GetType(typeName, false);
            if (t != null)
            {
                return t;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var a = assemblies[i];
                if (a == null)
                {
                    continue;
                }

                t = a.GetType(typeName, false);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        public static string GenerateEditorModelFromRuntimeType(
            string runtimeRowTypeName,
            string outputFolderAssetsPath,
            string targetNamespace,
            string editorClassName)
        {
            if (string.IsNullOrWhiteSpace(runtimeRowTypeName))
            {
                throw new ArgumentNullException(nameof(runtimeRowTypeName));
            }

            var runtimeRowType = ResolveTypeByName(runtimeRowTypeName);
            if (runtimeRowType == null)
            {
                throw new InvalidOperationException($"Cannot resolve runtime row type '{runtimeRowTypeName}'. Please provide a fully-qualified type name.");
            }

            if (string.IsNullOrWhiteSpace(outputFolderAssetsPath))
            {
                outputFolderAssetsPath = "Assets/Scripts/Editor/Excel/Generated";
            }

            if (!outputFolderAssetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("outputFolderAssetsPath must start with Assets/", nameof(outputFolderAssetsPath));
            }

            if (string.IsNullOrWhiteSpace(targetNamespace))
            {
                targetNamespace = "AbilityKit.ExcelSync";
            }

            if (string.IsNullOrWhiteSpace(editorClassName))
            {
                editorClassName = DeriveEditorModelClassName(runtimeRowType);
            }

            var absoluteFolder = ToAbsolutePathFromAssetsPath(outputFolderAssetsPath);
            if (string.IsNullOrEmpty(absoluteFolder))
            {
                throw new InvalidOperationException($"Invalid output folder '{outputFolderAssetsPath}'");
            }

            Directory.CreateDirectory(absoluteFolder);

            var fileName = editorClassName + ".g.cs";
            var absFile = Path.Combine(absoluteFolder, fileName);
            var assetsFile = outputFolderAssetsPath.TrimEnd('/', '\\') + "/" + fileName;

            if (File.Exists(absFile))
            {
                throw new InvalidOperationException($"Generated file already exists: {assetsFile}");
            }

            var members = CollectRuntimeMembers(runtimeRowType);
            var sb = new StringBuilder(4096);
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using AbilityKit.ExcelSync.Editor;");
            sb.AppendLine();
            sb.Append("namespace ").Append(targetNamespace).AppendLine();
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.Append("    public sealed class ").Append(editorClassName).AppendLine();
            sb.AppendLine("    {");

            for (var i = 0; i < members.Count; i++)
            {
                var m = members[i];
                var columnName = m.columnName;
                var fieldName = m.fieldName;
                var fieldType = m.fieldType;

                sb.Append("        [ExcelColumn(\"").Append(columnName).Append("\", Order = ").Append(i).AppendLine(")]");
                sb.Append("        public ").Append(GetCSharpTypeName(fieldType)).Append(' ').Append(fieldName).AppendLine(";");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(absFile, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            return assetsFile;
        }

        public static string GenerateEditorPartialRawFromExcel(
            ScriptableObject targetAsset,
            string runtimeRowTypeName,
            string excelFilePath,
            ExcelTableOptions options,
            string outputFolderAssetsPath)
        {
            if (targetAsset == null)
            {
                throw new ArgumentNullException(nameof(targetAsset));
            }

            if (string.IsNullOrWhiteSpace(runtimeRowTypeName))
            {
                throw new ArgumentNullException(nameof(runtimeRowTypeName));
            }

            if (string.IsNullOrWhiteSpace(excelFilePath))
            {
                throw new ArgumentNullException(nameof(excelFilePath));
            }

            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException(excelFilePath);
            }

            options ??= new ExcelTableOptions();

            var dataListMember = FindDataListMember(targetAsset.GetType());
            if (dataListMember == null)
            {
                throw new InvalidOperationException($"Cannot find DataList(List<T>) on {targetAsset.GetType().Name}");
            }

            var editorRowType = GetListElementType(dataListMember);
            if (editorRowType == null)
            {
                throw new InvalidOperationException($"DataList on {targetAsset.GetType().Name} is not List<T>");
            }

            var runtimeRowType = ResolveTypeByName(runtimeRowTypeName);
            if (runtimeRowType == null)
            {
                throw new InvalidOperationException($"Cannot resolve runtime row type '{runtimeRowTypeName}'. Please provide a fully-qualified type name.");
            }

            var runtimeSchema = CollectExpectedColumnSchema(runtimeRowType);
            if (runtimeSchema.Count == 0)
            {
                throw new InvalidOperationException($"Runtime row type '{runtimeRowType.FullName}' has no ExcelColumn members.");
            }

            var (headers, labels) = ReadHeadersAndLabelsFromExcel(excelFilePath, options);
            ValidateHeadersStrict(editorRowType, headers, options, "ExcelSoSync.GenerateEditorPartialRawFromExcel");

            if (string.IsNullOrWhiteSpace(outputFolderAssetsPath))
            {
                outputFolderAssetsPath = "Assets/Scripts/Editor/Excel/Generated";
            }

            if (!outputFolderAssetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("outputFolderAssetsPath must start with Assets/", nameof(outputFolderAssetsPath));
            }

            var absFolder = ToAbsolutePathFromAssetsPath(outputFolderAssetsPath);
            Directory.CreateDirectory(absFolder);

            var ns = string.IsNullOrEmpty(editorRowType.Namespace) ? "AbilityKit.ExcelSync" : editorRowType.Namespace;
            var className = editorRowType.Name;
            var fileName = className + ".Raw.g.cs";
            var absFile = Path.Combine(absFolder, fileName);
            var assetsFile = outputFolderAssetsPath.TrimEnd('/', '\\') + "/" + fileName;

            var hiddenPrefixes = CollectHiddenPrefixesFromEnhancedEditorMembers(editorRowType);

            var sb = new StringBuilder(4096);
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by ScriptableObjectExcelSync.GenerateEditorPartialRawFromExcel");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using AbilityKit.ExcelSync.Editor;");
            sb.AppendLine("using Newtonsoft.Json.Linq;");
            sb.AppendLine("using Sirenix.OdinInspector;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.Append("namespace ").Append(ns).AppendLine();
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.Append("    public sealed partial class ").Append(className).AppendLine();
            sb.AppendLine("    {");

            var order = 0;
            for (var col = 0; col < headers.Count; col++)
            {
                var header = NormalizeHeader(headers[col]);
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                if (!runtimeSchema.TryGetValue(header, out var fieldType))
                {
                    continue;
                }

                var label = (labels != null && col < labels.Count) ? (labels[col] ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrEmpty(label))
                {
                    label = header;
                }

                var hide = hiddenPrefixes.Any(p => header.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                sb.Append("        [LabelText(\"").Append(EscapeStringLiteral(label)).AppendLine("\")]");
                sb.Append("        [ExcelColumn(\"").Append(EscapeStringLiteral(header)).Append("\", Order = ").Append(order).AppendLine(")]");
                if (hide)
                {
                    sb.AppendLine("        [HideInInspector]");
                }

                sb.Append("        public ").Append(GetCSharpTypeNameForEditor(fieldType)).Append(' ').Append(SanitizeIdentifier(FindMemberNameByColumn(runtimeRowType, header) ?? header)).AppendLine(";");
                sb.AppendLine();
                order++;
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(absFile, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            return assetsFile;
        }

        private static (IReadOnlyList<string> headers, IReadOnlyList<string> labels) ReadHeadersAndLabelsFromExcel(string excelFilePath, ExcelTableOptions options)
        {
            #if EPPLUS_4_5_OR_NEWER
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            #endif
            using var package = new ExcelPackage(new FileInfo(excelFilePath));
            if (package.Workbook.Worksheets.Count == 0)
            {
                return (new List<string>(), new List<string>());
            }

            var sheet = string.IsNullOrEmpty(options.SheetName)
                ? package.Workbook.Worksheets[0]
                : (package.Workbook.Worksheets[options.SheetName] ?? package.Workbook.Worksheets[0]);

            if (sheet.Dimension == null)
            {
                return (new List<string>(), new List<string>());
            }

            var maxCols = sheet.Dimension.End.Column;
            var headers = new List<string>(maxCols);
            var labels = new List<string>(maxCols);

            var labelRow = options.HeaderRowIndex + 1;
            for (var c = 1; c <= maxCols; c++)
            {
                headers.Add((sheet.Cells[options.HeaderRowIndex, c].Value?.ToString() ?? string.Empty).Trim());
                labels.Add((sheet.Cells[labelRow, c].Value?.ToString() ?? string.Empty).Trim());
            }

            return (headers, labels);
        }

        private static IReadOnlyList<string> CollectHiddenPrefixesFromEnhancedEditorMembers(Type editorRowType)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance;
            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in editorRowType.GetFields(flags))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr == null || !attr.Ignore)
                {
                    continue;
                }

                var name = NormalizeHeader(attr.Name);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (name.EndsWith("Editor", StringComparison.OrdinalIgnoreCase) && name.Length > 6)
                {
                    prefixes.Add(name.Substring(0, name.Length - 6));
                }
                else
                {
                    prefixes.Add(name);
                }
            }

            foreach (var p in editorRowType.GetProperties(flags))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr == null || !attr.Ignore)
                {
                    continue;
                }

                var name = NormalizeHeader(attr.Name);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (name.EndsWith("Editor", StringComparison.OrdinalIgnoreCase) && name.Length > 6)
                {
                    prefixes.Add(name.Substring(0, name.Length - 6));
                }
                else
                {
                    prefixes.Add(name);
                }
            }

            return prefixes.OrderByDescending(x => x.Length).ToList();
        }

        private static string EscapeStringLiteral(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = NormalizeSingleLine(s);
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string NormalizeSingleLine(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            // Excel 单元格可能包含换行，直接写入 C# 字符串字面量会断行导致生成代码编译失败。
            // 这里统一替换为单空格，并做一次去重。
            s = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");

            while (s.Contains("  "))
            {
                s = s.Replace("  ", " ");
            }

            return s.Trim();
        }

        private static string FindMemberNameByColumn(Type runtimeRowType, string columnName)
        {
            if (runtimeRowType == null || string.IsNullOrEmpty(columnName))
            {
                return null;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in runtimeRowType.GetFields(flags))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var n = NormalizeHeader(attr?.Name ?? f.Name);
                if (string.Equals(n, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return f.Name;
                }
            }

            foreach (var p in runtimeRowType.GetProperties(flags))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var n = NormalizeHeader(attr?.Name ?? p.Name);
                if (string.Equals(n, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return p.Name;
                }
            }

            return null;
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Field";
            }

            name = name.Trim();
            if (char.IsLetter(name[0]) || name[0] == '_')
            {
                return name;
            }

            return "_" + name;
        }

        private static string GetCSharpTypeNameForEditor(Type t)
        {
            if (t == typeof(JObject)) return "string";
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var arg = t.GetGenericArguments()[0];
                if (arg == typeof(JObject))
                {
                    return "List<string>";
                }

                if (IsHotUpdateConfigCustomType(arg))
                {
                    return "List<string>";
                }
            }

            if (IsHotUpdateConfigCustomType(t))
            {
                return "string";
            }
            return GetCSharpTypeName(t);
        }

        private static bool IsHotUpdateConfigCustomType(Type t)
        {
            if (t == null)
            {
                return false;
            }

            // Treat runtime custom types under HotUpdate.Config as string in editor raw model,
            // so they can be edited directly as cell text (e.g. {x:1,y:2}).
            if (string.IsNullOrEmpty(t.Namespace) || !t.Namespace.StartsWith("HotUpdate.Config", StringComparison.Ordinal))
            {
                return false;
            }

            if (t.IsEnum || t.IsPrimitive)
            {
                return false;
            }

            if (t == typeof(string) || t == typeof(JObject))
            {
                return false;
            }

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                return false;
            }

            return true;
        }

        private static string DeriveEditorModelClassName(Type runtimeRowType)
        {
            var n = runtimeRowType.Name;
            if (n.StartsWith("T", StringComparison.Ordinal) && n.EndsWith("Data", StringComparison.Ordinal) && n.Length > 5)
            {
                n = n.Substring(1, n.Length - 1 - 4);
            }

            return n + "ConfigData_Gen";
        }

        private static List<(string columnName, string fieldName, Type fieldType)> CollectRuntimeMembers(Type runtimeRowType)
        {
            var list = new List<(string columnName, string fieldName, Type fieldType)>();
            var flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var f in runtimeRowType.GetFields(flags))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var columnName = NormalizeHeader(attr?.Name ?? f.Name);
                if (string.IsNullOrEmpty(columnName))
                {
                    continue;
                }

                list.Add((columnName, SanitizeIdentifier(f.Name), f.FieldType));
            }

            foreach (var p in runtimeRowType.GetProperties(flags))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var columnName = NormalizeHeader(attr?.Name ?? p.Name);
                if (string.IsNullOrEmpty(columnName))
                {
                    continue;
                }

                list.Add((columnName, SanitizeIdentifier(p.Name), p.PropertyType));
            }

            return list
                .GroupBy(x => x.columnName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.columnName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetCSharpTypeName(Type t)
        {
            if (t == typeof(int)) return "int";
            if (t == typeof(long)) return "long";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var arg = t.GetGenericArguments()[0];
                return $"List<{GetCSharpTypeName(arg)}>";
            }

            return t.FullName ?? t.Name;
        }

        private static Dictionary<string, Type> CollectExpectedColumnSchema(Type targetType)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance;
            var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in targetType.GetFields(flags))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var name = NormalizeHeader(attr?.Name ?? f.Name);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (!dict.ContainsKey(name))
                {
                    dict.Add(name, f.FieldType);
                }
            }

            foreach (var p in targetType.GetProperties(flags))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var name = NormalizeHeader(attr?.Name ?? p.Name);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (!dict.ContainsKey(name))
                {
                    dict.Add(name, p.PropertyType);
                }
            }

            return dict;
        }

        private static void ValidateTypeSchemaConsistency(Type editorRowType, Type runtimeRowType)
        {
            if (editorRowType == null)
            {
                throw new ArgumentNullException(nameof(editorRowType));
            }
            if (runtimeRowType == null)
            {
                throw new ArgumentNullException(nameof(runtimeRowType));
            }

            var editorSchema = CollectExpectedColumnSchema(editorRowType);
            var runtimeSchema = CollectExpectedColumnSchema(runtimeRowType);

            var missingInEditor = runtimeSchema.Keys
                .Where(k => !editorSchema.ContainsKey(k))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var extraInEditor = editorSchema.Keys
                .Where(k => !runtimeSchema.ContainsKey(k))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var typeMismatch = new List<string>();
            foreach (var kv in runtimeSchema)
            {
                if (!editorSchema.TryGetValue(kv.Key, out var editorType))
                {
                    continue;
                }

                var runtimeType = kv.Value;
                if (!IsSchemaTypeCompatible(editorType, runtimeType))
                {
                    typeMismatch.Add($"{kv.Key}: editor={editorType.Name} runtime={runtimeType.Name}");
                }
            }

            if (missingInEditor.Count == 0 && extraInEditor.Count == 0 && typeMismatch.Count == 0)
            {
                return;
            }

            var parts = new List<string>();
            if (missingInEditor.Count > 0)
            {
                parts.Add($"Missing in editor model ({editorRowType.Name}): {string.Join(",", missingInEditor)}");
            }

            if (extraInEditor.Count > 0)
            {
                parts.Add($"Extra in editor model ({editorRowType.Name}): {string.Join(",", extraInEditor)}");
            }

            if (typeMismatch.Count > 0)
            {
                parts.Add($"Type mismatch: {string.Join(" | ", typeMismatch)}");
            }

            throw new InvalidOperationException($"[ExcelSoSync.Schema] Editor model type '{editorRowType.FullName}' is not consistent with runtime type '{runtimeRowType.FullName}'. {string.Join(". ", parts)}");
        }

        private static bool IsSchemaTypeCompatible(Type editorType, Type runtimeType)
        {
            if (editorType == runtimeType)
            {
                return true;
            }

            if (runtimeType == typeof(Newtonsoft.Json.Linq.JObject) && editorType == typeof(string))
            {
                return true;
            }

            if (editorType == typeof(string) && IsHotUpdateConfigCustomType(runtimeType))
            {
                return true;
            }

            if (editorType.IsGenericType && runtimeType.IsGenericType &&
                editorType.GetGenericTypeDefinition() == typeof(List<>) &&
                runtimeType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var editorArg = editorType.GetGenericArguments()[0];
                var runtimeArg = runtimeType.GetGenericArguments()[0];
                return IsSchemaTypeCompatible(editorArg, runtimeArg);
            }

            return false;
        }

        private static IReadOnlyList<string> CollectExpectedColumnNames(Type targetType)
        {
            var members = new List<(MemberInfo member, string name, int order)>();
            var flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var f in targetType.GetFields(flags))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                members.Add((f, attr?.Name ?? f.Name, attr?.Order ?? int.MaxValue));
            }

            foreach (var p in targetType.GetProperties(flags))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                members.Add((p, attr?.Name ?? p.Name, attr?.Order ?? int.MaxValue));
            }

            return members
                .OrderBy(x => x.order)
                .ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .Select(x => NormalizeHeader(x.name))
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ValidateHeadersStrict(Type elementType, IReadOnlyList<string> headers, ExcelTableOptions options, string stage)
        {
            if (elementType == null)
            {
                throw new ArgumentNullException(nameof(elementType));
            }

            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            var normalizedHeaders = headers
                .Select(NormalizeHeader)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicated = new List<string>();
            foreach (var h in normalizedHeaders)
            {
                if (!seen.Add(h) && !duplicated.Contains(h, StringComparer.OrdinalIgnoreCase))
                {
                    duplicated.Add(h);
                }
            }

            if (duplicated.Count > 0)
            {
                throw new InvalidOperationException($"[{stage}] Duplicated headers: {string.Join(",", duplicated)}");
            }

            var pk = NormalizeHeader(options?.PrimaryKeyColumnName);
            if (!string.IsNullOrEmpty(pk))
            {
                var hasPk = normalizedHeaders.Any(x => string.Equals(x, pk, StringComparison.OrdinalIgnoreCase));
                if (!hasPk)
                {
                    throw new InvalidOperationException($"[{stage}] Missing primary key header '{pk}' in Excel headers.");
                }
            }

            var expected = CollectExpectedColumnNames(elementType);
            var missing = expected
                .Where(x => !normalizedHeaders.Any(h => string.Equals(h, x, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"[{stage}] Excel headers are missing columns required by {elementType.Name}: {string.Join(",", missing)}");
            }
        }

        public static void ImportToSingleAssetDataList(
            ScriptableObject targetAsset,
            string excelFilePath,
            ExcelTableOptions options,
            ITableReaderWriterFactory factory,
            ExcelCodecRegistry registry = null)
        {
            if (targetAsset == null)
            {
                throw new ArgumentNullException(nameof(targetAsset));
            }

            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException(excelFilePath);
            }

            Debug.Log($"[ExcelSoSync][Import] Target={targetAsset.GetType().FullName} AssetPath={AssetDatabase.GetAssetPath(targetAsset)} Excel={excelFilePath} Sheet='{options.SheetName}' HeaderRow={options.HeaderRowIndex} DataStartRow={options.DataStartRowIndex}");

            var dataListMember = FindDataListMember(targetAsset.GetType());
            if (dataListMember == null)
            {
                throw new InvalidOperationException($"Cannot find DataList(List<T>) on {targetAsset.GetType().Name}");
            }

            var elementType = GetListElementType(dataListMember);
            if (elementType == null)
            {
                throw new InvalidOperationException($"DataList on {targetAsset.GetType().Name} is not List<T>");
            }

            var beforeListObj = GetMemberValue(targetAsset, dataListMember) as System.Collections.IList;
            Debug.Log($"[ExcelSoSync][Import] DataListMember={dataListMember.Name} ElementType={elementType.FullName} BeforeCount={(beforeListObj != null ? beforeListObj.Count : -1)}");

            using var reader = factory.CreateReader(excelFilePath, options);
            var headers = reader.GetHeaders();
            ValidateHeadersStrict(elementType, headers, options, "ExcelSoSync.Import");
            var bindings = ExcelReflectionMapper.BuildBindings(elementType, headers);

            var primaryKeyColumnIndex = -1;
            for (var idx = 0; idx < headers.Count; idx++)
            {
                if (string.Equals(headers[idx]?.Trim(), options.PrimaryKeyColumnName?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    primaryKeyColumnIndex = idx;
                    break;
                }
            }

            if (primaryKeyColumnIndex < 0)
            {
                Debug.LogWarning($"[ExcelSoSync][Import] Cannot find primary key column '{options.PrimaryKeyColumnName}' in headers. Will import all non-empty rows.");
            }

            Debug.Log($"[ExcelSoSync][Import] Headers({headers.Count})={string.Join(",", headers.Take(30))}");
            Debug.Log($"[ExcelSoSync][Import] Bindings({bindings.Count})={string.Join(",", bindings.Select(b => $"{b.ColumnName}->{b.Member.Name}[{b.ValueType.Name}]@{b.ColumnIndex}").Take(30))}");

            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

            var baselineRows = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var rowCount = 0;
            var excelRowIndex = options.DataStartRowIndex;
            foreach (var row in reader.ReadRows(options.DataStartRowIndex))
            {
                if (row.Count == 0)
                {
                    excelRowIndex++;
                    continue;
                }

                if (primaryKeyColumnIndex >= 0)
                {
                    if (primaryKeyColumnIndex >= row.Count)
                    {
                        continue;
                    }

                    var pkObj = row[primaryKeyColumnIndex];
                    var pkStr = pkObj?.ToString();
                    if (string.IsNullOrWhiteSpace(pkStr))
                    {
                        continue;
                    }

                    if (long.TryParse(pkStr, out var pkLong) && pkLong == 0)
                    {
                        continue;
                    }
                }

                var item = Activator.CreateInstance(elementType);
                foreach (var b in bindings)
                {
                    if (b.ColumnIndex < 0 || b.ColumnIndex >= row.Count)
                    {
                        continue;
                    }

                    try
                    {
                        var v = ExcelReflectionMapper.ConvertCellValue(row[b.ColumnIndex], b.ValueType, b.ColumnName, registry ?? ExcelCodecRegistry.Default);
                        ExcelReflectionMapper.SetValue(item, b.Member, v);
                    }
                    catch (Exception e)
                    {
                        var raw = row[b.ColumnIndex];
                        var rawStr = raw == null ? "(null)" : raw.ToString();
                        var msg =
                            $"[ExcelSoSync][Import] ConvertCellValue failed. " +
                            $"ExcelRow={excelRowIndex} ColName='{b.ColumnName}' ColIndex={b.ColumnIndex} " +
                            $"Member='{b.Member.Name}' TargetType='{b.ValueType.FullName}' Raw='{rawStr}'.";
                        throw new FormatException(msg, e);
                    }
                }

                // 检查是否实现了ISerializationCallbackReceiver接口，如果是则调用OnAfterDeserialize()
                if (item is UnityEngine.ISerializationCallbackReceiver callbackReceiver)
                {
                    callbackReceiver.OnAfterDeserialize();
                }

                if (primaryKeyColumnIndex >= 0 && primaryKeyColumnIndex < row.Count)
                {
                    var pk = row[primaryKeyColumnIndex]?.ToString();
                    if (!string.IsNullOrWhiteSpace(pk))
                    {
                        pk = pk.Trim();
                        if (!baselineRows.ContainsKey(pk))
                        {
                            var values = new List<string>(headers.Count);
                            for (var i = 0; i < headers.Count; i++)
                            {
                                values.Add(i < row.Count ? NormalizeCellString(row[i], headers[i], registry) : string.Empty);
                            }
                            baselineRows.Add(pk, values);
                        }
                    }
                }

                list.Add(item);
                rowCount++;

                excelRowIndex++;

                if (rowCount <= 3)
                {
                    var preview = string.Join(",", row.Select(x => x == null ? "" : x.ToString()).Take(30));
                    Debug.Log($"[ExcelSoSync][Import] Row#{rowCount} Cols={row.Count} Preview={preview}");
                }
            }

            Debug.Log($"[ExcelSoSync][Import] ReadRowsCount={rowCount}");

            SetMemberValue(targetAsset, dataListMember, list);
            EditorUtility.SetDirty(targetAsset);

            var baselineAsset = GetOrCreateBaselineAsset(targetAsset);
            if (baselineAsset != null)
            {
                baselineAsset.Set(excelFilePath, options, headers, baselineRows);
                EditorUtility.SetDirty(baselineAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var afterListObj = GetMemberValue(targetAsset, dataListMember) as System.Collections.IList;
            Debug.Log($"[ExcelSoSync][Import] AfterCount={(afterListObj != null ? afterListObj.Count : -1)}");
        }

        public static void ExportFromSingleAssetDataList(
            ScriptableObject targetAsset,
            string excelFilePath,
            ExcelTableOptions options,
            ITableReaderWriterFactory factory,
            ExcelCodecRegistry registry = null)
        {
            if (targetAsset == null)
            {
                throw new ArgumentNullException(nameof(targetAsset));
            }

            var dataListMember = FindDataListMember(targetAsset.GetType());
            if (dataListMember == null)
            {
                throw new InvalidOperationException($"Cannot find DataList(List<T>) on {targetAsset.GetType().Name}");
            }

            var elementType = GetListElementType(dataListMember);
            if (elementType == null)
            {
                throw new InvalidOperationException($"DataList on {targetAsset.GetType().Name} is not List<T>");
            }

            var listObj = GetMemberValue(targetAsset, dataListMember) as System.Collections.IEnumerable;
            if (listObj != null && File.Exists(excelFilePath))
            {
                try
                {
                    #if EPPLUS_4_5_OR_NEWER
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    #endif
                    using var package = new ExcelPackage(new FileInfo(excelFilePath));
                    if (package.Workbook.Worksheets.Count == 0)
                    {
                        package.Workbook.Worksheets.Add(string.IsNullOrEmpty(options.SheetName) ? "Sheet1" : options.SheetName);
                    }

                    var sheet = string.IsNullOrEmpty(options.SheetName)
                        ? package.Workbook.Worksheets[0]
                        : (package.Workbook.Worksheets[options.SheetName] ?? package.Workbook.Worksheets[0]);

                    if (sheet.Dimension != null)
                    {
                        var maxCols = sheet.Dimension.End.Column;
                        var existingHeaders = new List<string>(maxCols);
                        for (var c = 1; c <= maxCols; c++)
                        {
                            existingHeaders.Add((sheet.Cells[options.HeaderRowIndex, c].Value?.ToString() ?? string.Empty).Trim());
                        }

                        ValidateHeadersStrict(elementType, existingHeaders, options, "ExcelSoSync.Export");

                        var primaryKeyColumnIndex = -1;
                        for (var idx = 0; idx < existingHeaders.Count; idx++)
                        {
                            if (string.Equals(existingHeaders[idx], options.PrimaryKeyColumnName?.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                primaryKeyColumnIndex = idx;
                                break;
                            }
                        }

                        if (primaryKeyColumnIndex >= 0)
                        {
                            var bindings = BuildExportBindings(elementType, existingHeaders);

                            var pkMember = FindMemberIgnoreCase(elementType, options.PrimaryKeyColumnName);
                            if (pkMember == null)
                            {
                                throw new InvalidOperationException($"Cannot find primary key member '{options.PrimaryKeyColumnName}' on {elementType.Name}");
                            }

                            var codeToRow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            var startRow = options.DataStartRowIndex;
                            var maxRows = sheet.Dimension.End.Row;
                            var lastDataRow = startRow - 1;
                            for (var r = startRow; r <= maxRows; r++)
                            {
                                var v = sheet.Cells[r, primaryKeyColumnIndex + 1].Value?.ToString();
                                if (string.IsNullOrWhiteSpace(v))
                                {
                                    continue;
                                }

                                v = v.Trim();
                                if (!codeToRow.ContainsKey(v))
                                {
                                    codeToRow.Add(v, r);
                                }

                                lastDataRow = Math.Max(lastDataRow, r);
                            }

                            if (lastDataRow < startRow)
                            {
                                lastDataRow = startRow;
                            }

                            var lastAnchorRow = lastDataRow;

                            var baselineAssetPath = GetBaselineAssetPath(targetAsset);
                            var baselineAsset = string.IsNullOrEmpty(baselineAssetPath)
                                ? null
                                : AssetDatabase.LoadAssetAtPath<ExcelSoSyncBaselineAsset>(baselineAssetPath);
                            if (baselineAsset == null)
                            {
                                throw new InvalidOperationException("Baseline is missing. Please run Import first to establish a baseline before Export.");
                            }

                            var baseRowMap = baselineAsset.BuildRowMap();
                            var conflictList = new List<ExcelSoSyncConflict>();

                            foreach (var item in listObj)
                            {
                                if (item == null)
                                {
                                    continue;
                                }

                                var pkObj = ExcelReflectionMapper.GetValue(item, pkMember);
                                var pkStr = pkObj?.ToString();
                                if (string.IsNullOrWhiteSpace(pkStr))
                                {
                                    continue;
                                }

                                pkStr = pkStr.Trim();
                                if (long.TryParse(pkStr, out var pkLong) && pkLong == 0)
                                {
                                    continue;
                                }

                                var isNewRow = false;
                                if (!codeToRow.TryGetValue(pkStr, out var rowIndex))
                                {
                                    var insertRow = lastAnchorRow >= startRow ? lastAnchorRow + 1 : Math.Max(lastDataRow + 1, startRow);
                                    var styleFromRow = lastAnchorRow >= startRow ? lastAnchorRow : Math.Max(lastDataRow, startRow);

                                    sheet.InsertRow(insertRow, 1, styleFromRow);

                                    var keys = codeToRow.Keys.ToList();
                                    foreach (var k in keys)
                                    {
                                        var r = codeToRow[k];
                                        if (r >= insertRow)
                                        {
                                            codeToRow[k] = r + 1;
                                        }
                                    }

                                    rowIndex = insertRow;
                                    codeToRow[pkStr] = rowIndex;
                                    isNewRow = true;
                                }

                                lastAnchorRow = rowIndex;

                                foreach (var b in bindings)
                                {
                                    if (b.ColumnIndex < 0)
                                    {
                                        continue;
                                    }

                                    var cell = sheet.Cells[rowIndex, b.ColumnIndex + 1];
                                    var localStr = NormalizeCellString(ExcelReflectionMapper.GetValue(item, b.Member), b.ColumnName, registry);

                                    if (isNewRow)
                                    {
                                        cell.Value = localStr;
                                        continue;
                                    }

                                    if (!baseRowMap.TryGetValue(pkStr, out var baseRowValues) || baseRowValues == null)
                                    {
                                        continue;
                                    }

                                    if (b.ColumnIndex >= baseRowValues.Count)
                                    {
                                        continue;
                                    }

                                    var baseStr = baseRowValues[b.ColumnIndex] ?? string.Empty;
                                    var remoteStr = NormalizeCellString(cell.Value, b.ColumnName, registry);

                                    if (string.Equals(localStr, baseStr, StringComparison.Ordinal))
                                    {
                                        continue;
                                    }

                                    if (!string.Equals(remoteStr, baseStr, StringComparison.Ordinal) && !string.Equals(remoteStr, localStr, StringComparison.Ordinal))
                                    {
                                        conflictList.Add(new ExcelSoSyncConflict
                                        {
                                            Key = pkStr,
                                            Column = b.ColumnName,
                                            Address = $"{sheet.Name}!{cell.Address}",
                                            Base = baseStr,
                                            Local = localStr,
                                            Remote = remoteStr
                                        });
                                        continue;
                                    }

                                    cell.Value = localStr;
                                }
                            }

                            if (conflictList.Count > 0)
                            {
                                var conflictReportAssetPath = GetBaselineAssetPath(targetAsset) + ".conflicts.json";
                                var conflictReportPath = ToAbsolutePathFromAssetsPath(conflictReportAssetPath);
                                File.WriteAllText(conflictReportPath, JsonUtility.ToJson(new ConflictWrapper { Items = conflictList }, true));
                                AssetDatabase.Refresh();
                                throw new InvalidOperationException($"Export aborted due to {conflictList.Count} conflicts. See {conflictReportPath}");
                            }

                            package.Save();
                            AssetDatabase.Refresh();
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ExcelSoSync][Export] Safe export aborted: {e.Message}");
                    throw;
                }
            }

            throw new InvalidOperationException("Safe export requires an existing Excel file and an import-created baseline. Sequential overwrite export is disabled to avoid overwriting others' changes.");
        }

        [Serializable]
        private sealed class ConflictWrapper
        {
            public List<ExcelSoSyncConflict> Items = new List<ExcelSoSyncConflict>();
        }

        private static MemberInfo FindMemberIgnoreCase(Type t, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in t.GetFields(flags))
            {
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (!p.CanRead)
                {
                    continue;
                }

                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }

        private static void CopyRowStyle(ExcelWorksheet sheet, int sourceRow, int targetRow, int columnCount)
        {
            if (sheet == null)
            {
                return;
            }

            if (sourceRow <= 0 || targetRow <= 0 || columnCount <= 0)
            {
                return;
            }

            for (var c = 1; c <= columnCount; c++)
            {
                var src = sheet.Cells[sourceRow, c];
                var dst = sheet.Cells[targetRow, c];
                dst.StyleID = src.StyleID;
            }

            if (sheet.Row(sourceRow) != null && sheet.Row(targetRow) != null)
            {
                sheet.Row(targetRow).Height = sheet.Row(sourceRow).Height;
                sheet.Row(targetRow).Hidden = sheet.Row(sourceRow).Hidden;
            }
        }

        public static void ImportToAssets(
            Type scriptableObjectType,
            string excelFilePath,
            string outputFolder,
            ExcelTableOptions options,
            ITableReaderWriterFactory factory)
        {
            if (scriptableObjectType == null)
            {
                throw new ArgumentNullException(nameof(scriptableObjectType));
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(scriptableObjectType))
            {
                throw new ArgumentException("Type must be ScriptableObject", nameof(scriptableObjectType));
            }

            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException(excelFilePath);
            }

            if (string.IsNullOrEmpty(outputFolder) || !outputFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("outputFolder must start with Assets/", nameof(outputFolder));
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder.Substring("Assets/".Length)));

            using var reader = factory.CreateReader(excelFilePath, options);
            var headers = reader.GetHeaders();
            var bindings = ExcelReflectionMapper.BuildBindings(scriptableObjectType, headers);

            var codeMember = FindCodeMember(scriptableObjectType, options.PrimaryKeyColumnName);
            if (codeMember == null)
            {
                throw new InvalidOperationException($"Cannot find primary key member '{options.PrimaryKeyColumnName}' on {scriptableObjectType.Name}");
            }

            var dirtyAssets = new List<UnityEngine.Object>();
            var rowIndex = options.DataStartRowIndex;
            foreach (var row in reader.ReadRows(options.DataStartRowIndex))
            {
                if (row.Count == 0)
                {
                    continue;
                }

                var code = ReadPrimaryKey(row, headers, codeMember, options);
                if (code == 0)
                {
                    rowIndex++;
                    continue;
                }

                var assetPath = BuildAssetPath(outputFolder, scriptableObjectType, code);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, scriptableObjectType) as ScriptableObject;
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(scriptableObjectType);
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                foreach (var b in bindings)
                {
                    if (b.ColumnIndex < 0 || b.ColumnIndex >= row.Count)
                    {
                        continue;
                    }

                    var v = ExcelReflectionMapper.ConvertCellValue(row[b.ColumnIndex], b.ValueType, b.ColumnName, ExcelCodecRegistry.Default);
                    ExcelReflectionMapper.SetValue(asset, b.Member, v);
                }

                EditorUtility.SetDirty(asset);
                dirtyAssets.Add(asset);
                rowIndex++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ExportFromAssets(
            Type scriptableObjectType,
            string excelFilePath,
            string inputFolder,
            ExcelTableOptions options,
            ITableReaderWriterFactory factory)
        {
            if (scriptableObjectType == null)
            {
                throw new ArgumentNullException(nameof(scriptableObjectType));
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(scriptableObjectType))
            {
                throw new ArgumentException("Type must be ScriptableObject", nameof(scriptableObjectType));
            }

            if (string.IsNullOrEmpty(inputFolder) || !inputFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("inputFolder must start with Assets/", nameof(inputFolder));
            }

            var headers = ExcelReflectionMapper.BuildHeaders(scriptableObjectType);
            var bindings = BuildExportBindings(scriptableObjectType, headers);

            var assets = LoadAssetsInFolder(inputFolder, scriptableObjectType)
                .OrderBy(x => GetCodeValue(x, options.PrimaryKeyColumnName))
                .ToList();

            using var writer = factory.CreateWriter(excelFilePath, options);
            writer.WriteHeaders(headers, options.HeaderRowIndex);

            var rowIndex = options.DataStartRowIndex;
            foreach (var asset in assets)
            {
                var row = new object[headers.Count];
                foreach (var b in bindings)
                {
                    var v = ExcelReflectionMapper.GetValue(asset, b.Member);
                    row[b.ColumnIndex] = ExcelReflectionMapper.FormatCellValue(v, b.ColumnName, ExcelCodecRegistry.Default);
                }

                writer.WriteRow(rowIndex, row);
                rowIndex++;
            }

            writer.Save();
            AssetDatabase.Refresh();
        }

        private static IReadOnlyList<ExcelReflectionMapper.ColumnBinding> BuildExportBindings(Type targetType, IReadOnlyList<string> headers)
        {
            var members = new List<(MemberInfo member, string name, int order)>();

            foreach (var f in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                members.Add((f, attr?.Name ?? f.Name, attr?.Order ?? int.MaxValue));
            }

            foreach (var p in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite)
                {
                    continue;
                }

                var attr = p.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                members.Add((p, attr?.Name ?? p.Name, attr?.Order ?? int.MaxValue));
            }

            var ordered = members
                .OrderBy(x => x.order)
                .ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                nameToIndex[headers[i]] = i;
            }

            var result = new List<ExcelReflectionMapper.ColumnBinding>();
            foreach (var x in ordered)
            {
                if (!nameToIndex.TryGetValue(x.name, out var idx))
                {
                    continue;
                }

                result.Add(new ExcelReflectionMapper.ColumnBinding
                {
                    ColumnName = x.name,
                    ColumnIndex = idx,
                    Member = x.member,
                    ValueType = ExcelReflectionMapper.GetMemberType(x.member)
                });
            }

            return result;
        }

        private static IEnumerable<ScriptableObject> LoadAssetsInFolder(string folder, Type t)
        {
            var guids = AssetDatabase.FindAssets($"t:{t.Name}", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, t) as ScriptableObject;
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }

        private static long GetCodeValue(ScriptableObject asset, string primaryKeyName)
        {
            var m = FindCodeMember(asset.GetType(), primaryKeyName);
            if (m == null)
            {
                return 0;
            }

            var v = ExcelReflectionMapper.GetValue(asset, m);
            if (v == null)
            {
                return 0;
            }

            if (v is long l)
            {
                return l;
            }

            if (v is int i)
            {
                return i;
            }

            long.TryParse(v.ToString(), out var r);
            return r;
        }

        private static MemberInfo FindCodeMember(Type t, string name)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                return f;
            }

            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p;
        }

        private static MemberInfo FindDataListMember(Type t)
        {
            var f = t.GetField("DataList", BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                return f;
            }

            var p = t.GetProperty("DataList", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanRead && p.CanWrite)
            {
                return p;
            }

            return null;
        }

        private static Type GetListElementType(MemberInfo member)
        {
            var mt = ExcelReflectionMapper.GetMemberType(member);
            if (!mt.IsGenericType || mt.GetGenericTypeDefinition() != typeof(List<>))
            {
                return null;
            }

            return mt.GetGenericArguments()[0];
        }

        private static object GetMemberValue(object target, MemberInfo member)
        {
            return ExcelReflectionMapper.GetValue(target, member);
        }

        private static void SetMemberValue(object target, MemberInfo member, object value)
        {
            ExcelReflectionMapper.SetValue(target, member, value);
        }

        private static long ReadPrimaryKey(IReadOnlyList<object> row, IReadOnlyList<string> headers, MemberInfo codeMember, ExcelTableOptions options)
        {
            var codeColumnIndex = -1;
            for (int idx = 0; idx < headers.Count; idx++)
            {
                if (string.Equals(headers[idx], options.PrimaryKeyColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    codeColumnIndex = idx;
                    break;
                }
            }

            if (codeColumnIndex < 0 || codeColumnIndex >= row.Count)
            {
                return 0;
            }

            var codeColumnName = (headers != null && codeColumnIndex >= 0 && codeColumnIndex < headers.Count) ? headers[codeColumnIndex] : null;
            var v = ExcelReflectionMapper.ConvertCellValue(row[codeColumnIndex], ExcelReflectionMapper.GetMemberType(codeMember), codeColumnName, ExcelCodecRegistry.Default);
            if (v == null)
            {
                return 0;
            }

            if (v is long l)
            {
                return l;
            }

            if (v is int i)
            {
                return i;
            }

            long.TryParse(v.ToString(), out var r);
            return r;
        }

        private static string BuildAssetPath(string outputFolder, Type t, long code)
        {
            var safeName = $"{t.Name}_{code}.asset";
            return $"{outputFolder.TrimEnd('/')}/{safeName}";
        }

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
                return string.Empty;
            
            return header.Trim().Replace(" ", "").Replace("_", "").Replace("-", "");
        }
    }
}
