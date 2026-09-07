#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Inspectors
{
    [CustomEditor(typeof(TriggerAuthoringTemplateAsset))]
    internal sealed class TriggerAuthoringTemplateAssetEditor : OdinEditor
    {
        private readonly AdvancedDropdownState _nodeBrowserState = new AdvancedDropdownState();
        private TriggerAuthoringTemplateAsset _asset;
        private TriggerAuthoringSyncInspection _inspection;
        private TriggerTypeDescriptorCatalog _types;
        private TriggerEventDescriptorCatalog _events;
        private TriggerGlobalBlackboardDescriptorCatalog _globalBlackboard;
        private Vector2 _scroll;
        private double _nextInspectionAt;
        private bool _showParameters = true;
        private bool _showCondition = true;
        private bool _showActions = true;
        private bool _showDiagnostics = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            _asset = target as TriggerAuthoringTemplateAsset;
            RebuildCatalogs();
        }

        public override void OnInspectorGUI()
        {
            if (_asset == null) return;

            PrepareUndoForInput();
            EditorGUI.BeginChangeCheck();
            DrawToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTemplateHeader();
            DrawParameters();
            _asset.Template.Condition = DrawTemplateTree(
                "Condition",
                TriggerNodeKind.Condition,
                _asset.Template.Condition,
                ref _showCondition);
            _asset.Template.Actions = DrawTemplateTree(
                "Actions",
                TriggerNodeKind.Action,
                _asset.Template.Actions,
                ref _showActions);
            DrawValidation();
            EditorGUILayout.EndScrollView();
            if (!EditorGUI.EndChangeCheck()) return;

            EditorUtility.SetDirty(_asset);
            RebuildCatalogs();
            _nextInspectionAt = 0d;
        }

        private void DrawToolbar()
        {
            RefreshInspection();
            SirenixEditorGUI.BeginHorizontalToolbar();
            GUILayout.Label("Source", GUILayout.Width(44f));
            var state = _inspection != null ? _inspection.State.ToString() : "Unknown";
            var oldColor = GUI.color;
            GUI.color = GetSyncColor(_inspection != null ? _inspection.State : TriggerAuthoringSyncState.Untracked);
            GUILayout.Label(state, EditorStyles.boldLabel, GUILayout.Width(92f));
            GUI.color = oldColor;
            GUILayout.FlexibleSpace();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Import", "Import Template Source JSON"))) Import();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Export", "Export Template Source JSON"))) Export();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Validate", "Validate template schema and trees"))) Repaint();
            SirenixEditorGUI.EndHorizontalToolbar();
        }

        private void DrawTemplateHeader()
        {
            _asset.Metadata = _asset.Metadata ?? new TriggerAuthoringSourceMetadata();
            _asset.Template = _asset.Template ?? new TriggerAuthoringTemplateData();
            var template = _asset.Template;

            SirenixEditorGUI.BeginBox("Template");
            template.TemplateId = EditorGUILayout.TextField("Template Id", template.TemplateId);
            template.TemplateVersion = EditorGUILayout.TextField("Version", template.TemplateVersion);
            template.DisplayName = EditorGUILayout.TextField("Display Name", template.DisplayName);
            template.Description = EditorGUILayout.TextField("Description", template.Description);
            _asset.Metadata.Author = EditorGUILayout.TextField("Author", _asset.Metadata.Author);
            _asset.Metadata.Description = EditorGUILayout.TextField("Source Note", _asset.Metadata.Description);

            EditorGUILayout.BeginHorizontal();
            template.Event = EditorGUILayout.TextField("Event", template.Event);
            if (GUILayout.Button(new GUIContent("Select", "Choose from Event Catalog"), GUILayout.Width(58f)))
                ShowEventMenu(template);
            EditorGUILayout.EndHorizontal();
            SirenixEditorGUI.EndBox();
        }

        private void DrawParameters()
        {
            var template = _asset.Template;
            template.Parameters = template.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            _showParameters = EditorGUILayout.Foldout(
                _showParameters,
                "Parameters (" + template.Parameters.Count + ")",
                true);
            if (!_showParameters) return;

            for (var i = 0; i < template.Parameters.Count; i++)
            {
                var index = i;
                var parameter = template.Parameters[i] ?? (template.Parameters[i] = new TriggerAuthoringTemplateParameterData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                parameter.Name = EditorGUILayout.TextField(parameter.Name);
                var nextType = (TriggerValueType)EditorGUILayout.EnumPopup(parameter.Type, GUILayout.Width(108f));
                if (nextType != parameter.Type)
                {
                    parameter.Type = nextType;
                    parameter.DefaultValue = CreateValue(nextType);
                }
                parameter.Required = GUILayout.Toggle(parameter.Required, "Required", GUILayout.Width(72f));
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();

                parameter.AllowedSources = (TriggerTemplateValueSourceMask)EditorGUILayout.EnumFlagsField(
                    "Instance Sources",
                    parameter.AllowedSources);
                parameter.Description = EditorGUILayout.TextField("Description", parameter.Description);
                parameter.HasDefault = EditorGUILayout.Toggle("Has Default", parameter.HasDefault);
                if (parameter.HasDefault)
                {
                    parameter.DefaultValue = parameter.DefaultValue ?? CreateValue(parameter.Type);
                    TriggerAuthoringValueRefEditor.Draw(
                        parameter.DefaultValue,
                        new TriggerParameterDescriptor(
                            "default",
                            parameter.Type,
                            true,
                            TriggerValueSourceMask.Constant),
                        BuildValueContext());
                }
                EditorGUILayout.EndVertical();

                if (!remove) continue;
                Undo.RecordObject(_asset, "Remove Template Parameter");
                template.Parameters.RemoveAt(index);
                EditorUtility.SetDirty(_asset);
                i--;
            }

            if (GUILayout.Button("Add Parameter", EditorStyles.miniButton))
            {
                Undo.RecordObject(_asset, "Add Template Parameter");
                template.Parameters.Add(new TriggerAuthoringTemplateParameterData
                {
                    Name = CreateUniqueParameterName(template.Parameters),
                    Type = TriggerValueType.Number,
                    DefaultValue = CreateValue(TriggerValueType.Number)
                });
                EditorUtility.SetDirty(_asset);
            }
        }

        private TriggerNodeData DrawTemplateTree(
            string title,
            TriggerNodeKind kind,
            TriggerNodeData root,
            ref bool expanded)
        {
            SirenixEditorGUI.BeginBox(title);
            EditorGUILayout.BeginHorizontal();
            expanded = EditorGUILayout.Foldout(expanded, title, true);
            GUILayout.FlexibleSpace();
            if (root == null)
            {
                if (GUILayout.Button("Add Root", EditorStyles.miniButton, GUILayout.Width(70f)))
                    ShowNodeCreationMenu(kind, created => SetTemplateRoot(kind, created), GUILayoutUtility.GetLastRect());
            }
            else
            {
                if (GUILayout.Button(new GUIContent("Copy", "Copy this tree to clipboard"), EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                    TriggerAuthoringNodeClipboard.Copy(root, kind);
                using (new EditorGUI.DisabledScope(!TriggerAuthoringNodeClipboard.HasNode()))
                {
                    if (GUILayout.Button(new GUIContent("Paste", "Replace root from clipboard"), EditorStyles.miniButtonMid, GUILayout.Width(44f)))
                    {
                        PasteRoot(kind, value => SetTemplateRoot(kind, value));
                        root = GetTemplateRoot(kind);
                    }
                }
                if (GUILayout.Button("x", EditorStyles.miniButtonRight, GUILayout.Width(25f)))
                    root = null;
            }
            EditorGUILayout.EndHorizontal();

            if (expanded && root != null)
                root = DrawNode(root, kind, 0, true);
            SirenixEditorGUI.EndBox();
            return root;
        }

        private TriggerNodeData GetTemplateRoot(TriggerNodeKind kind)
        {
            return kind == TriggerNodeKind.Condition
                ? _asset.Template.Condition
                : _asset.Template.Actions;
        }

        private void SetTemplateRoot(TriggerNodeKind kind, TriggerNodeData root)
        {
            if (kind == TriggerNodeKind.Condition) _asset.Template.Condition = root;
            else _asset.Template.Actions = root;
        }

        private TriggerNodeData DrawNode(TriggerNodeData node, TriggerNodeKind kind, int depth, bool root)
        {
            if (node == null) return null;
            EditorGUILayout.BeginVertical(depth == 0 ? EditorStyles.helpBox : SirenixGUIStyles.BoxContainer);
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                EditorGUILayout.HelpBox("Template trees cannot reference module-local groups.", MessageType.Error);
                node.GroupReference = EditorGUILayout.TextField("Group Reference", node.GroupReference);
                if (GUILayout.Button("Clear Group Reference", EditorStyles.miniButton))
                    node.GroupReference = string.Empty;
                EditorGUILayout.EndVertical();
                return node;
            }

            _types.TryGet(kind, node.Type, out var descriptor);
            var children = node.Children ?? (node.Children = new List<TriggerNodeData>());
            var maxChildren = descriptor != null ? descriptor.MaxChildren : 0;
            var canPasteChild = maxChildren != 0 &&
                                (maxChildren < 0 || children.Count < maxChildren) &&
                                TriggerAuthoringNodeClipboard.HasNode();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(node.Type ?? "<type>", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!canPasteChild))
            {
                if (GUILayout.Button(new GUIContent("Paste", "Paste clipboard node as a child"), EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                    PasteChild(children, kind);
            }
            if (GUILayout.Button(new GUIContent("Type", "Change node type"), EditorStyles.miniButtonMid, GUILayout.Width(42f)))
                ShowNodeTypeMenu(kind, descriptor => ApplyDescriptor(node, descriptor), GUILayoutUtility.GetLastRect());
            var remove = GUILayout.Button(new GUIContent("x", "Remove node"), EditorStyles.miniButtonRight, GUILayout.Width(25f));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                EditorGUILayout.EndVertical();
                return null;
            }

            DrawNodeArguments(node, descriptor);
            if (maxChildren != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Children (" + children.Count + ")", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(maxChildren > 0 && children.Count >= maxChildren))
                {
                    if (GUILayout.Button("+ Child", EditorStyles.miniButton, GUILayout.Width(64f)))
                        ShowNodeCreationMenu(kind, children.Add, GUILayoutUtility.GetLastRect());
                }
                EditorGUILayout.EndHorizontal();

                for (var i = 0; i < children.Count; i++)
                {
                    var child = DrawNode(children[i], kind, depth + 1, false);
                    if (child == null)
                    {
                        children.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        children[i] = child;
                    }
                }
            }
            EditorGUILayout.EndVertical();
            return node;
        }

        private void DrawNodeArguments(TriggerNodeData node, TriggerTypeDescriptor descriptor)
        {
            var arguments = node.Arguments ?? (node.Arguments = new List<TriggerArgumentData>());
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox("Unknown node descriptor. Existing arguments are preserved.", MessageType.Error);
                DrawRawArguments(arguments);
                return;
            }

            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                var argument = FindArgument(arguments, parameter.Name);
                if (argument == null)
                {
                    if (!parameter.Required)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(parameter.Name, EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(42f)))
                            arguments.Add(CreateArgument(parameter));
                        EditorGUILayout.EndHorizontal();
                    }
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(parameter.Name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (!parameter.Required && GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    arguments.Remove(argument);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                argument.Value = argument.Value ?? CreateValue(parameter.Type);
                TriggerAuthoringValueRefEditor.Draw(argument.Value, parameter, BuildValueContext());
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null || HasParameter(descriptor, argument.Name)) continue;
                EditorGUILayout.HelpBox("Unknown argument '" + argument.Name + "' is preserved.", MessageType.Warning);
                TriggerAuthoringValueRefEditor.Draw(argument.Value ?? (argument.Value = new TriggerValueRefData()), null, BuildValueContext());
            }
        }

        private void DrawRawArguments(List<TriggerArgumentData> arguments)
        {
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i] ?? (arguments[i] = new TriggerArgumentData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                argument.Name = EditorGUILayout.TextField(argument.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                TriggerAuthoringValueRefEditor.Draw(argument.Value ?? (argument.Value = new TriggerValueRefData()), null, BuildValueContext());
                EditorGUILayout.EndVertical();
                if (!remove) continue;
                arguments.RemoveAt(i);
                i--;
            }
            if (GUILayout.Button("Add Raw Argument", EditorStyles.miniButton))
                arguments.Add(new TriggerArgumentData());
        }

        private void DrawValidation()
        {
            _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, "Diagnostics", true);
            if (!_showDiagnostics) return;
            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                _asset.Template,
                TriggerAuthoringValidationContext.Create(_asset));
            if (diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox("No diagnostics.", MessageType.Info);
                return;
            }
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Info) continue;
                EditorGUILayout.HelpBox(
                    diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message,
                    diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        private void ShowEventMenu(TriggerAuthoringTemplateData template)
        {
            var menu = new GenericMenu();
            if (_events == null || _events.Definitions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Event Catalog"));
            }
            else
            {
                var definitions = _events.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null) continue;
                    var family = definition.MatchMode == TriggerEventMatchMode.Prefix ? "Event Families" : definition.Category;
                    var label = family + "/" + (string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName);
                    var captured = definition;
                    menu.AddItem(new GUIContent(label), string.Equals(template.Event, definition.Id, StringComparison.Ordinal), () =>
                    {
                        Undo.RecordObject(_asset, "Select Template Event");
                        template.Event = captured.Id;
                        EditorUtility.SetDirty(_asset);
                    });
                }
            }
            menu.ShowAsContext();
        }

        private void ShowNodeTypeMenu(TriggerNodeKind kind, Action<TriggerTypeDescriptor> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor)
            {
                Undo.RecordObject(_asset, "Select Template Node Type");
                selected(descriptor);
                EditorUtility.SetDirty(_asset);
            }
            new TriggerNodeTypeBrowser(_nodeBrowserState, kind, OnType).Show(activator);
        }

        private void ShowNodeCreationMenu(TriggerNodeKind kind, Action<TriggerNodeData> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor)
            {
                Undo.RecordObject(_asset, "Add Template Node");
                selected(CreateNode(descriptor));
                EditorUtility.SetDirty(_asset);
            }
            new TriggerNodeTypeBrowser(_nodeBrowserState, kind, OnType).Show(activator);
        }

        private void PasteRoot(TriggerNodeKind kind, Action<TriggerNodeData> selected)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("Paste Node", "Clipboard does not contain a matching node.", "OK");
                return;
            }
            Undo.RecordObject(_asset, "Paste Template Node");
            selected(pasted);
            EditorUtility.SetDirty(_asset);
        }

        private void PasteChild(List<TriggerNodeData> children, TriggerNodeKind kind)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("Paste Node", "Clipboard does not contain a matching node.", "OK");
                return;
            }
            Undo.RecordObject(_asset, "Paste Template Node");
            children.Add(pasted);
            EditorUtility.SetDirty(_asset);
        }

        private TriggerAuthoringValueRefEditorContext BuildValueContext()
        {
            return new TriggerAuthoringValueRefEditorContext
            {
                Events = _events,
                GlobalBlackboard = _globalBlackboard,
                TemplateParameters = _asset != null && _asset.Template != null
                    ? _asset.Template.Parameters
                    : null
            };
        }

        private void RebuildCatalogs()
        {
            _types = TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var project = _asset != null ? _asset.Project : null;
            _events = TriggerEventDescriptorCatalog.FromAsset(project != null ? project.EventCatalog : null);
            _globalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                project != null ? project.GlobalBlackboardCatalog : null);
        }

        private void Export()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path))
            {
                var name = _asset.Template != null && !string.IsNullOrWhiteSpace(_asset.Template.TemplateId)
                    ? _asset.Template.TemplateId
                    : _asset.name;
                path = EditorUtility.SaveFilePanel(
                    "Export Trigger Template Source JSON", Application.dataPath, name,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringTemplateSourceSync.Export(_asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Template Source Conflict",
                    result.Message + "\n\nForce export and overwrite Source JSON?",
                    "Force Export",
                    "Cancel"))
                result = TriggerAuthoringTemplateSourceSync.Export(_asset, path, true);
            ShowResult("Export", result);
        }

        private void Import()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "Import Trigger Template Source JSON", Application.dataPath,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringTemplateSourceSync.PreviewImport(_asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringTemplateSourceSync.Import(_asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Template Asset Conflict",
                    result.Message + "\n\nForce import and overwrite Asset content?",
                    "Force Import",
                    "Cancel"))
                result = TriggerAuthoringTemplateSourceSync.Import(_asset, path, true);
            ShowResult("Import", result);
        }

        private void ShowResult(string operation, TriggerAuthoringSyncResult result)
        {
            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                _nextInspectionAt = 0d;
                ShowNotification("Template " + operation.ToLowerInvariant() + " succeeded");
                return;
            }
            EditorUtility.DisplayDialog("Trigger Template " + operation + " Failed", result.Message, "OK");
        }

        private void RefreshInspection()
        {
            if (_inspection != null && EditorApplication.timeSinceStartup < _nextInspectionAt) return;
            _inspection = TriggerAuthoringTemplateSourceSync.Inspect(_asset);
            _nextInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
        }

        private string ResolveSourcePath()
        {
            if (string.IsNullOrWhiteSpace(_asset.SourceJsonPath)) return string.Empty;
            if (Path.IsPathRooted(_asset.SourceJsonPath)) return Path.GetFullPath(_asset.SourceJsonPath);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, _asset.SourceJsonPath));
        }

        private void PrepareUndoForInput()
        {
            var current = Event.current;
            if (current == null) return;
            if (current.type == EventType.MouseDown || current.type == EventType.KeyDown)
                Undo.RecordObject(_asset, "Edit Trigger Authoring Template");
        }

        private static TriggerNodeData CreateNode(TriggerTypeDescriptor descriptor)
        {
            var node = new TriggerNodeData
            {
                Kind = descriptor != null ? descriptor.Kind : TriggerNodeKind.Action,
                Type = descriptor != null ? descriptor.Type : string.Empty
            };
            if (descriptor == null) return node;
            AddDefaultArguments(node.Arguments, descriptor);
            return node;
        }

        private static void ApplyDescriptor(TriggerNodeData node, TriggerTypeDescriptor descriptor)
        {
            node.Kind = descriptor.Kind;
            node.GroupReference = string.Empty;
            node.Type = descriptor.Type;
            node.Arguments = new List<TriggerArgumentData>();
            node.Children = new List<TriggerNodeData>();
            AddDefaultArguments(node.Arguments, descriptor);
        }

        private static void AddDefaultArguments(
            ICollection<TriggerArgumentData> arguments,
            TriggerTypeDescriptor descriptor)
        {
            var createdGroups = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                if (parameter.Required ||
                    !string.IsNullOrEmpty(parameter.RequiredGroup) && createdGroups.Add(parameter.RequiredGroup))
                    arguments.Add(CreateArgument(parameter));
            }
        }

        private static TriggerArgumentData CreateArgument(TriggerParameterDescriptor parameter)
        {
            return new TriggerArgumentData
            {
                Name = parameter.Name,
                Value = TriggerAuthoringValueRefEditor.CreateDefaultValue(parameter)
            };
        }

        private static TriggerValueRefData CreateValue(TriggerValueType type)
        {
            return TriggerAuthoringValueRefEditor.CreateDefaultValue(type);
        }

        private static TriggerArgumentData FindArgument(IReadOnlyList<TriggerArgumentData> arguments, string name)
        {
            if (arguments == null) return null;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument != null && string.Equals(argument.Name, name, StringComparison.Ordinal)) return argument;
            }
            return null;
        }

        private static bool HasParameter(TriggerTypeDescriptor descriptor, string name)
        {
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                if (string.Equals(descriptor.Parameters[i].Name, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string CreateUniqueParameterName(IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var suffix = 1;
            while (ContainsParameter(parameters, "param_" + suffix)) suffix++;
            return "param_" + suffix;
        }

        private static bool ContainsParameter(IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters, string name)
        {
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter != null && string.Equals(parameter.Name, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static Color GetSyncColor(TriggerAuthoringSyncState state)
        {
            switch (state)
            {
                case TriggerAuthoringSyncState.InSync: return new Color(0.55f, 0.9f, 0.62f);
                case TriggerAuthoringSyncState.AssetChanged:
                case TriggerAuthoringSyncState.JsonChanged: return new Color(1f, 0.82f, 0.38f);
                case TriggerAuthoringSyncState.Conflict:
                case TriggerAuthoringSyncState.InvalidSource: return new Color(1f, 0.48f, 0.44f);
                default: return Color.white;
            }
        }

        private static void ShowNotification(string message)
        {
            var window = EditorWindow.focusedWindow;
            if (window != null) window.ShowNotification(new GUIContent(message));
        }
    }
}
#endif
