#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Editor.Debugging.Contributors;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 标准纵向行为树节点：非根节点从顶部接收父节点，组合/装饰节点从底部连接子节点。
    /// </summary>
    internal sealed class AuthoringNodeView : Node
    {
        private readonly Label _runtimeStateLabel;
        private readonly VisualElement _badgeContainer;
        private readonly VisualElement _markerContainer;
        private readonly Color _defaultTitleColor;

        public NodeDefinition Node { get; }
        internal int ObservationApplyCount { get; private set; }

        public AuthoringNodeView(NodeDefinition node, string displayName, bool isRoot, float x, float y)
        {
            Node = node;
            title = string.IsNullOrWhiteSpace(displayName) ? node.Type : displayName;
            SetPosition(new Rect(x, y, 190f, 104f));
            style.minWidth = 190f;
            style.maxWidth = 190f;
            titleContainer.style.minHeight = 30f;
            titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.style.paddingLeft = 8f;
            titleContainer.style.paddingRight = 8f;
            mainContainer.style.borderBottomLeftRadius = 4f;
            mainContainer.style.borderBottomRightRadius = 4f;
            mainContainer.style.borderTopLeftRadius = 4f;
            mainContainer.style.borderTopRightRadius = 4f;
            _runtimeStateLabel = new Label
            {
                style =
                {
                    display = DisplayStyle.None,
                    opacity = 0.85f,
                    fontSize = 10f,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                },
            };

            _badgeContainer = new VisualElement
            {
                style =
                {
                    display = DisplayStyle.None,
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 3f,
                },
            };
            _markerContainer = new VisualElement
            {
                style =
                {
                    display = DisplayStyle.None,
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 2f,
                },
            };

            NodeDescriptor? descriptor = null;
            var isParentKind = EditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out descriptor)
                && (descriptor.Kind == NodeKind.Composite || descriptor.Kind == NodeKind.Decorator);
            _defaultTitleColor = descriptor != null
                ? ResolveNodeColor(descriptor)
                : new Color(0.3f, 0.3f, 0.3f);
            titleContainer.style.backgroundColor = _defaultTitleColor;

            if (descriptor != null)
            {
                titleContainer.style.backgroundColor = ResolveNodeColor(descriptor);
                var typeLabel = new Label(KindLabel(descriptor.Kind) + "  ·  " + descriptor.DisplayName)
                {
                    tooltip = descriptor.Category + "\n" + node.Type,
                    style =
                    {
                        opacity = 0.65f,
                        fontSize = 10f,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        paddingLeft = 6f,
                        paddingRight = 6f,
                        paddingTop = 4f,
                        paddingBottom = 4f,
                    },
                };
                extensionContainer.Add(typeLabel);
            }
            extensionContainer.Add(_badgeContainer);
            extensionContainer.Add(_markerContainer);
            extensionContainer.Add(_runtimeStateLabel);

            if (!isRoot)
            {
                InputPort = Port.Create<Edge>(
                    Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(Port));
                InputPort.portName = "";
                InputPort.tooltip = "连接父节点";
                inputContainer.Add(InputPort);
            }

            if (isParentKind)
            {
                // 按 Kind 约束端口：装饰节点恰好一个子，组合节点多个子
                var outputCapacity = descriptor!.Kind == NodeKind.Decorator
                    ? Port.Capacity.Single
                    : Port.Capacity.Multi;
                OutputPort = Port.Create<Edge>(
                    Orientation.Vertical, Direction.Output, outputCapacity, typeof(Port));
                OutputPort.portName = "";
                OutputPort.tooltip = "连接子节点";
                outputContainer.Add(OutputPort);
            }

            // GraphView 默认把端口容器左右排布；重新挂到主容器首尾后形成真正的上下连线。
            inputContainer.RemoveFromHierarchy();
            outputContainer.RemoveFromHierarchy();
            ConfigurePortContainer(inputContainer);
            ConfigurePortContainer(outputContainer);
            mainContainer.Insert(0, inputContainer);
            mainContainer.Add(outputContainer);
            topContainer.style.display = DisplayStyle.None;

            RefreshExpandedState();
        }

        public Port? InputPort { get; }
        public Port? OutputPort { get; }

        private static void ConfigurePortContainer(VisualElement container)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;
            container.style.minHeight = 16f;
            container.style.width = Length.Percent(100f);
        }

        private static string KindLabel(NodeKind kind)
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

        /// <summary>节点主题色：描述符 ColorHint 优先，否则按 Kind 给默认色。</summary>
        private static Color ResolveNodeColor(NodeDescriptor descriptor)
        {
            if (!string.IsNullOrEmpty(descriptor.ColorHint) && ColorUtility.TryParseHtmlString(descriptor.ColorHint, out var custom))
            {
                return custom;
            }
            return descriptor.Kind switch
            {
                NodeKind.Composite => new Color(0.22f, 0.42f, 0.62f),
                NodeKind.Decorator => new Color(0.42f, 0.3f, 0.58f),
                NodeKind.Condition => new Color(0.2f, 0.48f, 0.32f),
                NodeKind.Action => new Color(0.48f, 0.4f, 0.2f),
                _ => new Color(0.3f, 0.3f, 0.3f),
            };
        }

        /// <summary>观察模式着色：标题栏按状态着色，运行中节点加亮色边框。</summary>
        public void ApplyRuntimeState(NodeDebugInfo info)
        {
            titleContainer.style.backgroundColor = info.State switch
            {
                NodeState.Running => new Color(0.65f, 0.55f, 0.1f),
                NodeState.Success => new Color(0.18f, 0.5f, 0.25f),
                NodeState.Failure => new Color(0.55f, 0.18f, 0.15f),
                _ => new Color(0.18f, 0.18f, 0.18f, 0.4f),
            };

            _runtimeStateLabel.text = info.State
                + (info.RunningChildIndex >= 0 ? "  ·  child " + (info.RunningChildIndex + 1) : "")
                + (info.OnStackCount > 0 ? "  ·  active" : "");
            _runtimeStateLabel.style.display = DisplayStyle.Flex;

            var border = info.OnStackCount > 0
                ? new Color(1f, 0.85f, 0.3f)
                : new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = border;
            style.borderTopColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderBottomWidth = info.OnStackCount > 0 ? 2f : 0f;
            style.borderTopWidth = info.OnStackCount > 0 ? 2f : 0f;
            style.borderLeftWidth = info.OnStackCount > 0 ? 2f : 0f;
            style.borderRightWidth = info.OnStackCount > 0 ? 2f : 0f;
        }

        public void ApplyObservation(
            NodeDebugInfo? info,
            System.Collections.Generic.IReadOnlyList<ObservationOverlay> overlays,
            bool isActive)
        {
            ObservationApplyCount++;
            overlays ??= System.Array.Empty<ObservationOverlay>();
            if (info == null)
            {
                ClearObservationState();
                return;
            }

            titleContainer.style.backgroundColor = info.State switch
            {
                NodeState.Running => new Color(0.65f, 0.55f, 0.1f),
                NodeState.Success => new Color(0.18f, 0.5f, 0.25f),
                NodeState.Failure => new Color(0.55f, 0.18f, 0.15f),
                _ => new Color(0.18f, 0.18f, 0.18f, 0.4f),
            };

            _runtimeStateLabel.text = info.State
                + (info.RunningChildIndex >= 0 ? "  / child " + (info.RunningChildIndex + 1) : "")
                + (info.OnStackCount > 0 ? "  / active" : "");
            _runtimeStateLabel.style.display = DisplayStyle.Flex;

            var hasBorderOverlay = HasOverlay(overlays, ObservationOverlayKind.Border);
            var border = info.OnStackCount > 0 || isActive || hasBorderOverlay
                ? new Color(1f, 0.85f, 0.3f)
                : new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = border;
            style.borderTopColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            var borderWidth = info.OnStackCount > 0 || isActive || hasBorderOverlay ? 2f : 0f;
            style.borderBottomWidth = borderWidth;
            style.borderTopWidth = borderWidth;
            style.borderLeftWidth = borderWidth;
            style.borderRightWidth = borderWidth;

            ApplyOverlayText(overlays);
        }

        public void ClearObservationState()
        {
            ObservationApplyCount++;
            titleContainer.style.backgroundColor = _defaultTitleColor;
            _runtimeStateLabel.style.display = DisplayStyle.None;
            _runtimeStateLabel.text = "";
            _badgeContainer.Clear();
            _badgeContainer.style.display = DisplayStyle.None;
            _markerContainer.Clear();
            _markerContainer.style.display = DisplayStyle.None;
            tooltip = "";
            var border = new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = border;
            style.borderTopColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderBottomWidth = 0f;
            style.borderTopWidth = 0f;
            style.borderLeftWidth = 0f;
            style.borderRightWidth = 0f;
        }

        private static bool HasOverlay(
            System.Collections.Generic.IReadOnlyList<ObservationOverlay> overlays,
            ObservationOverlayKind kind)
        {
            for (var i = 0; i < overlays.Count; i++)
                if (overlays[i].Kind == kind) return true;
            return false;
        }

        private void ApplyOverlayText(System.Collections.Generic.IReadOnlyList<ObservationOverlay> overlays)
        {
            _badgeContainer.Clear();
            _markerContainer.Clear();
            var tooltips = new System.Collections.Generic.List<string>();
            var sorted = new System.Collections.Generic.List<ObservationOverlay>(overlays);
            sorted.Sort((a, b) =>
            {
                var priority = a.Priority.CompareTo(b.Priority);
                return priority != 0 ? priority : a.Kind.CompareTo(b.Kind);
            });

            foreach (var overlay in sorted)
            {
                if (string.IsNullOrEmpty(overlay.Text)) continue;
                switch (overlay.Kind)
                {
                    case ObservationOverlayKind.Badge:
                        _badgeContainer.Add(CreateOverlayLabel(overlay.Text, true));
                        break;
                    case ObservationOverlayKind.Marker:
                        _markerContainer.Add(CreateOverlayLabel(overlay.Text, false));
                        break;
                    case ObservationOverlayKind.Tooltip:
                    case ObservationOverlayKind.Border:
                        tooltips.Add(overlay.Text);
                        break;
                }
            }

            _badgeContainer.style.display = _badgeContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _markerContainer.style.display = _markerContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            tooltip = tooltips.Count == 0 ? "" : string.Join("\n", tooltips);
        }

        private static Label CreateOverlayLabel(string text, bool isBadge)
        {
            return new Label(text)
            {
                style =
                {
                    fontSize = 10f,
                    marginRight = 4f,
                    marginBottom = 2f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 1f,
                    paddingBottom = 1f,
                    color = Color.white,
                    backgroundColor = isBadge
                        ? new Color(0.15f, 0.42f, 0.2f, 0.92f)
                        : new Color(0.36f, 0.36f, 0.36f, 0.85f),
                },
            };
        }

        public void ClearRuntimeState()
        {
            _runtimeStateLabel.style.display = DisplayStyle.None;
            var info = new NodeDebugInfo(
                Node.Id, "", Node.Type, NodeKind.Action,
                NodeState.Inactive, 0, 0, -1);
            ApplyRuntimeState(info);
            _runtimeStateLabel.style.display = DisplayStyle.None;
        }

        /// <summary>编辑模式校验错误标记（红边框；观察模式的运行高亮互不干扰——不同模式使用）。</summary>
        public void SetErrorBorder(bool hasError)
        {
            var border = hasError ? new Color(0.95f, 0.25f, 0.2f) : new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = border;
            style.borderTopColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderBottomWidth = hasError ? 2f : 0f;
            style.borderTopWidth = hasError ? 2f : 0f;
            style.borderLeftWidth = hasError ? 2f : 0f;
            style.borderRightWidth = hasError ? 2f : 0f;
        }
    }
}
