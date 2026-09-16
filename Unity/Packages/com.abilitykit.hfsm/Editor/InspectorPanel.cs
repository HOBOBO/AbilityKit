using System.Linq;
using UnityEditor;
using UnityEngine;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// Inspector panel for editing HFSM nodes and transitions.
    /// Displays in the right sidebar of the editor window.
    /// </summary>
    public class InspectorPanel : EditorWindow
    {
        private EditorContext _context;
        private Vector2 _scrollPosition;
        private BehaviorInspector _behaviorInspector;
        private StateNode _lastInspectedState;

        public void Initialize(EditorContext context)
        {
            _context = context;
            _behaviorInspector = null;
            _lastInspectedState = null;
        }

        public void OnGUI()
        {
            if (_context == null)
                return;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Show selection info
            if (_context.SelectedEdge != null)
            {
                DrawEdgeInspector(_context.SelectedEdge);
            }
            else if (_context.HasSelection && _context.FirstSelectedNode != null)
            {
                DrawNodeInspector(_context.FirstSelectedNode);
            }
            else
            {
                DrawNoSelection();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawNoSelection()
        {
            EditorGUILayout.HelpBox("请选择节点或转换以编辑其属性。", MessageType.Info);
        }

        /// <summary>
        /// Records an undo snapshot of the graph asset before an inspector edit. It must be called
        /// before mutating the model, because Undo.RecordObject snapshots the state at call time.
        /// </summary>
        private void RecordUndo(string label)
        {
            if (_context != null && _context.GraphAsset != null)
                Undo.RecordObject(_context.GraphAsset, label);
        }

        private void DrawNodeInspector(NodeBase node)
        {
            EditorGUILayout.LabelField("节点检查器", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Display name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("名称", node.DisplayName);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 DisplayName");
                node.DisplayName = newName;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Position
            EditorGUILayout.LabelField("位置", $"{node.Position.x:F0}, {node.Position.y:F0}");

            // Node type (read only)
            EditorGUILayout.LabelField("类型", node is StateMachineNode ? "状态机" : "叶状态");

            EditorGUILayout.Space();

            if (node is StateNode stateNode)
            {
                DrawStateInspector(stateNode);
            }
            else if (node is StateMachineNode smNode)
            {
                DrawStateMachineInspector(smNode);
            }

            EditorGUILayout.Space();

            // Actions section
            DrawActionSection(node);
        }

        private void DrawStateInspector(StateNode state)
        {
            EditorGUILayout.LabelField("Next Runtime 绑定", EditorStyles.boldLabel);
            var isParallel = state.NextParallelBehaviorKeys.Count > 0;
            EditorGUI.BeginChangeCheck();
            isParallel = EditorGUILayout.Toggle("并行状态", isParallel);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("编辑状态机属性");
                if (isParallel)
                    state.NextParallelBehaviorKeysInternal.Add(string.Empty);
                else
                    state.NextParallelBehaviorKeysInternal.Clear();
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            if (!isParallel)
            {
                DrawBindingField(
                    "行为",
                    BindingKind.State,
                    state.NextBehaviorKey,
                    value => state.NextBehaviorKey = value);
            }
            else
            {
                DrawParallelBehaviorBindings(state);
            }
            EditorGUILayout.Space();

            // Needs exit time
            EditorGUI.BeginChangeCheck();
            bool needsExitTime = EditorGUILayout.Toggle("需要退出时间", state.NeedsExitTime);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 NeedsExitTime");
                state.NeedsExitTime = needsExitTime;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Ghost state
            EditorGUI.BeginChangeCheck();
            bool isGhost = EditorGUILayout.Toggle("幽灵状态", state.IsGhostState);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 IsGhostState");
                state.IsGhostState = isGhost;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Default state toggle
            EditorGUI.BeginChangeCheck();
            bool isDefault = EditorGUILayout.Toggle("设为默认状态", state.isDefault);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("编辑状态机属性");
                if (isDefault && !state.isDefault)
                {
                    _context.SetDefaultState(state);
                }
                else
                {
                    state.isDefault = isDefault;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }

            EditorGUILayout.Space();

            // Behavior Editor
            DrawBehaviorEditorSection(state);
        }

        private void DrawBehaviorEditorSection(StateNode state)
        {
            EditorGUILayout.LabelField("行为编辑器", EditorStyles.boldLabel);

            // Initialize behavior inspector if needed
            if (_behaviorInspector == null || _lastInspectedState != state)
            {
                _behaviorInspector = new BehaviorInspector(state, () =>
                {
                    EditorUtility.SetDirty(_context.GraphAsset);
                }, () => RecordUndo("编辑行为"));
                _lastInspectedState = state;
            }

            // Draw behavior inspector
            _behaviorInspector.Draw();
        }

        private void DrawStateMachineInspector(StateMachineNode stateMachine)
        {
            EditorGUILayout.LabelField("状态机设置", EditorStyles.boldLabel);

            // Remember last state
            EditorGUI.BeginChangeCheck();
            bool rememberLast = EditorGUILayout.Toggle("记住上次状态", stateMachine.RememberLastState);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 RememberLastState");
                stateMachine.RememberLastState = rememberLast;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUI.BeginChangeCheck();
            var needsExitTime = EditorGUILayout.Toggle("需要退出时间", stateMachine.NeedsExitTime);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 NeedsExitTime");
                stateMachine.NeedsExitTime = needsExitTime;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUI.BeginChangeCheck();
            var isGhostState = EditorGUILayout.Toggle("幽灵状态", stateMachine.IsGhostState);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 IsGhostState");
                stateMachine.IsGhostState = isGhostState;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Default state selector
            EditorGUILayout.LabelField("默认状态", stateMachine.DefaultStateId ?? "无");

            // Child count
            EditorGUILayout.LabelField("子状态数", stateMachine.ChildNodeIds.Count.ToString());
        }

        private void DrawParallelBehaviorBindings(StateNode state)
        {
            EditorGUI.BeginChangeCheck();
            var policyIndex = EditorGUILayout.Popup(
                "退出许可",
                state.NextParallelExitPolicy == ParallelExitPolicy.All ? 1 : 0,
                new[] { "任一行为允许", "全部行为允许" });
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 NextParallelExitPolicy");
                state.NextParallelExitPolicy = policyIndex == 1
                    ? ParallelExitPolicy.All
                    : ParallelExitPolicy.Any;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            for (var index = 0; index < state.NextParallelBehaviorKeys.Count; index++)
            {
                var bindingIndex = index;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                DrawBindingField(
                    $"并行行为 {index + 1}",
                    BindingKind.State,
                    state.NextParallelBehaviorKeys[bindingIndex],
                    value => state.NextParallelBehaviorKeysInternal[bindingIndex] = value);
                EditorGUILayout.EndVertical();
                if (GUILayout.Button(new GUIContent("-", "移除此并行行为"), GUILayout.Width(24)))
                {
                    RecordUndo("编辑状态机属性");
                    state.NextParallelBehaviorKeysInternal.RemoveAt(bindingIndex);
                    EditorUtility.SetDirty(_context.GraphAsset);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 添加并行行为"))
            {
                RecordUndo("编辑状态机属性");
                state.NextParallelBehaviorKeysInternal.Add(string.Empty);
                EditorUtility.SetDirty(_context.GraphAsset);
            }
        }

        private void DrawActionSection(NodeBase node)
        {
            if (!(node is StateNode stateNode))
                return;

            EditorGUILayout.LabelField("动作", EditorStyles.boldLabel);

            // Entry actions
            EditorGUILayout.LabelField("进入时", EditorStyles.miniLabel);
            DrawActionList(stateNode.EntryActionMethodNames, "进入动作");

            // Logic actions
            EditorGUILayout.LabelField("逻辑更新时", EditorStyles.miniLabel);
            DrawActionList(stateNode.LogicActionMethodNames, "逻辑动作");

            // Exit actions
            EditorGUILayout.LabelField("退出时", EditorStyles.miniLabel);
            DrawActionList(stateNode.ExitActionMethodNames, "退出动作");

            // Can exit methods
            if (stateNode.NeedsExitTime)
            {
                EditorGUILayout.LabelField("可退出判断", EditorStyles.miniLabel);
                DrawActionList(stateNode.CanExitMethodNames, "可退出方法");
            }
        }

        private void DrawActionList(System.Collections.Generic.IReadOnlyList<string> actions, string listName)
        {
            EditorGUI.indentLevel++;

            int count = actions.Count;
            for (int i = 0; i < count; i++)
            {
                EditorGUILayout.LabelField($"- {actions[i]}");
            }

            if (count == 0)
            {
                EditorGUILayout.LabelField("（无）", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawEdgeInspector(TransitionEdge edge)
        {
            EditorGUILayout.LabelField("转换检查器", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Source and target
            var sourceNode = _context.GraphAsset.GetNodeById(edge.SourceNodeId);
            var targetNode = _context.GraphAsset.GetNodeById(edge.TargetNodeId);

            EditorGUILayout.LabelField("来源", sourceNode?.DisplayName ?? "未知");
            EditorGUILayout.LabelField("目标", targetNode?.DisplayName ?? "未知");

            EditorGUILayout.Space();

            // Priority
            EditorGUI.BeginChangeCheck();
            int priority = EditorGUILayout.IntField("优先级", edge.Priority);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 Priority");
                edge.Priority = priority;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Force instantly
            EditorGUI.BeginChangeCheck();
            bool forceInstantly = EditorGUILayout.Toggle("立即强制转换", edge.ForceInstantly);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 ForceInstantly");
                edge.ForceInstantly = forceInstantly;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Is exit transition
            EditorGUI.BeginChangeCheck();
            bool isExit = EditorGUILayout.Toggle("退出转换", edge.IsExitTransition);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 IsExitTransition");
                edge.IsExitTransition = isExit;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Next Runtime 绑定", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string triggerId = EditorGUILayout.TextField("触发器 ID", edge.NextTriggerId);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 NextTriggerId");
                edge.NextTriggerId = triggerId;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            DrawBindingField(
                "条件",
                BindingKind.Condition,
                edge.NextConditionKey,
                value => edge.NextConditionKey = value);
            DrawBindingField(
                "动作",
                BindingKind.Action,
                edge.NextActionKey,
                value => edge.NextActionKey = value);

            EditorGUI.BeginChangeCheck();
            long minimumDurationRaw = EditorGUILayout.LongField(
                new GUIContent("最短持续时间原始值", "Q32.32 确定性时长原始值"),
                edge.NextMinimumActiveDurationRaw);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 NextMinimumActiveDurationRaw");
                edge.NextMinimumActiveDurationRaw = minimumDurationRaw;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Conditions section
            DrawConditionSection(edge);

            EditorGUILayout.Space();

            // Delete button
            if (GUILayout.Button("删除转换"))
            {
                _context.DeleteEdge(edge);
            }
        }

        private void DrawConditionSection(TransitionEdge edge)
        {
            EditorGUILayout.LabelField("条件", EditorStyles.boldLabel);

            // Condition combination mode
            EditorGUI.BeginChangeCheck();
            bool useAndLogic = EditorGUILayout.Toggle("满足全部条件（AND）", edge.UseAndLogic);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 UseAndLogic");
                edge.UseAndLogic = useAndLogic;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Draw existing conditions
            var conditions = edge.Conditions;
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    DrawConditionItem(edge, conditions[i], i);
                }
            }

            // Add condition dropdown
            EditorGUILayout.Space();
            if (EditorGUILayout.DropdownButton(new GUIContent("+ 添加条件"), FocusType.Passive))
            {
                ShowAddConditionMenu(edge);
            }

            if (conditions == null || conditions.Count == 0)
            {
                EditorGUILayout.LabelField("（始终转换）", EditorStyles.miniLabel);
            }
        }

        private void DrawConditionItem(TransitionEdge edge, TransitionCondition condition, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 使用 Dummy 创建可点击区域
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetConditionDisplayName(condition), EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField(GetConditionDescription(condition), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            // X 按钮删除 - 使用更明显的样式
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button(new GUIContent("X", "删除条件"), EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(16)))
            {
                RecordUndo("删除条件");
                EditorUtility.SetDirty(_context.GraphAsset);
                edge.RemoveCondition(condition);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Draw condition-specific fields
            DrawConditionFields(edge, condition);

            EditorGUILayout.EndVertical();

            // 右键菜单检测 - 在 EndVertical 之后检测
            if (Event.current.type == EventType.ContextClick)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                if (lastRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("删除条件"), false, () =>
                    {
                        RecordUndo("删除条件");
                        EditorUtility.SetDirty(_context.GraphAsset);
                        edge.RemoveCondition(condition);
                    });
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(2);
        }

        private void DrawConditionFields(TransitionEdge edge, TransitionCondition condition)
        {
            EditorGUI.indentLevel++;

            if (condition is ParameterCondition paramCondition)
            {
                DrawParameterConditionFields(edge, paramCondition);
            }
            else if (condition is TimeElapsedCondition timeCondition)
            {
                DrawTimeElapsedConditionFields(edge, timeCondition);
            }
            else if (condition is BehaviorCompleteCondition behaviorCondition)
            {
                DrawBehaviorCompleteConditionFields(edge, behaviorCondition);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawParameterConditionFields(TransitionEdge edge, ParameterCondition condition)
        {
            var parameters = _context.GraphAsset.Parameters;

            // Parameter name dropdown
            string[] parameterNames = new string[parameters.Count];
            int selectedIndex = -1;
            for (int i = 0; i < parameters.Count; i++)
            {
                parameterNames[i] = parameters[i].Name;
                if (parameters[i].Name == condition.ParameterName)
                    selectedIndex = i;
            }

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("参数", selectedIndex, parameterNames);
            if (EditorGUI.EndChangeCheck() && selectedIndex >= 0)
            {
                RecordUndo("修改 ParameterName");
                condition.ParameterName = parameterNames[selectedIndex];
                condition.ParameterType = parameters[selectedIndex].ParameterType;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Parameter type
            EditorGUI.BeginChangeCheck();
            ParameterValueType paramType = DrawParameterTypePopup("类型", condition.ParameterType);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 ParameterType");
                condition.ParameterType = paramType;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Operator (for numeric types)
            if (condition.ParameterType == ParameterValueType.Float || condition.ParameterType == ParameterValueType.Int)
            {
                EditorGUI.BeginChangeCheck();
                CompareOperator op = DrawCompareOperatorPopup("运算符", condition.Operator);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("修改 Operator");
                    condition.Operator = op;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }

            // Value based on type
            if (condition.ParameterType == ParameterValueType.Bool)
            {
                EditorGUI.BeginChangeCheck();
                bool boolValue = EditorGUILayout.Toggle("值", condition.BoolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("修改 BoolValue");
                    condition.BoolValue = boolValue;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }
            else if (condition.ParameterType == ParameterValueType.Float)
            {
                EditorGUI.BeginChangeCheck();
                float floatValue = EditorGUILayout.FloatField("值", condition.FloatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("修改 FloatValue");
                    condition.FloatValue = floatValue;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }
            else if (condition.ParameterType == ParameterValueType.Int)
            {
                EditorGUI.BeginChangeCheck();
                int intValue = EditorGUILayout.IntField("值", condition.IntValue);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("修改 IntValue");
                    condition.IntValue = intValue;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }
            else if (condition.ParameterType == ParameterValueType.Trigger)
            {
                EditorGUILayout.LabelField("条件", "触发器已设置", EditorStyles.miniLabel);
            }
        }

        private void DrawTimeElapsedConditionFields(TransitionEdge edge, TimeElapsedCondition condition)
        {
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUILayout.FloatField("持续时间（秒）", condition.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 Duration");
                condition.Duration = duration;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUI.BeginChangeCheck();
            CompareOperator op = DrawCompareOperatorPopup("运算符", condition.Operator);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("修改 Operator");
                condition.Operator = op;
                EditorUtility.SetDirty(_context.GraphAsset);
            }
        }

        private void DrawBehaviorCompleteConditionFields(TransitionEdge edge, BehaviorCompleteCondition condition)
        {
            EditorGUILayout.LabelField("来源", edge.SourceNodeId ?? "自身", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("条件", "所有行为均已完成", EditorStyles.miniLabel);
        }

        private void ShowAddConditionMenu(TransitionEdge edge)
        {
            var menu = new GenericMenu();

            // Parameter conditions
            menu.AddItem(new GUIContent("参数/布尔比较"), false, () => AddCondition(edge, new ParameterCondition { ParameterType = ParameterValueType.Bool }));
            menu.AddItem(new GUIContent("参数/浮点比较"), false, () => AddCondition(edge, new ParameterCondition { ParameterType = ParameterValueType.Float }));
            menu.AddItem(new GUIContent("参数/整数比较"), false, () => AddCondition(edge, new ParameterCondition { ParameterType = ParameterValueType.Int }));
            menu.AddItem(new GUIContent("参数/触发器"), false, () => AddCondition(edge, new ParameterCondition { ParameterType = ParameterValueType.Trigger }));

            menu.AddSeparator("");

            // Time condition
            menu.AddItem(new GUIContent("时间/经过时间"), false, () => AddCondition(edge, new TimeElapsedCondition { SourceNodeId = edge.SourceNodeId, Duration = 1f }));

            // Behavior condition
            menu.AddItem(new GUIContent("行为完成"), false, () => AddCondition(edge, new BehaviorCompleteCondition { SourceNodeId = edge.SourceNodeId }));

            menu.ShowAsContext();
        }

        private void AddCondition(TransitionEdge edge, TransitionCondition condition)
        {
            RecordUndo("添加条件");
            edge.AddCondition(condition);
            EditorUtility.SetDirty(_context.GraphAsset);
        }

        private void DrawBindingField(
            string label,
            BindingKind kind,
            string currentKey,
            System.Action<string> assign)
        {
            var descriptors = EditorBindingCatalog.Catalog.Descriptors
                .Where(descriptor => descriptor.Kind == kind)
                .ToArray();
            var options = new string[descriptors.Length + 2];
            options[0] = "（无）";
            options[1] = "自定义...";
            var selected = string.IsNullOrEmpty(currentKey) ? 0 : 1;
            for (var index = 0; index < descriptors.Length; index++)
            {
                var descriptor = descriptors[index];
                options[index + 2] = string.IsNullOrEmpty(descriptor.Category)
                    ? $"{descriptor.DisplayName} [{descriptor.Key}]"
                    : $"{descriptor.Category}/{descriptor.DisplayName} [{descriptor.Key}]";
                if (descriptor.Key == currentKey) selected = index + 2;
            }

            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Popup(label, selected, options);
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("编辑状态机属性");
                assign(next == 0 ? string.Empty : next == 1 ? currentKey : descriptors[next - 2].Key);
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            if (next == 1)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                var custom = EditorGUILayout.TextField("稳定键", currentKey ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("编辑状态机属性");
                    assign(custom);
                    EditorUtility.SetDirty(_context.GraphAsset);
                }

                if (!string.IsNullOrEmpty(custom) && !EditorBindingCatalog.Catalog.Contains(kind, custom))
                    EditorGUILayout.HelpBox("此键没有已注册的描述符，将阻止 Next Runtime 导出。", MessageType.Warning);
                EditorGUI.indentLevel--;
            }
        }

        private static ParameterValueType DrawParameterTypePopup(string label, ParameterValueType value)
        {
            var index = EditorGUILayout.Popup(label, (int)value, new[] { "布尔", "浮点", "整数", "触发器" });
            return (ParameterValueType)index;
        }

        private static CompareOperator DrawCompareOperatorPopup(string label, CompareOperator value)
        {
            var index = EditorGUILayout.Popup(label, (int)value, new[]
            {
                "等于（==）",
                "不等于（!=）",
                "大于（>）",
                "小于（<）",
                "大于等于（>=）",
                "小于等于（<=）"
            });
            return (CompareOperator)index;
        }

        private static string GetConditionDisplayName(TransitionCondition condition)
        {
            if (condition is ParameterCondition parameter)
            {
                switch (parameter.ParameterType)
                {
                    case ParameterValueType.Bool: return "布尔参数";
                    case ParameterValueType.Float: return "浮点参数";
                    case ParameterValueType.Int: return "整数参数";
                    case ParameterValueType.Trigger: return "触发器参数";
                }
            }
            if (condition is TimeElapsedCondition) return "经过时间";
            if (condition is BehaviorCompleteCondition) return "行为完成";
            return condition.DisplayName;
        }

        private static string GetConditionDescription(TransitionCondition condition)
        {
            if (condition is ParameterCondition parameter)
            {
                if (parameter.ParameterType == ParameterValueType.Bool)
                    return $"{parameter.ParameterName} = {(parameter.BoolValue ? "真" : "假")}";
                if (parameter.ParameterType == ParameterValueType.Trigger)
                    return $"{parameter.ParameterName} 已触发";
                var value = parameter.ParameterType == ParameterValueType.Float
                    ? parameter.FloatValue.ToString()
                    : parameter.IntValue.ToString();
                return $"{parameter.ParameterName} {GetCompareOperatorSymbol(parameter.Operator)} {value}";
            }
            if (condition is TimeElapsedCondition elapsed)
                return $"时间 {GetCompareOperatorSymbol(elapsed.Operator)} {elapsed.Duration:F2} 秒";
            if (condition is BehaviorCompleteCondition) return "所有行为均已完成";
            return condition.GetDescription();
        }

        private static string GetCompareOperatorSymbol(CompareOperator value)
        {
            switch (value)
            {
                case CompareOperator.Equal: return "==";
                case CompareOperator.NotEqual: return "!=";
                case CompareOperator.GreaterThan: return ">";
                case CompareOperator.LessThan: return "<";
                case CompareOperator.GreaterOrEqual: return ">=";
                case CompareOperator.LessOrEqual: return "<=";
                default: return "?";
            }
        }
    }
}
