using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityHFSM;
using UnityHFSM.Graph;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// 行为检查器 - 在状态检查器中绘制行为列表，支持拖拽重排序
    /// </summary>
    public class HfsmBehaviorInspector
    {
        private HfsmStateNode targetState;
        private Action onDirty;

        private const float INDENT_WIDTH = 15f;
        private const float ITEM_HEIGHT = 22f;
        private const float DRAG_HANDLE_WIDTH = 12f;

        // Drag and drop state
        private string draggedItemId;
        private string dropTargetId;
        private bool isDraggingOverChild;
        private Vector2 lastMousePosition;

        // Scroll position for behavior list
        private Vector2 scrollPosition;

        public HfsmBehaviorInspector(HfsmStateNode state, Action onDirty)
        {
            this.targetState = state;
            this.onDirty = onDirty;
        }

        public void Draw()
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Behaviors", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Help button
            if (GUILayout.Button("?", GUILayout.Width(20)))
            {
                ShowHelpMenu();
            }
            EditorGUILayout.EndHorizontal();

            if (targetState.BehaviorItems == null || targetState.BehaviorItems.Count == 0)
            {
                targetState.InitializeBehaviorItems(new List<HfsmBehaviorItem>());
            }

            // Draw behavior list with scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Mathf.Min(targetState.BehaviorItems.Count * 25 + 60, 300)));

            DrawBehaviorList();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Add button
            DrawAddButton();

            // Handle drag and drop
            HandleDragAndDrop();
        }

        private void DrawBehaviorList()
        {
            if (targetState.BehaviorItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No behaviors. Click '+ Add' to add one.\nDrag behaviors to reorder.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Find root behavior items (items without a parent)
            var rootItems = GetRootItems();

            for (int i = 0; i < rootItems.Count; i++)
            {
                DrawBehaviorItemRecursive(rootItems[i], 0, i == rootItems.Count - 1);
            }

            EditorGUILayout.EndVertical();
        }

        private List<HfsmBehaviorItem> GetRootItems()
        {
            var roots = new List<HfsmBehaviorItem>();
            foreach (var item in targetState.BehaviorItems)
            {
                if (string.IsNullOrEmpty(item.parentId))
                {
                    roots.Add(item);
                }
            }
            return roots;
        }

        private void DrawBehaviorItemRecursive(HfsmBehaviorItem item, int depth, bool isLast)
        {
            bool isDragTarget = dropTargetId == item.id;
            bool isDragged = draggedItemId == item.id;

            // Highlight drop target
            if (isDragTarget && !isDraggingOverChild)
            {
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f, 0.3f);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;
            }
            else if (isDragged)
            {
                GUI.color = new Color(1, 1, 1, 0.5f);
                EditorGUILayout.BeginHorizontal();
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
            }

            // Draw drag handle
            DrawDragHandle(item);

            // Indent
            GUILayout.Space(depth * INDENT_WIDTH + 5);

            // Expand/collapse button
            if (item.IsComposite || item.IsDecorator)
            {
                GUI.enabled = true;
                EditorGUI.BeginChangeCheck();
                bool expanded = EditorGUILayout.Foldout(item.isExpanded, "");
                if (EditorGUI.EndChangeCheck())
                {
                    item.isExpanded = expanded;
                }
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Space(15);
            }

            // Behavior type icon
            DrawBehaviorIcon(item.Type);

            // Display name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(item.displayName, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                item.displayName = newName;
            }

            // Description
            string desc = GetDetailedDescription(item);
            if (!string.IsNullOrEmpty(desc))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                EditorGUILayout.LabelField(desc, EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            // Drag indicator when this is a drop target
            if (isDragTarget && !isDraggingOverChild)
            {
                GUI.color = new Color(0.3f, 0.8f, 0.3f);
                GUILayout.Label("▼", GUILayout.Width(15));
                GUI.color = Color.white;
            }

            // Delete button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button(new GUIContent("X", "Delete"), EditorStyles.miniButton, GUILayout.Width(20)))
            {
                DeleteBehaviorItem(item);
                EditorGUILayout.EndHorizontal();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Draw parameters
            if (item.isExpanded && !item.IsComposite && !item.IsDecorator)
            {
                GUILayout.Space(2);
                EditorGUI.indentLevel++;
                DrawBehaviorParameters(item);
                EditorGUI.indentLevel--;
                GUILayout.Space(2);
            }

            // Draw children
            if ((item.IsComposite || item.IsDecorator) && item.isExpanded)
            {
                EditorGUI.indentLevel++;
                var children = GetChildren(item);
                for (int i = 0; i < children.Count; i++)
                {
                    DrawBehaviorItemRecursive(children[i], depth + 1, i == children.Count - 1);
                }
                EditorGUI.indentLevel--;

                // Add child button
                GUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space((depth + 2) * INDENT_WIDTH);
                if (GUILayout.Button("+ Add Child", EditorStyles.miniButton))
                {
                    ShowAddChildMenu(item);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDragHandle(HfsmBehaviorItem item)
        {
            Rect handleRect = GUILayoutUtility.GetRect(DRAG_HANDLE_WIDTH, ITEM_HEIGHT, GUILayout.Width(DRAG_HANDLE_WIDTH));

            // Draw grip lines
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            float y = handleRect.y + 6;
            for (int i = 0; i < 3; i++)
            {
                float x = handleRect.x + 3;
                GUI.Label(new Rect(x, y + i * 4, 6, 2), "___");
            }
            GUI.color = Color.white;

            // Handle drag detection
            if (Event.current.type == EventType.MouseDown && handleRect.Contains(Event.current.mousePosition))
            {
                draggedItemId = item.id;
                dropTargetId = null;
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                Event.current.Use();
            }
        }

        private void HandleDragAndDrop()
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDrag)
            {
                if (!string.IsNullOrEmpty(draggedItemId) && GUIUtility.hotControl != 0)
                {
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                if (!string.IsNullOrEmpty(draggedItemId) && !string.IsNullOrEmpty(dropTargetId))
                {
                    // Perform the drop
                    PerformDrop(draggedItemId, dropTargetId, isDraggingOverChild);
                }

                draggedItemId = null;
                dropTargetId = null;
                isDraggingOverChild = false;
                GUIUtility.hotControl = 0;
                e.Use();
            }
            else if (e.type == EventType.Repaint)
            {
                if (!string.IsNullOrEmpty(draggedItemId))
                {
                    // Check if we're over any behavior item
                    dropTargetId = null;
                    isDraggingOverChild = false;
                    // This is simplified - in a real implementation, you'd raycast to find the target
                }
            }
        }

        private void PerformDrop(string draggedId, string targetId, bool asChild)
        {
            var draggedItem = targetState.GetBehaviorItem(draggedId);
            if (draggedItem == null)
                return;

            // Don't drop on self or own child
            if (draggedId == targetId || IsChildOf(draggedId, targetId))
                return;

            var targetItem = targetState.GetBehaviorItem(targetId);
            if (targetItem == null)
                return;

            // Remove from old parent
            if (!string.IsNullOrEmpty(draggedItem.parentId))
            {
                var oldParent = targetState.GetBehaviorItem(draggedItem.parentId);
                oldParent?.childIds.Remove(draggedId);
            }

            if (asChild && (targetItem.IsComposite || targetItem.IsDecorator))
            {
                // Add as child
                draggedItem.parentId = targetId;
                targetItem.childIds.Add(draggedId);
                targetItem.isExpanded = true;
            }
            else
            {
                // Add as sibling (insert before target)
                draggedItem.parentId = targetItem.parentId;

                var parent = string.IsNullOrEmpty(targetItem.parentId) ? null :
                    targetState.GetBehaviorItem(targetItem.parentId);

                if (parent != null)
                {
                    int index = parent.childIds.IndexOf(targetId);
                    if (index >= 0)
                    {
                        parent.childIds.Insert(index, draggedId);
                    }
                    else
                    {
                        parent.childIds.Add(draggedId);
                    }
                }
            }

            onDirty?.Invoke();
        }

        private bool IsChildOf(string itemId, string potentialParentId)
        {
            var item = targetState.GetBehaviorItem(itemId);
            if (item == null)
                return false;

            while (!string.IsNullOrEmpty(item.parentId))
            {
                if (item.parentId == potentialParentId)
                    return true;
                item = targetState.GetBehaviorItem(item.parentId);
            }

            return false;
        }

        private void DrawBehaviorIcon(HfsmBehaviorType type)
        {
            Color iconColor = GetBehaviorColor(type);

            Rect iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
            EditorGUI.DrawRect(iconRect, iconColor);

            // Draw icon text
            string iconChar = GetBehaviorIconChar(type);
            if (!string.IsNullOrEmpty(iconChar))
            {
                GUI.color = Color.white;
                GUI.Label(iconRect, iconChar, new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    fontStyle = FontStyle.Bold
                });
                GUI.color = Color.white;
            }
        }

        private Color GetBehaviorColor(HfsmBehaviorType type)
        {
            return type switch
            {
                HfsmBehaviorType.Wait => new Color(0.3f, 0.7f, 0.3f),
                HfsmBehaviorType.Log => new Color(0.5f, 0.5f, 0.5f),
                HfsmBehaviorType.SetFloat or HfsmBehaviorType.SetBool or HfsmBehaviorType.SetInt => new Color(0.2f, 0.5f, 0.8f),
                HfsmBehaviorType.PlayAnimation => new Color(0.8f, 0.4f, 0.2f),
                HfsmBehaviorType.SetActive or HfsmBehaviorType.MoveTo => new Color(0.6f, 0.3f, 0.6f),
                HfsmBehaviorType.Sequence or HfsmBehaviorType.RandomSequence => new Color(0.2f, 0.6f, 0.8f),
                HfsmBehaviorType.Selector or HfsmBehaviorType.RandomSelector => new Color(0.8f, 0.6f, 0.2f),
                HfsmBehaviorType.Parallel => new Color(0.4f, 0.4f, 0.8f),
                HfsmBehaviorType.Repeat or HfsmBehaviorType.UntilSuccess or HfsmBehaviorType.UntilFailure => new Color(0.6f, 0.6f, 0.3f),
                HfsmBehaviorType.Invert => new Color(0.5f, 0.2f, 0.5f),
                HfsmBehaviorType.TimeLimit or HfsmBehaviorType.Cooldown => new Color(0.4f, 0.6f, 0.4f),
                HfsmBehaviorType.If => new Color(0.7f, 0.5f, 0.3f),
                _ => Color.gray
            };
        }

        private string GetBehaviorIconChar(HfsmBehaviorType type)
        {
            return type switch
            {
                HfsmBehaviorType.Wait => "W",
                HfsmBehaviorType.Log => "L",
                HfsmBehaviorType.SetFloat or HfsmBehaviorType.SetBool or HfsmBehaviorType.SetInt => "S",
                HfsmBehaviorType.PlayAnimation => "A",
                HfsmBehaviorType.Sequence => ">",
                HfsmBehaviorType.Selector => "?",
                HfsmBehaviorType.Parallel => "&",
                HfsmBehaviorType.Repeat => "R",
                HfsmBehaviorType.Invert => "!",
                HfsmBehaviorType.MoveTo => "M",
                _ => ""
            };
        }

        private void DrawBehaviorParameters(HfsmBehaviorItem item)
        {
            foreach (var param in item.parameters)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(param.name, GUILayout.Width(80));

                switch (param.ValueType)
                {
                    case HfsmBehaviorParameterType.Float:
                        EditorGUI.BeginChangeCheck();
                        float f = EditorGUILayout.FloatField(param.floatValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.floatValue = f;
                        }
                        break;

                    case HfsmBehaviorParameterType.Int:
                        EditorGUI.BeginChangeCheck();
                        int i = EditorGUILayout.IntField(param.intValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.intValue = i;
                        }
                        break;

                    case HfsmBehaviorParameterType.Bool:
                        EditorGUI.BeginChangeCheck();
                        bool b = EditorGUILayout.Toggle(param.boolValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.boolValue = b;
                        }
                        break;

                    case HfsmBehaviorParameterType.String:
                        EditorGUI.BeginChangeCheck();
                        string s = EditorGUILayout.TextField(param.stringValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.stringValue = s;
                        }
                        break;

                    case HfsmBehaviorParameterType.Object:
                        EditorGUI.BeginChangeCheck();
                        UnityEngine.Object obj = EditorGUILayout.ObjectField(param.objectValue, typeof(UnityEngine.Object), true);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.objectValue = obj;
                        }
                        break;

                    case HfsmBehaviorParameterType.Vector3:
                        EditorGUI.BeginChangeCheck();
                        Vector3 v3 = EditorGUILayout.Vector3Field("", param.vector3Value);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.vector3Value = v3;
                        }
                        break;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private string GetDetailedDescription(HfsmBehaviorItem item)
        {
            // 对于复合行为，显示子行为名称
            if (item.IsComposite && item.childIds.Count > 0)
            {
                var childNames = new List<string>();
                foreach (var childId in item.childIds)
                {
                    var child = targetState.GetBehaviorItem(childId);
                    if (child != null)
                    {
                        // 简短显示：类型名或自定义名
                        string childInfo = GetChildBriefInfo(child);
                        childNames.Add(childInfo);
                    }
                }

                if (childNames.Count > 0)
                {
                    return "=> " + string.Join(", ", childNames);
                }
            }

            // 对于修饰器行为，如果有子行为也显示
            if (item.IsDecorator && item.childIds.Count > 0)
            {
                var child = targetState.GetBehaviorItem(item.childIds[0]);
                if (child != null)
                {
                    return "=> " + GetChildBriefInfo(child);
                }
            }

            // 对于原子行为，显示具体参数
            string paramDesc = GetParameterDescription(item);
            if (!string.IsNullOrEmpty(paramDesc))
            {
                return paramDesc;
            }

            // 回退到默认描述
            string defaultDesc = item.GetDescription();
            return (defaultDesc != item.displayName) ? defaultDesc : "";
        }

        private string GetChildBriefInfo(HfsmBehaviorItem child)
        {
            // 显示子行为的简短信息
            string shortType = GetShortTypeName(child.Type);
            string customName = child.displayName;

            // 如果有自定义名称且与类型默认名不同，显示自定义名
            if (!string.IsNullOrEmpty(customName) && customName != GetDefaultDisplayName(child.Type))
            {
                // 尝试获取参数摘要
                string paramBrief = GetParameterBrief(child);
                if (!string.IsNullOrEmpty(paramBrief))
                {
                    return $"{shortType}({paramBrief})";
                }
                return customName;
            }

            // 否则显示类型名和关键参数
            string paramBrief2 = GetParameterBrief(child);
            if (!string.IsNullOrEmpty(paramBrief2))
            {
                return $"{shortType}({paramBrief2})";
            }

            return shortType;
        }

        private string GetParameterBrief(HfsmBehaviorItem item)
        {
            switch (item.Type)
            {
                case HfsmBehaviorType.Wait:
                    return $"{item.GetParamValue<float>("duration")}s";
                case HfsmBehaviorType.Log:
                    string msg = item.GetParamValue<string>("message");
                    if (msg.Length > 15) msg = msg.Substring(0, 12) + "...";
                    return $"\"{msg}\"";
                case HfsmBehaviorType.SetFloat:
                case HfsmBehaviorType.SetBool:
                case HfsmBehaviorType.SetInt:
                    string varName = item.GetParamValue<string>("variableName");
                    if (string.IsNullOrEmpty(varName)) varName = "?";
                    return varName;
                case HfsmBehaviorType.PlayAnimation:
                    return item.GetParamValue<string>("stateName") ?? "?";
                case HfsmBehaviorType.Repeat:
                    int count = item.GetParamValue<int>("count");
                    return count < 0 ? "inf" : count.ToString();
                case HfsmBehaviorType.TimeLimit:
                    return $"{item.GetParamValue<float>("timeLimit")}s";
                case HfsmBehaviorType.Cooldown:
                    return $"{item.GetParamValue<float>("cooldownDuration")}s";
                case HfsmBehaviorType.If:
                    return "?";
                case HfsmBehaviorType.Sequence:
                case HfsmBehaviorType.Selector:
                case HfsmBehaviorType.Parallel:
                case HfsmBehaviorType.RandomSelector:
                case HfsmBehaviorType.RandomSequence:
                    return $"[{item.childIds.Count}]";
                default:
                    return "";
            }
        }

        private string GetParameterDescription(HfsmBehaviorItem item)
        {
            switch (item.Type)
            {
                case HfsmBehaviorType.Wait:
                    return $"Duration: {item.GetParamValue<float>("duration")}s";
                case HfsmBehaviorType.Log:
                    string msg = item.GetParamValue<string>("message");
                    if (string.IsNullOrEmpty(msg)) return "Message: (empty)";
                    if (msg.Length > 30) msg = msg.Substring(0, 27) + "...";
                    return $"Message: \"{msg}\"";
                case HfsmBehaviorType.SetFloat:
                    return $"Var: {item.GetParamValue<string>("variableName")}, Value: {item.GetParamValue<float>("value")}";
                case HfsmBehaviorType.SetBool:
                    return $"Var: {item.GetParamValue<string>("variableName")}, Value: {item.GetParamValue<bool>("value")}";
                case HfsmBehaviorType.SetInt:
                    return $"Var: {item.GetParamValue<string>("variableName")}, Value: {item.GetParamValue<int>("value")}";
                case HfsmBehaviorType.PlayAnimation:
                    return $"State: {item.GetParamValue<string>("stateName")}, CrossFade: {item.GetParamValue<float>("crossFadeDuration")}s";
                case HfsmBehaviorType.SetActive:
                    bool active = item.GetParamValue<bool>("active");
                    return active ? "Set Active" : "Set Inactive";
                case HfsmBehaviorType.MoveTo:
                    var dest = item.GetParamValue<UnityEngine.Vector3>("destination");
                    return $"To ({dest.x:F1}, {dest.y:F1}, {dest.z:F1}) @ {item.GetParamValue<float>("speed")}m/s";
                case HfsmBehaviorType.Repeat:
                    int count = item.GetParamValue<int>("count");
                    return count < 0 ? "Repeat: Infinite" : $"Repeat: {count} times";
                case HfsmBehaviorType.TimeLimit:
                    return $"Time Limit: {item.GetParamValue<float>("timeLimit")}s";
                case HfsmBehaviorType.Cooldown:
                    return $"Cooldown: {item.GetParamValue<float>("cooldownDuration")}s";
                case HfsmBehaviorType.Invert:
                    return "Invert result";
                case HfsmBehaviorType.UntilSuccess:
                    return "Until Success";
                case HfsmBehaviorType.UntilFailure:
                    return "Until Failure";
                case HfsmBehaviorType.If:
                    return "Conditional branch";
                default:
                    return "";
            }
        }

        private string GetShortTypeName(HfsmBehaviorType type)
        {
            return type switch
            {
                HfsmBehaviorType.Sequence => "Seq",
                HfsmBehaviorType.Selector => "Sel",
                HfsmBehaviorType.Parallel => "Par",
                HfsmBehaviorType.RandomSelector => "RandSel",
                HfsmBehaviorType.RandomSequence => "RandSeq",
                HfsmBehaviorType.Repeat => "Repeat",
                HfsmBehaviorType.Invert => "Invert",
                HfsmBehaviorType.UntilSuccess => "UntilSucc",
                HfsmBehaviorType.UntilFailure => "UntilFail",
                HfsmBehaviorType.TimeLimit => "TimeLimit",
                HfsmBehaviorType.Cooldown => "Cooldown",
                HfsmBehaviorType.If => "If",
                HfsmBehaviorType.Wait => "Wait",
                HfsmBehaviorType.Log => "Log",
                HfsmBehaviorType.SetFloat => "SetF",
                HfsmBehaviorType.SetBool => "SetB",
                HfsmBehaviorType.SetInt => "SetI",
                HfsmBehaviorType.PlayAnimation => "Anim",
                HfsmBehaviorType.SetActive => "Active",
                HfsmBehaviorType.MoveTo => "MoveTo",
                _ => type.ToString()
            };
        }

        private string GetDefaultDisplayName(HfsmBehaviorType type)
        {
            return type switch
            {
                HfsmBehaviorType.Wait => "Wait",
                HfsmBehaviorType.WaitUntil => "Wait Until",
                HfsmBehaviorType.Log => "Log",
                HfsmBehaviorType.SetFloat => "Set Float",
                HfsmBehaviorType.SetBool => "Set Bool",
                HfsmBehaviorType.SetInt => "Set Int",
                HfsmBehaviorType.PlayAnimation => "Play Animation",
                HfsmBehaviorType.SetActive => "Set Active",
                HfsmBehaviorType.MoveTo => "Move To",
                HfsmBehaviorType.Sequence => "Sequence",
                HfsmBehaviorType.Selector => "Selector",
                HfsmBehaviorType.Parallel => "Parallel",
                HfsmBehaviorType.RandomSelector => "Random Selector",
                HfsmBehaviorType.RandomSequence => "Random Sequence",
                HfsmBehaviorType.Repeat => "Repeat",
                HfsmBehaviorType.Invert => "Invert",
                HfsmBehaviorType.TimeLimit => "Time Limit",
                HfsmBehaviorType.UntilSuccess => "Until Success",
                HfsmBehaviorType.UntilFailure => "Until Failure",
                HfsmBehaviorType.Cooldown => "Cooldown",
                HfsmBehaviorType.If => "If",
                _ => type.ToString()
            };
        }

        private List<HfsmBehaviorItem> GetChildren(HfsmBehaviorItem parent)
        {
            var children = new List<HfsmBehaviorItem>();
            foreach (var id in parent.childIds)
            {
                var child = targetState.GetBehaviorItem(id);
                if (child != null)
                {
                    children.Add(child);
                }
            }
            return children;
        }

        private void DrawAddButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Add Behavior", GUILayout.Width(120)))
            {
                ShowAddRootMenu();
            }

            if (GUILayout.Button("+ Add Sequence", GUILayout.Width(100)))
            {
                AddBehavior(HfsmBehaviorType.Sequence, null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowHelpMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("About Behaviors"), false, ShowAbout);
            menu.AddItem(new GUIContent("Behavior Types"), false, ShowBehaviorTypes);
            menu.ShowAsContext();
        }

        private void ShowAbout()
        {
            EditorUtility.DisplayDialog("HFSM Behavior System",
                "HFSM Behavior System v1.0\n\n" +
                "Build complex behaviors using a visual node-based editor.\n\n" +
                "Features:\n" +
                "- Primitive actions (Wait, Log, Set Variables)\n" +
                "- Composite actions (Sequence, Selector, Parallel)\n" +
                "- Decorator actions (Repeat, Invert, TimeLimit)\n\n" +
                "Drag behaviors to reorder. Use the drag handle on the left.",
                "OK");
        }

        private void ShowBehaviorTypes()
        {
            EditorUtility.DisplayDialog("Behavior Types",
                "Primitive Actions:\n" +
                "- Wait: Wait for specified duration\n" +
                "- Log: Output a message\n" +
                "- Set Float/Bool/Int: Set a variable\n" +
                "- Play Animation: Play an Animator state\n" +
                "- Set Active: Enable/disable GameObject\n" +
                "- Move To: Move transform to position\n\n" +
                "Composite Actions:\n" +
                "- Sequence: Execute children in order\n" +
                "- Selector: Execute until one succeeds\n" +
                "- Parallel: Execute all children\n" +
                "- Random Selector: Random child selection\n" +
                "- Random Sequence: Random order execution\n\n" +
                "Decorator Actions:\n" +
                "- Repeat: Repeat child N times\n" +
                "- Invert: Invert child result\n" +
                "- TimeLimit: Limit execution time\n" +
                "- Until Success: Repeat until success\n" +
                "- Until Failure: Repeat until failure\n" +
                "- Cooldown: Wait between executions",
                "OK");
        }

        private void ShowAddRootMenu()
        {
            var menu = new GenericMenu();

            // Primitive actions
            menu.AddItem(new GUIContent("Primitive/Wait"), false, () => AddBehavior(HfsmBehaviorType.Wait, null));
            menu.AddItem(new GUIContent("Primitive/Log"), false, () => AddBehavior(HfsmBehaviorType.Log, null));
            menu.AddItem(new GUIContent("Primitive/Set Float"), false, () => AddBehavior(HfsmBehaviorType.SetFloat, null));
            menu.AddItem(new GUIContent("Primitive/Set Bool"), false, () => AddBehavior(HfsmBehaviorType.SetBool, null));
            menu.AddItem(new GUIContent("Primitive/Set Int"), false, () => AddBehavior(HfsmBehaviorType.SetInt, null));
            menu.AddItem(new GUIContent("Primitive/Play Animation"), false, () => AddBehavior(HfsmBehaviorType.PlayAnimation, null));
            menu.AddItem(new GUIContent("Primitive/Set Active"), false, () => AddBehavior(HfsmBehaviorType.SetActive, null));
            menu.AddItem(new GUIContent("Primitive/Move To"), false, () => AddBehavior(HfsmBehaviorType.MoveTo, null));

            menu.AddSeparator("");

            // Composite actions
            menu.AddItem(new GUIContent("Composite/Sequence"), false, () => AddBehavior(HfsmBehaviorType.Sequence, null));
            menu.AddItem(new GUIContent("Composite/Selector"), false, () => AddBehavior(HfsmBehaviorType.Selector, null));
            menu.AddItem(new GUIContent("Composite/Parallel"), false, () => AddBehavior(HfsmBehaviorType.Parallel, null));
            menu.AddItem(new GUIContent("Composite/Random Selector"), false, () => AddBehavior(HfsmBehaviorType.RandomSelector, null));
            menu.AddItem(new GUIContent("Composite/Random Sequence"), false, () => AddBehavior(HfsmBehaviorType.RandomSequence, null));

            menu.AddSeparator("");

            // Decorator actions
            menu.AddItem(new GUIContent("Decorator/Repeat"), false, () => AddBehavior(HfsmBehaviorType.Repeat, null));
            menu.AddItem(new GUIContent("Decorator/Invert"), false, () => AddBehavior(HfsmBehaviorType.Invert, null));
            menu.AddItem(new GUIContent("Decorator/Time Limit"), false, () => AddBehavior(HfsmBehaviorType.TimeLimit, null));
            menu.AddItem(new GUIContent("Decorator/Until Success"), false, () => AddBehavior(HfsmBehaviorType.UntilSuccess, null));
            menu.AddItem(new GUIContent("Decorator/Until Failure"), false, () => AddBehavior(HfsmBehaviorType.UntilFailure, null));
            menu.AddItem(new GUIContent("Decorator/Cooldown"), false, () => AddBehavior(HfsmBehaviorType.Cooldown, null));
            menu.AddItem(new GUIContent("Decorator/If"), false, () => AddBehavior(HfsmBehaviorType.If, null));

            menu.ShowAsContext();
        }

        private void ShowAddChildMenu(HfsmBehaviorItem parent)
        {
            var menu = new GenericMenu();

            // Primitive actions
            menu.AddItem(new GUIContent("Primitive/Wait"), false, () => AddBehavior(HfsmBehaviorType.Wait, parent));
            menu.AddItem(new GUIContent("Primitive/Log"), false, () => AddBehavior(HfsmBehaviorType.Log, parent));
            menu.AddItem(new GUIContent("Primitive/Set Float"), false, () => AddBehavior(HfsmBehaviorType.SetFloat, parent));
            menu.AddItem(new GUIContent("Primitive/Set Bool"), false, () => AddBehavior(HfsmBehaviorType.SetBool, parent));
            menu.AddItem(new GUIContent("Primitive/Set Int"), false, () => AddBehavior(HfsmBehaviorType.SetInt, parent));
            menu.AddItem(new GUIContent("Primitive/Play Animation"), false, () => AddBehavior(HfsmBehaviorType.PlayAnimation, parent));
            menu.AddItem(new GUIContent("Primitive/Set Active"), false, () => AddBehavior(HfsmBehaviorType.SetActive, parent));
            menu.AddItem(new GUIContent("Primitive/Move To"), false, () => AddBehavior(HfsmBehaviorType.MoveTo, parent));

            menu.AddSeparator("");

            // Composite actions
            menu.AddItem(new GUIContent("Composite/Sequence"), false, () => AddBehavior(HfsmBehaviorType.Sequence, parent));
            menu.AddItem(new GUIContent("Composite/Selector"), false, () => AddBehavior(HfsmBehaviorType.Selector, parent));
            menu.AddItem(new GUIContent("Composite/Parallel"), false, () => AddBehavior(HfsmBehaviorType.Parallel, parent));
            menu.AddItem(new GUIContent("Composite/Random Selector"), false, () => AddBehavior(HfsmBehaviorType.RandomSelector, parent));
            menu.AddItem(new GUIContent("Composite/Random Sequence"), false, () => AddBehavior(HfsmBehaviorType.RandomSequence, parent));

            menu.AddSeparator("");

            // Decorator actions
            menu.AddItem(new GUIContent("Decorator/Repeat"), false, () => AddBehavior(HfsmBehaviorType.Repeat, parent));
            menu.AddItem(new GUIContent("Decorator/Invert"), false, () => AddBehavior(HfsmBehaviorType.Invert, parent));
            menu.AddItem(new GUIContent("Decorator/Time Limit"), false, () => AddBehavior(HfsmBehaviorType.TimeLimit, parent));
            menu.AddItem(new GUIContent("Decorator/Until Success"), false, () => AddBehavior(HfsmBehaviorType.UntilSuccess, parent));
            menu.AddItem(new GUIContent("Decorator/Until Failure"), false, () => AddBehavior(HfsmBehaviorType.UntilFailure, parent));
            menu.AddItem(new GUIContent("Decorator/Cooldown"), false, () => AddBehavior(HfsmBehaviorType.Cooldown, parent));
            menu.AddItem(new GUIContent("Decorator/If"), false, () => AddBehavior(HfsmBehaviorType.If, parent));

            menu.ShowAsContext();
        }

        private void AddBehavior(HfsmBehaviorType type, HfsmBehaviorItem parent)
        {
            if (targetState.BehaviorItems == null || targetState.BehaviorItems.Count == 0)
            {
                targetState.InitializeBehaviorItems(new List<HfsmBehaviorItem>());
            }

            var newItem = new HfsmBehaviorItem(type);

            if (parent != null)
            {
                newItem.parentId = parent.id;
                parent.childIds.Add(newItem.id);
                parent.isExpanded = true;
            }

            targetState.BehaviorItemsInternal.Add(newItem);
            onDirty?.Invoke();
        }

        private void DeleteBehaviorItem(HfsmBehaviorItem item)
        {
            // Recursively delete children
            var children = GetChildren(item);
            foreach (var child in children)
            {
                DeleteBehaviorItem(child);
            }

            // Remove from parent
            if (!string.IsNullOrEmpty(item.parentId))
            {
                var parent = targetState.GetBehaviorItem(item.parentId);
                parent?.childIds.Remove(item.id);
            }

            // Remove from list
            targetState.BehaviorItemsInternal.Remove(item);
            onDirty?.Invoke();
        }

        /// <summary>
        /// Handle context click
        /// </summary>
        public void HandleContextClick()
        {
            var e = Event.current;
            if (e.type == EventType.ContextClick)
            {
                // Can be extended for additional context menu options
            }
        }
    }
}
