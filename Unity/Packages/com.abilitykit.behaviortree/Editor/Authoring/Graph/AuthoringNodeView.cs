#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.Deterministic;
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
        internal const float CenteredPortSize = 12f;
        internal const string AbortTypeBadgeName = "bt-node-abort-type";
        internal const string BehaviorSummaryLabelName = "bt-node-behavior-summary";

        private readonly Label _runtimeStateLabel;
        private readonly VisualElement _propertySummaryContainer;
        private readonly Label _behaviorSummaryLabel;
        private readonly Label _abortTypeBadge;
        private readonly VisualElement _badgeContainer;
        private readonly VisualElement _markerContainer;
        private readonly Color _defaultTitleColor;
        private readonly BlackboardSchema? _blackboard;

        public NodeDefinition Node { get; }
        internal int ObservationApplyCount { get; private set; }

        public AuthoringNodeView(
            NodeDefinition node,
            string displayName,
            bool isRoot,
            float x,
            float y,
            BlackboardSchema? blackboard = null)
        {
            Node = node;
            _blackboard = blackboard;
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

            _propertySummaryContainer = new VisualElement
            {
                style =
                {
                    display = DisplayStyle.None,
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 4f,
                    paddingBottom = 2f,
                },
            };
            _behaviorSummaryLabel = new Label { name = BehaviorSummaryLabelName };
            _behaviorSummaryLabel.style.display = DisplayStyle.None;
            _behaviorSummaryLabel.style.flexGrow = 1f;
            _behaviorSummaryLabel.style.minWidth = 0f;
            _behaviorSummaryLabel.style.fontSize = 11f;
            _behaviorSummaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _behaviorSummaryLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _behaviorSummaryLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _behaviorSummaryLabel.style.overflow = Overflow.Hidden;
            _behaviorSummaryLabel.style.textOverflow = TextOverflow.Ellipsis;
            _behaviorSummaryLabel.style.color = new Color(0.9f, 0.92f, 0.94f);
            _behaviorSummaryLabel.style.paddingLeft = 3f;
            _behaviorSummaryLabel.style.paddingRight = 3f;
            _behaviorSummaryLabel.style.paddingTop = 2f;
            _behaviorSummaryLabel.style.paddingBottom = 2f;
            _behaviorSummaryLabel.style.marginBottom = 2f;
            _propertySummaryContainer.Add(_behaviorSummaryLabel);

            _abortTypeBadge = new Label { name = AbortTypeBadgeName };
            _abortTypeBadge.style.fontSize = 10f;
            _abortTypeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _abortTypeBadge.style.paddingLeft = 5f;
            _abortTypeBadge.style.paddingRight = 5f;
            _abortTypeBadge.style.paddingTop = 2f;
            _abortTypeBadge.style.paddingBottom = 2f;
            _abortTypeBadge.style.marginRight = 4f;
            _abortTypeBadge.style.marginBottom = 2f;
            _abortTypeBadge.style.borderBottomLeftRadius = 3f;
            _abortTypeBadge.style.borderBottomRightRadius = 3f;
            _abortTypeBadge.style.borderTopLeftRadius = 3f;
            _abortTypeBadge.style.borderTopRightRadius = 3f;
            _propertySummaryContainer.Add(_abortTypeBadge);

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
            extensionContainer.Add(_propertySummaryContainer);
            extensionContainer.Add(_badgeContainer);
            extensionContainer.Add(_markerContainer);
            extensionContainer.Add(_runtimeStateLabel);

            if (!isRoot)
            {
                InputPort = Port.Create<AuthoringTreeEdge>(
                    Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(Port));
                InputPort.portName = "";
                InputPort.tooltip = "连接父节点";
                ConfigureCenteredPort(InputPort);
                inputContainer.Add(InputPort);
            }

            if (isParentKind)
            {
                // 按 Kind 约束端口：装饰节点恰好一个子，组合节点多个子
                var outputCapacity = descriptor!.Kind == NodeKind.Decorator
                    ? Port.Capacity.Single
                    : Port.Capacity.Multi;
                OutputPort = Port.Create<AuthoringTreeEdge>(
                    Orientation.Vertical, Direction.Output, outputCapacity, typeof(Port));
                OutputPort.portName = "";
                OutputPort.tooltip = "连接子节点";
                ConfigureCenteredPort(OutputPort);
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

            RefreshPropertySummary();
            RefreshExpandedState();
        }

        public Port? InputPort { get; }
        public Port? OutputPort { get; }

        internal void RefreshPropertySummary()
        {
            if (AuthoringNodeSummaryFormatter.TryFormat(Node, _blackboard, out var summary))
            {
                _behaviorSummaryLabel.text = summary;
                _behaviorSummaryLabel.tooltip = summary;
                _behaviorSummaryLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _behaviorSummaryLabel.text = "";
                _behaviorSummaryLabel.tooltip = "";
                _behaviorSummaryLabel.style.display = DisplayStyle.None;
            }

            var showAbortType = EditorNodeCatalog.Registry.TryGetDescriptor(Node.Type, out var descriptor)
                && descriptor.Kind == NodeKind.Composite;
            if (showAbortType)
            {
                var abortType = AbortType.None;
                if (Node.Properties.TryGet(CompositeNode.AbortTypeProperty, out var value)
                    && value.TryGetInt64(out var rawValue)
                    && rawValue is >= (long)AbortType.None and <= (long)AbortType.Both)
                {
                    abortType = (AbortType)rawValue;
                }

                _abortTypeBadge.text = "中止：" + AbortTypeLabel(abortType);
                _abortTypeBadge.tooltip = "条件中止类型：" + AbortTypeLabel(abortType);
                _abortTypeBadge.style.color = abortType == AbortType.None
                    ? new Color(0.72f, 0.72f, 0.72f)
                    : Color.white;
                _abortTypeBadge.style.backgroundColor = AbortTypeColor(abortType);
                _abortTypeBadge.style.display = DisplayStyle.Flex;
            }
            else
            {
                _abortTypeBadge.style.display = DisplayStyle.None;
            }

            _propertySummaryContainer.style.display = showAbortType || !string.IsNullOrEmpty(summary)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static string AbortTypeLabel(AbortType abortType)
        {
            return abortType switch
            {
                AbortType.Self => "自身",
                AbortType.LowerPriority => "低优先级",
                AbortType.Both => "两者",
                _ => "无",
            };
        }

        private static Color AbortTypeColor(AbortType abortType)
        {
            return abortType switch
            {
                AbortType.Self => new Color(0.62f, 0.38f, 0.12f, 0.95f),
                AbortType.LowerPriority => new Color(0.16f, 0.42f, 0.58f, 0.95f),
                AbortType.Both => new Color(0.64f, 0.22f, 0.18f, 0.95f),
                _ => new Color(0.24f, 0.24f, 0.24f, 0.9f),
            };
        }

        private static void ConfigurePortContainer(VisualElement container)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;
            container.style.minHeight = 16f;
            container.style.width = Length.Percent(100f);
        }

        private static void ConfigureCenteredPort(Port port)
        {
            port.style.width = CenteredPortSize;
            port.style.minWidth = CenteredPortSize;
            port.style.maxWidth = CenteredPortSize;
            port.style.height = CenteredPortSize;
            port.style.minHeight = CenteredPortSize;
            port.style.maxHeight = CenteredPortSize;
            port.style.flexGrow = 0f;
            port.style.flexShrink = 0f;
            port.style.alignSelf = Align.Center;
            port.style.marginLeft = 0f;
            port.style.marginRight = 0f;
            port.style.paddingLeft = 0f;
            port.style.paddingRight = 0f;

            var connector = port.Q<VisualElement>("connector");
            if (connector != null)
            {
                connector.style.width = CenteredPortSize;
                connector.style.height = CenteredPortSize;
                connector.style.marginLeft = 0f;
                connector.style.marginRight = 0f;
            }

            var typeLabel = port.Q<Label>("type");
            if (typeLabel != null) typeLabel.style.display = DisplayStyle.None;
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
                NodeState.Faulted => new Color(0.65f, 0.12f, 0.08f),
                _ => new Color(0.18f, 0.18f, 0.18f, 0.4f),
            };

            _runtimeStateLabel.text = EditorDisplayText.NodeState(info.State)
                + (info.RunningChildIndex >= 0 ? "  ·  子节点 " + (info.RunningChildIndex + 1) : "")
                + (info.OnStackCount > 0 ? "  ·  执行中" : "");
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
                NodeState.Faulted => new Color(0.65f, 0.12f, 0.08f),
                _ => new Color(0.18f, 0.18f, 0.18f, 0.4f),
            };

            _runtimeStateLabel.text = EditorDisplayText.NodeState(info.State)
                + (info.RunningChildIndex >= 0 ? "  / 子节点 " + (info.RunningChildIndex + 1) : "")
                + (info.OnStackCount > 0 ? "  / 执行中" : "");
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

    internal static class AuthoringNodeSummaryFormatter
    {
        internal static bool TryFormat(
            NodeDefinition node,
            BlackboardSchema? blackboard,
            out string summary)
        {
            var properties = new PropertyReader(node.Properties);
            switch (node.Type)
            {
                case BuiltInNodeTypes.BlackboardCompare:
                    summary = FormatBlackboardCompare(properties, blackboard);
                    return true;
                case BuiltInNodeTypes.SetBlackboard:
                    summary = FormatSetBlackboard(properties, blackboard);
                    return true;
                case BuiltInNodeTypes.BlackboardHasKey:
                    summary = "存在键 " + DisplayKey(properties.GetString(BlackboardHasKeyNode.KeyProperty, ""));
                    return true;
                case BuiltInNodeTypes.Probability:
                    summary = "概率 " + properties.GetInt64(ProbabilityNode.PercentProperty, 50) + "%";
                    return true;
                case BuiltInNodeTypes.Wait:
                    summary = properties.GetInt64(WaitNode.ModeProperty, 0) == 1
                        ? "等待 " + properties.GetInt64(WaitNode.DurationFramesProperty, 30) + " 帧"
                        : "等待 " + properties.GetFixed64(WaitNode.DurationSecondsProperty, Fixed64.One) + " 秒";
                    return true;
                case BuiltInNodeTypes.Log:
                    summary = LogLevelLabel(properties.GetInt64(LogNode.LevelProperty, 1))
                        + ": " + Quote(properties.GetString(LogNode.MessageProperty, ""));
                    return true;
                case BuiltInNodeTypes.Repeater:
                    var repeatCount = properties.GetInt64(RepeaterNode.CountProperty, 1);
                    summary = repeatCount < 0 ? "永久重复" : "重复 " + repeatCount + " 次";
                    return true;
                case BuiltInNodeTypes.Retry:
                    var retryCount = properties.GetInt64(RetryNode.CountProperty, 1);
                    summary = retryCount < 0 ? "无限重试" : "重试 " + retryCount + " 次";
                    return true;
                case BuiltInNodeTypes.Timeout:
                    summary = "超时 "
                        + properties.GetFixed64(TimeoutNode.DurationSecondsProperty, Fixed64.One) + " 秒";
                    return true;
                case BuiltInNodeTypes.Cooldown:
                    summary = "冷却 "
                        + properties.GetFixed64(CooldownNode.CooldownSecondsProperty, Fixed64.One)
                        + " 秒 / "
                        + ResultLabel(properties.GetInt64(CooldownNode.ResultOnCooldownProperty, 0));
                    return true;
                case BuiltInNodeTypes.Once:
                    summary = "之后返回"
                        + ResultLabel(properties.GetInt64(OnceNode.ResultAfterFirstProperty, 0));
                    return true;
                case BuiltInNodeTypes.Subtree:
                    var treeId = properties.GetString(SubtreeNode.TreeIdProperty, "");
                    var bindingCount = node.SubtreeBlackboard?.Bindings.Count ?? 0;
                    summary = string.IsNullOrEmpty(treeId) ? "未选择引用树" : treeId;
                    if (bindingCount > 0) summary += "  |  " + bindingCount + " 项绑定";
                    if (node.SubtreeBlackboard?.IsolateUnmappedKeys == true) summary += "  |  局部隔离";
                    return true;
                default:
                    summary = "";
                    return false;
            }
        }

        private static string FormatBlackboardCompare(
            PropertyReader properties,
            BlackboardSchema? blackboard)
        {
            var leftKey = properties.GetString(BlackboardCompareNode.LeftKeyProperty, "");
            var op = CompareOperator(properties.GetInt64(BlackboardCompareNode.OpProperty, 0));
            if (properties.GetInt64(BlackboardCompareNode.RightKindProperty, 0) == 1)
            {
                return DisplayKey(leftKey) + " " + op + " "
                    + DisplayKey(properties.GetString(BlackboardCompareNode.RightKeyProperty, ""));
            }

            return DisplayKey(leftKey) + " " + op + " "
                + FormatConstant(
                    properties,
                    blackboard,
                    leftKey,
                    BlackboardCompareNode.RightBoolProperty,
                    BlackboardCompareNode.RightInt64Property,
                    BlackboardCompareNode.RightFixed64RawProperty,
                    BlackboardCompareNode.RightStringProperty);
        }

        private static string FormatSetBlackboard(
            PropertyReader properties,
            BlackboardSchema? blackboard)
        {
            var targetKey = properties.GetString(SetBlackboardNode.KeyProperty, "");
            if (properties.GetInt64(SetBlackboardNode.ValueKindProperty, 0) == 1)
            {
                return DisplayKey(targetKey) + " <- "
                    + DisplayKey(properties.GetString(SetBlackboardNode.FromKeyProperty, ""));
            }

            return DisplayKey(targetKey) + " = "
                + FormatConstant(
                    properties,
                    blackboard,
                    targetKey,
                    SetBlackboardNode.ConstBoolProperty,
                    SetBlackboardNode.ConstInt64Property,
                    SetBlackboardNode.ConstFixed64Property,
                    SetBlackboardNode.ConstStringProperty);
        }

        private static string FormatConstant(
            PropertyReader properties,
            BlackboardSchema? blackboard,
            string key,
            string boolProperty,
            string int64Property,
            string fixed64Property,
            string stringProperty)
        {
            if (blackboard == null || !blackboard.TryGetType(key, out var type)) return "<?>";
            return type switch
            {
                ValueType.Bool => properties.GetBool(boolProperty, false) ? "是" : "否",
                ValueType.Int64 => properties.GetInt64(int64Property, 0).ToString(),
                ValueType.Fixed64 => properties.GetFixed64(fixed64Property, Fixed64.Zero).ToString(),
                ValueType.String => Quote(properties.GetString(stringProperty, "")),
                _ => "<?>"
            };
        }

        private static string CompareOperator(long value)
        {
            return value switch
            {
                1 => "!=",
                2 => "<",
                3 => "<=",
                4 => ">",
                5 => ">=",
                _ => "==",
            };
        }

        private static string LogLevelLabel(long value)
        {
            return value switch
            {
                0 => "跟踪",
                2 => "警告",
                3 => "错误",
                _ => "信息",
            };
        }

        private static string ResultLabel(long value) => value == 1 ? "成功" : "失败";

        private static string DisplayKey(string value)
            => string.IsNullOrWhiteSpace(value) ? "<选择黑板键>" : value;

        private static string Quote(string value)
        {
            var escaped = (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }
    }

    /// <summary>行为树专用边：强化父节点底部中央的纵向出线段。</summary>
    internal sealed class AuthoringTreeEdge : Edge
    {
        internal static AuthoringTreeEdge Connect(Port parentOutput, Port childInput)
        {
            return parentOutput.ConnectTo<AuthoringTreeEdge>(childInput);
        }

        public override bool UpdateEdgeControl()
        {
            if (!base.UpdateEdgeControl()) return false;

            var anchored = false;
            if (output?.node is AuthoringNodeView parentNode)
            {
                edgeControl.from = this.WorldToLocal(
                    ResolveNodeAnchor(parentNode.worldBound, Direction.Output));
                anchored = true;
            }
            if (input?.node is AuthoringNodeView childNode)
            {
                edgeControl.to = this.WorldToLocal(
                    ResolveNodeAnchor(childNode.worldBound, Direction.Input));
                anchored = true;
            }

            if (anchored) edgeControl.UpdateLayout();
            return true;
        }

        internal static Vector2 ResolveNodeAnchor(Rect nodeWorldBound, Direction direction)
        {
            var y = direction == Direction.Output
                ? nodeWorldBound.yMax
                : nodeWorldBound.yMin;
            return new Vector2(nodeWorldBound.center.x, y);
        }

        protected override EdgeControl CreateEdgeControl()
        {
            return new AuthoringTreeEdgeControl
            {
                edgeWidth = 2,
                interceptWidth = 8f,
                capRadius = 4f,
            };
        }
    }

    internal sealed class AuthoringTreeEdgeControl : EdgeControl
    {
        internal const float MinimumDepartureLength = 32f;
        internal const float MaximumDepartureLength = 72f;
        internal const float MinimumArrivalLength = 22f;
        internal const float MaximumArrivalLength = 48f;

        protected override void ComputeControlPoints()
        {
            base.ComputeControlPoints();

            var points = controlPoints;
            if (points == null || points.Length != 4) return;

            var verticalDistance = to.y - from.y;
            if (verticalDistance <= MinimumDepartureLength + MinimumArrivalLength + 8f) return;

            var departure = Mathf.Clamp(
                verticalDistance * 0.32f,
                MinimumDepartureLength,
                MaximumDepartureLength);
            var arrival = Mathf.Clamp(
                verticalDistance * 0.2f,
                MinimumArrivalLength,
                MaximumArrivalLength);

            // Keep both tangents vertical: the edge leaves the parent downward from its
            // center, turns across the gap, then enters the child from its top center.
            points[1] = new Vector2(from.x, from.y + departure);
            points[2] = new Vector2(to.x, to.y - arrival);
        }

        internal void ComputeControlPointsForTests()
        {
            ComputeControlPoints();
        }
    }
}
