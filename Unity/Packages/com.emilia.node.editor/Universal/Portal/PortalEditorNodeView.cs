using System;
using System.Collections.Generic;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// Portal节点视图，负责Portal节点的UI显示和交互
    /// </summary>
    [EditorNode(typeof(PortalNodeAsset))]
    public class PortalEditorNodeView : UniversalEditorNodeView
    {
        private static readonly Color HighlightColor = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color EntryDefaultColor = new Color(0.2f, 0.6f, 0.3f);
        private static readonly Color ExitDefaultColor = new Color(0.3f, 0.4f, 0.7f);
        private const float ClickAreaSize = 10;

        private PortalNodeAsset _portalAsset;

        public override bool canExpanded => false;

        public override void Initialize(EditorGraphView graphView, EditorNodeAsset asset)
        {
            _portalAsset = asset as PortalNodeAsset;
            base.Initialize(graphView, asset);

            SetColor(GetDefaultColor());
            HideTitleContainer();
            SetupClickAreaBorder();
            SetupContextualMenu();
        }

        private void SetupContextualMenu()
        {
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (_portalAsset == null) return;

            if (_portalAsset.direction == PortalDirection.Entry)
            {
                evt.menu.AppendAction("创建 Exit Portal", CreateLinkedExitPortal);
            }
            else
            {
                evt.menu.AppendAction("创建 Entry Portal", CreateLinkedEntryPortal);
            }
        }

        private void CreateLinkedExitPortal(DropdownMenuAction action)
        {
            CreateLinkedPortal(PortalDirection.Exit);
        }

        private void CreateLinkedEntryPortal(DropdownMenuAction action)
        {
            CreateLinkedPortal(PortalDirection.Entry);
        }

        private void CreateLinkedPortal(PortalDirection direction)
        {
            if (_portalAsset == null) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Vector2 offset = direction == PortalDirection.Exit ? new Vector2(150, 0) : new Vector2(-150, 0);
            Vector2 newPosition = _portalAsset.position.position + offset;

            var newPortalView = PortalHelper.CreatePortalNode(
                graphView,
                direction,
                newPosition,
                _portalAsset.portalGroupId,
                _portalAsset.portOrientation);

            var newPortalAsset = newPortalView.asset as PortalNodeAsset;
            if (newPortalAsset != null)
            {
                Undo.RecordObject(newPortalAsset, "Link Portal");
                Undo.RecordObject(_portalAsset, "Link Portal");

                newPortalAsset.linkedPortalId = _portalAsset.id;
                _portalAsset.linkedPortalId = newPortalAsset.id;
            }

            // 刷新端口视图以获取正确的类型信息
            PortalHelper.RefreshPortalView(newPortalView);
            RefreshPortFromConnections();

            Undo.CollapseUndoOperations(undoGroup);
            Undo.SetCurrentGroupName("Create Linked Portal");
        }

        private void HideTitleContainer()
        {
            titleContainer.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// 设置可点击区域的边框，扩展Portal的可选择范围
        /// </summary>
        private void SetupClickAreaBorder()
        {
            bool isHorizontal = _portalAsset.portOrientation == EditorOrientation.Horizontal;
            bool isEntry = _portalAsset.direction == PortalDirection.Entry;

            nodeBottomContainer.pickingMode = PickingMode.Position;

            if (isHorizontal)
            {
                // 横向布局：Entry向右扩展，Exit向左扩展
                if (isEntry)
                    nodeBottomContainer.style.borderRightWidth = ClickAreaSize;
                else
                    nodeBottomContainer.style.borderLeftWidth = ClickAreaSize;
            }
            else
            {
                // 纵向布局：Entry向下扩展，Exit向上扩展
                if (isEntry)
                    nodeTopContainer.style.borderBottomWidth = ClickAreaSize;
                else
                    nodeBottomContainer.style.borderTopWidth = ClickAreaSize;
            }

            UpdateBorderColors();
        }

        private Color GetDefaultColor()
        {
            return _portalAsset.direction == PortalDirection.Entry ? EntryDefaultColor : ExitDefaultColor;
        }

        public override void SetColor(Color color)
        {
            base.SetColor(color);
            UpdateBorderColors();
        }

        private void UpdateBorderColors()
        {
            Color borderColor = topicColor;
            SetContainerBorderColor(nodeTopContainer, borderColor);
            SetContainerBorderColor(nodeBottomContainer, borderColor);
        }

        private void SetContainerBorderColor(VisualElement container, Color color)
        {
            container.style.borderTopColor = color;
            container.style.borderBottomColor = color;
            container.style.borderLeftColor = color;
            container.style.borderRightColor = color;
        }

        public override List<EditorPortInfo> CollectStaticPortAssets()
        {
            var portInfos = new List<EditorPortInfo>();
            if (_portalAsset == null) return portInfos;

            var (displayName, portColor) = GetPortDisplayInfo();

            portInfos.Add(new EditorPortInfo
            {
                id = PortalHelper.PortalPortId,
                displayName = displayName,
                portType = typeof(object),
                direction = _portalAsset.direction == PortalDirection.Entry
                    ? EditorPortDirection.Input
                    : EditorPortDirection.Output,
                orientation = _portalAsset.portOrientation,
                canMultiConnect = true,
                color = portColor
            });

            return portInfos;
        }

        /// <summary>
        /// 获取端口显示信息（名称和颜色）
        /// </summary>
        private (string displayName, Color portColor) GetPortDisplayInfo()
        {
            var connectionInfo = GetConnectionInfo();
            return (connectionInfo.displayName, connectionInfo.portColor);
        }

        /// <summary>
        /// 从自身或关联Portal的连接中获取端口信息
        /// </summary>
        private (Type portType, Color portColor, string displayName) GetConnectionInfo()
        {
            Type portType = typeof(object);
            Color portColor = Color.white;
            string displayName = string.Empty;

            if (_portalAsset == null || graphView?.graphAsset == null)
                return (portType, portColor, displayName);

            // 优先从自身的连接获取信息
            var selfEdges = graphView.graphAsset.GetEdges(_portalAsset.id, PortalHelper.PortalPortId);
            if (selfEdges.Count > 0)
                return CollectPortInfoFromEdges(selfEdges, _portalAsset);

            // 如果自身没有连接，尝试从关联Portal的连接获取
            string linkedPortalId = _portalAsset.linkedPortalId;
            if (string.IsNullOrEmpty(linkedPortalId))
                return (portType, portColor, displayName);

            var linkedPortal = graphView.graphAsset.nodeMap.GetValueOrDefault(linkedPortalId) as PortalNodeAsset;
            if (linkedPortal == null)
                return (portType, portColor, displayName);

            var linkedEdges = graphView.graphAsset.GetEdges(linkedPortal.id, PortalHelper.PortalPortId);
            if (linkedEdges.Count == 0)
                return (portType, portColor, displayName);

            return CollectPortInfoFromEdges(linkedEdges, linkedPortal);
        }

        /// <summary>
        /// 从边集合中收集端口信息
        /// </summary>
        private (Type portType, Color portColor, string displayName) CollectPortInfoFromEdges(
            List<EditorEdgeAsset> edges,
            PortalNodeAsset linkedPortal)
        {
            var portNames = new List<string>();
            Type firstPortType = null;
            Color firstPortColor = Color.white;

            foreach (EditorEdgeAsset edge in edges)
            {
                var (nodeId, portId) = GetConnectedPortIds(edge, linkedPortal);
                var portView = GetConnectedPortView(nodeId, portId);

                if (portView != null)
                {
                    portNames.Add(GetPortDisplayName(portView));

                    if (firstPortType == null)
                    {
                        firstPortType = portView.info.portType ?? typeof(object);
                        firstPortColor = portView.info.color;
                    }
                }
            }

            if (portNames.Count == 0)
                return (typeof(object), Color.white, string.Empty);

            string displayName = AreAllNamesEqual(portNames) ? portNames[0] : "...";
            return (firstPortType ?? typeof(object), firstPortColor, displayName);
        }

        private (string nodeId, string portId) GetConnectedPortIds(EditorEdgeAsset edge, PortalNodeAsset linkedPortal)
        {
            // Entry Portal的输入端口连接到其他节点的输出端口
            // Exit Portal的输出端口连接到其他节点的输入端口
            if (linkedPortal.direction == PortalDirection.Entry)
                return (edge.outputNodeId, edge.outputPortId);
            else
                return (edge.inputNodeId, edge.inputPortId);
        }

        private IEditorPortView GetConnectedPortView(string nodeId, string portId)
        {
            var nodeView = graphView.graphElementCache.nodeViewById.GetValueOrDefault(nodeId);
            return nodeView?.GetPortView(portId);
        }

        private string GetPortDisplayName(IEditorPortView portView)
        {
            Type type = portView.info.portType;
            if (type == null || type == typeof(object))
                return portView.info.displayName ?? string.Empty;
            return type.Name;
        }

        private bool AreAllNamesEqual(List<string> names)
        {
            if (names.Count <= 1) return true;
            string first = names[0];
            for (int i = 1; i < names.Count; i++)
            {
                if (names[i] != first) return false;
            }
            return true;
        }

        public override void UpdateTitle()
        {
            if (_portalAsset != null)
                title = _portalAsset.title;
        }

        public override void Select()
        {
            base.Select();
            SetLinkedPortalsHighlight(true);
        }

        public override void Unselect()
        {
            base.Unselect();
            SetLinkedPortalsHighlight(false);
        }

        /// <summary>
        /// 设置关联Portal的高亮状态
        /// </summary>
        private void SetLinkedPortalsHighlight(bool highlight)
        {
            if (_portalAsset == null || string.IsNullOrEmpty(_portalAsset.portalGroupId))
                return;

            PortalDirection targetDirection = _portalAsset.direction == PortalDirection.Entry
                ? PortalDirection.Exit
                : PortalDirection.Entry;

            var linkedPortals = PortalHelper.FindLinkedPortals(graphView, _portalAsset, targetDirection);

            foreach (IEditorNodeView nodeView in linkedPortals)
            {
                if (nodeView is PortalEditorNodeView linkedPortalView)
                {
                    if (highlight)
                        linkedPortalView.SetFocus(HighlightColor);
                    else
                        linkedPortalView.ClearFocus();
                }
            }
        }

        /// <summary>
        /// 刷新端口视图，当连接改变时调用以更新端口信息和颜色
        /// </summary>
        public void RefreshPortFromConnections()
        {
            RebuildPortView();
            UpdateColorFromConnections();
        }

        private void UpdateColorFromConnections()
        {
            var (_, portColor, _) = GetConnectionInfo();
            SetColor(portColor != Color.white ? portColor : GetDefaultColor());
        }
    }
}
