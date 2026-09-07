#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Editor.Authoring.Extensions;
using AbilityKit.BehaviorTree.Editor.Authoring.Workspace;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AbilityKit.BehaviorTree.Editor.Tests")]

namespace AbilityKit.BehaviorTree.Editor
{
    internal interface IAuthoringInspectorHost
    {
        AuthoringSourceDocument Document { get; }
        bool IsReadOnly { get; }
        ObservationSnapshot? DisplayedObservationSnapshot { get; }
        ObservationSnapshot? PreviousObservationSnapshot { get; }
        ObservationDiff? DisplayedObservationDiff { get; }
        string ResolveNodeDisplayName(NodeDefinition node);
        void RecordChange();
        void RecordChange(string beforeChangeSnapshot);
        void RefreshNodeTitles();
        void RebuildGraph();
        void RefreshChrome();
        void FocusNode(string nodeId);
    }

    /// <summary>行为树右侧属性面板；只通过宿主契约读取文档和请求刷新。</summary>
    internal sealed class AuthoringInspectorRenderer
    {
        private readonly ScrollView _root;
        private readonly IAuthoringInspectorHost _host;
        private NodeDefinition? _selectedNode;
        private Label? _runtimeNodeStateLabel;
        private Label? _runtimeBlackboardLabel;
        private VisualElement? _runtimeTreeBlackboardContainer;
        private TextField? _runtimeBlackboardSearchField;
        private Toggle? _runtimeBlackboardChangedOnlyToggle;
        private string _runtimeBlackboardSearch = "";
        private bool _runtimeBlackboardChangedOnly;

        public AuthoringInspectorRenderer(ScrollView root, IAuthoringInspectorHost host)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void Render(NodeDefinition? selectedNode)
        {
            _selectedNode = selectedNode;
            _root.Clear();
            if (_selectedNode == null)
            {
                DrawTreePanel();
                return;
            }

            var node = _selectedNode;
            var nodeMetadata = _host.Document.GetOrCreateNodeMetadata(node.Id);
            var fallbackName = EditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out var nodeDescriptor)
                ? nodeDescriptor.DisplayName
                : node.Type;
            var displayName = string.IsNullOrWhiteSpace(nodeMetadata.DisplayName)
                ? fallbackName
                : nodeMetadata.DisplayName;

            if (_host.IsReadOnly)
            {
                _root.Add(new Label(displayName));
                if (!string.IsNullOrWhiteSpace(nodeMetadata.Comment))
                    _root.Add(new Label(nodeMetadata.Comment));
                _runtimeNodeStateLabel = new Label
                {
                    style =
                    {
                        whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal,
                        paddingTop = 8f,
                        unityFontStyleAndWeight = FontStyle.Bold,
                    },
                };
                _runtimeBlackboardLabel = new Label
                {
                    style =
                    {
                        whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal,
                        paddingTop = 6f,
                    },
                };
                _root.Add(_runtimeNodeStateLabel);
                _root.Add(_runtimeBlackboardLabel);
                RefreshRuntimeDetails();
            }
            else
            {
                var nameField = new TextField("显示名") { value = displayName, isDelayed = true };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    _host.RecordChange();
                    nodeMetadata.DisplayName = evt.newValue ?? "";
                    _host.RefreshNodeTitles();
                });
                _root.Add(nameField);

                var commentField = new TextField("备注")
                {
                    value = nodeMetadata.Comment,
                    multiline = true,
                    isDelayed = true,
                };
                commentField.RegisterValueChangedCallback(evt =>
                {
                    _host.RecordChange();
                    nodeMetadata.Comment = evt.newValue ?? "";
                });
                _root.Add(commentField);

                if (!string.Equals(_host.Document.Tree.RootNodeId, node.Id, StringComparison.Ordinal))
                {
                    _root.Add(new Button(() =>
                    {
                        _host.RecordChange();
                        _host.Document.Tree.RootNodeId = node.Id;
                        _host.RebuildGraph();
                    }) { text = "设为根节点" });
                }
            }

            _root.Add(new Label("Node ID: " + node.Id));
            _root.Add(new Label("Type: " + node.Type) { tooltip = node.Type });

            if (!EditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out var descriptor))
            {
                DrawExtensionSections(node);
                return;
            }

            _root.Add(new Label("属性") { style = { paddingTop = 8f, unityFontStyleAndWeight = FontStyle.Bold } });
            foreach (var field in descriptor.PropertySchema.OrderBy(f => f.Order))
            {
                var fieldRow = new VisualElement
                {
                    tooltip = field.Tooltip,
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center },
                };
                fieldRow.Add(new Label(field.Name) { style = { width = 140 } });

                var current = node.Properties.TryGet(field.Name, out var existing)
                    ? existing
                    : (field.Default ?? DefaultOf(field.Type));
                var customBinding = EditorExtensionRegistry.ResolvePropertyFieldEditor(
                    descriptor.TypeId,
                    field.Name);
                if (customBinding != null
                    && TryCreateCustomFieldEditor(customBinding, descriptor, field, current, node, out var customEditor))
                {
                    customEditor.style.flexGrow = 1f;
                    fieldRow.Add(customEditor);
                    _root.Add(fieldRow);
                    continue;
                }

                if (_host.IsReadOnly)
                {
                    // 观察模式只读：仅展示当前值
                    fieldRow.Add(new Label(FormatFieldValue(field, current)));
                    _root.Add(fieldRow);
                    continue;
                }

                if (field.Kind == PropertyFieldKind.Enum)
                {
                    var options = field.Options.Count > 0 ? field.Options : new[] { "<空>" };
                    var index = (int)Math.Clamp(current.Int64Value, 0, options.Count - 1);
                    var popup = new PopupField<string>(new List<string>(options), index);
                    popup.style.flexGrow = 1f;
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        _host.RecordChange();
                        node.Properties.Set(field.Name, PropertyValue.Of((long)popup.index));
                    });
                    fieldRow.Add(popup);
                }
                else if (field.Kind == PropertyFieldKind.BlackboardKeyRef)
                {
                    var choices = new List<string>();
                    foreach (var key in _host.Document.Tree.Blackboard.Keys)
                    {
                        if (!choices.Contains(key.Name)) choices.Add(key.Name);
                    }
                    if (!choices.Contains(current.StringValue)) choices.Add(current.StringValue);
                    var popup = new PopupField<string>(choices, current.StringValue);
                    popup.style.flexGrow = 1f;
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        _host.RecordChange();
                        node.Properties.Set(field.Name, PropertyValue.Of(evt.newValue));
                    });
                    fieldRow.Add(popup);
                }
                else
                {
                    switch (field.Type)
                    {
                        case ValueType.Bool:
                            var toggle = new Toggle { value = current.BoolValue };
                            toggle.RegisterValueChangedCallback(evt =>
                            {
                                _host.RecordChange();
                                node.Properties.Set(field.Name, PropertyValue.Of(evt.newValue));
                            });
                            fieldRow.Add(toggle);
                            break;

                        case ValueType.Int64:
                            var intField = new LongField { value = current.Int64Value };
                            intField.style.flexGrow = 1f;
                            intField.RegisterValueChangedCallback(evt =>
                            {
                                _host.RecordChange();
                                var value = evt.newValue;
                                if (field.Min.HasValue) value = Math.Max(value, field.Min.Value);
                                if (field.Max.HasValue) value = Math.Min(value, field.Max.Value);
                                intField.SetValueWithoutNotify(value);
                                node.Properties.Set(field.Name, PropertyValue.Of(value));
                            });
                            fieldRow.Add(intField);
                            break;

                        case ValueType.Fixed64:
                            var fixedField = new FloatField
                            {
                                value = AbilityKit.Deterministic.Fixed64.FromRaw(current.Fixed64Raw).ToSingle(),
                            };
                            fixedField.style.flexGrow = 1f;
                            fixedField.RegisterValueChangedCallback(evt =>
                            {
                                _host.RecordChange();
                                var fixedValue = AbilityKit.Deterministic.Fixed64.FromSingle(evt.newValue);
                                var raw = fixedValue.RawValue;
                                if (field.Min.HasValue) raw = Math.Max(raw, field.Min.Value);
                                if (field.Max.HasValue) raw = Math.Min(raw, field.Max.Value);
                                fixedValue = AbilityKit.Deterministic.Fixed64.FromRaw(raw);
                                fixedField.SetValueWithoutNotify(fixedValue.ToSingle());
                                node.Properties.Set(field.Name, PropertyValue.Of(fixedValue));
                            });
                            fieldRow.Add(fixedField);
                            break;

                        case ValueType.String:
                            var textField = new TextField { value = current.StringValue };
                            textField.style.flexGrow = 1f;
                            textField.RegisterValueChangedCallback(evt =>
                            {
                                _host.RecordChange();
                                node.Properties.Set(field.Name, PropertyValue.Of(evt.newValue));
                            });
                            fieldRow.Add(textField);
                            break;
                    }
                }

                if (field.Min.HasValue || field.Max.HasValue)
                {
                    fieldRow.Add(new Label($"[{field.Min?.ToString() ?? "-∞"}, {field.Max?.ToString() ?? "+∞"}]")
                        { style = { opacity = 0.5f } });
                }

                _root.Add(fieldRow);
            }

            DrawChildOrder(node);
            DrawExtensionSections(node);

            // 子树引用节点：跨树跳转——打开被引用树的授权资产
            if (!_host.IsReadOnly && node.Type == BuiltInNodeTypes.Subtree
                && node.Properties.TryGet(SubtreeNode.TreeIdProperty, out var treeIdValue)
                && treeIdValue.TryGetString(out var referencedTreeId)
                && !string.IsNullOrEmpty(referencedTreeId))
            {
                _root.Add(new Button(() => OpenReferencedTree(referencedTreeId))
                {
                    text = "打开引用树：" + referencedTreeId,
                });
            }
        }

        private bool TryCreateCustomFieldEditor(
            PropertyFieldEditorBinding binding,
            NodeDescriptor descriptor,
            PropertyField field,
            PropertyValue current,
            NodeDefinition node,
            out VisualElement editor)
        {
            try
            {
                editor = binding.CreateEditor(new PropertyFieldEditorContext(
                    descriptor,
                    field,
                    current,
                    _host.IsReadOnly,
                    value =>
                    {
                        if (_host.IsReadOnly || value == null) return;
                        _host.RecordChange();
                        node.Properties.Set(field.Name, value);
                        _host.RefreshChrome();
                    }));
                return editor != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[BtEditor] 自定义字段编辑器 '{descriptor.TypeId}.{field.Name}' 构建失败并已回退: {exception.Message}");
                editor = null!;
                return false;
            }
        }

        private void DrawExtensionSections(NodeDefinition node)
        {
            var sections = EditorExtensionRegistry
                .EnumerateInspectorSections(new InspectorSectionContext(
                    _host.Document,
                    node,
                    _host.IsReadOnly))
                .Where(section => section != null)
                .OrderBy(section => section.Order)
                .ThenBy(section => section.Title, StringComparer.Ordinal)
                .ToArray();

            foreach (var section in sections)
            {
                try
                {
                    var content = section.Build();
                    if (content == null) continue;
                    _root.Add(new Label(section.Title)
                    {
                        style = { paddingTop = 10f, unityFontStyleAndWeight = FontStyle.Bold },
                    });
                    _root.Add(content);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[BtEditor] Inspector 扩展区块 '{section.Title}' 构建失败并已隔离: {exception.Message}");
                }
            }
        }

        public void RefreshRuntimeDetails()
        {
            if (!_host.IsReadOnly) return;
            if (_selectedNode == null)
            {
                RefreshRuntimeBlackboardPanel();
                return;
            }
            if (_runtimeNodeStateLabel == null) return;
            var snapshot = _host.DisplayedObservationSnapshot;
            if (snapshot == null || !snapshot.TryGetNode(_selectedNode.Id, out var state))
            {
                _runtimeNodeStateLabel.text = "运行状态：无数据";
                if (_runtimeBlackboardLabel != null) _runtimeBlackboardLabel.text = "";
                return;
            }

            var detail = "运行状态：" + state.State
                + "\n执行路径：" + (state.OnStackCount > 0 ? "是" : "否")
                + "  ·  深度 " + state.Depth;
            if (state.RunningChildIndex >= 0)
                detail += "\n当前子节点：" + (state.RunningChildIndex + 1);
            if (!string.IsNullOrEmpty(state.SourceTreeId))
                detail += "\n来源树：" + state.SourceTreeId;
            _runtimeNodeStateLabel.text = detail;

            if (_runtimeBlackboardLabel == null
                || !EditorNodeCatalog.Registry.TryGetDescriptor(_selectedNode.Type, out var descriptor)) return;
            var blackboardView = ObservationBlackboardView.Create(
                snapshot,
                _host.PreviousObservationSnapshot,
                _host.DisplayedObservationDiff);
            var values = new List<string>();
            foreach (var field in descriptor.PropertySchema)
            {
                if (field.Kind != PropertyFieldKind.BlackboardKeyRef
                    || !_selectedNode.Properties.TryGet(field.Name, out var property)
                    || !property.TryGetString(out var keyName)
                    || string.IsNullOrEmpty(keyName)) continue;
                values.Add(field.Name + " -> " + keyName + " = " + FormatBlackboardValue(blackboardView, keyName));
            }
            _runtimeBlackboardLabel.text = values.Count == 0
                ? ""
                : "关联黑板\n" + string.Join("\n", values);
        }

        private static string FormatBlackboardValue(ObservationBlackboard? snapshot, string keyName)
        {
            if (snapshot == null) return "<无快照>";
            var index = snapshot.IndexOf(keyName);
            return index < 0 ? "<未声明>" : snapshot.GetDisplayValue(index);
        }

        private static string FormatBlackboardValue(ObservationBlackboardView view, string keyName)
        {
            if (view == null || view.Count == 0) return "<no snapshot>";
            if (!view.TryGetRow(keyName, out var row)) return "<missing>";
            if (row.IsRemoved) return "<removed; previous " + row.PreviousValue + ">";
            return row.HasPreviousValue && row.IsChanged
                ? row.CurrentValue + " (prev " + row.PreviousValue + ")"
                : row.CurrentValue;
        }

        private void DrawRuntimeBlackboardPanel()
        {
            _root.Add(new Label("Blackboard") { style = { paddingTop = 8f, unityFontStyleAndWeight = FontStyle.Bold } });
            _runtimeBlackboardSearchField = new TextField
            {
                value = _runtimeBlackboardSearch,
                isDelayed = false,
            };
            _runtimeBlackboardSearchField.RegisterValueChangedCallback(evt =>
            {
                _runtimeBlackboardSearch = evt.newValue ?? "";
                RefreshRuntimeBlackboardPanel();
            });
            _root.Add(_runtimeBlackboardSearchField);

            _runtimeBlackboardChangedOnlyToggle = new Toggle("Changed only")
            {
                value = _runtimeBlackboardChangedOnly,
            };
            _runtimeBlackboardChangedOnlyToggle.RegisterValueChangedCallback(evt =>
            {
                _runtimeBlackboardChangedOnly = evt.newValue;
                RefreshRuntimeBlackboardPanel();
            });
            _root.Add(_runtimeBlackboardChangedOnlyToggle);

            _runtimeTreeBlackboardContainer = new VisualElement();
            _root.Add(_runtimeTreeBlackboardContainer);
            RefreshRuntimeBlackboardPanel();
        }

        private void RefreshRuntimeBlackboardPanel()
        {
            if (_runtimeTreeBlackboardContainer == null) return;
            _runtimeTreeBlackboardContainer.Clear();
            var view = ObservationBlackboardView.Create(
                _host.DisplayedObservationSnapshot,
                _host.PreviousObservationSnapshot,
                _host.DisplayedObservationDiff);
            if (view.Count == 0)
            {
                _runtimeTreeBlackboardContainer.Add(new Label("(empty)") { style = { opacity = 0.65f } });
                return;
            }

            foreach (var row in view.Search(_runtimeBlackboardSearch, _runtimeBlackboardChangedOnly))
            {
                var label = new Label(FormatBlackboardRow(row))
                {
                    tooltip = row.Key,
                    style =
                    {
                        whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal,
                        paddingTop = 2f,
                        paddingBottom = 2f,
                        unityFontStyleAndWeight = row.IsChanged ? FontStyle.Bold : FontStyle.Normal,
                    },
                };
                if (row.IsChanged) label.style.backgroundColor = new Color(1f, 0.85f, 0.45f, 0.18f);
                _runtimeTreeBlackboardContainer.Add(label);
            }
        }

        private static string FormatBlackboardRow(ObservationBlackboardRow row)
        {
            var current = row.HasCurrentValue ? row.CurrentValue : "<removed>";
            if (!row.HasPreviousValue) return row.Key + " [" + row.Type + "] = " + current;
            if (!row.IsChanged) return row.Key + " [" + row.Type + "] = " + current;
            return row.Key + " [" + row.Type + "] = " + current + " (prev " + row.PreviousValue + ")";
        }

        private void DrawChildOrder(NodeDefinition parent)
        {
            if (parent.ChildIds.Count == 0) return;
            _root.Add(new Label("子节点执行顺序")
                { style = { paddingTop = 10f, unityFontStyleAndWeight = FontStyle.Bold } });

            for (var i = 0; i < parent.ChildIds.Count; i++)
            {
                var index = i;
                var childId = parent.ChildIds[index];
                var child = _host.Document.Tree.Nodes.Find(n => string.Equals(n.Id, childId, StringComparison.Ordinal));
                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, height = 26f },
                };
                row.Add(new Label((index + 1).ToString()) { style = { width = 24f, unityTextAlign = TextAnchor.MiddleCenter } });
                var focus = new Button(() => _host.FocusNode(childId))
                {
                    text = child == null ? childId + "（缺失）" : _host.ResolveNodeDisplayName(child),
                    tooltip = childId,
                };
                focus.style.flexGrow = 1f;
                row.Add(focus);

                if (!_host.IsReadOnly)
                {
                    var up = new Button(() => MoveChild(parent, index, index - 1)) { text = "▲", tooltip = "提高执行优先级" };
                    var down = new Button(() => MoveChild(parent, index, index + 1)) { text = "▼", tooltip = "降低执行优先级" };
                    up.style.width = 28f;
                    down.style.width = 28f;
                    up.SetEnabled(index > 0);
                    down.SetEnabled(index < parent.ChildIds.Count - 1);
                    row.Add(up);
                    row.Add(down);
                }
                _root.Add(row);
            }
        }

        private void MoveChild(NodeDefinition parent, int fromIndex, int toIndex)
        {
            if (_host.IsReadOnly) return;
            if (toIndex < 0 || toIndex >= parent.ChildIds.Count) return;
            _host.RecordChange();
            if (!GraphOperations.MoveChild(_host.Document.Tree, parent.Id, fromIndex, toIndex)) return;
            _host.RefreshNodeTitles();
            Render(_selectedNode);
        }

        /// <summary>跨树跳转：按 TreeId 找到授权资产并打开其图编辑器。</summary>
        private static void OpenReferencedTree(string treeId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AuthoringAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && string.Equals(asset.LoadDocument().Tree.TreeId, treeId, System.StringComparison.Ordinal))
                {
                    AuthoringGraphWindow.Open(asset);
                    return;
                }
            }
            Debug.LogWarning($"[BtAuthoring] 未找到 TreeId='{treeId}' 的授权资产。");
        }

        private static string FormatFieldValue(PropertyField field, PropertyValue value)
        {
            if (field.Kind == PropertyFieldKind.Enum)
            {
                var index = (int)value.Int64Value;
                return index >= 0 && index < field.Options.Count ? field.Options[index] : index.ToString();
            }
            return value?.ToString() ?? "";
        }

        /// <summary>未选中节点时的树级面板：TreeId / 描述 / 黑板 schema 编辑。</summary>
        private void DrawTreePanel()
        {
            _root.Add(new Label("Tree"));
            _root.Add(new Label("（选中图上节点编辑其属性）") { style = { opacity = 0.6f } });

            if (_host.IsReadOnly)
            {
                _root.Add(new Label("TreeId: " + _host.Document.Tree.TreeId));
                DrawRuntimeBlackboardPanel();
                return;
            }

            var treeIdField = new TextField("TreeId（=导出文件名）") { value = _host.Document.Tree.TreeId };
            treeIdField.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                _host.Document.Tree.TreeId = evt.newValue;
                _host.RefreshChrome();
            });
            _root.Add(treeIdField);

            var descriptionField = new TextField("描述") { value = _host.Document.Metadata.Description };
            descriptionField.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                _host.Document.Metadata.Description = evt.newValue;
            });
            _root.Add(descriptionField);

            _root.Add(new Label($"节点 {_host.Document.Tree.Nodes.Count} · 黑板 {_host.Document.Tree.Blackboard.Keys.Count} · 分组 {_host.Document.Groups.Count}")
                { style = { opacity = 0.65f, paddingTop = 6f } });
            _root.Add(new Label("Blackboard Schema") { style = { paddingTop = 8 } });
            _root.Add(new Label("重命名会同步所有描述符声明的黑板 key 引用。")
                { style = { opacity = 0.6f, whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal } });

            for (var i = 0; i < _host.Document.Tree.Blackboard.Keys.Count; i++)
            {
                var index = i;
                var oldName = _host.Document.Tree.Blackboard.Keys[index].Name;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var nameField = new TextField { value = oldName, isDelayed = true };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    var newName = evt.newValue;
                    if (string.Equals(oldName, newName, System.StringComparison.Ordinal)) return;
                    var beforeRename = AuthoringJson.Save(_host.Document);
                    try
                    {
                        var affected = AuthoringMutationService.FindBlackboardUsages(
                            _host.Document,
                            EditorNodeCatalog.Registry,
                            oldName);
                        KeyReferenceIndex.RenameKey(
                            _host.Document.Tree, EditorNodeCatalog.Registry, oldName, newName);
                        _host.RecordChange(beforeRename);
                        Debug.Log($"[BtAuthoring] 重命名黑板 key '{oldName}' -> '{newName}'，同步 {affected.Count} 处引用。");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[BtAuthoring] 黑板 key 重命名失败: " + ex.Message);
                    }
                    Render(_selectedNode);
                });
                row.Add(nameField);

                var typeField = new EnumField(_host.Document.Tree.Blackboard.Keys[index].Type);
                typeField.RegisterValueChangedCallback(evt =>
                {
                    var nextType = (ValueType)evt.newValue;
                    var beforeTypeChange = AuthoringJson.Save(_host.Document);
                    var impact = AuthoringMutationService.AnalyzeBlackboardTypeChange(
                        _host.Document,
                        EditorNodeCatalog.Registry,
                        oldName,
                        nextType);
                    _host.Document.Tree.Blackboard.Keys[index].Type = nextType;
                    if (_host.Document.Tree.Blackboard.Keys[index].Default != null
                        && _host.Document.Tree.Blackboard.Keys[index].Default.Type != nextType)
                    {
                        _host.Document.Tree.Blackboard.Keys[index].Default = null;
                    }
                    _host.RecordChange(beforeTypeChange);
                    if (impact.HasImpact)
                    {
                        Debug.LogWarning(
                            $"[BtAuthoring] Blackboard key '{oldName}' type changed {impact.FromType} -> {impact.ToType}; {impact.Usages.Count} references need validation.");
                    }
                });
                row.Add(typeField);

                var refCount = KeyReferenceIndex.FindReferences(
                    _host.Document.Tree, EditorNodeCatalog.Registry, oldName).Count;
                if (refCount > 0)
                {
                    row.Add(new Label(refCount + " 引用") { style = { opacity = 0.6f } });
                }

                var removeButton = new Button(() =>
                {
                    var references = KeyReferenceIndex.FindReferences(
                        _host.Document.Tree, EditorNodeCatalog.Registry, oldName);
                    var detail = references.Count == 0
                        ? $"确定删除黑板 Key '{oldName}'？"
                        : $"黑板 Key '{oldName}' 正被 {references.Count} 处节点属性引用。\n\n" +
                          "删除后这些引用会被清空，相关节点需要重新选择 Key。";
                    if (!EditorUtility.DisplayDialog("删除 Blackboard Key", detail, "删除", "取消")) return;

                    _host.RecordChange();
                    AuthoringMutationService.ClearBlackboardReferences(
                        _host.Document,
                        EditorNodeCatalog.Registry,
                        oldName);
                    _host.Document.Tree.Blackboard.Keys.RemoveAt(index);
                    Render(_selectedNode);
                }) { text = "-" };
                row.Add(removeButton);
                _root.Add(row);
            }

            _root.Add(new Button(() =>
            {
                _host.RecordChange();
                _host.Document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                {
                    Name = "key" + _host.Document.Tree.Blackboard.Keys.Count,
                    Type = ValueType.Int64,
                });
                Render(_selectedNode);
            }) { text = "+ 添加 Key" });

            _root.Add(new Label("Groups") { style = { paddingTop = 8 } });
            for (var i = 0; i < _host.Document.Groups.Count; i++)
            {
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var titleField = new TextField { value = _host.Document.Groups[index].Title, isDelayed = true };
                titleField.RegisterValueChangedCallback(evt =>
                {
                    _host.RecordChange();
                    _host.Document.Groups[index].Title = evt.newValue;
                    _host.RebuildGraph();
                });
                row.Add(titleField);
                row.Add(new Label(_host.Document.Groups[index].NodeIds.Count + " 节点")
                    { style = { opacity = 0.5f } });
                row.Add(new Button(() =>
                {
                    _host.RecordChange();
                    _host.Document.Groups.RemoveAt(index);
                    _host.RebuildGraph();
                }) { text = "-" });
                _root.Add(row);
            }
        }

        private static PropertyValue DefaultOf(ValueType type) => type switch
        {
            ValueType.Bool => PropertyValue.Of(false),
            ValueType.Int64 => PropertyValue.Of(0L),
            ValueType.Fixed64 => PropertyValue.Of(AbilityKit.Deterministic.Fixed64.Zero),
            ValueType.String => PropertyValue.Of(""),
            _ => PropertyValue.Of(0L),
        };
    }
}
