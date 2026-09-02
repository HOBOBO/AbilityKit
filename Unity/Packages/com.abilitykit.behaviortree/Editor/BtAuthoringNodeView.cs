#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 标准纵向行为树节点：非根节点从顶部接收父节点，组合/装饰节点从底部连接子节点。
    /// </summary>
    internal sealed class BtAuthoringNodeView : Node
    {
        private readonly Label _runtimeStateLabel;

        public BtNodeDefinition Node { get; }

        public BtAuthoringNodeView(BtNodeDefinition node, string displayName, bool isRoot, float x, float y)
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

            BtNodeDescriptor? descriptor = null;
            var isParentKind = BtEditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out descriptor)
                && (descriptor.Kind == BtNodeKind.Composite || descriptor.Kind == BtNodeKind.Decorator);

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
                var outputCapacity = descriptor!.Kind == BtNodeKind.Decorator
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

        private static string KindLabel(BtNodeKind kind)
        {
            return kind switch
            {
                BtNodeKind.Composite => "组合",
                BtNodeKind.Decorator => "装饰",
                BtNodeKind.Condition => "条件",
                BtNodeKind.Action => "动作",
                _ => "节点",
            };
        }

        /// <summary>节点主题色：描述符 ColorHint 优先，否则按 Kind 给默认色。</summary>
        private static Color ResolveNodeColor(BtNodeDescriptor descriptor)
        {
            if (!string.IsNullOrEmpty(descriptor.ColorHint) && ColorUtility.TryParseHtmlString(descriptor.ColorHint, out var custom))
            {
                return custom;
            }
            return descriptor.Kind switch
            {
                BtNodeKind.Composite => new Color(0.22f, 0.42f, 0.62f),
                BtNodeKind.Decorator => new Color(0.42f, 0.3f, 0.58f),
                BtNodeKind.Condition => new Color(0.2f, 0.48f, 0.32f),
                BtNodeKind.Action => new Color(0.48f, 0.4f, 0.2f),
                _ => new Color(0.3f, 0.3f, 0.3f),
            };
        }

        /// <summary>观察模式着色：标题栏按状态着色，运行中节点加亮色边框。</summary>
        public void ApplyRuntimeState(BtNodeDebugInfo info)
        {
            titleContainer.style.backgroundColor = info.State switch
            {
                BtNodeState.Running => new Color(0.65f, 0.55f, 0.1f),
                BtNodeState.Success => new Color(0.18f, 0.5f, 0.25f),
                BtNodeState.Failure => new Color(0.55f, 0.18f, 0.15f),
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

        public void ClearRuntimeState()
        {
            _runtimeStateLabel.style.display = DisplayStyle.None;
            var info = new BtNodeDebugInfo(
                Node.Id, "", Node.Type, BtNodeKind.Action,
                BtNodeState.Inactive, 0, 0, -1);
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
