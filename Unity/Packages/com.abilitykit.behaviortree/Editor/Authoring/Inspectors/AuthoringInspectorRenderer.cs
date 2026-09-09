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
    internal interface IAuthoringInspectorHost : IAuthoringWorkspaceHost
    {
        ObservationSnapshot? DisplayedObservationSnapshot { get; }
        ObservationSnapshot? PreviousObservationSnapshot { get; }
        ObservationDiff? DisplayedObservationDiff { get; }
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
            AddInspectorHeader(
                displayName,
                nodeDescriptor == null
                    ? node.Type
                    : NodeKindLabel(nodeDescriptor.Kind) + "  ·  " + nodeDescriptor.Category);

            if (_host.IsReadOnly)
            {
                if (!string.IsNullOrWhiteSpace(nodeMetadata.Comment))
                {
                    _root.Add(new Label(nodeMetadata.Comment)
                    {
                        style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6f },
                    });
                }
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
                commentField.style.minHeight = 58f;
                commentField.RegisterValueChangedCallback(evt =>
                {
                    _host.RecordChange();
                    nodeMetadata.Comment = evt.newValue ?? "";
                });
                _root.Add(commentField);

                if (!string.Equals(_host.Document.Tree.RootNodeId, node.Id, StringComparison.Ordinal))
                {
                    var setRoot = new Button(() =>
                    {
                        _host.RecordChange();
                        _host.Document.Tree.RootNodeId = node.Id;
                        _host.RebuildGraph();
                    }) { text = "设为根节点" };
                    setRoot.style.height = 24f;
                    setRoot.style.marginTop = 4f;
                    _root.Add(setRoot);
                }
            }

            _root.Add(new Label(node.Id + "  ·  " + node.Type)
            {
                tooltip = node.Type,
                style = { opacity = 0.6f, marginTop = 5f, whiteSpace = WhiteSpace.Normal },
            });

            if (!EditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out var descriptor))
            {
                DrawExtensionSections(node);
                return;
            }

            AddSectionLabel("属性");
            foreach (var field in descriptor.PropertySchema.OrderBy(f => f.Order))
            {
                var fieldRow = new VisualElement
                {
                    tooltip = field.Tooltip,
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        minHeight = 25f,
                        marginBottom = 2f,
                    },
                };
                fieldRow.Add(new Label(EditorDisplayText.PropertyName(field.Name))
                {
                    style =
                    {
                        width = 112f,
                        minWidth = 88f,
                        whiteSpace = WhiteSpace.Normal,
                    },
                });

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
                        _host.RefreshNodeTitles();
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
                        _host.RefreshNodeTitles();
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
                                _host.RefreshNodeTitles();
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
                                _host.RefreshNodeTitles();
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
                                _host.RefreshNodeTitles();
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
                                _host.RefreshNodeTitles();
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

            if (node.Type == BuiltInNodeTypes.Subtree)
                DrawSubtreeBlackboard(node);

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
                        _host.RefreshNodeTitles();
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

            var detail = "运行状态：" + EditorDisplayText.NodeState(state.State)
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
                values.Add(EditorDisplayText.PropertyName(field.Name) + " -> " + keyName + " = " + FormatBlackboardValue(blackboardView, keyName));
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
            if (view == null || view.Count == 0) return "<无快照>";
            if (!view.TryGetRow(keyName, out var row)) return "<未找到>";
            if (row.IsRemoved) return "<已移除；前值 " + row.PreviousValue + ">";
            return row.HasPreviousValue && row.IsChanged
                ? row.CurrentValue + "（前值 " + row.PreviousValue + "）"
                : row.CurrentValue;
        }

        private void DrawRuntimeBlackboardPanel()
        {
            _root.Add(new Label("黑板") { style = { paddingTop = 8f, unityFontStyleAndWeight = FontStyle.Bold } });
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

            _runtimeBlackboardChangedOnlyToggle = new Toggle("仅显示变化")
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
                _runtimeTreeBlackboardContainer.Add(new Label("（空）") { style = { opacity = 0.65f } });
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
            var current = row.HasCurrentValue ? row.CurrentValue : "<已移除>";
            if (!row.HasPreviousValue) return row.Key + " [" + row.Type + "] = " + current;
            if (!row.IsChanged) return row.Key + " [" + row.Type + "] = " + current;
            return row.Key + " [" + row.Type + "] = " + current + "（前值 " + row.PreviousValue + "）";
        }

        private void DrawSubtreeBlackboard(NodeDefinition node)
        {
            AddSectionLabel("子树黑板");
            var configuration = node.SubtreeBlackboard;

            if (_host.IsReadOnly)
            {
                _root.Add(new Label(configuration?.IsolateUnmappedKeys == true
                    ? "未映射键：实例隔离"
                    : "未映射键：与父树共享"));
                if (configuration != null)
                {
                    foreach (var binding in configuration.Bindings)
                        _root.Add(new Label(binding.SubtreeKey + "  ->  " + binding.ParentKey));
                }
                return;
            }

            var isolate = new Toggle("隔离未映射键")
            {
                value = configuration?.IsolateUnmappedKeys == true,
                tooltip = "每个子树实例为未绑定键使用独立命名空间",
            };
            isolate.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                EnsureSubtreeBlackboard(node).IsolateUnmappedKeys = evt.newValue;
                _host.RefreshNodeTitles();
            });
            _root.Add(isolate);

            var childKeys = ResolveReferencedBlackboardKeys(node);
            var parentKeys = _host.Document.Tree.Blackboard.Keys.Select(key => key.Name).ToList();
            if (configuration != null)
            {
                for (var i = 0; i < configuration.Bindings.Count; i++)
                    AddSubtreeBindingRow(node, configuration.Bindings[i], i, childKeys, parentKeys);
            }

            var add = new Button(() =>
            {
                _host.RecordChange();
                var target = EnsureSubtreeBlackboard(node);
                target.Bindings.Add(new SubtreeBlackboardBinding
                {
                    SubtreeKey = FirstUnusedKey(childKeys, target.Bindings.Select(binding => binding.SubtreeKey)),
                    ParentKey = parentKeys.Count > 0 ? parentKeys[0] : "",
                });
                Render(node);
                _host.RefreshNodeTitles();
            })
            {
                text = "+",
                tooltip = "添加黑板输入/输出绑定",
            };
            add.style.width = 30f;
            add.style.marginTop = 4f;
            _root.Add(add);
        }

        private void AddSubtreeBindingRow(
            NodeDefinition node,
            SubtreeBlackboardBinding binding,
            int index,
            List<string> childKeys,
            List<string> parentKeys)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 26f,
                    marginTop = 2f,
                },
            };

            var child = new PopupField<string>(WithCurrentValue(childKeys, binding.SubtreeKey), binding.SubtreeKey)
            {
                tooltip = "子树键",
            };
            child.style.flexGrow = 1f;
            child.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                binding.SubtreeKey = evt.newValue ?? "";
                _host.RefreshNodeTitles();
            });
            row.Add(child);

            row.Add(new Label("->")
            {
                style = { width = 28f, unityTextAlign = TextAnchor.MiddleCenter },
            });

            var parent = new PopupField<string>(WithCurrentValue(parentKeys, binding.ParentKey), binding.ParentKey)
            {
                tooltip = "父树键",
            };
            parent.style.flexGrow = 1f;
            parent.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                binding.ParentKey = evt.newValue ?? "";
                _host.RefreshNodeTitles();
            });
            row.Add(parent);

            var remove = new Button(() =>
            {
                _host.RecordChange();
                var current = node.SubtreeBlackboard;
                if (current != null && index >= 0 && index < current.Bindings.Count)
                    current.Bindings.RemoveAt(index);
                Render(node);
                _host.RefreshNodeTitles();
            })
            {
                text = "-",
                tooltip = "删除绑定",
            };
            remove.style.width = 28f;
            row.Add(remove);
            _root.Add(row);
        }

        private static SubtreeBlackboardConfiguration EnsureSubtreeBlackboard(NodeDefinition node)
            => node.SubtreeBlackboard ??= new SubtreeBlackboardConfiguration();

        private static List<string> WithCurrentValue(IEnumerable<string> source, string current)
        {
            var result = source.Where(value => !string.IsNullOrEmpty(value)).Distinct().ToList();
            if (!string.IsNullOrEmpty(current) && !result.Contains(current)) result.Add(current);
            if (result.Count == 0) result.Add(current ?? "");
            return result;
        }

        private static string FirstUnusedKey(IEnumerable<string> choices, IEnumerable<string> used)
        {
            var usedSet = new HashSet<string>(used, StringComparer.Ordinal);
            return choices.FirstOrDefault(key => !usedSet.Contains(key)) ?? "";
        }

        private static List<string> ResolveReferencedBlackboardKeys(NodeDefinition node)
        {
            if (!node.Properties.TryGet(SubtreeNode.TreeIdProperty, out var value)
                || !value.TryGetString(out var treeId)
                || string.IsNullOrEmpty(treeId))
                return new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:AuthoringAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var tree = asset.LoadDocument().Tree;
                if (!string.Equals(tree.TreeId, treeId, StringComparison.Ordinal)) continue;
                return tree.Blackboard.Keys.Select(key => key.Name).ToList();
            }
            return new List<string>();
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
            AddInspectorHeader(
                string.IsNullOrWhiteSpace(_host.Document.Tree.TreeId)
                    ? "行为树"
                    : _host.Document.Tree.TreeId,
                $"{_host.Document.Tree.Nodes.Count} 个节点  ·  " +
                $"{_host.Document.Tree.Blackboard.Keys.Count} 个黑板键  ·  " +
                $"{_host.Document.Groups.Count} 个分组");

            if (_host.IsReadOnly)
            {
                DrawRuntimeBlackboardPanel();
                return;
            }

            var treeIdField = new TextField("Tree ID")
            {
                value = _host.Document.Tree.TreeId,
                isDelayed = true,
                tooltip = "运行时 JSON 文件名",
            };
            treeIdField.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                _host.Document.Tree.TreeId = evt.newValue;
                _host.RefreshChrome();
            });
            _root.Add(treeIdField);

            var descriptionField = new TextField("描述")
            {
                value = _host.Document.Metadata.Description,
                multiline = true,
                isDelayed = true,
            };
            descriptionField.style.minHeight = 58f;
            descriptionField.RegisterValueChangedCallback(evt =>
            {
                _host.RecordChange();
                _host.Document.Metadata.Description = evt.newValue;
            });
            _root.Add(descriptionField);

            AddSectionLabel("黑板");

            for (var i = 0; i < _host.Document.Tree.Blackboard.Keys.Count; i++)
            {
                var index = i;
                var oldName = _host.Document.Tree.Blackboard.Keys[index].Name;
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        minHeight = 26f,
                        marginBottom = 3f,
                    },
                };

                var nameField = new TextField { value = oldName, isDelayed = true };
                nameField.style.flexGrow = 1f;
                nameField.style.minWidth = 90f;
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
                    _host.RefreshNodeTitles();
                    Render(_selectedNode);
                });
                row.Add(nameField);

                var typeField = new EnumField(_host.Document.Tree.Blackboard.Keys[index].Type);
                typeField.style.width = 82f;
                typeField.style.marginLeft = 4f;
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
                            $"[BtAuthoring] 黑板键 '{oldName}' 的类型已从 {impact.FromType} 改为 {impact.ToType}；需要重新校验 {impact.Usages.Count} 处引用。");
                    }
                    _host.RefreshNodeTitles();
                });
                row.Add(typeField);

                var refCount = KeyReferenceIndex.FindReferences(
                    _host.Document.Tree, EditorNodeCatalog.Registry, oldName).Count;
                if (refCount > 0)
                {
                    row.Add(new Label(refCount.ToString())
                    {
                        tooltip = refCount + " 处引用",
                        style = { opacity = 0.6f, width = 24f, unityTextAlign = TextAnchor.MiddleCenter },
                    });
                }

                var removeButton = new Button(() =>
                {
                    var references = KeyReferenceIndex.FindReferences(
                        _host.Document.Tree, EditorNodeCatalog.Registry, oldName);
                    var detail = references.Count == 0
                        ? $"确定删除黑板 Key '{oldName}'？"
                        : $"黑板 Key '{oldName}' 正被 {references.Count} 处节点属性引用。\n\n" +
                          "删除后这些引用会被清空，相关节点需要重新选择 Key。";
                    if (!EditorUtility.DisplayDialog("删除黑板键", detail, "删除", "取消")) return;

                    _host.RecordChange();
                    AuthoringMutationService.ClearBlackboardReferences(
                        _host.Document,
                        EditorNodeCatalog.Registry,
                        oldName);
                    _host.Document.Tree.Blackboard.Keys.RemoveAt(index);
                    _host.RefreshNodeTitles();
                    Render(_selectedNode);
                }) { text = "-", tooltip = "删除黑板键" };
                removeButton.style.width = 26f;
                removeButton.style.marginLeft = 4f;
                row.Add(removeButton);
                _root.Add(row);
            }

            var addKey = new Button(() =>
            {
                _host.RecordChange();
                _host.Document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                {
                    Name = "key" + _host.Document.Tree.Blackboard.Keys.Count,
                    Type = ValueType.Int64,
                });
                Render(_selectedNode);
            }) { text = "添加黑板键" };
            addKey.style.height = 24f;
            addKey.style.marginTop = 3f;
            _root.Add(addKey);

            AddSectionLabel("分组");
            for (var i = 0; i < _host.Document.Groups.Count; i++)
            {
                var index = i;
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        minHeight = 26f,
                        marginBottom = 3f,
                    },
                };
                var titleField = new TextField { value = _host.Document.Groups[index].Title, isDelayed = true };
                titleField.style.flexGrow = 1f;
                titleField.style.minWidth = 90f;
                titleField.RegisterValueChangedCallback(evt =>
                {
                    _host.RecordChange();
                    _host.Document.Groups[index].Title = evt.newValue;
                    _host.RebuildGraph();
                });
                row.Add(titleField);
                row.Add(new Label(_host.Document.Groups[index].NodeIds.Count + " 节点")
                    { style = { opacity = 0.5f, width = 52f, unityTextAlign = TextAnchor.MiddleCenter } });
                var removeGroup = new Button(() =>
                {
                    _host.RecordChange();
                    _host.Document.Groups.RemoveAt(index);
                    _host.RebuildGraph();
                }) { text = "-", tooltip = "删除分组" };
                removeGroup.style.width = 26f;
                row.Add(removeGroup);
                _root.Add(row);
            }
        }

        private void AddInspectorHeader(string title, string subtitle)
        {
            _root.Add(new Label(title)
            {
                tooltip = title,
                style =
                {
                    fontSize = 16f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 2f,
                },
            });
            _root.Add(new Label(subtitle)
            {
                tooltip = subtitle,
                style =
                {
                    opacity = 0.62f,
                    fontSize = 10f,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 8f,
                },
            });
        }

        private void AddSectionLabel(string title)
        {
            _root.Add(new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 12f,
                    marginBottom = 5f,
                    paddingBottom = 3f,
                    borderBottomWidth = 1f,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f),
                },
            });
        }

        private static string NodeKindLabel(NodeKind kind)
        {
            return kind switch
            {
                NodeKind.Composite => "组合",
                NodeKind.Decorator => "装饰",
                NodeKind.Condition => "条件",
                NodeKind.Action => "动作",
                _ => "节点",
            };
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
