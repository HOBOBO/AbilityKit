using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbilityKit.ProtocolEditor.Schema;
using UnityEditor;
using UnityEngine;

#pragma warning disable CS0618 // ProtocolDefinition 已冻结为一次性迁移读取专用。

namespace AbilityKit.ProtocolEditor.UI
{
    /// <summary>
    /// 旧 ScriptableObject 协议定义（ProtocolDefinition）与代码生成双轨入口已于 2026-08 冻结。
    /// 本窗口是唯一保留的入口：只读取既有 ProtocolDefinition 资产，
    /// 通过官方 CatalogCompiler 写入 YAML catalog，完成一次性迁移后即可删除旧资产。
    /// 日常协议定义、校验与导出一律使用 Tools/AbilityKit/Framework/Protocol/Protocol Workspace。
    /// </summary>
    public sealed class LegacyProtocolDefinitionMigrationWindow : EditorWindow
    {
        private const string OfficialWorkspaceMenu = "Tools/AbilityKit/Framework/Protocol/Protocol Workspace";

        private ProtocolDefinition _definition;
        private string _projectId = "migrated";
        private string _catalogId = "";
        private string _domain = "";
        private string _status = string.Empty;
        private MessageType _statusType = MessageType.Info;
        private Vector2 _statusScroll;

        [MenuItem("Tools/AbilityKit/Framework/Protocol/Migrate Legacy ProtocolDefinition (one-time)")]
        private static void Open()
        {
            var window = GetWindow<LegacyProtocolDefinitionMigrationWindow>("Legacy ProtocolDefinition Migration");
            window.minSize = new Vector2(520f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Legacy ScriptableObject protocol definitions and their code generator are FROZEN (superseded by the YAML Protocol Workspace). " +
                "This one-time migration window only reads an existing ProtocolDefinition asset and writes it into a YAML protocol catalog via the official compiler. " +
                "It never generates C# code (MemoryPack DTO / OpCodes generation has been removed).",
                MessageType.Warning);

            _definition = (ProtocolDefinition)EditorGUILayout.ObjectField(
                "Legacy Definition", _definition, typeof(ProtocolDefinition), false);

            if (_definition == null)
            {
                if (GUILayout.Button("Open Protocol Workspace (official entry)")) OpenWorkspace();
                return;
            }

            EditorGUILayout.LabelField("RegistryId", _definition.RegistryId);
            if (string.IsNullOrEmpty(_domain)) _domain = string.IsNullOrWhiteSpace(_definition.Domain) ? "legacy" : _definition.Domain.Trim();
            _projectId = EditorGUILayout.TextField("Project ID", _projectId);
            _domain = EditorGUILayout.TextField("Domain", _domain);
            if (string.IsNullOrWhiteSpace(_catalogId))
            {
                _catalogId = string.IsNullOrWhiteSpace(_definition.RegistryId)
                    ? _projectId.Trim() + "." + _domain
                    : _definition.RegistryId.Trim();
            }
            _catalogId = EditorGUILayout.TextField("Catalog ID", _catalogId);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Messages in asset: {SafeCount(_definition)}", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_projectId) || string.IsNullOrWhiteSpace(_domain)))
            {
                if (GUILayout.Button("Migrate To YAML Catalog...", GUILayout.Height(28f))) Migrate();
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Open Protocol Workspace (official entry)")) OpenWorkspace();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4f);
                _statusScroll = EditorGUILayout.BeginScrollView(_statusScroll, GUILayout.Height(90f));
                EditorGUILayout.HelpBox(_status, _statusType);
                EditorGUILayout.EndScrollView();
            }
        }

        private static int SafeCount(ProtocolDefinition definition) =>
            definition?.Messages?.Count ?? 0;

        private void Migrate()
        {
            var path = EditorUtility.SaveFilePanel(
                "Migrate to YAML catalog",
                Path.Combine(ProtocolCompilerBridge.RepositoryRoot, "Protocols", "Catalogs"),
                (_projectId.Trim() + "." + _domain).Replace(' ', '_') + ".protocol",
                "yaml");
            if (string.IsNullOrEmpty(path)) return;
            // 工作台按 *.protocol.yaml 扫描 Catalogs，归一化保存路径避免 SaveFilePanel 丢掉后缀。
            if (path.EndsWith(".protocol.yaml", StringComparison.OrdinalIgnoreCase)) { }
            else if (path.EndsWith(".protocol", StringComparison.OrdinalIgnoreCase)) path += ".yaml";
            else path += ".protocol.yaml";

            var (catalog, skipped) = BuildCatalog(path);
            if (catalog.messages.Length == 0 && skipped.Count > 0)
            {
                _status = "No migratable messages (all were skipped):\n" + string.Join("\n", skipped);
                _statusType = MessageType.Error;
                return;
            }

            if (ProtocolCompilerBridge.WriteCatalog(catalog, out var status))
            {
                AssetDatabase.Refresh();
                _status = (string.IsNullOrWhiteSpace(status) ? "Catalog written." : status.Trim()) +
                          "\nTarget: " + path +
                          (skipped.Count > 0 ? "\nSkipped messages:\n" + string.Join("\n", skipped) : string.Empty) +
                          "\nNext: open Protocol Workspace to review, then delete the legacy asset.";
                _statusType = MessageType.Info;
            }
            else
            {
                _status = (string.IsNullOrWhiteSpace(status) ? "Migration failed." : status.Trim()) +
                          (skipped.Count > 0 ? "\nSkipped messages:\n" + string.Join("\n", skipped) : string.Empty);
                _statusType = MessageType.Error;
            }
        }

        private (ProtocolCatalogDto catalog, List<string> skipped) BuildCatalog(string targetPath)
        {
            var messages = new List<ProtocolMessageDto>();
            var skipped = new List<string>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            if (_definition.Messages != null)
            {
                foreach (var message in _definition.Messages)
                {
                    if (message == null) continue;

                    if (string.IsNullOrWhiteSpace(message.Name) ||
                        string.IsNullOrWhiteSpace(message.PayloadTypeName) ||
                        message.OpCode <= 0)
                    {
                        skipped.Add($"'{message?.Name ?? "<null>"}' (opCode={message?.OpCode ?? 0}) — needs a name, a payload type and a positive opCode.");
                        continue;
                    }

                    var id = message.Name.Trim();
                    var suffix = 2;
                    while (!usedIds.Add(id)) id = message.Name.Trim() + "_" + suffix++;

                    messages.Add(new ProtocolMessageDto
                    {
                        id = id,
                        opCode = (uint)message.OpCode,
                        direction = MapDirection(message.Channel),
                        kind = MapKind(message.Channel),
                        payloadType = message.PayloadTypeName.Trim(),
                        codec = MapCodec(message.Backend),
                        reliability = "reliable",
                        response = string.Empty,
                        minimumSchemaVersion = 1,
                        maximumSchemaVersion = 1,
                        maximumPayloadBytes = 1048576,
                        captureSampleRate = 1d,
                        sensitiveFields = Array.Empty<string>()
                    });
                }
            }

            var catalog = new ProtocolCatalogDto
            {
                // ProtocolCompilerBridge.WriteCatalog 以 sourcePath 作为 YAML 输出路径。
                sourcePath = targetPath,
                schemaVersion = 1,
                catalogId = _catalogId.Trim(),
                projectId = _projectId.Trim(),
                domain = _domain.Trim(),
                revision = 1,
                defaultCodec = "memorypack",
                messages = messages.ToArray()
            };
            return (catalog, skipped);
        }

        private static string MapDirection(ProtocolDefinition.ChannelKind channel) => channel switch
        {
            ProtocolDefinition.ChannelKind.SnapshotDecoder => "s2c",
            ProtocolDefinition.ChannelKind.SnapshotCmdHandler => "c2s",
            _ => "bidirectional"
        };

        private static string MapKind(ProtocolDefinition.ChannelKind channel) => channel switch
        {
            ProtocolDefinition.ChannelKind.SnapshotDecoder => "push",
            ProtocolDefinition.ChannelKind.SnapshotCmdHandler => "request",
            _ => "event"
        };

        private static string MapCodec(ProtocolDefinition.CodecBackend backend) => backend switch
        {
            ProtocolDefinition.CodecBackend.CustomBinary => "custom-binary",
            ProtocolDefinition.CodecBackend.Protobuf => "protobuf",
            // 旧 Json backend 在 YAML 工作台没有对应 codec，落到默认 memorypack，迁移后需人工复核。
            _ => "memorypack"
        };

        private static void OpenWorkspace() =>
            EditorApplication.ExecuteMenuItem(OfficialWorkspaceMenu);
    }

    public sealed class ProtocolWorkspaceWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "Catalogs", "Wire Schemas", "Export" };
        private static readonly string[] Directions = { "c2s", "s2c", "bidirectional" };
        private static readonly string[] Kinds = { "request", "response", "push", "event" };
        private static readonly string[] Reliabilities = { "reliable", "realtime" };
        private static readonly string[] ScalarTypes =
        {
            "bool", "uint8", "int32", "int64", "uint32", "uint64", "float", "double", "string", "bytes"
        };
        private static readonly string[] MemoryPackModes = { "version-tolerant", "sequential" };
        private static readonly string[] DeclarationKinds = { "class", "struct" };
        private static readonly string[] MemberStyles = { "property", "field" };

        private ProtocolWorkspaceDto _workspace;
        private Vector2 _leftScroll;
        private Vector2 _contentScroll;
        private int _tab;
        private int _catalogIndex;
        private int _messageIndex;
        private int _wireSchemaIndex;
        private int _wireFieldIndex;
        private int _exportProjectIndex;
        private string _exportFolder = "";
        private string _exportNamespace = "AbilityKit.Protocol.Generated";
        private bool _includeUnreferenced;
        private bool _strictExport;
        private bool _dirty;
        private string _status = "Workspace not loaded.";
        private MessageType _statusType = MessageType.Info;

        [MenuItem("Tools/AbilityKit/Framework/Protocol/Protocol Workspace")]
        private static void OpenWorkspace()
        {
            var window = GetWindow<ProtocolWorkspaceWindow>("Protocol Workspace");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_exportFolder))
                _exportFolder = Path.Combine(ProtocolCompilerBridge.RepositoryRoot, "local", "ProtocolExports");
            Reload();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_workspace == null)
            {
                EditorGUILayout.HelpBox(_status, _statusType);
                return;
            }

            var nextTab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(24f));
            if (nextTab != _tab && CanSwitchDocument()) _tab = nextTab;
            EditorGUILayout.Space(4f);
            switch (_tab)
            {
                case 0: DrawCatalogs(); break;
                case 1: DrawWireSchemas(); break;
                default: DrawExport(); break;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_status, _statusType);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Refresh").image, "Reload YAML workspace"),
                        EditorStyles.toolbarButton, GUILayout.Width(32f)))
                    Reload();
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("SaveAs").image, "Save selected YAML document"),
                        EditorStyles.toolbarButton, GUILayout.Width(32f)))
                    SaveSelected();
                if (GUILayout.Button("Compile Catalogs", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                    CompileCatalogs();
                GUILayout.Space(8f);
                GUILayout.Label(WorkspaceSummary(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (_dirty) GUILayout.Label("Modified", EditorStyles.miniBoldLabel);
            }
        }

        private void DrawCatalogs()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCatalogList();
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope()) DrawCatalogInspector();
            }
        }

        private void DrawCatalogList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Catalogs", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+", GUILayout.Width(28f)) && CanSwitchDocument()) CreateCatalog();
                }
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                for (var i = 0; i < _workspace.catalogs.Length; i++)
                {
                    var selected = i == _catalogIndex;
                    var label = string.Format("{0}\n{1} / r{2}",
                        _workspace.catalogs[i].catalogId,
                        _workspace.catalogs[i].projectId,
                        _workspace.catalogs[i].revision);
                    if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(42f)) &&
                        !selected && CanSwitchDocument())
                    {
                        _catalogIndex = i;
                        _messageIndex = 0;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCatalogInspector()
        {
            var catalog = SelectedCatalog();
            if (catalog == null)
            {
                GUILayout.Label("No catalog selected.");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(catalog.catalogId, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open YAML", GUILayout.Width(90f))) OpenSource(catalog.sourcePath);
                if (GUILayout.Button("Save", GUILayout.Width(70f))) SaveCatalog(catalog);
            }
            EditorGUILayout.LabelField("Source", catalog.sourcePath, EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            catalog.catalogId = EditorGUILayout.TextField("Catalog ID", catalog.catalogId);
            catalog.projectId = EditorGUILayout.TextField("Project ID", catalog.projectId);
            catalog.domain = EditorGUILayout.TextField("Domain", catalog.domain);
            catalog.revision = Mathf.Max(1, EditorGUILayout.IntField("Revision", catalog.revision));
            catalog.defaultCodec = EditorGUILayout.TextField("Default Codec", catalog.defaultCodec);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Messages", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add", GUILayout.Width(56f))) AddMessage(catalog);
                using (new EditorGUI.DisabledScope(catalog.messages.Length == 0))
                {
                    if (GUILayout.Button("Duplicate", GUILayout.Width(74f))) DuplicateMessage(catalog);
                    if (GUILayout.Button("Delete", GUILayout.Width(56f))) DeleteMessage(catalog);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(330f))) DrawMessageList(catalog);
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope()) DrawMessageInspector(catalog);
            }
        }

        private void DrawMessageList(ProtocolCatalogDto catalog)
        {
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            for (var i = 0; i < catalog.messages.Length; i++)
            {
                var message = catalog.messages[i];
                var schemaState = HasWireSchema(message.payloadType) ? "schema" : "external";
                var label = string.Format("{0,5}  {1}\n{2} / {3} / {4}",
                    message.opCode, message.id, message.direction, message.kind, schemaState);
                var selected = i == _messageIndex;
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(38f)) && !selected)
                    _messageIndex = i;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawMessageInspector(ProtocolCatalogDto catalog)
        {
            if (catalog.messages.Length == 0) return;
            _messageIndex = Mathf.Clamp(_messageIndex, 0, catalog.messages.Length - 1);
            var message = catalog.messages[_messageIndex];
            GUILayout.Label("Message", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            message.id = EditorGUILayout.TextField("ID", message.id);
            message.opCode = (uint)Math.Min(
                uint.MaxValue,
                Math.Max(1L, EditorGUILayout.LongField("OpCode", message.opCode)));
            message.direction = Popup("Direction", message.direction, Directions);
            message.kind = Popup("Kind", message.kind, Kinds);
            message.payloadType = EditorGUILayout.TextField("Payload Type", message.payloadType);
            message.codec = EditorGUILayout.TextField("Codec", message.codec);
            message.reliability = Popup("Reliability", message.reliability, Reliabilities);
            message.response = EditorGUILayout.TextField("Response", message.response);
            message.minimumSchemaVersion = Mathf.Max(1,
                EditorGUILayout.IntField("Min Schema", message.minimumSchemaVersion));
            message.maximumSchemaVersion = Mathf.Max(message.minimumSchemaVersion,
                EditorGUILayout.IntField("Max Schema", message.maximumSchemaVersion));
            message.maximumPayloadBytes = Mathf.Max(1,
                EditorGUILayout.IntField("Max Payload Bytes", message.maximumPayloadBytes));
            message.captureSampleRate = EditorGUILayout.Slider(
                "Capture Sample Rate", (float)message.captureSampleRate, 0f, 1f);
            var sensitive = string.Join(", ", message.sensitiveFields ?? Array.Empty<string>());
            var updated = EditorGUILayout.TextField("Sensitive Fields", sensitive);
            if (!string.Equals(sensitive, updated, StringComparison.Ordinal))
                message.sensitiveFields = SplitValues(updated);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }

        private void DrawWireSchemas()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawWireSchemaList();
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope()) DrawWireSchemaInspector();
            }
        }

        private void DrawWireSchemaList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Wire Schemas", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(SelectedWireSchema() == null))
                    {
                        if (GUILayout.Button("Add Type", GUILayout.Width(72f)) && CanSwitchDocument())
                            AddWireSchemaType();
                    }
                    if (GUILayout.Button("+", GUILayout.Width(28f)) && CanSwitchDocument()) CreateWireSchema();
                }
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                for (var i = 0; i < _workspace.wireSchemas.Length; i++)
                {
                    var selected = i == _wireSchemaIndex;
                    var schema = _workspace.wireSchemas[i];
                    var source = string.IsNullOrEmpty(schema.sourcePath)
                        ? "<unsaved>"
                        : Path.GetFileName(schema.sourcePath);
                    var label = string.Format("{0}\n{1} / {2} / {3}",
                        schema.QualifiedType, schema.projectId, schema.groupId, source);
                    if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(42f)) &&
                        !selected && CanSwitchDocument())
                    {
                        _wireSchemaIndex = i;
                        _wireFieldIndex = 0;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawWireSchemaInspector()
        {
            var schema = SelectedWireSchema();
            if (schema == null)
            {
                GUILayout.Label("No wire schema selected.");
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(schema.QualifiedType, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open YAML", GUILayout.Width(90f))) OpenSource(schema.sourcePath);
                if (GUILayout.Button("Save", GUILayout.Width(70f))) SaveWireSchema(schema);
            }
            EditorGUILayout.LabelField("Source", schema.sourcePath, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Project ID, Group ID and namespace are shared by every type in this wire document.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(File.Exists(schema.sourcePath)))
            {
                schema.projectId = EditorGUILayout.TextField("Project ID", schema.projectId);
                schema.groupId = EditorGUILayout.TextField("Group ID", schema.groupId);
                schema.@namespace = EditorGUILayout.TextField("Namespace", schema.@namespace);
            }
            schema.type = EditorGUILayout.TextField("Type", schema.type);
            schema.memoryPackMode = Popup("MemoryPack Mode", schema.memoryPackMode, MemoryPackModes);
            schema.declaration = Popup("Declaration", schema.declaration, DeclarationKinds);
            schema.memberStyle = Popup("Member Style", schema.memberStyle, MemberStyles);
            schema.reservedIdsText = EditorGUILayout.TextField(
                "Reserved IDs", schema.reservedIdsText ?? JoinIds(schema.reservedIds));
            if (EditorGUI.EndChangeCheck()) _dirty = true;
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Fields", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add", GUILayout.Width(56f))) AddWireField(schema);
                using (new EditorGUI.DisabledScope(schema.fields.Length == 0))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(56f))) DeleteWireField(schema);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(330f)))
                {
                    _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
                    for (var i = 0; i < schema.fields.Length; i++)
                    {
                        var field = schema.fields[i];
                        var label = string.Format("{0,4}  {1}\n{2}{3}{4}", field.id, field.name,
                            string.IsNullOrEmpty(field.typeName) ? field.scalarType : field.typeName,
                            field.array ? "[]" : "", field.optional ? "?" : "");
                        var selected = i == _wireFieldIndex;
                        if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(38f)) && !selected)
                            _wireFieldIndex = i;
                    }
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope()) DrawWireFieldInspector(schema);
            }
        }

        private void DrawWireFieldInspector(ProtocolWireSchemaDto schema)
        {
            if (schema.fields.Length == 0) return;
            _wireFieldIndex = Mathf.Clamp(_wireFieldIndex, 0, schema.fields.Length - 1);
            var field = schema.fields[_wireFieldIndex];
            GUILayout.Label("Field", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            field.id = (uint)Math.Min(
                uint.MaxValue,
                Math.Max(0L, EditorGUILayout.LongField("ID", field.id)));
            field.name = EditorGUILayout.TextField("Name", field.name);
            var customType = !string.IsNullOrWhiteSpace(field.typeName);
            var typeMode = EditorGUILayout.Popup("Type Mode", customType ? 1 : 0, new[] { "Scalar", "Custom" });
            if (typeMode == 0)
            {
                if (customType) field.typeName = string.Empty;
                field.external = false;
                field.scalarType = Popup("Scalar Type", field.scalarType, ScalarTypes);
            }
            else
            {
                if (!customType) field.typeName = "AbilityKit.Protocol.Generated.NestedPayload";
                field.typeName = EditorGUILayout.TextField("Type Reference", field.typeName);
                field.external = EditorGUILayout.Toggle("External Owner", field.external);
            }
            field.array = EditorGUILayout.Toggle("Array", field.array);
            field.optional = EditorGUILayout.Toggle("Optional", field.optional);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }

        private void DrawExport()
        {
            GUILayout.Label("Project MemoryPack Export", EditorStyles.boldLabel);
            var projects = _workspace.projects ?? Array.Empty<string>();
            if (projects.Length == 0)
            {
                EditorGUILayout.HelpBox("No project IDs are available.", MessageType.Warning);
                return;
            }
            _exportProjectIndex = Mathf.Clamp(_exportProjectIndex, 0, projects.Length - 1);
            _exportProjectIndex = EditorGUILayout.Popup("Project", _exportProjectIndex, projects);
            using (new EditorGUILayout.HorizontalScope())
            {
                _exportFolder = EditorGUILayout.TextField("Output Folder", _exportFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Protocol export folder", _exportFolder, "");
                    if (!string.IsNullOrEmpty(selected)) _exportFolder = selected;
                }
            }
            _exportNamespace = EditorGUILayout.TextField("Catalog Namespace", _exportNamespace);
            _includeUnreferenced = EditorGUILayout.Toggle("Include Unreferenced Schemas", _includeUnreferenced);
            _strictExport = EditorGUILayout.Toggle("Fail On Missing Schemas", _strictExport);
            EditorGUILayout.Space(8f);

            var project = projects[_exportProjectIndex];
            var catalogs = _workspace.catalogs.Count(value => value.projectId == project);
            var messages = _workspace.catalogs.Where(value => value.projectId == project)
                .Sum(value => value.messages.Length);
            var schemas = _workspace.wireSchemas.Count(value => value.projectId == project);
            EditorGUILayout.LabelField("Catalogs", catalogs.ToString());
            EditorGUILayout.LabelField("Messages", messages.ToString());
            EditorGUILayout.LabelField("Owned Wire Schemas", schemas.ToString());
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_exportFolder)))
            {
                if (GUILayout.Button("Export Project", GUILayout.Height(28f))) ExportProject(project);
            }
        }

        private void Reload()
        {
            if (_dirty && _workspace != null && !EditorUtility.DisplayDialog(
                    "Reload Protocol Workspace", "Discard unsaved workspace edits?", "Reload", "Cancel"))
                return;
            if (ProtocolCompilerBridge.LoadWorkspace(out var workspace, out var status))
            {
                _workspace = workspace;
                _catalogIndex = Mathf.Clamp(_catalogIndex, 0, Math.Max(0, workspace.catalogs.Length - 1));
                _wireSchemaIndex = Mathf.Clamp(_wireSchemaIndex, 0, Math.Max(0, workspace.wireSchemas.Length - 1));
                _dirty = false;
                SetStatus(status, workspace.diagnostics.Length == 0 ? MessageType.Info : MessageType.Warning);
            }
            else
            {
                SetStatus(status, MessageType.Error);
            }
        }

        private void SaveSelected()
        {
            if (_tab == 0) SaveCatalog(SelectedCatalog());
            else if (_tab == 1) SaveWireSchema(SelectedWireSchema());
        }

        private void SaveCatalog(ProtocolCatalogDto catalog)
        {
            if (catalog == null) return;
            if (ProtocolCompilerBridge.WriteCatalog(catalog, out var status))
            {
                _dirty = false;
                SetStatus(status, MessageType.Info);
                ReloadWithoutPrompt();
            }
            else SetStatus(status, MessageType.Error);
        }

        private void SaveWireSchema(ProtocolWireSchemaDto schema)
        {
            if (schema == null) return;
            if (!TryParseIds(schema.reservedIdsText, out schema.reservedIds, out var error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }
            if (ProtocolCompilerBridge.WriteWireSchema(schema, out var status))
            {
                _dirty = false;
                SetStatus(status, MessageType.Info);
                ReloadWithoutPrompt();
            }
            else SetStatus(status, MessageType.Error);
        }

        private void CompileCatalogs()
        {
            if (_dirty)
            {
                SetStatus("Save the selected YAML document before compiling catalogs.", MessageType.Warning);
                return;
            }
            if (ProtocolCompilerBridge.CompileCatalogs(out var status))
            {
                AssetDatabase.Refresh();
                SetStatus(status, MessageType.Info);
            }
            else SetStatus(status, MessageType.Error);
        }

        private void ExportProject(string project)
        {
            if (ProtocolCompilerBridge.ExportMemoryPack(
                    project, _exportFolder, _exportNamespace, _includeUnreferenced, _strictExport, out var status))
            {
                AssetDatabase.Refresh();
                SetStatus(status, MessageType.Info);
            }
            else SetStatus(status, MessageType.Error);
        }

        private void CreateCatalog()
        {
            var path = EditorUtility.SaveFilePanel(
                "New Protocol Catalog",
                Path.Combine(ProtocolCompilerBridge.RepositoryRoot, "Protocols", "Catalogs"),
                "project.protocol",
                "yaml");
            if (string.IsNullOrEmpty(path)) return;
            var projectId = SelectedCatalog()?.projectId ?? _workspace.projects.FirstOrDefault() ?? "project.id";
            var catalog = new ProtocolCatalogDto
            {
                sourcePath = path,
                schemaVersion = 1,
                catalogId = projectId + ".domain",
                projectId = projectId,
                domain = "domain",
                revision = 1,
                defaultCodec = "memorypack",
                messages = Array.Empty<ProtocolMessageDto>()
            };
            SaveCatalog(catalog);
        }

        private void CreateWireSchema()
        {
            var path = EditorUtility.SaveFilePanel(
                "New Wire Schema",
                Path.Combine(ProtocolCompilerBridge.RepositoryRoot, "Protocols", "WireSchemas"),
                "Payload.wire",
                "yaml");
            if (string.IsNullOrEmpty(path)) return;
            var projectId = SelectedWireSchema()?.projectId ??
                            SelectedCatalog()?.projectId ??
                            _workspace.projects.FirstOrDefault() ??
                            "project.id";
            var schema = new ProtocolWireSchemaDto
            {
                sourcePath = path,
                schemaVersion = 2,
                sourceType = "Payload",
                projectId = projectId,
                groupId = "domain",
                @namespace = "AbilityKit.Protocol.Generated",
                type = "Payload",
                memoryPackMode = "version-tolerant",
                declaration = "class",
                memberStyle = "property",
                fields = Array.Empty<ProtocolWireFieldDto>(),
                reservedIds = Array.Empty<uint>()
            };
            SaveWireSchema(schema);
        }

        private void AddWireSchemaType()
        {
            var selected = SelectedWireSchema();
            if (selected == null) return;

            var typeName = "New" + selected.type;
            var suffix = 2;
            while (_workspace.wireSchemas.Any(value =>
                       string.Equals(value.sourcePath, selected.sourcePath, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(value.type, typeName, StringComparison.Ordinal)))
            {
                typeName = "New" + selected.type + suffix++;
            }

            var schema = new ProtocolWireSchemaDto
            {
                sourcePath = selected.sourcePath,
                schemaVersion = 2,
                // An empty sourceType tells the compiler to append this type to the existing group document.
                sourceType = string.Empty,
                projectId = selected.projectId,
                groupId = selected.groupId,
                @namespace = selected.@namespace,
                type = typeName,
                memoryPackMode = selected.memoryPackMode,
                declaration = selected.declaration,
                memberStyle = selected.memberStyle,
                fields = Array.Empty<ProtocolWireFieldDto>(),
                reservedIds = Array.Empty<uint>(),
                reservedIdsText = string.Empty
            };

            var schemas = new List<ProtocolWireSchemaDto>(_workspace.wireSchemas)
            {
                schema
            };
            _workspace.wireSchemas = schemas.ToArray();
            _wireSchemaIndex = _workspace.wireSchemas.Length - 1;
            _wireFieldIndex = 0;
            _dirty = true;
            SetStatus(
                $"New type '{schema.type}' staged for group '{schema.groupId}'. Save to append it to {Path.GetFileName(schema.sourcePath)}.",
                MessageType.Info);
        }

        private void AddMessage(ProtocolCatalogDto catalog)
        {
            var values = new List<ProtocolMessageDto>(catalog.messages)
            {
                new ProtocolMessageDto
                {
                    id = "message.event",
                    opCode = NextOpCode(catalog),
                    direction = "c2s",
                    kind = "event",
                    payloadType = "AbilityKit.Protocol.Generated.Payload",
                    codec = catalog.defaultCodec,
                    reliability = "reliable",
                    minimumSchemaVersion = 1,
                    maximumSchemaVersion = 1,
                    maximumPayloadBytes = 1048576,
                    captureSampleRate = 1d,
                    sensitiveFields = Array.Empty<string>()
                }
            };
            catalog.messages = values.ToArray();
            _messageIndex = catalog.messages.Length - 1;
            _dirty = true;
        }

        private void DuplicateMessage(ProtocolCatalogDto catalog)
        {
            if (catalog.messages.Length == 0) return;
            var copy = JsonUtility.FromJson<ProtocolMessageDto>(
                JsonUtility.ToJson(catalog.messages[Mathf.Clamp(_messageIndex, 0, catalog.messages.Length - 1)]));
            copy.id += "-copy";
            copy.opCode = NextOpCode(catalog);
            var values = new List<ProtocolMessageDto>(catalog.messages) { copy };
            catalog.messages = values.ToArray();
            _messageIndex = catalog.messages.Length - 1;
            _dirty = true;
        }

        private void DeleteMessage(ProtocolCatalogDto catalog)
        {
            if (catalog.messages.Length == 0) return;
            var values = new List<ProtocolMessageDto>(catalog.messages);
            values.RemoveAt(Mathf.Clamp(_messageIndex, 0, values.Count - 1));
            catalog.messages = values.ToArray();
            _messageIndex = Mathf.Clamp(_messageIndex, 0, Math.Max(0, catalog.messages.Length - 1));
            _dirty = true;
        }

        private void AddWireField(ProtocolWireSchemaDto schema)
        {
            var nextId = schema.fields.Length == 0
                ? (string.Equals(schema.memoryPackMode, "sequential", StringComparison.Ordinal) ? 0u : 1u)
                : schema.fields.Max(value => value.id) + 1u;
            var fields = new List<ProtocolWireFieldDto>(schema.fields)
            {
                new ProtocolWireFieldDto { id = nextId, name = "value", scalarType = "int32" }
            };
            schema.fields = fields.ToArray();
            _wireFieldIndex = schema.fields.Length - 1;
            _dirty = true;
        }

        private void DeleteWireField(ProtocolWireSchemaDto schema)
        {
            if (schema.fields.Length == 0) return;
            var fields = new List<ProtocolWireFieldDto>(schema.fields);
            fields.RemoveAt(Mathf.Clamp(_wireFieldIndex, 0, fields.Count - 1));
            schema.fields = fields.ToArray();
            _wireFieldIndex = Mathf.Clamp(_wireFieldIndex, 0, Math.Max(0, schema.fields.Length - 1));
            _dirty = true;
        }

        private ProtocolCatalogDto SelectedCatalog() =>
            _workspace == null || _workspace.catalogs.Length == 0
                ? null
                : _workspace.catalogs[Mathf.Clamp(_catalogIndex, 0, _workspace.catalogs.Length - 1)];

        private ProtocolWireSchemaDto SelectedWireSchema() =>
            _workspace == null || _workspace.wireSchemas.Length == 0
                ? null
                : _workspace.wireSchemas[Mathf.Clamp(_wireSchemaIndex, 0, _workspace.wireSchemas.Length - 1)];

        private bool HasWireSchema(string payloadType)
        {
            var type = payloadType ?? string.Empty;
            while (type.EndsWith("[]", StringComparison.Ordinal)) type = type.Substring(0, type.Length - 2);
            return _workspace.wireSchemas.Any(value => value.QualifiedType == type);
        }

        private string WorkspaceSummary() => _workspace == null
            ? "No workspace"
            : string.Format("{0} catalogs  {1} messages  {2} wire schemas  {3} diagnostics",
                _workspace.catalogs.Length,
                _workspace.catalogs.Sum(value => value.messages.Length),
                _workspace.wireSchemas.Length,
                _workspace.diagnostics.Length);

        private static string Popup(string label, string value, string[] options)
        {
            var index = Math.Max(0, Array.IndexOf(options, value));
            return options[EditorGUILayout.Popup(label, index, options)];
        }

        private static string[] SplitValues(string value) => value.Split(',')
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        private static string JoinIds(uint[] values) => string.Join(", ", values ?? Array.Empty<uint>());

        private static bool TryParseIds(string value, out uint[] ids, out string error)
        {
            var parsed = new List<uint>();
            foreach (var item in SplitValues(value ?? string.Empty))
            {
                if (!uint.TryParse(item, out var id) || id == 0)
                {
                    ids = Array.Empty<uint>();
                    error = "Reserved IDs must be positive unsigned integers.";
                    return false;
                }
                parsed.Add(id);
            }
            ids = parsed.Distinct().OrderBy(item => item).ToArray();
            error = string.Empty;
            return true;
        }

        private static uint NextOpCode(ProtocolCatalogDto catalog) =>
            catalog.messages.Length == 0 ? 1u : catalog.messages.Max(value => value.opCode) + 1u;

        private static void OpenSource(string path)
        {
            if (!string.IsNullOrEmpty(path)) UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1);
        }

        private bool CanSwitchDocument()
        {
            if (!_dirty) return true;
            var choice = EditorUtility.DisplayDialogComplex(
                "Unsaved Protocol Changes",
                "Save the current YAML document before switching?",
                "Save",
                "Cancel",
                "Discard");
            if (choice == 0)
            {
                SaveSelected();
                return !_dirty;
            }
            if (choice == 2)
            {
                ReloadWithoutPrompt();
                return true;
            }
            return false;
        }

        private void ReloadWithoutPrompt()
        {
            _dirty = false;
            Reload();
        }

        private void SetStatus(string value, MessageType type)
        {
            _status = string.IsNullOrWhiteSpace(value) ? "Completed." : value.Trim();
            _statusType = type;
            Repaint();
        }
    }

    internal static class ProtocolCompilerBridge
    {
        public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string CompilerProject => Path.Combine(
            RepositoryRoot, "tools", "AbilityKit.Protocol.CatalogCompiler", "AbilityKit.Protocol.CatalogCompiler.csproj");
        private static string CatalogRoot => Path.Combine(RepositoryRoot, "Protocols", "Catalogs");
        private static string WireRoot => Path.Combine(RepositoryRoot, "Protocols", "WireSchemas");
        private static string TempRoot => Path.Combine(Application.dataPath, "..", "Library", "AbilityKit", "ProtocolEditor");

        public static bool LoadWorkspace(out ProtocolWorkspaceDto workspace, out string status)
        {
            Directory.CreateDirectory(TempRoot);
            var output = Path.Combine(TempRoot, "workspace.json");
            var result = RunDotnet(new[]
            {
                "--input", CatalogRoot,
                "--wire-input", WireRoot,
                "--workspace-output", output
            });
            if (!result.Success || !File.Exists(output))
            {
                workspace = null;
                status = result.Message;
                return false;
            }
            workspace = JsonUtility.FromJson<ProtocolWorkspaceDto>(File.ReadAllText(output));
            workspace.Normalize();
            status = result.Message;
            return true;
        }

        public static bool WriteCatalog(ProtocolCatalogDto catalog, out string status)
        {
            Directory.CreateDirectory(TempRoot);
            var input = Path.Combine(TempRoot, "catalog-edit.json");
            File.WriteAllText(input, JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));
            var result = RunDotnet(new[] { "--write-catalog", input, "--output", catalog.sourcePath });
            status = result.Message;
            return result.Success;
        }

        public static bool WriteWireSchema(ProtocolWireSchemaDto schema, out string status)
        {
            Directory.CreateDirectory(TempRoot);
            var input = Path.Combine(TempRoot, "wire-schema-edit.json");
            File.WriteAllText(input, JsonUtility.ToJson(schema, true), new UTF8Encoding(false));
            var result = RunDotnet(new[] { "--write-wire-schema", input, "--output", schema.sourcePath });
            status = result.Message;
            return result.Success;
        }

        public static bool ExportMemoryPack(
            string projectId,
            string outputFolder,
            string targetNamespace,
            bool includeUnreferenced,
            bool strict,
            out string status)
        {
            var arguments = new List<string>
            {
                "--input", CatalogRoot,
                "--wire-input", WireRoot,
                "--export-memorypack", outputFolder,
                "--project", projectId,
                "--namespace", targetNamespace
            };
            if (includeUnreferenced) arguments.Add("--include-unreferenced");
            if (strict) arguments.Add("--strict");
            var result = RunDotnet(arguments);
            status = result.Message;
            return result.Success;
        }

        public static bool CompileCatalogs(out string status)
        {
            var script = Path.Combine(RepositoryRoot, "tools", "compile-protocol-catalogs.ps1");
            var result = RunProcess("powershell.exe", new[]
            {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script
            });
            status = result.Message;
            return result.Success;
        }

        private static ProcessResult RunDotnet(IEnumerable<string> commandArguments)
        {
            var arguments = new List<string>
            {
                "run", "--project", CompilerProject, "--no-restore", "--"
            };
            arguments.AddRange(commandArguments);
            return RunProcess("dotnet", arguments);
        }

        private static ProcessResult RunProcess(string executable, IEnumerable<string> arguments)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = string.Join(" ", arguments.Select(Quote)),
                    WorkingDirectory = RepositoryRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = new System.Diagnostics.Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data != null) output.AppendLine(eventArgs.Data);
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data != null) error.AppendLine(eventArgs.Data);
                    };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    process.WaitForExit();
                    var message = output.ToString().Trim();
                    var errors = error.ToString().Trim();
                    if (errors.Length > 0) message = message.Length == 0 ? errors : message + "\n" + errors;
                    return new ProcessResult(process.ExitCode == 0, message);
                }
            }
            catch (Exception exception)
            {
                return new ProcessResult(false, exception.Message);
            }
        }

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private readonly struct ProcessResult
        {
            public ProcessResult(bool success, string message)
            {
                Success = success;
                Message = message;
            }
            public bool Success { get; }
            public string Message { get; }
        }
    }

    [Serializable]
    internal sealed class ProtocolWorkspaceDto
    {
        public int schemaVersion = 1;
        public string generatorVersion = string.Empty;
        public string[] projects;
        public ProtocolCatalogDto[] catalogs;
        public ProtocolWireSchemaDto[] wireSchemas;
        public ProtocolDiagnosticDto[] diagnostics;

        public void Normalize()
        {
            projects = projects ?? Array.Empty<string>();
            catalogs = catalogs ?? Array.Empty<ProtocolCatalogDto>();
            wireSchemas = wireSchemas ?? Array.Empty<ProtocolWireSchemaDto>();
            diagnostics = diagnostics ?? Array.Empty<ProtocolDiagnosticDto>();
            foreach (var catalog in catalogs) catalog.Normalize();
            foreach (var schema in wireSchemas) schema.Normalize();
        }
    }

    [Serializable]
    internal sealed class ProtocolCatalogDto
    {
        public string sourcePath;
        public int schemaVersion;
        public string catalogId;
        public string projectId;
        public string domain;
        public int revision;
        public string defaultCodec;
        public ProtocolMessageDto[] messages;
        public void Normalize() { messages = messages ?? Array.Empty<ProtocolMessageDto>(); }
    }

    [Serializable]
    internal sealed class ProtocolMessageDto
    {
        public string id;
        public uint opCode;
        public string direction;
        public string kind;
        public string payloadType;
        public string codec;
        public string reliability;
        public string response;
        public int minimumSchemaVersion;
        public int maximumSchemaVersion;
        public int maximumPayloadBytes;
        public double captureSampleRate;
        public string[] sensitiveFields;
    }

    [Serializable]
    internal sealed class ProtocolWireSchemaDto
    {
        public string sourcePath;
        public int schemaVersion;
        public string sourceType;
        public string projectId;
        public string groupId;
        public string @namespace;
        public string type;
        public string memoryPackMode;
        public string declaration;
        public string memberStyle;
        public ProtocolWireFieldDto[] fields;
        public uint[] reservedIds;
        [NonSerialized] public string reservedIdsText;
        public string QualifiedType => string.IsNullOrEmpty(@namespace) ? type : @namespace + "." + type;
        public void Normalize()
        {
            sourceType = string.IsNullOrWhiteSpace(sourceType) ? type : sourceType;
            memoryPackMode = string.IsNullOrWhiteSpace(memoryPackMode) ? "version-tolerant" : memoryPackMode;
            declaration = string.IsNullOrWhiteSpace(declaration) ? "class" : declaration;
            memberStyle = string.IsNullOrWhiteSpace(memberStyle) ? "property" : memberStyle;
            fields = fields ?? Array.Empty<ProtocolWireFieldDto>();
            reservedIds = reservedIds ?? Array.Empty<uint>();
            reservedIdsText = string.Join(", ", reservedIds);
        }
    }

    [Serializable]
    internal sealed class ProtocolWireFieldDto
    {
        public uint id;
        public string name;
        public string scalarType;
        public string typeName;
        public bool external;
        public bool array;
        public bool optional;
    }

    [Serializable]
    internal sealed class ProtocolDiagnosticDto
    {
        public string severity = string.Empty;
        public string code = string.Empty;
        public string catalogId = string.Empty;
        public string messageId = string.Empty;
        public string message = string.Empty;
    }
}
