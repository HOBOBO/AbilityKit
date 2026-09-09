using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// 行为检查器 - 在状态检查器中绘制行为列表，支持拖拽重排序
    /// </summary>
    public class BehaviorInspector
    {
        private StateNode targetState;
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

        public BehaviorInspector(StateNode state, Action onDirty)
        {
            this.targetState = state;
            this.onDirty = onDirty;
        }

        public void Draw()
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("行为", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Help button
            if (GUILayout.Button("?", GUILayout.Width(20)))
            {
                ShowHelpMenu();
            }
            EditorGUILayout.EndHorizontal();

            if (targetState.BehaviorItems == null || targetState.BehaviorItems.Count == 0)
            {
                targetState.InitializeBehaviorItems(new List<BehaviorItem>());
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
                EditorGUILayout.HelpBox("暂无行为。单击“添加行为”创建行为，可通过拖拽调整顺序。", MessageType.Info);
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

        private List<BehaviorItem> GetRootItems()
        {
            var roots = new List<BehaviorItem>();
            foreach (var item in targetState.BehaviorItems)
            {
                if (string.IsNullOrEmpty(item.parentId))
                {
                    roots.Add(item);
                }
            }
            return roots;
        }

        private void DrawBehaviorItemRecursive(BehaviorItem item, int depth, bool isLast)
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
            DrawBehaviorIcon(item.TypeName);

            // Display name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(GetVisibleDisplayName(item), GUILayout.Width(80));
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
            if (GUILayout.Button(new GUIContent("X", "删除行为"), EditorStyles.miniButton, GUILayout.Width(20)))
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
                if (GUILayout.Button("+ 添加子行为", EditorStyles.miniButton))
                {
                    ShowAddChildMenu(item);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDragHandle(BehaviorItem item)
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

        private void DrawBehaviorIcon(string typeName)
        {
            Color iconColor = GetBehaviorColor(typeName);

            Rect iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
            EditorGUI.DrawRect(iconRect, iconColor);

            // Draw icon text
            string iconChar = GetBehaviorIconChar(typeName);
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

        private Color GetBehaviorColor(string typeName)
        {
            return typeName switch
            {
                "Wait" => new Color(0.3f, 0.7f, 0.3f),
                "Log" => new Color(0.5f, 0.5f, 0.5f),
                "SetFloat" or "SetBool" or "SetInt" => new Color(0.2f, 0.5f, 0.8f),
                "PlayAnimation" => new Color(0.8f, 0.4f, 0.2f),
                "SetActive" or "MoveTo" => new Color(0.6f, 0.3f, 0.6f),
                "Sequence" or "RandomSequence" => new Color(0.2f, 0.6f, 0.8f),
                "Selector" or "RandomSelector" => new Color(0.8f, 0.6f, 0.2f),
                "Parallel" => new Color(0.4f, 0.4f, 0.8f),
                "Repeat" or "UntilSuccess" or "UntilFailure" => new Color(0.6f, 0.6f, 0.3f),
                "Invert" => new Color(0.5f, 0.2f, 0.5f),
                "TimeLimit" or "Cooldown" => new Color(0.4f, 0.6f, 0.4f),
                "If" => new Color(0.7f, 0.5f, 0.3f),
                _ => Color.gray
            };
        }

        private string GetBehaviorIconChar(string typeName)
        {
            return typeName switch
            {
                "Wait" => "W",
                "Log" => "L",
                "SetFloat" or "SetBool" or "SetInt" => "S",
                "PlayAnimation" => "A",
                "Sequence" => ">",
                "Selector" => "?",
                "Parallel" => "&",
                "Repeat" => "R",
                "Invert" => "!",
                "MoveTo" => "M",
                _ => ""
            };
        }

        private void DrawBehaviorParameters(BehaviorItem item)
        {
            foreach (var param in item.parameters)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(GetParameterDisplayName(item.TypeName, param.name), GUILayout.Width(80));

                switch (param.ValueType)
                {
                    case BehaviorParameterType.Float:
                        EditorGUI.BeginChangeCheck();
                        float f = EditorGUILayout.FloatField(param.floatValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.floatValue = f;
                        }
                        break;

                    case BehaviorParameterType.Int:
                        EditorGUI.BeginChangeCheck();
                        int i = EditorGUILayout.IntField(param.intValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.intValue = i;
                        }
                        break;

                    case BehaviorParameterType.Bool:
                        EditorGUI.BeginChangeCheck();
                        bool b = EditorGUILayout.Toggle(param.boolValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.boolValue = b;
                        }
                        break;

                    case BehaviorParameterType.String:
                        EditorGUI.BeginChangeCheck();
                        string s = EditorGUILayout.TextField(param.stringValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.stringValue = s;
                        }
                        break;

                    case BehaviorParameterType.Object:
                        EditorGUI.BeginChangeCheck();
                        UnityEngine.Object obj = EditorGUILayout.ObjectField(param.objectValue, typeof(UnityEngine.Object), true);
                        if (EditorGUI.EndChangeCheck())
                        {
                            param.objectValue = obj;
                        }
                        break;

                    case BehaviorParameterType.Vector3:
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

        private string GetDetailedDescription(BehaviorItem item)
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

        private string GetChildBriefInfo(BehaviorItem child)
        {
            // 显示子行为的简短信息
            string shortType = GetShortTypeName(child.TypeName);
            string customName = child.displayName;

            // 如果有自定义名称且与类型默认名不同，显示自定义名
            if (!string.IsNullOrEmpty(customName) && !IsDefaultDisplayName(child.TypeName, customName))
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

        private string GetParameterBrief(BehaviorItem item)
        {
            switch (item.TypeName)
            {
                case "Wait":
                    return $"{item.GetParamValue<float>("duration")}秒";
                case "Log":
                    string msg = item.GetParamValue<string>("message");
                    if (msg.Length > 15) msg = msg.Substring(0, 12) + "...";
                    return $"\"{msg}\"";
                case "SetFloat":
                case "SetBool":
                case "SetInt":
                    string varName = item.GetParamValue<string>("variableName");
                    if (string.IsNullOrEmpty(varName)) varName = "?";
                    return varName;
                case "PlayAnimation":
                    return item.GetParamValue<string>("stateName") ?? "?";
                case "Repeat":
                    int count = item.GetParamValue<int>("count");
                    return count < 0 ? "无限" : count.ToString();
                case "TimeLimit":
                    return $"{item.GetParamValue<float>("timeLimit")}秒";
                case "Cooldown":
                    return $"{item.GetParamValue<float>("cooldownDuration")}秒";
                case "If":
                    return "?";
                case "Sequence":
                case "Selector":
                case "Parallel":
                case "RandomSelector":
                case "RandomSequence":
                    return $"[{item.childIds.Count}]";
                default:
                    return "";
            }
        }

        private string GetParameterDescription(BehaviorItem item)
        {
            switch (item.TypeName)
            {
                case "Wait":
                    return $"持续时间：{item.GetParamValue<float>("duration")} 秒";
                case "Log":
                    string msg = item.GetParamValue<string>("message");
                    if (string.IsNullOrEmpty(msg)) return "消息：（空）";
                    if (msg.Length > 30) msg = msg.Substring(0, 27) + "...";
                    return $"消息：\"{msg}\"";
                case "SetFloat":
                    return $"变量：{item.GetParamValue<string>("variableName")}，值：{item.GetParamValue<float>("value")}";
                case "SetBool":
                    return $"变量：{item.GetParamValue<string>("variableName")}，值：{(item.GetParamValue<bool>("value") ? "真" : "假")}";
                case "SetInt":
                    return $"变量：{item.GetParamValue<string>("variableName")}，值：{item.GetParamValue<int>("value")}";
                case "PlayAnimation":
                    return $"状态：{item.GetParamValue<string>("stateName")}，交叉淡化：{item.GetParamValue<float>("crossFadeDuration")} 秒";
                case "SetActive":
                    bool active = item.GetParamValue<bool>("active");
                    return active ? "设为激活" : "设为未激活";
                case "MoveTo":
                    var dest = item.GetParamValue<UnityEngine.Vector3>("destination");
                    return $"目标（{dest.x:F1}, {dest.y:F1}, {dest.z:F1}），速度 {item.GetParamValue<float>("speed")} 米/秒";
                case "Repeat":
                    int count = item.GetParamValue<int>("count");
                    return count < 0 ? "重复：无限" : $"重复：{count} 次";
                case "TimeLimit":
                    return $"时间限制：{item.GetParamValue<float>("timeLimit")} 秒";
                case "Cooldown":
                    return $"冷却时间：{item.GetParamValue<float>("cooldownDuration")} 秒";
                case "Invert":
                    return "反转执行结果";
                case "UntilSuccess":
                    return "重复直到成功";
                case "UntilFailure":
                    return "重复直到失败";
                case "If":
                    return "条件分支";
                case "Sequence":
                    return "按顺序执行子行为";
                case "Selector":
                    return "执行子行为直到其中一个成功";
                case "Parallel":
                    return "并行执行所有子行为";
                case "RandomSelector":
                    return "随机选择子行为执行";
                case "RandomSequence":
                    return "以随机顺序执行子行为";
                default:
                    return "";
            }
        }

        private string GetShortTypeName(string typeName)
        {
            return GetDefaultDisplayName(typeName);
        }

        private string GetDefaultDisplayName(string typeName)
        {
            if (!BehaviorTypeRegistry.IsInitialized)
                BehaviorTypeRegistry.Initialize();
            return BehaviorTypeRegistry.GetDefinition(typeName)?.displayName ?? typeName;
        }

        private string GetVisibleDisplayName(BehaviorItem item)
        {
            return IsDefaultDisplayName(item.TypeName, item.displayName)
                ? GetDefaultDisplayName(item.TypeName)
                : item.displayName;
        }

        private bool IsDefaultDisplayName(string typeName, string displayName)
        {
            if (displayName == GetDefaultDisplayName(typeName) || displayName == typeName)
                return true;
            return typeName switch
            {
                "SetFloat" => displayName == "Set Float",
                "SetBool" => displayName == "Set Bool",
                "SetInt" => displayName == "Set Int",
                "PlayAnimation" => displayName == "Play Animation",
                "SetActive" => displayName == "Set Active",
                "MoveTo" => displayName == "Move To",
                "RandomSelector" => displayName == "Random Selector",
                "RandomSequence" => displayName == "Random Sequence",
                "TimeLimit" => displayName == "Time Limit",
                "UntilSuccess" => displayName == "Until Success",
                "UntilFailure" => displayName == "Until Failure",
                _ => false
            };
        }

        private static string GetParameterDisplayName(string typeName, string parameterName)
        {
            if (!BehaviorTypeRegistry.IsInitialized)
                BehaviorTypeRegistry.Initialize();
            var definition = BehaviorTypeRegistry.GetDefinition(typeName);
            if (definition != null)
            {
                foreach (var parameter in definition.parameters)
                    if (parameter.name == parameterName)
                        return parameter.displayName;
            }
            return parameterName;
        }

        private List<BehaviorItem> GetChildren(BehaviorItem parent)
        {
            var children = new List<BehaviorItem>();
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

            if (GUILayout.Button("+ 添加行为", GUILayout.Width(120)))
            {
                ShowAddRootMenu();
            }

            if (GUILayout.Button("+ 添加序列", GUILayout.Width(100)))
            {
                AddBehavior("Sequence", null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowHelpMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("关于行为系统"), false, ShowAbout);
            menu.AddItem(new GUIContent("行为类型说明"), false, ShowBehaviorTypes);
            menu.ShowAsContext();
        }

        private void ShowAbout()
        {
            EditorUtility.DisplayDialog("HFSM 行为系统",
                "HFSM 行为系统 v1.0\n\n" +
                "使用可视化节点编辑器构建复杂行为。\n\n" +
                "功能：\n" +
                "- 基础行为（等待、日志、设置变量）\n" +
                "- 复合行为（序列、选择器、并行）\n" +
                "- 修饰行为（重复、反转、时间限制）\n\n" +
                "拖拽行为可调整顺序，拖拽手柄位于每项左侧。",
                "确定");
        }

        private void ShowBehaviorTypes()
        {
            EditorUtility.DisplayDialog("行为类型说明",
                "基础行为：\n" +
                "- 等待：等待指定时长\n" +
                "- 日志：输出一条消息\n" +
                "- 设置浮点/布尔/整数：设置变量值\n" +
                "- 播放动画：播放 Animator 状态\n" +
                "- 设置激活：启用或禁用 GameObject\n" +
                "- 移动至：将 Transform 移动到指定位置\n\n" +
                "复合行为：\n" +
                "- 序列：按顺序执行子行为\n" +
                "- 选择器：执行到某个子行为成功为止\n" +
                "- 并行：执行所有子行为\n" +
                "- 随机选择器：随机选择子行为\n" +
                "- 随机序列：以随机顺序执行\n\n" +
                "修饰行为：\n" +
                "- 重复：将子行为重复指定次数\n" +
                "- 反转：反转子行为结果\n" +
                "- 时间限制：限制执行时间\n" +
                "- 直到成功：重复执行直到成功\n" +
                "- 直到失败：重复执行直到失败\n" +
                "- 冷却：在两次执行之间等待",
                "确定");
        }

        private void ShowAddRootMenu()
        {
            var menu = new GenericMenu();
            PopulateBehaviorMenu(menu, null);
            menu.ShowAsContext();
        }

        private void ShowAddChildMenu(BehaviorItem parent)
        {
            var menu = new GenericMenu();
            PopulateBehaviorMenu(menu, parent);
            menu.ShowAsContext();
        }

        private void PopulateBehaviorMenu(GenericMenu menu, BehaviorItem parent)
        {
            if (!BehaviorTypeRegistry.IsInitialized)
                BehaviorTypeRegistry.Initialize();

            foreach (var definition in BehaviorTypeRegistry.AllTypes)
            {
                var typeName = definition.typeName;
                var category = definition.category switch
                {
                    BehaviorCategory.Primitive => "基础行为",
                    BehaviorCategory.Composite => "复合行为",
                    BehaviorCategory.Decorator => "修饰器",
                    _ => definition.categoryName
                };
                var path = $"{category}/{definition.displayName}";
                menu.AddItem(new GUIContent(path), false, () => AddBehavior(typeName, parent));
            }
        }

        private void AddBehavior(string typeName, BehaviorItem parent)
        {
            if (targetState.BehaviorItems == null || targetState.BehaviorItems.Count == 0)
            {
                targetState.InitializeBehaviorItems(new List<BehaviorItem>());
            }

            var newItem = new BehaviorItem(typeName);

            if (parent != null)
            {
                newItem.parentId = parent.id;
                parent.childIds.Add(newItem.id);
                parent.isExpanded = true;
            }

            targetState.BehaviorItemsInternal.Add(newItem);
            onDirty?.Invoke();
        }

        private void DeleteBehaviorItem(BehaviorItem item)
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
