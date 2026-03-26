using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityHFSM;
using UnityHFSM.Actions;
using UnityHFSM.Graph;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Layer that renders and handles state nodes.
    /// </summary>
    public class GraphStateLayer : GraphLayer
    {
        // State styles
        private static readonly Color DefaultColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color DefaultBorderColor = Color.gray;
        private static readonly Color SelectedColor = new Color(0.3f, 0.5f, 0.8f);
        private static readonly Color SelectedBorderColor = new Color(0.4f, 0.7f, 1f);
        private static readonly Color StateMachineColor = new Color(0.15f, 0.25f, 0.35f);
        private static readonly Color StateMachineBorderColor = new Color(0.3f, 0.5f, 0.7f);
        private static readonly Color DefaultStateColor = new Color(0.3f, 0.25f, 0.1f);
        private static readonly Color DefaultStateBorderColor = new Color(0.9f, 0.7f, 0.3f);
        private static readonly Color RunningColor = new Color(0.2f, 0.5f, 0.2f);
        private static readonly Color RunningBorderColor = Color.green;

        private const float NodeWidth = 150f;
        private const float NodeHeight = 50f;
        private const float CornerRadius = 6f;
        private const float DoubleClickTime = 0.3f;

        private float _lastClickTime = -1f;
        private HfsmNodeBase _lastClickedNode;

        public GraphStateLayer(EditorWindow editorWindow) : base(editorWindow)
        {
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            if (Event.current.type != EventType.Repaint)
                return;

            if (Context.GraphAsset == null)
                return;

            // Draw all nodes
            foreach (var node in Context.CurrentChildNodes)
            {
                DrawNode(node);
            }
        }

        public override void ProcessEvent()
        {
            HandleNodeClick();
            HandleNodeDrag();
            HandleContextMenu();
            HandleKeyInput();
        }

        private void DrawNode(HfsmNodeBase node)
        {
            Vector2 screenPos = ContentPosToScreen(node.Position);
            Rect screenRect = new Rect(screenPos, node.Size * Context.ZoomFactor);

            // Determine colors based on state
            Color backgroundColor = GetNodeBackgroundColor(node);
            Color borderColor = GetNodeBorderColor(node);
            bool isSelected = Context.IsSelected(node);

            // Draw background
            DrawNodeBackground(screenRect, backgroundColor, borderColor, isSelected);

            // Draw content
            DrawNodeContent(screenRect, node);

            // Draw state machine indicator
            if (node.NodeType == HfsmNodeType.StateMachine)
            {
                DrawStateMachineIndicator(screenRect);
            }

            // Draw default state indicator
            if (node.isDefault)
            {
                DrawDefaultIndicator(screenRect);
            }
        }

        private Color GetNodeBackgroundColor(HfsmNodeBase node)
        {
            if (node.NodeType == HfsmNodeType.StateMachine)
            {
                return StateMachineColor;
            }

            if (node.isDefault)
            {
                return DefaultStateColor;
            }

            return DefaultColor;
        }

        private Color GetNodeBorderColor(HfsmNodeBase node)
        {
            bool isSelected = Context.IsSelected(node);

            if (isSelected)
            {
                return SelectedBorderColor;
            }

            if (node.NodeType == HfsmNodeType.StateMachine)
            {
                return StateMachineBorderColor;
            }

            if (node.isDefault)
            {
                return DefaultStateBorderColor;
            }

            return DefaultBorderColor;
        }

        private void DrawNodeBackground(Rect rect, Color backgroundColor, Color borderColor, bool isSelected)
        {
            // Draw shadow
            Rect shadowRect = rect;
            shadowRect.x += 2;
            shadowRect.y += 2;
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.3f));

            // Draw background
            EditorGUI.DrawRect(rect, backgroundColor);

            // Draw border
            Rect borderRect = new Rect(rect.x, rect.y, rect.width, 1);
            EditorGUI.DrawRect(borderRect, borderColor);

            borderRect = new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1);
            EditorGUI.DrawRect(borderRect, borderColor);

            borderRect = new Rect(rect.x, rect.y, 1, rect.height);
            EditorGUI.DrawRect(borderRect, borderColor);

            borderRect = new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height);
            EditorGUI.DrawRect(borderRect, borderColor);

            // Draw selected highlight
            if (isSelected)
            {
                Rect highlightRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                DrawRoundedRect(highlightRect, 2, SelectedColor);
            }
        }

        private void DrawNodeContent(Rect rect, HfsmNodeBase node)
        {
            // Calculate text area
            Rect textRect = rect;
            textRect.x += 8 * Context.ZoomFactor;
            textRect.y += 4 * Context.ZoomFactor;
            textRect.width -= 16 * Context.ZoomFactor;
            textRect.height -= 8 * Context.ZoomFactor;

            // Draw node name
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(12 * Context.ZoomFactor),
                normal = { textColor = Color.white }
            };

            string displayName = node.DisplayName;
            if (displayName.Length > 15)
            {
                displayName = displayName.Substring(0, 12) + "...";
            }

            // Calculate height needed for behavior summary
            float baseHeight = labelStyle.fontSize + 4;
            string behaviorSummary = "";

            if (node is HfsmStateNode stateNode && stateNode.BehaviorItems.Count > 0)
            {
                behaviorSummary = GenerateBehaviorSummary(stateNode, Context.GraphAsset);
            }

            float summaryHeight = 0;
            if (!string.IsNullOrEmpty(behaviorSummary))
            {
                GUIStyle summaryStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = Mathf.RoundToInt(9 * Context.ZoomFactor),
                    wordWrap = true,
                    normal = { textColor = new Color(0.6f, 0.9f, 0.6f) }
                };
                summaryHeight = summaryStyle.CalcHeight(new GUIContent(behaviorSummary), textRect.width) + 2;
            }

            // Draw node name
            Rect nameRect = textRect;
            nameRect.height = baseHeight;
            GUI.Label(nameRect, displayName, labelStyle);

            // Draw behavior summary below name
            if (!string.IsNullOrEmpty(behaviorSummary))
            {
                GUIStyle summaryStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = Mathf.RoundToInt(9 * Context.ZoomFactor),
                    wordWrap = true,
                    normal = { textColor = new Color(0.6f, 0.9f, 0.6f) }
                };

                Rect summaryRect = textRect;
                summaryRect.y = nameRect.y + nameRect.height;
                summaryRect.height = summaryHeight;
                GUI.Label(summaryRect, behaviorSummary, summaryStyle);
            }

            // Draw node type label
            GUIStyle typeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(9 * Context.ZoomFactor),
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            Rect typeRect = textRect;
            float typeY = nameRect.y + nameRect.height + summaryHeight + 2;
            if (!string.IsNullOrEmpty(behaviorSummary))
            {
                Rect summaryRect = textRect;
                summaryRect.y = nameRect.y + nameRect.height;
                summaryRect.height = summaryHeight;
                typeY = Mathf.Max(typeY, summaryRect.y + summaryHeight + 2);
            }
            typeRect.y = typeY;
            typeRect.height = typeStyle.fontSize + 2;

            GUI.Label(typeRect, node.GetNodeTypeDescription(), typeStyle);
        }

        private string GenerateBehaviorSummary(HfsmStateNode stateNode, HfsmGraphAsset graph)
        {
            var items = stateNode.BehaviorItems;
            if (items.Count == 0)
                return "";

            // Get root items
            var rootItems = new List<UnityHFSM.HfsmBehaviorItem>();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.parentId))
                    rootItems.Add(item);
            }

            if (rootItems.Count == 0)
                return "";

            // Generate summary text
            var parts = new List<string>();
            foreach (var root in rootItems)
            {
                string behaviorInfo = GetBehaviorDisplayText(root, stateNode, 0);
                parts.Add(behaviorInfo);
            }

            string summary = string.Join(" > ", parts);
            if (summary.Length > 30)
            {
                summary = summary.Substring(0, 27) + "...";
            }
            return summary;
        }

        private string GetBehaviorDisplayText(UnityHFSM.HfsmBehaviorItem item, HfsmStateNode stateNode, int depth)
        {
            string typeName = GetBehaviorTypeShortName(item.Type);

            // 获取子行为
            var childItems = new List<UnityHFSM.HfsmBehaviorItem>();
            foreach (var childId in item.childIds)
            {
                var child = stateNode.GetBehaviorItem(childId);
                if (child != null)
                {
                    childItems.Add(child);
                }
            }

            // 对于复合行为，显示子行为的简短信息
            if (item.IsComposite && childItems.Count > 0)
            {
                var childNames = new List<string>();
                foreach (var child in childItems)
                {
                    childNames.Add(GetBehaviorDisplayText(child, stateNode, depth + 1));
                }

                if (childNames.Count <= 2)
                {
                    return $"{typeName}({string.Join(", ", childNames)})";
                }
                else
                {
                    return $"{typeName}({childNames[0]}, {childNames[1]}, ...[{childNames.Count - 2}])";
                }
            }

            // 对于修饰器行为，显示子行为信息
            if (item.IsDecorator && childItems.Count > 0)
            {
                string childInfo = GetBehaviorDisplayText(childItems[0], stateNode, depth + 1);
                return $"{typeName}({childInfo})";
            }

            // 对于原子行为，显示类型名和简短参数
            string paramBrief = GetBehaviorParamBrief(item);
            if (!string.IsNullOrEmpty(paramBrief))
            {
                return $"{typeName}({paramBrief})";
            }

            // 如果有自定义名称，显示名称
            if (!string.IsNullOrEmpty(item.displayName) && item.displayName != item.Type.ToString())
            {
                return item.displayName;
            }

            return typeName;
        }

        private string GetBehaviorParamBrief(UnityHFSM.HfsmBehaviorItem item)
        {
            switch (item.Type)
            {
                case UnityHFSM.HfsmBehaviorType.Wait:
                    return $"{item.GetParamValue<float>("duration")}s";
                case UnityHFSM.HfsmBehaviorType.Log:
                    string msg = item.GetParamValue<string>("message");
                    if (string.IsNullOrEmpty(msg)) return "(empty)";
                    if (msg.Length > 10) msg = msg.Substring(0, 7) + "...";
                    return $"\"{msg}\"";
                case UnityHFSM.HfsmBehaviorType.SetFloat:
                case UnityHFSM.HfsmBehaviorType.SetBool:
                case UnityHFSM.HfsmBehaviorType.SetInt:
                    string varName = item.GetParamValue<string>("variableName");
                    if (string.IsNullOrEmpty(varName)) return "?";
                    return varName;
                case UnityHFSM.HfsmBehaviorType.PlayAnimation:
                    string stateName = item.GetParamValue<string>("stateName");
                    if (string.IsNullOrEmpty(stateName)) return "?";
                    if (stateName.Length > 8) stateName = stateName.Substring(0, 5) + "...";
                    return stateName;
                case UnityHFSM.HfsmBehaviorType.Repeat:
                    int count = item.GetParamValue<int>("count");
                    return count < 0 ? "inf" : count.ToString();
                case UnityHFSM.HfsmBehaviorType.TimeLimit:
                    return $"{item.GetParamValue<float>("timeLimit")}s";
                case UnityHFSM.HfsmBehaviorType.Cooldown:
                    return $"{item.GetParamValue<float>("cooldownDuration")}s";
                case UnityHFSM.HfsmBehaviorType.SetActive:
                    bool active = item.GetParamValue<bool>("active");
                    return active ? "ON" : "OFF";
                default:
                    return "";
            }
        }

        private string GetBehaviorTypeShortName(HfsmBehaviorType type)
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
                HfsmBehaviorType.WaitUntil => "WaitUntil",
                HfsmBehaviorType.Log => "Log",
                HfsmBehaviorType.SetBool => "SetBool",
                HfsmBehaviorType.SetInt => "SetInt",
                HfsmBehaviorType.SetFloat => "SetFloat",
                HfsmBehaviorType.PlayAnimation => "Anim",
                HfsmBehaviorType.SetActive => "SetActive",
                HfsmBehaviorType.MoveTo => "MoveTo",
                _ => type.ToString()
            };
        }

        private void DrawStateMachineIndicator(Rect rect)
        {
            // Draw nested indicator icon (chevrons)
            Rect iconRect = new Rect(rect.xMax - 20 * Context.ZoomFactor, rect.y + 4 * Context.ZoomFactor,
                                      16 * Context.ZoomFactor, 12 * Context.ZoomFactor);

            GUIStyle iconStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(10 * Context.ZoomFactor),
                normal = { textColor = new Color(0.6f, 0.8f, 1f) }
            };

            GUI.Label(iconRect, ">>", iconStyle);
        }

        private void DrawDefaultIndicator(Rect rect)
        {
            // Draw arrow pointing to default state
            Rect arrowRect = new Rect(rect.x - 15 * Context.ZoomFactor, rect.y + rect.height / 2 - 5 * Context.ZoomFactor,
                                       12 * Context.ZoomFactor, 10 * Context.ZoomFactor);

            GUIStyle arrowStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(12 * Context.ZoomFactor),
                normal = { textColor = DefaultStateBorderColor }
            };

            GUI.Label(arrowRect, ">", arrowStyle);
        }

        private void DrawRoundedRect(Rect rect, float thickness, Color color)
        {
            // Draw simple rounded rectangle outline
            // Top
            EditorGUI.DrawRect(new Rect(rect.x + CornerRadius, rect.y, rect.width - CornerRadius * 2, thickness), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x + CornerRadius, rect.y + rect.height - thickness, rect.width - CornerRadius * 2, thickness), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + CornerRadius, thickness, rect.height - CornerRadius * 2), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - thickness, rect.y + CornerRadius, thickness, rect.height - CornerRadius * 2), color);
        }

        private void HandleNodeClick()
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
                return;

            Vector2 mousePos = Event.current.mousePosition;

            // Find clicked node
            HfsmNodeBase clickedNode = null;
            foreach (var node in Context.CurrentChildNodes)
            {
                Rect screenRect = new Rect(ContentPosToScreen(node.Position), node.Size * Context.ZoomFactor);
                if (screenRect.Contains(mousePos))
                {
                    clickedNode = node;
                    break;
                }
            }

            if (clickedNode != null)
            {
                // Check for double click
                float clickInterval = (float)EditorApplication.timeSinceStartup - _lastClickTime;
                bool isDoubleClick = clickedNode == _lastClickedNode && clickInterval < DoubleClickTime;

                _lastClickTime = (float)EditorApplication.timeSinceStartup;
                _lastClickedNode = clickedNode;

                if (isDoubleClick)
                {
                    // Double click - navigate into state machine
                    if (clickedNode.NodeType == HfsmNodeType.StateMachine)
                    {
                        Context.NavigateInto((HfsmStateMachineNode)clickedNode);
                    }
                }
                else
                {
                    // Single click - select node or create transition
                    if (Context.IsPreviewTransition)
                    {
                        // Complete transition
                        Context.UpdateTransitionPreviewTarget(clickedNode);
                        Context.CompleteTransition();
                    }
                    else
                    {
                        // Select node
                        Context.SetSelection(clickedNode);
                    }
                }

                Event.current.Use();
            }
            else
            {
                // Clicked on empty space
                if (!Context.IsPreviewTransition)
                {
                    Context.ClearSelection();
                }
            }
        }

        private void HandleNodeDrag()
        {
            if (Event.current.type != EventType.MouseDrag || Event.current.button != 0)
                return;

            if (Context.HasSelection && Context.SelectedNodes.Count > 0)
            {
                Vector2 delta = Event.current.delta / Context.ZoomFactor;
                Context.MoveSelectedNodes(delta);
                Event.current.Use();
            }
        }

        private void HandleContextMenu()
        {
            if (Event.current.type != EventType.ContextClick)
                return;

            Vector2 mousePos = Event.current.mousePosition;

            // Find clicked node
            HfsmNodeBase clickedNode = null;
            foreach (var node in Context.CurrentChildNodes)
            {
                Rect screenRect = new Rect(ContentPosToScreen(node.Position), node.Size * Context.ZoomFactor);
                if (screenRect.Contains(mousePos))
                {
                    clickedNode = node;
                    break;
                }
            }

            if (clickedNode != null)
            {
                ShowNodeContextMenu(clickedNode, mousePos);
            }
            else
            {
                ShowEmptySpaceContextMenu(mousePos);
            }

            Event.current.Use();
        }

        private void ShowNodeContextMenu(HfsmNodeBase node, Vector2 position)
        {
            GenericMenu menu = new GenericMenu();

            // Rename
            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                ShowRenameDialog(node);
            });

            menu.AddSeparator("");

            // Transition option
            menu.AddItem(new GUIContent("Make Transition"), false, () =>
            {
                Context.StartTransitionPreview(node);
            });

            menu.AddSeparator("");

            // Set as default
            if (!node.isDefault && node.NodeType != HfsmNodeType.StateMachine)
            {
                menu.AddItem(new GUIContent("Set as Default State"), false, () =>
                {
                    Context.SetDefaultState(node);
                });
            }

            // Navigate into (for state machines)
            if (node.NodeType == HfsmNodeType.StateMachine)
            {
                menu.AddItem(new GUIContent("Open"), false, () =>
                {
                    Context.NavigateInto((HfsmStateMachineNode)node);
                });
            }

            menu.AddSeparator("");

            // Duplicate
            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                DuplicateNode(node);
            });

            // Delete
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                Context.DeleteNode(node);
            });

            menu.ShowAsContext();
        }

        private void ShowRenameDialog(HfsmNodeBase node)
        {
            if (node == null || Context.GraphAsset == null)
                return;

            EditorInputDialog.Show("Rename Node", "Enter new name:", node.DisplayName, (newName) =>
            {
                if (!string.IsNullOrWhiteSpace(newName) && newName != node.DisplayName)
                {
                    Undo.RecordObject(Context.GraphAsset, "Rename Node");
                    node.DisplayName = newName.Trim();
                    EditorUtility.SetDirty(Context.GraphAsset);
                }
            });
        }

        private void DuplicateNode(HfsmNodeBase node)
        {
            if (node == null || Context.GraphAsset == null)
                return;

            Vector2 offset = new Vector2(30, 30);

            if (node is HfsmStateNode)
            {
                Context.CreateState(node.DisplayName + "_copy", node.Position + offset);
            }
            else if (node is HfsmStateMachineNode)
            {
                Context.CreateStateMachine(node.DisplayName + "_copy", node.Position + offset);
            }
        }

        private void ShowEmptySpaceContextMenu(Vector2 position)
        {
            GenericMenu menu = new GenericMenu();

            Vector2 contentPos = ScreenPosToContent(position);

            // Create State
            menu.AddItem(new GUIContent("Create State"), false, () =>
            {
                Context.CreateState("New State", contentPos);
            });

            // Create State Machine
            menu.AddItem(new GUIContent("Create State Machine"), false, () =>
            {
                Context.CreateStateMachine("New FSM", contentPos);
            });

            menu.ShowAsContext();
        }

        private void HandleKeyInput()
        {
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Delete || Event.current.keyCode == KeyCode.Backspace)
                {
                    if (Context.SelectedEdge != null)
                    {
                        Context.DeleteEdge(Context.SelectedEdge);
                        Event.current.Use();
                    }
                    else if (Context.HasSelection)
                    {
                        foreach (var node in Context.SelectedNodes.ToArray())
                        {
                            Context.DeleteNode(node);
                        }
                        Event.current.Use();
                    }
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    if (Context.IsPreviewTransition)
                    {
                        Context.CancelTransition();
                        Event.current.Use();
                    }
                }
                else if (Event.current.keyCode == KeyCode.F)
                {
                    FrameSelected();
                    Event.current.Use();
                }
            }
        }

        private void FrameSelected()
        {
            if (Context.CurrentChildNodes.Count == 0)
                return;

            Rect bounds = Context.GetNodesBounds();
            Context.PanOffset = new Vector2(
                ViewBounds.width / 2 - (bounds.x + bounds.width / 2),
                ViewBounds.height / 2 - (bounds.y + bounds.height / 2)
            );
            Context.ZoomFactor = 1f;
        }
    }
}
