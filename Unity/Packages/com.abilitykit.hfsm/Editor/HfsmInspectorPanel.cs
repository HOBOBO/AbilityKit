using UnityEditor;
using UnityEngine;
using UnityHFSM.Graph;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Inspector panel for editing HFSM nodes and transitions.
    /// Displays in the right sidebar of the editor window.
    /// </summary>
    public class HfsmInspectorPanel : EditorWindow
    {
        private HfsmEditorContext _context;
        private Vector2 _scrollPosition;
        private HfsmBehaviorInspector _behaviorInspector;
        private HfsmStateNode _lastInspectedState;

        public void Initialize(HfsmEditorContext context)
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
            EditorGUILayout.HelpBox("Select a node or transition to edit its properties.", MessageType.Info);
        }

        private void DrawNodeInspector(HfsmNodeBase node)
        {
            EditorGUILayout.LabelField("Node Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Display name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Name", node.DisplayName);
            if (EditorGUI.EndChangeCheck())
            {
                node.DisplayName = newName;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Position
            EditorGUILayout.LabelField("Position", $"{node.Position.x:F0}, {node.Position.y:F0}");

            // Node type (read only)
            EditorGUILayout.LabelField("Type", node.GetNodeTypeDescription());

            EditorGUILayout.Space();

            if (node is HfsmStateNode stateNode)
            {
                DrawStateInspector(stateNode);
            }
            else if (node is HfsmStateMachineNode smNode)
            {
                DrawStateMachineInspector(smNode);
            }

            EditorGUILayout.Space();

            // Actions section
            DrawActionSection(node);
        }

        private void DrawStateInspector(HfsmStateNode state)
        {
            // Needs exit time
            EditorGUI.BeginChangeCheck();
            bool needsExitTime = EditorGUILayout.Toggle("Needs Exit Time", state.NeedsExitTime);
            if (EditorGUI.EndChangeCheck())
            {
                state.NeedsExitTime = needsExitTime;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Ghost state
            EditorGUI.BeginChangeCheck();
            bool isGhost = EditorGUILayout.Toggle("Ghost State", state.IsGhostState);
            if (EditorGUI.EndChangeCheck())
            {
                state.IsGhostState = isGhost;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Default state toggle
            EditorGUI.BeginChangeCheck();
            bool isDefault = EditorGUILayout.Toggle("Is Default", state.isDefault);
            if (EditorGUI.EndChangeCheck())
            {
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

        private void DrawBehaviorEditorSection(HfsmStateNode state)
        {
            EditorGUILayout.LabelField("Behavior Editor", EditorStyles.boldLabel);

            // Initialize behavior inspector if needed
            if (_behaviorInspector == null || _lastInspectedState != state)
            {
                _behaviorInspector = new HfsmBehaviorInspector(state, () =>
                {
                    EditorUtility.SetDirty(_context.GraphAsset);
                });
                _lastInspectedState = state;
            }

            // Draw behavior inspector
            _behaviorInspector.Draw();
        }

        private void DrawStateMachineInspector(HfsmStateMachineNode stateMachine)
        {
            EditorGUILayout.LabelField("State Machine Settings", EditorStyles.boldLabel);

            // Remember last state
            EditorGUI.BeginChangeCheck();
            bool rememberLast = EditorGUILayout.Toggle("Remember Last State", stateMachine.RememberLastState);
            if (EditorGUI.EndChangeCheck())
            {
                stateMachine.RememberLastState = rememberLast;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Default state selector
            EditorGUILayout.LabelField("Default State", stateMachine.DefaultStateId ?? "None");

            // Child count
            EditorGUILayout.LabelField("Child States", stateMachine.ChildNodeIds.Count.ToString());
        }

        private void DrawActionSection(HfsmNodeBase node)
        {
            if (!(node is HfsmStateNode stateNode))
                return;

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Entry actions
            EditorGUILayout.LabelField("On Enter", EditorStyles.miniLabel);
            DrawActionList(stateNode.EntryActionMethodNames, "Entry Actions");

            // Logic actions
            EditorGUILayout.LabelField("On Logic", EditorStyles.miniLabel);
            DrawActionList(stateNode.LogicActionMethodNames, "Logic Actions");

            // Exit actions
            EditorGUILayout.LabelField("On Exit", EditorStyles.miniLabel);
            DrawActionList(stateNode.ExitActionMethodNames, "Exit Actions");

            // Can exit methods
            if (stateNode.NeedsExitTime)
            {
                EditorGUILayout.LabelField("Can Exit", EditorStyles.miniLabel);
                DrawActionList(stateNode.CanExitMethodNames, "Can Exit Methods");
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
                EditorGUILayout.LabelField("(None)", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawEdgeInspector(HfsmTransitionEdge edge)
        {
            EditorGUILayout.LabelField("Transition Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Source and target
            var sourceNode = _context.GraphAsset.GetNodeById(edge.SourceNodeId);
            var targetNode = _context.GraphAsset.GetNodeById(edge.TargetNodeId);

            EditorGUILayout.LabelField("From", sourceNode?.DisplayName ?? "Unknown");
            EditorGUILayout.LabelField("To", targetNode?.DisplayName ?? "Unknown");

            EditorGUILayout.Space();

            // Priority
            EditorGUI.BeginChangeCheck();
            int priority = EditorGUILayout.IntField("Priority", edge.Priority);
            if (EditorGUI.EndChangeCheck())
            {
                edge.Priority = priority;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Force instantly
            EditorGUI.BeginChangeCheck();
            bool forceInstantly = EditorGUILayout.Toggle("Force Instantly", edge.ForceInstantly);
            if (EditorGUI.EndChangeCheck())
            {
                edge.ForceInstantly = forceInstantly;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Is exit transition
            EditorGUI.BeginChangeCheck();
            bool isExit = EditorGUILayout.Toggle("Exit Transition", edge.IsExitTransition);
            if (EditorGUI.EndChangeCheck())
            {
                edge.IsExitTransition = isExit;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUILayout.Space();

            // Conditions section
            DrawConditionSection(edge);

            EditorGUILayout.Space();

            // Delete button
            if (GUILayout.Button("Delete Transition"))
            {
                _context.DeleteEdge(edge);
            }
        }

        private void DrawConditionSection(HfsmTransitionEdge edge)
        {
            EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);

            // Condition combination mode
            EditorGUI.BeginChangeCheck();
            bool useAndLogic = EditorGUILayout.Toggle("Require All (AND)", edge.UseAndLogic);
            if (EditorGUI.EndChangeCheck())
            {
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
            if (EditorGUILayout.DropdownButton(new GUIContent("+ Add Condition"), FocusType.Passive))
            {
                ShowAddConditionMenu(edge);
            }

            if (conditions == null || conditions.Count == 0)
            {
                EditorGUILayout.LabelField("(Always transition)", EditorStyles.miniLabel);
            }
        }

        private void DrawConditionItem(HfsmTransitionEdge edge, HfsmTransitionCondition condition, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 使用 Dummy 创建可点击区域
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(condition.DisplayName, EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField(condition.GetDescription(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            // X 按钮删除 - 使用更明显的样式
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button(new GUIContent("X", "Delete condition"), EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(16)))
            {
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
                    menu.AddItem(new GUIContent("Delete Condition"), false, () =>
                    {
                        EditorUtility.SetDirty(_context.GraphAsset);
                        edge.RemoveCondition(condition);
                    });
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(2);
        }

        private void DrawConditionFields(HfsmTransitionEdge edge, HfsmTransitionCondition condition)
        {
            EditorGUI.indentLevel++;

            if (condition is HfsmParameterCondition paramCondition)
            {
                DrawParameterConditionFields(edge, paramCondition);
            }
            else if (condition is HfsmTimeElapsedCondition timeCondition)
            {
                DrawTimeElapsedConditionFields(edge, timeCondition);
            }
            else if (condition is HfsmBehaviorCompleteCondition behaviorCondition)
            {
                DrawBehaviorCompleteConditionFields(edge, behaviorCondition);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawParameterConditionFields(HfsmTransitionEdge edge, HfsmParameterCondition condition)
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
            selectedIndex = EditorGUILayout.Popup("Parameter", selectedIndex, parameterNames);
            if (EditorGUI.EndChangeCheck() && selectedIndex >= 0)
            {
                condition.ParameterName = parameterNames[selectedIndex];
                condition.ParameterType = parameters[selectedIndex].ParameterType;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Parameter type
            EditorGUI.BeginChangeCheck();
            HfsmParameterType paramType = (HfsmParameterType)EditorGUILayout.EnumPopup("Type", condition.ParameterType);
            if (EditorGUI.EndChangeCheck())
            {
                condition.ParameterType = paramType;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Operator (for numeric types)
            if (condition.ParameterType == HfsmParameterType.Float || condition.ParameterType == HfsmParameterType.Int)
            {
                EditorGUI.BeginChangeCheck();
                HfsmCompareOperator op = (HfsmCompareOperator)EditorGUILayout.EnumPopup("Operator", condition.Operator);
                if (EditorGUI.EndChangeCheck())
                {
                    condition.Operator = op;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }

            // Value based on type
            if (condition.ParameterType == HfsmParameterType.Bool)
            {
                EditorGUI.BeginChangeCheck();
                bool boolValue = EditorGUILayout.Toggle("Value", condition.BoolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    condition.BoolValue = boolValue;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }
            else if (condition.ParameterType == HfsmParameterType.Float)
            {
                EditorGUI.BeginChangeCheck();
                float floatValue = EditorGUILayout.FloatField("Value", condition.FloatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    condition.FloatValue = floatValue;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }
            else if (condition.ParameterType == HfsmParameterType.Int)
            {
                EditorGUI.BeginChangeCheck();
                int intValue = EditorGUILayout.IntField("Value", condition.IntValue);
                if (EditorGUI.EndChangeCheck())
                {
                    condition.IntValue = intValue;
                    EditorUtility.SetDirty(_context.GraphAsset);
                }
            }
            else if (condition.ParameterType == HfsmParameterType.Trigger)
            {
                EditorGUILayout.LabelField("Condition", "Trigger is set", EditorStyles.miniLabel);
            }
        }

        private void DrawTimeElapsedConditionFields(HfsmTransitionEdge edge, HfsmTimeElapsedCondition condition)
        {
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUILayout.FloatField("Duration (s)", condition.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                condition.Duration = duration;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            EditorGUI.BeginChangeCheck();
            HfsmCompareOperator op = (HfsmCompareOperator)EditorGUILayout.EnumPopup("Operator", condition.Operator);
            if (EditorGUI.EndChangeCheck())
            {
                condition.Operator = op;
                EditorUtility.SetDirty(_context.GraphAsset);
            }
        }

        private void DrawBehaviorCompleteConditionFields(HfsmTransitionEdge edge, HfsmBehaviorCompleteCondition condition)
        {
            EditorGUILayout.LabelField("Source", edge.SourceNodeId ?? "Self", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Condition", "All behaviors completed", EditorStyles.miniLabel);
        }

        private void ShowAddConditionMenu(HfsmTransitionEdge edge)
        {
            var menu = new GenericMenu();

            // Parameter conditions
            menu.AddItem(new GUIContent("Parameter/Bool Compare"), false, () => AddCondition(edge, new HfsmParameterCondition { ParameterType = HfsmParameterType.Bool }));
            menu.AddItem(new GUIContent("Parameter/Float Compare"), false, () => AddCondition(edge, new HfsmParameterCondition { ParameterType = HfsmParameterType.Float }));
            menu.AddItem(new GUIContent("Parameter/Int Compare"), false, () => AddCondition(edge, new HfsmParameterCondition { ParameterType = HfsmParameterType.Int }));
            menu.AddItem(new GUIContent("Parameter/Trigger"), false, () => AddCondition(edge, new HfsmParameterCondition { ParameterType = HfsmParameterType.Trigger }));

            menu.AddSeparator("");

            // Time condition
            menu.AddItem(new GUIContent("Time/Elapsed"), false, () => AddCondition(edge, new HfsmTimeElapsedCondition { SourceNodeId = edge.SourceNodeId, Duration = 1f }));

            // Behavior condition
            menu.AddItem(new GUIContent("Behavior Complete"), false, () => AddCondition(edge, new HfsmBehaviorCompleteCondition { SourceNodeId = edge.SourceNodeId }));

            menu.ShowAsContext();
        }

        private void AddCondition(HfsmTransitionEdge edge, HfsmTransitionCondition condition)
        {
            edge.AddCondition(condition);
            EditorUtility.SetDirty(_context.GraphAsset);
        }

        private void RemoveCondition(HfsmTransitionEdge edge, HfsmTransitionCondition condition)
        {
            edge.RemoveCondition(condition);
            EditorUtility.SetDirty(_context.GraphAsset);
        }
    }
}
