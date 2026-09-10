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
        private bool _showDefinition = true;
        private bool _showBlackboard = true;
        private bool _showCondition = true;
        private bool _showActions = true;
        private bool _showDiagnostics = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            _asset = target as TriggerAuthoringTemplateAsset;
            if (_asset != null && TriggerAuthoringTemplateDefinition.Normalize(_asset.Template))
                EditorUtility.SetDirty(_asset);
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
            DrawTriggerDefinition();
            var definition = TriggerAuthoringTemplateDefinition.Get(_asset.Template);
            definition.Condition = DrawTemplateTree(
                "触发条件",
                TriggerNodeKind.Condition,
                definition.Condition,
                ref _showCondition);
            definition.Actions = DrawTemplateTree(
                "执行行为",
                TriggerNodeKind.Action,
                definition.Actions,
                ref _showActions);
            DrawValidation();
            EditorGUILayout.EndScrollView();
            if (!EditorGUI.EndChangeCheck()) return;

            TriggerAuthoringTemplateDefinition.Normalize(_asset.Template);
            EditorUtility.SetDirty(_asset);
            RebuildCatalogs();
            _nextInspectionAt = 0d;
        }

        private void DrawToolbar()
        {
            RefreshInspection();
            SirenixEditorGUI.BeginHorizontalToolbar();
            GUILayout.Label(TriggerAuthoringEditorIntegration.T("source"), GUILayout.Width(44f));
            var state = _inspection != null ? GetSyncStateLabel(_inspection.State) : "未知";
            var oldColor = GUI.color;
            GUI.color = GetSyncColor(_inspection != null ? _inspection.State : TriggerAuthoringSyncState.Untracked);
            GUILayout.Label(state, EditorStyles.boldLabel, GUILayout.Width(92f));
            GUI.color = oldColor;
            GUILayout.FlexibleSpace();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("导入", "导入模板 Source JSON"))) Import();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("导出", "导出模板 Source JSON"))) Export();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("校验", "校验模板 Schema 和节点树"))) Repaint();
            SirenixEditorGUI.EndHorizontalToolbar();
        }

        private void DrawTemplateHeader()
        {
            _asset.Metadata = _asset.Metadata ?? new TriggerAuthoringSourceMetadata();
            _asset.Template = _asset.Template ?? new TriggerAuthoringTemplateData();
            var template = _asset.Template;

            SirenixEditorGUI.BeginBox("模板");
            template.TemplateId = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("template-id"), template.TemplateId);
            template.TemplateVersion = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("version"), template.TemplateVersion);
            template.DisplayName = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("display-name"), template.DisplayName);
            template.Description = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("description"), template.Description);
            _asset.Metadata.Author = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("author"), _asset.Metadata.Author);
            _asset.Metadata.Description = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("source-note"), _asset.Metadata.Description);

            SirenixEditorGUI.EndBox();
        }

        private void DrawTriggerDefinition()
        {
            var definition = TriggerAuthoringTemplateDefinition.Get(_asset.Template);
            SirenixEditorGUI.BeginBox("完整触发器原型");
            _showDefinition = EditorGUILayout.Foldout(_showDefinition, "入口与执行配置", true);
            if (_showDefinition)
            {
                EditorGUILayout.HelpBox(
                    "模板实例仅提供实例 ID、业务分组和调用输入；以下触发器配置由模板统一提供。",
                    MessageType.Info);
                definition.Name = EditorGUILayout.TextField("触发器名称", definition.Name);
                definition.Enabled = EditorGUILayout.Toggle("默认启用", definition.Enabled);
                definition.EntryMode = (TriggerEntryMode)EditorGUILayout.EnumPopup("入口模式", definition.EntryMode);
                if (definition.EntryMode == TriggerEntryMode.Event)
                {
                    EditorGUILayout.BeginHorizontal();
                    definition.Event = EditorGUILayout.TextField("触发事件", definition.Event);
                    if (GUILayout.Button(new GUIContent("选择", "从事件目录中选择"), GUILayout.Width(58f)))
                        ShowEventMenu(definition);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    definition.Event = string.Empty;
                    EditorGUILayout.HelpBox("仅供调用的模板不会直接订阅 EventBus 事件。", MessageType.None);
                }
                definition.Phase = EditorGUILayout.TextField("执行阶段", definition.Phase);
                definition.Scope = EditorGUILayout.TextField("作用域", definition.Scope);
                definition.Priority = EditorGUILayout.IntField("优先级", definition.Priority);
                definition.InterruptPriority = EditorGUILayout.IntField("中断优先级", definition.InterruptPriority);
                definition.AllowExternal = EditorGUILayout.Toggle("允许外部触发", definition.AllowExternal);
                definition.Note = EditorGUILayout.TextField("备注", definition.Note);

                definition.Cue = definition.Cue ?? new TriggerCueData();
                definition.Cue.CueId = EditorGUILayout.TextField("表现提示 ID", definition.Cue.CueId);
                definition.Schedule = definition.Schedule ?? new TriggerScheduleData();
                definition.Schedule.Mode = EditorGUILayout.TextField("调度模式", definition.Schedule.Mode);
                definition.Schedule.DelayMilliseconds = EditorGUILayout.IntField("延迟（毫秒）", definition.Schedule.DelayMilliseconds);
                definition.Schedule.IntervalMilliseconds = EditorGUILayout.IntField("间隔（毫秒）", definition.Schedule.IntervalMilliseconds);
                definition.Schedule.RepeatCount = EditorGUILayout.IntField("重复次数", definition.Schedule.RepeatCount);
                definition.ExecutionControl = definition.ExecutionControl ?? new TriggerExecutionControlData();
                definition.ExecutionControl.InterruptPolicy = EditorGUILayout.TextField(
                    "中断策略", definition.ExecutionControl.InterruptPolicy);
                definition.ExecutionControl.StopPropagationOnSuccess = EditorGUILayout.Toggle(
                    "成功后停止传播", definition.ExecutionControl.StopPropagationOnSuccess);
                definition.ExecutionControl.StopPropagationOnFailure = EditorGUILayout.Toggle(
                    "失败后停止传播", definition.ExecutionControl.StopPropagationOnFailure);
            }

            definition.Blackboard = definition.Blackboard ?? new List<TriggerBlackboardVariableData>();
            _showBlackboard = EditorGUILayout.Foldout(
                _showBlackboard,
                "触发器 LocalVar（" + definition.Blackboard.Count + "）",
                true);
            if (_showBlackboard) DrawTemplateBlackboard(definition.Blackboard);
            SirenixEditorGUI.EndBox();
        }

        private void DrawTemplateBlackboard(List<TriggerBlackboardVariableData> variables)
        {
            for (var i = 0; i < variables.Count; i++)
            {
                var index = i;
                var variable = variables[i] ?? (variables[i] = new TriggerBlackboardVariableData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                variable.Key = EditorGUILayout.TextField("LocalVar Key", variable.Key);
                var remove = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                var nextType = DrawValueTypePopup(variable.Type);
                if (nextType != variable.Type)
                {
                    variable.Type = nextType;
                    variable.DefaultValue = CreateValue(nextType);
                }
                variable.ReadOnly = EditorGUILayout.Toggle(
                    new GUIContent("只读", "模板调用输入对应的 LocalVar 必须保持只读"),
                    variable.ReadOnly);
                variable.Description = EditorGUILayout.TextField("说明", variable.Description);
                variable.DefaultValue = variable.DefaultValue ?? CreateValue(variable.Type);
                TriggerAuthoringValueRefEditor.Draw(
                    variable.DefaultValue,
                    new TriggerParameterDescriptor(
                        "default",
                        variable.Type,
                        true,
                        TriggerValueSourceMask.Constant),
                    BuildValueContext());
                if (IsTemplateInputVariable(variable.Key))
                    EditorGUILayout.LabelField("由模板调用输入初始化", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                if (!remove) continue;
                variables.RemoveAt(index);
                i--;
            }

            if (GUILayout.Button("+ 添加 LocalVar", EditorStyles.miniButton))
            {
                variables.Add(new TriggerBlackboardVariableData
                {
                    Key = CreateUniqueLocalVariableKey(variables),
                    Type = TriggerValueType.Number,
                    DefaultValue = CreateValue(TriggerValueType.Number)
                });
            }
        }

        private bool IsTemplateInputVariable(string key)
        {
            var parameters = _asset?.Template?.Parameters;
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && string.Equals(parameters[i].LocalVariableKey, key, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string CreateUniqueLocalVariableKey(IReadOnlyList<TriggerBlackboardVariableData> variables)
        {
            var suffix = 1;
            while (ContainsLocalVariable(variables, "local_" + suffix)) suffix++;
            return "local_" + suffix;
        }

        private static bool ContainsLocalVariable(IReadOnlyList<TriggerBlackboardVariableData> variables, string key)
        {
            if (variables == null) return false;
            for (var i = 0; i < variables.Count; i++)
                if (variables[i] != null && string.Equals(variables[i].Key, key, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private void DrawParameters()
        {
            var template = _asset.Template;
            template.Parameters = template.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            _showParameters = EditorGUILayout.Foldout(
                _showParameters,
                "调用输入（" + template.Parameters.Count + "）",
                true);
            if (!_showParameters) return;

            for (var i = 0; i < template.Parameters.Count; i++)
            {
                var index = i;
                var parameter = template.Parameters[i] ?? (template.Parameters[i] = new TriggerAuthoringTemplateParameterData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                parameter.Name = EditorGUILayout.TextField(
                    new GUIContent("输入名称", "模板实例对外暴露的调用参数名称"),
                    parameter.Name);
                var nextType = DrawValueTypePopup(parameter.Type, GUILayout.Width(108f));
                if (nextType != parameter.Type)
                {
                    parameter.Type = nextType;
                    parameter.DefaultValue = CreateValue(nextType);
                }
                parameter.Required = GUILayout.Toggle(parameter.Required, "调用必填", GUILayout.Width(68f));
                var remove = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();

                DrawAllowedSources(parameter);
                parameter.LocalVariableKey = EditorGUILayout.TextField(
                    new GUIContent("写入 LocalVar", "调用模板时，此输入值会写入模板触发器的只读局部变量"),
                    parameter.LocalVariableKey);
                parameter.Description = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("description"), parameter.Description);
                parameter.HasDefault = EditorGUILayout.Toggle(TriggerAuthoringEditorIntegration.T("has-default"), parameter.HasDefault);
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
                Undo.RecordObject(_asset, "删除模板参数");
                template.Parameters.RemoveAt(index);
                EditorUtility.SetDirty(_asset);
                i--;
            }

            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add-parameter"), EditorStyles.miniButton))
            {
                Undo.RecordObject(_asset, "添加模板参数");
                template.Parameters.Add(new TriggerAuthoringTemplateParameterData
                {
                    Name = CreateUniqueParameterName(template.Parameters),
                    LocalVariableKey = CreateUniqueParameterName(template.Parameters),
                    Type = TriggerValueType.Number,
                    DefaultValue = CreateValue(TriggerValueType.Number)
                });
                TriggerAuthoringTemplateDefinition.Normalize(template);
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
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add-root"), EditorStyles.miniButton, GUILayout.Width(70f)))
                    ShowNodeCreationMenu(kind, created => SetTemplateRoot(kind, created), GUILayoutUtility.GetLastRect());
            }
            else
            {
                if (GUILayout.Button(new GUIContent("复制", "将此节点树复制到剪贴板"), EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                    TriggerAuthoringNodeClipboard.Copy(root, kind);
                using (new EditorGUI.DisabledScope(!TriggerAuthoringNodeClipboard.HasNode()))
                {
                    if (GUILayout.Button(new GUIContent("粘贴", "使用剪贴板内容替换根节点"), EditorStyles.miniButtonMid, GUILayout.Width(44f)))
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
                ? TriggerAuthoringTemplateDefinition.Get(_asset.Template).Condition
                : TriggerAuthoringTemplateDefinition.Get(_asset.Template).Actions;
        }

        private void SetTemplateRoot(TriggerNodeKind kind, TriggerNodeData root)
        {
            var definition = TriggerAuthoringTemplateDefinition.Get(_asset.Template);
            if (kind == TriggerNodeKind.Condition) definition.Condition = root;
            else definition.Actions = root;
        }

        private TriggerNodeData DrawNode(TriggerNodeData node, TriggerNodeKind kind, int depth, bool root)
        {
            if (node == null) return null;
            EditorGUILayout.BeginVertical(depth == 0 ? EditorStyles.helpBox : SirenixGUIStyles.BoxContainer);
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("template-trees-note"), MessageType.Error);
                node.GroupReference = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("group-reference"), node.GroupReference);
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("clear-group-reference"), EditorStyles.miniButton))
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
            GUILayout.Label(TriggerAuthoringEditorLabels.Node(node.Type, descriptor != null ? descriptor.DisplayName : null), EditorStyles.boldLabel);
            GUILayout.Label(node.Type ?? "<未选择类型>", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!canPasteChild))
            {
                if (GUILayout.Button(new GUIContent("粘贴", "将剪贴板节点粘贴为子节点"), EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                    PasteChild(children, kind);
            }
            if (GUILayout.Button(new GUIContent("类型", "更改节点类型"), EditorStyles.miniButtonMid, GUILayout.Width(42f)))
                ShowNodeTypeMenu(kind, descriptor => ApplyDescriptor(node, descriptor), GUILayoutUtility.GetLastRect());
            var remove = GUILayout.Button(new GUIContent("x", "删除节点"), EditorStyles.miniButtonRight, GUILayout.Width(25f));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                EditorGUILayout.EndVertical();
                return null;
            }

            DrawNodeArguments(node, descriptor);
            if (kind == TriggerNodeKind.Action &&
                string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                DrawTemplateConditionalBranches(node, depth);
                EditorGUILayout.EndVertical();
                return node;
            }
            if (maxChildren != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriggerAuthoringEditorIntegration.F("children-format", children.Count), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(maxChildren > 0 && children.Count >= maxChildren))
                {
                    if (GUILayout.Button("+ 子节点", EditorStyles.miniButton, GUILayout.Width(70f)))
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

        private void DrawTemplateConditionalBranches(TriggerNodeData root, int depth)
        {
            root.Children = root.Children ?? new List<TriggerNodeData>();
            root.ElseChildren = root.ElseChildren ?? new List<TriggerNodeData>();
            DrawTemplateBranchCondition(root, depth + 1);
            DrawTemplateActionBranch("如果成立时执行", root.Children, depth);

            var branches = new List<TriggerNodeData>();
            TriggerAuthoringConditionalChain.CollectElseIfBranches(root, branches);
            for (var i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                branch.Children = branch.Children ?? new List<TriggerNodeData>();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("否则如果 " + (i + 1), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var remove = GUILayout.Button("删除分支", EditorStyles.miniButton, GUILayout.Width(66f));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    TriggerAuthoringConditionalChain.RemoveElseIf(root, branch);
                    EditorGUILayout.EndVertical();
                    break;
                }
                DrawTemplateBranchCondition(branch, depth + 1);
                DrawTemplateActionBranch("该分支成立时执行", branch.Children, depth);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ 添加否则如果", EditorStyles.miniButton, GUILayout.Width(112f)))
            {
                _types.TryGet(TriggerNodeKind.Action, "conditional", out var descriptor);
                var branch = CreateNode(descriptor);
                branch.Kind = TriggerNodeKind.Action;
                branch.Type = "conditional";
                branch.Condition = branch.Condition ?? CreateDefaultEmbeddedCondition();
                TriggerAuthoringConditionalChain.AppendElseIf(root, branch);
            }

            DrawTemplateActionBranch(
                "否则执行",
                TriggerAuthoringConditionalChain.GetFallbackActions(root),
                depth);
        }

        private void DrawTemplateBranchCondition(TriggerNodeData node, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("判断条件", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (node.Condition == null && GUILayout.Button("+ 添加条件", EditorStyles.miniButton, GUILayout.Width(82f)))
                ShowNodeCreationMenu(TriggerNodeKind.Condition, created => node.Condition = created, GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();
            if (node.Condition != null)
                node.Condition = DrawNode(node.Condition, TriggerNodeKind.Condition, depth, false);
        }

        private void DrawTemplateActionBranch(string label, List<TriggerNodeData> actions, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label + "（" + actions.Count + "）", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加行为", EditorStyles.miniButton, GUILayout.Width(82f)))
                ShowNodeCreationMenu(TriggerNodeKind.Action, actions.Add, GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();
            for (var i = 0; i < actions.Count; i++)
            {
                var child = DrawNode(actions[i], TriggerNodeKind.Action, depth + 1, false);
                if (child == null)
                {
                    actions.RemoveAt(i);
                    i--;
                }
                else
                {
                    actions[i] = child;
                }
            }
        }

        private void DrawNodeArguments(TriggerNodeData node, TriggerTypeDescriptor descriptor)
        {
            var arguments = node.Arguments ?? (node.Arguments = new List<TriggerArgumentData>());
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("unknown-node-descriptor"), MessageType.Error);
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
                        GUILayout.Label(TriggerAuthoringEditorLabels.Parameter(parameter.Name), EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add"), EditorStyles.miniButton, GUILayout.Width(42f)))
                            arguments.Add(CreateArgument(parameter));
                        EditorGUILayout.EndHorizontal();
                    }
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriggerAuthoringEditorLabels.Parameter(parameter.Name), EditorStyles.miniBoldLabel);
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
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.F("unknown-argument-format", argument.Name), MessageType.Warning);
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
            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add-raw-argument"), EditorStyles.miniButton))
                arguments.Add(new TriggerArgumentData());
        }

        private void DrawValidation()
        {
            _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, "诊断", true);
            if (!_showDiagnostics) return;
            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                _asset.Template,
                TriggerAuthoringValidationContext.Create(_asset));
            if (diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("no-diagnostics"), MessageType.Info);
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

        private void ShowEventMenu(TriggerDefinitionData prototype)
        {
            var menu = new GenericMenu();
            if (_events == null || _events.Definitions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有事件目录"));
            }
            else
            {
                var definitions = _events.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var eventDefinition = definitions[i];
                    if (eventDefinition == null) continue;
                    var family = eventDefinition.MatchMode == TriggerEventMatchMode.Prefix ? "事件族" : eventDefinition.Category;
                    var label = family + "/" + (string.IsNullOrWhiteSpace(eventDefinition.DisplayName) ? eventDefinition.Id : eventDefinition.DisplayName);
                    var captured = eventDefinition;
                    menu.AddItem(new GUIContent(label), string.Equals(prototype.Event, captured.Id, StringComparison.Ordinal), () =>
                    {
                        Undo.RecordObject(_asset, "选择模板事件");
                        TriggerAuthoringTemplateDefinition.Get(_asset.Template).Event = captured.Id;
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
                Undo.RecordObject(_asset, "选择模板节点类型");
                selected(descriptor);
                EditorUtility.SetDirty(_asset);
            }
            new TriggerNodeTypeBrowser(
                _nodeBrowserState,
                kind,
                OnType,
                catalog: _types).Show(activator);
        }

        private void ShowNodeCreationMenu(TriggerNodeKind kind, Action<TriggerNodeData> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor)
            {
                Undo.RecordObject(_asset, "添加模板节点");
                selected(CreateNode(descriptor));
                EditorUtility.SetDirty(_asset);
            }
            new TriggerNodeTypeBrowser(
                _nodeBrowserState,
                kind,
                OnType,
                catalog: _types).Show(activator);
        }

        private void PasteRoot(TriggerNodeKind kind, Action<TriggerNodeData> selected)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("粘贴节点", "剪贴板中没有匹配的节点。", "确定");
                return;
            }
            Undo.RecordObject(_asset, "粘贴模板节点");
            selected(pasted);
            EditorUtility.SetDirty(_asset);
        }

        private void PasteChild(List<TriggerNodeData> children, TriggerNodeKind kind)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("粘贴节点", "剪贴板中没有匹配的节点。", "确定");
                return;
            }
            Undo.RecordObject(_asset, "粘贴模板节点");
            children.Add(pasted);
            EditorUtility.SetDirty(_asset);
        }

        private TriggerAuthoringValueRefEditorContext BuildValueContext()
        {
            return new TriggerAuthoringValueRefEditorContext
            {
                Trigger = TriggerAuthoringTemplateDefinition.Get(_asset.Template),
                Events = _events,
                GlobalBlackboard = _globalBlackboard
            };
        }

        private void RebuildCatalogs()
        {
            var project = _asset != null ? _asset.Project : null;
            _types = TriggerTypeDescriptorCatalog.CreateForProject(project);
            _events = TriggerEventDescriptorCatalog.FromProject(project);
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
                    "导出触发器模板 Source JSON", Application.dataPath, name,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringTemplateSourceSync.Export(_asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器模板源文件冲突",
                    result.Message + "\n\n是否强制导出并覆盖 Source JSON？",
                    "强制导出",
                    "取消"))
                result = TriggerAuthoringTemplateSourceSync.Export(_asset, path, true);
            ShowResult("导出", result);
        }

        private void Import()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "导入触发器模板 Source JSON", Application.dataPath,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringTemplateSourceSync.PreviewImport(_asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringTemplateSourceSync.Import(_asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器模板资产冲突",
                    result.Message + "\n\n是否强制导入并覆盖资产内容？",
                    "强制导入",
                    "取消"))
                result = TriggerAuthoringTemplateSourceSync.Import(_asset, path, true);
            ShowResult("导入", result);
        }

        private void ShowResult(string operation, TriggerAuthoringSyncResult result)
        {
            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                _nextInspectionAt = 0d;
                ShowNotification("模板" + operation + "成功");
                return;
            }
            EditorUtility.DisplayDialog("触发器模板" + operation + "失败", result.Message, "确定");
        }

        private void RefreshInspection()
        {
            if (_inspection != null && EditorApplication.timeSinceStartup < _nextInspectionAt) return;
            _inspection = TriggerAuthoringTemplateSourceSync.Inspect(_asset);
            _nextInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
        }

        private static readonly TriggerValueType[] ValueTypeOptions =
        {
            TriggerValueType.None,
            TriggerValueType.Integer,
            TriggerValueType.Number,
            TriggerValueType.Boolean,
            TriggerValueType.String,
            TriggerValueType.Entity,
            TriggerValueType.ObjectId,
            TriggerValueType.IntegerList,
            TriggerValueType.Vector3,
            TriggerValueType.Object
        };

        private static readonly string[] ValueSourceNames =
        {
            "常量", "事件参数", "运行上下文", "局部黑板", "全局黑板", "表达式"
        };

        private static readonly TriggerTemplateValueSourceMask[] ValueSourceMasks =
        {
            TriggerTemplateValueSourceMask.Constant,
            TriggerTemplateValueSourceMask.Payload,
            TriggerTemplateValueSourceMask.Context,
            TriggerTemplateValueSourceMask.LocalBlackboard,
            TriggerTemplateValueSourceMask.GlobalBlackboard,
            TriggerTemplateValueSourceMask.Expression
        };

        private void DrawAllowedSources(TriggerAuthoringTemplateParameterData parameter)
        {
            var rect = EditorGUILayout.GetControlRect();
            var fieldRect = EditorGUI.PrefixLabel(rect, new GUIContent("实例可用来源"));
            if (!GUI.Button(fieldRect, BuildAllowedSourcesLabel(parameter.AllowedSources), EditorStyles.popup)) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("全部"), parameter.AllowedSources == TriggerTemplateValueSourceMask.InstanceBinding,
                () => SetAllowedSources(parameter, TriggerTemplateValueSourceMask.InstanceBinding));
            menu.AddItem(new GUIContent("清除全部"), parameter.AllowedSources == TriggerTemplateValueSourceMask.None,
                () => SetAllowedSources(parameter, TriggerTemplateValueSourceMask.None));
            menu.AddSeparator(string.Empty);
            for (var i = 0; i < ValueSourceMasks.Length; i++)
            {
                var mask = ValueSourceMasks[i];
                var label = ValueSourceNames[i];
                menu.AddItem(new GUIContent(label), (parameter.AllowedSources & mask) != 0, () =>
                {
                    var next = parameter.AllowedSources ^ mask;
                    SetAllowedSources(parameter, next);
                });
            }
            menu.DropDown(fieldRect);
        }

        private void SetAllowedSources(
            TriggerAuthoringTemplateParameterData parameter,
            TriggerTemplateValueSourceMask value)
        {
            Undo.RecordObject(_asset, "设置模板参数可用来源");
            parameter.AllowedSources = value;
            EditorUtility.SetDirty(_asset);
            Repaint();
        }

        private static string BuildAllowedSourcesLabel(TriggerTemplateValueSourceMask value)
        {
            if (value == TriggerTemplateValueSourceMask.None) return "未选择";
            if (value == TriggerTemplateValueSourceMask.InstanceBinding) return "全部";
            var names = new List<string>();
            for (var i = 0; i < ValueSourceMasks.Length; i++)
                if ((value & ValueSourceMasks[i]) != 0)
                    names.Add(ValueSourceNames[i]);
            return names.Count > 0 ? string.Join("、", names.ToArray()) : "未选择";
        }

        private static TriggerValueType DrawValueTypePopup(TriggerValueType value, params GUILayoutOption[] options)
        {
            var names = new string[ValueTypeOptions.Length];
            var selected = 0;
            for (var i = 0; i < ValueTypeOptions.Length; i++)
            {
                names[i] = TriggerAuthoringEditorLabels.ValueType(ValueTypeOptions[i]);
                if (ValueTypeOptions[i] == value) selected = i;
            }
            return ValueTypeOptions[EditorGUILayout.Popup(selected, names, options)];
        }

        private static string GetSyncStateLabel(TriggerAuthoringSyncState state)
        {
            switch (state)
            {
                case TriggerAuthoringSyncState.Untracked: return "未跟踪";
                case TriggerAuthoringSyncState.InSync: return "已同步";
                case TriggerAuthoringSyncState.AssetChanged: return "资产已修改";
                case TriggerAuthoringSyncState.JsonChanged: return "源文件已修改";
                case TriggerAuthoringSyncState.Conflict: return "存在冲突";
                case TriggerAuthoringSyncState.SourceMissing: return "源文件缺失";
                case TriggerAuthoringSyncState.InvalidSource: return "源文件无效";
                default: return "未知";
            }
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
                Undo.RecordObject(_asset, "编辑触发器模板");
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
            if (descriptor.Kind == TriggerNodeKind.Action &&
                string.Equals(descriptor.Type, "conditional", StringComparison.OrdinalIgnoreCase))
                node.Condition = CreateDefaultEmbeddedCondition();
            return node;
        }

        private static TriggerNodeData CreateDefaultEmbeddedCondition()
        {
            return new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = "always_true" };
        }

        private static void ApplyDescriptor(TriggerNodeData node, TriggerTypeDescriptor descriptor)
        {
            node.Kind = descriptor.Kind;
            node.GroupReference = string.Empty;
            node.Type = descriptor.Type;
            node.Arguments = new List<TriggerArgumentData>();
            node.Condition = descriptor.Kind == TriggerNodeKind.Action &&
                             string.Equals(descriptor.Type, "conditional", StringComparison.OrdinalIgnoreCase)
                ? CreateDefaultEmbeddedCondition()
                : null;
            node.Children = new List<TriggerNodeData>();
            node.ElseChildren = new List<TriggerNodeData>();
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
