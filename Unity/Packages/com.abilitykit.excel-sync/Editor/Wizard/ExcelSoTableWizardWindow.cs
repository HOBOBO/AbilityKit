using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AbilityKit.ExcelSync.Editor.Codecs;
using OfficeOpenXml;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    public sealed class ExcelSoTableWizardWindow : EditorWindow
    {
        private const string DefaultCodeOutputFolder = "Assets/Scripts/Editor/Excel/Generated";
        private const string DefaultAssetOutputFolder = "Assets/Game/Configs/Excel";

        private string _excelAbsolutePath;
        private string _sheetName = "Sheet1";
        private int _headerRowIndex = 6;
        private int _dataStartRowIndex = 8;
        private string _primaryKeyColumnName = "code";
        private string _runtimeRowTypeName;

        private string _codeOutputFolderAssetsPath = DefaultCodeOutputFolder;
        private string _assetOutputFolderAssetsPath = DefaultAssetOutputFolder;

        [MenuItem("Tools/Excel/Create DataList Table From Excel...")]
        private static void Open()
        {
            GetWindow<ExcelSoTableWizardWindow>(false, "Excel->SO Wizard", true);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Excel", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _excelAbsolutePath = EditorGUILayout.TextField("Excel Path", _excelAbsolutePath);
                    if (GUILayout.Button("Pick", GUILayout.Width(60)))
                    {
                        var p = EditorUtility.OpenFilePanel("Pick Excel", string.Empty, "xlsx");
                        if (!string.IsNullOrEmpty(p))
                        {
                            _excelAbsolutePath = p;
                            if (string.IsNullOrWhiteSpace(_sheetName))
                            {
                                _sheetName = "Sheet1";
                            }
                        }
                    }
                }

                _sheetName = EditorGUILayout.TextField("Sheet", _sheetName);
                _headerRowIndex = EditorGUILayout.IntField("Header Row", _headerRowIndex);
                _dataStartRowIndex = EditorGUILayout.IntField("Data Start Row", _dataStartRowIndex);
                _primaryKeyColumnName = EditorGUILayout.TextField("Primary Key", _primaryKeyColumnName);

                EditorGUILayout.Space(4);
                _runtimeRowTypeName = EditorGUILayout.TextField("Runtime Row Type (optional)", _runtimeRowTypeName);
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                _codeOutputFolderAssetsPath = EditorGUILayout.TextField("Code Folder", _codeOutputFolderAssetsPath);
                _assetOutputFolderAssetsPath = EditorGUILayout.TextField("Asset Folder", _assetOutputFolderAssetsPath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate Code", GUILayout.Width(140)))
                    {
                        GenerateCodeOnly();
                    }

                    if (GUILayout.Button("Create/Import", GUILayout.Width(140)))
                    {
                        CreateAndImport();
                    }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "约定：Row 类型为 {SheetName}Row，Table 类型为 {SheetName}Table（含 DataList 字段）。\n" +
                "如果 Runtime Row Type 提供，则按其 ExcelColumn 字段类型生成；否则所有列使用 string 类型。\n" +
                "首次点击 Create/Import 会先生成代码并触发编译，编译完成后再点一次即可创建资产并导入。",
                MessageType.Info);
        }

        private ExcelTableOptions BuildOptions()
        {
            return new ExcelTableOptions
            {
                SheetName = _sheetName ?? string.Empty,
                HeaderRowIndex = Mathf.Max(1, _headerRowIndex),
                DataStartRowIndex = Mathf.Max(1, _dataStartRowIndex),
                PrimaryKeyColumnName = _primaryKeyColumnName ?? string.Empty,
            };
        }

        private void GenerateCodeOnly()
        {
            if (!ValidateInputs(requireExcel: true))
            {
                return;
            }

            try
            {
                var options = BuildOptions();
                var sheetName = string.IsNullOrWhiteSpace(options.SheetName) ? "Sheet1" : options.SheetName.Trim();

                var (headers, labels) = ReadHeadersAndLabels(_excelAbsolutePath, options);

                var rowTypeName = sheetName + "Row";
                var tableTypeName = sheetName + "Table";

                var schema = TryBuildRuntimeSchema(_runtimeRowTypeName);

                EnsureAssetsFolder(_codeOutputFolderAssetsPath);

                var rowShellPath = _codeOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + rowTypeName + ".cs";
                var rowRawPath = _codeOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + rowTypeName + ".Raw.g.cs";
                var tablePath = _codeOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + tableTypeName + ".cs";

                WriteRowShellIfMissing(rowShellPath, rowTypeName);
                WriteRowRaw(rowRawPath, rowTypeName, headers, labels, schema);
                WriteTableShell(tablePath, tableTypeName, rowTypeName);

                AssetDatabase.Refresh();
                Debug.Log($"[ExcelSync][Wizard] Generated: {rowShellPath}, {rowRawPath}, {tablePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Excel->SO Wizard", e.Message, "OK");
            }
        }

        private void CreateAndImport()
        {
            if (!ValidateInputs(requireExcel: true))
            {
                return;
            }

            var options = BuildOptions();
            var sheetName = string.IsNullOrWhiteSpace(options.SheetName) ? "Sheet1" : options.SheetName.Trim();
            var tableTypeName = sheetName + "Table";
            var rowTypeName = sheetName + "Row";

            var tableType = ResolveTypeByName(tableTypeName);
            if (tableType == null)
            {
                GenerateCodeOnly();
                EditorUtility.DisplayDialog(
                    "Excel->SO Wizard",
                    $"Generated code for {rowTypeName}/{tableTypeName}. Unity needs to compile scripts first.\n\nAfter compilation finishes, click Create/Import again.",
                    "OK");
                return;
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(tableType))
            {
                EditorUtility.DisplayDialog("Excel->SO Wizard", $"Type '{tableType.FullName}' is not a ScriptableObject.", "OK");
                return;
            }

            try
            {
                EnsureAssetsFolder(_assetOutputFolderAssetsPath);

                var assetPath = _assetOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + tableTypeName + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, tableType) as ScriptableObject;
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(tableType);
                    AssetDatabase.CreateAsset(asset, assetPath);
                    AssetDatabase.SaveAssets();
                }

                var backend = new EpplusTableReaderWriterFactory();
                ExcelSyncService.Import(asset, _excelAbsolutePath, options, backend, ExcelCodecRegistry.Default);

                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
                Debug.Log($"[ExcelSync][Wizard] Imported excel '{_excelAbsolutePath}' -> '{assetPath}'");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Excel->SO Wizard", e.Message, "OK");
            }
        }

        private bool ValidateInputs(bool requireExcel)
        {
            if (requireExcel)
            {
                if (string.IsNullOrWhiteSpace(_excelAbsolutePath))
                {
                    EditorUtility.DisplayDialog("Excel->SO Wizard", "Excel Path is empty.", "OK");
                    return false;
                }

                if (!File.Exists(_excelAbsolutePath))
                {
                    EditorUtility.DisplayDialog("Excel->SO Wizard", "Excel file not found: " + _excelAbsolutePath, "OK");
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(_sheetName))
            {
                EditorUtility.DisplayDialog("Excel->SO Wizard", "Sheet is empty.", "OK");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_codeOutputFolderAssetsPath) || !_codeOutputFolderAssetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Excel->SO Wizard", "Code Folder must start with Assets/", "OK");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_assetOutputFolderAssetsPath) || !_assetOutputFolderAssetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Excel->SO Wizard", "Asset Folder must start with Assets/", "OK");
                return false;
            }

            return true;
        }

        private static void EnsureAssetsFolder(string assetsPath)
        {
            var full = ToAbsolutePathFromAssetsPath(assetsPath);
            if (string.IsNullOrEmpty(full))
            {
                throw new InvalidOperationException("Invalid Assets path: " + assetsPath);
            }
            Directory.CreateDirectory(full);
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

        private static Type ResolveTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var a = assemblies[i];
                if (a == null)
                {
                    continue;
                }

                var t = a.GetTypes().FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.Ordinal));
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        private static (IReadOnlyList<string> headers, IReadOnlyList<string> labels) ReadHeadersAndLabels(string excelFilePath, ExcelTableOptions options)
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

        private static Dictionary<string, Type> TryBuildRuntimeSchema(string runtimeRowTypeName)
        {
            if (string.IsNullOrWhiteSpace(runtimeRowTypeName))
            {
                return null;
            }

            var runtimeType = Type.GetType(runtimeRowTypeName, false);
            if (runtimeType == null)
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    runtimeType = a.GetType(runtimeRowTypeName, false);
                    if (runtimeType != null)
                    {
                        break;
                    }
                }
            }

            if (runtimeType == null)
            {
                throw new InvalidOperationException("Cannot resolve runtime row type: " + runtimeRowTypeName);
            }

            var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var f in runtimeType.GetFields(flags))
            {
                var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                if (attr != null && attr.Ignore)
                {
                    continue;
                }

                var col = (attr?.Name ?? f.Name)?.Trim();
                if (string.IsNullOrWhiteSpace(col))
                {
                    continue;
                }

                if (!dict.ContainsKey(col))
                {
                    dict.Add(col, f.FieldType);
                }
            }

            foreach (var p in runtimeType.GetProperties(flags))
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

                var col = (attr?.Name ?? p.Name)?.Trim();
                if (string.IsNullOrWhiteSpace(col))
                {
                    continue;
                }

                if (!dict.ContainsKey(col))
                {
                    dict.Add(col, p.PropertyType);
                }
            }

            return dict;
        }

        private static void WriteRowShellIfMissing(string assetsPath, string rowTypeName)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);
            if (File.Exists(abs))
            {
                return;
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace AbilityKit.ExcelSync.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.Append("    public sealed partial class ").Append(rowTypeName).AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteRowRaw(
            string assetsPath,
            string rowTypeName,
            IReadOnlyList<string> headers,
            IReadOnlyList<string> labels,
            Dictionary<string, Type> runtimeSchema)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);

            var sb = new StringBuilder(4096);
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by AbilityKit.ExcelSync.Editor.ExcelSoTableWizardWindow");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using AbilityKit.ExcelSync.Editor;");
            sb.AppendLine("using Sirenix.OdinInspector;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace AbilityKit.ExcelSync.Generated");
            sb.AppendLine("{");
            sb.Append("    public sealed partial class ").Append(rowTypeName).AppendLine();
            sb.AppendLine("    {");

            var order = 0;
            for (var i = 0; i < headers.Count; i++)
            {
                var header = (headers[i] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var label = (labels != null && i < labels.Count ? labels[i] : string.Empty) ?? string.Empty;
                label = label.Trim();
                if (string.IsNullOrEmpty(label))
                {
                    label = header;
                }

                var fieldType = typeof(string);
                if (runtimeSchema != null && runtimeSchema.TryGetValue(header, out var rt) && rt != null)
                {
                    fieldType = rt;
                }

                sb.Append("        [LabelText(\"").Append(EscapeStringLiteral(label)).AppendLine("\")]");
                sb.Append("        [ExcelColumn(\"").Append(EscapeStringLiteral(header)).Append("\", Order = ").Append(order).AppendLine(")]" );
                sb.Append("        public ").Append(GetCSharpTypeName(fieldType)).Append(' ').Append(SanitizeIdentifier(header)).AppendLine(";");
                sb.AppendLine();

                order++;
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteTableShell(string assetsPath, string tableTypeName, string rowTypeName)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);

            var sb = new StringBuilder(512);
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace AbilityKit.ExcelSync.Generated");
            sb.AppendLine("{");
            sb.Append("    public sealed class ").Append(tableTypeName).AppendLine(" : ScriptableObject");
            sb.AppendLine("    {");
            sb.Append("        public List<").Append(rowTypeName).AppendLine("> DataList = new List<" + rowTypeName + ">();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeStringLiteral(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Field";
            }

            var sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (i == 0)
                {
                    if (char.IsLetter(ch) || ch == '_')
                    {
                        sb.Append(ch);
                    }
                    else if (char.IsDigit(ch))
                    {
                        sb.Append('_').Append(ch);
                    }
                    else
                    {
                        sb.Append('_');
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(ch) || ch == '_')
                    {
                        sb.Append(ch);
                    }
                    else
                    {
                        sb.Append('_');
                    }
                }
            }

            return sb.ToString();
        }

        private static string GetCSharpTypeName(Type t)
        {
            if (t == null)
            {
                return "string";
            }

            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(long)) return "long";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                return "List<" + GetCSharpTypeName(t.GetGenericArguments()[0]) + ">";
            }

            if (t.IsArray)
            {
                return GetCSharpTypeName(t.GetElementType()) + "[]";
            }

            return t.FullName?.Replace('+', '.') ?? t.Name;
        }
    }
}
