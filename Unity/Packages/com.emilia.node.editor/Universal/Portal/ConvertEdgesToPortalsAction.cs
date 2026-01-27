using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 边转换操作的中间数据结构
    /// </summary>
    internal struct EdgeConversionInfo
    {
        public IEditorEdgeView edgeView;
        public IEditorPortView outputPort;
        public IEditorPortView inputPort;
    }

    /// <summary>
    /// Portal创建结果
    /// </summary>
    internal struct PortalCreationResult
    {
        public List<IEditorNodeView> nodeViews;
        public List<IEditorEdgeView> edgeViews;

        public static PortalCreationResult Create()
        {
            return new PortalCreationResult
            {
                nodeViews = new List<IEditorNodeView>(),
                edgeViews = new List<IEditorEdgeView>()
            };
        }
    }

    /// <summary>
    /// 将选中的边转换为Portal节点对的操作。
    /// 同一输出端口的多条边会共享一个Entry Portal，每条边对应一个Exit Portal。
    /// </summary>
    [Action("Convert/Convert to Portals", 7000, OperateMenuTagDefine.UniversalActionTag)]
    public class ConvertEdgesToPortalsAction : OperateMenuAction
    {
        private const float EntryOffsetX = 100f;
        private const float ExitOffsetX = -175f;
        private const float EntryOffsetY = 50f;
        private const float ExitOffsetY = -80f;
        private const float PortalOffset = 25f;
        private const float PortalSpacing = 60f;

        public override OperateMenuActionValidity GetValidity(OperateMenuContext context)
        {
            var graphData = context.graphView.GetGraphData<UniversalGraphData>();
            if (graphData.graphSetting.disabledTransmitNode)
                return OperateMenuActionValidity.NotApplicable;

            return GetSelectedEdges(context.graphView).Count > 0
                ? OperateMenuActionValidity.Valid
                : OperateMenuActionValidity.NotApplicable;
        }

        public override void Execute(OperateMenuActionContext context)
        {
            ConvertEdges(context.graphView);
        }

        private void ConvertEdges(EditorGraphView graphView)
        {
            var selectedEdges = GetSelectedEdges(graphView);
            if (selectedEdges.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            graphView.RegisterCompleteObjectUndo("Convert Edges to Portals");

            var edgeGroups = GroupEdgesByOutputPort(selectedEdges);
            var result = CreatePortalsForGroups(graphView, edgeGroups);

            FinalizeConversion(graphView, result);

            Undo.CollapseUndoOperations(undoGroup);
            Undo.SetCurrentGroupName("Convert Edges to Portals");
        }

        private List<IEditorEdgeView> GetSelectedEdges(EditorGraphView graphView)
        {
            return graphView.selection
                .OfType<IEditorEdgeView>()
                .Where(e => e.asset != null)
                .ToList();
        }

        /// <summary>
        /// 按输出端口对边进行分组，同一输出端口的边将共享Entry Portal
        /// </summary>
        private Dictionary<string, List<EdgeConversionInfo>> GroupEdgesByOutputPort(List<IEditorEdgeView> edges)
        {
            var groups = new Dictionary<string, List<EdgeConversionInfo>>();

            foreach (IEditorEdgeView edgeView in edges)
            {
                if (edgeView.inputPortView == null || edgeView.outputPortView == null)
                    continue;

                string outputPortKey = PortalHelper.GetPortKey(edgeView.outputPortView);

                if (!groups.TryGetValue(outputPortKey, out var edgeList))
                {
                    edgeList = new List<EdgeConversionInfo>();
                    groups[outputPortKey] = edgeList;
                }

                edgeList.Add(new EdgeConversionInfo
                {
                    edgeView = edgeView,
                    outputPort = edgeView.outputPortView,
                    inputPort = edgeView.inputPortView
                });
            }

            return groups;
        }

        private PortalCreationResult CreatePortalsForGroups(
            EditorGraphView graphView,
            Dictionary<string, List<EdgeConversionInfo>> edgeGroups)
        {
            var result = PortalCreationResult.Create();
            var exitCountByInputPort = new Dictionary<string, int>();

            foreach (var group in edgeGroups.Values)
            {
                if (group.Count == 0) continue;
                CreatePortalPairForGroup(graphView, group, exitCountByInputPort, result);
            }

            return result;
        }

        /// <summary>
        /// 为一组边创建Portal对：一个Entry Portal和多个Exit Portal
        /// </summary>
        private void CreatePortalPairForGroup(
            EditorGraphView graphView,
            List<EdgeConversionInfo> edgeGroup,
            Dictionary<string, int> exitCountByInputPort,
            PortalCreationResult result)
        {
            var firstEdge = edgeGroup[0];
            EditorOrientation orientation = firstEdge.outputPort.info.orientation;

            // 创建Entry Portal
            Vector2 entryPosition = CalculateEntryPosition(graphView, firstEdge.outputPort, orientation);
            string portalGroupId = Guid.NewGuid().ToString();

            var entryNodeView = PortalHelper.CreatePortalNode(
                graphView, PortalDirection.Entry, entryPosition, portalGroupId, orientation, registerUndo: false);
            var entryPortal = entryNodeView.asset as PortalNodeAsset;
            result.nodeViews.Add(entryNodeView);

            Undo.RegisterCreatedObjectUndo(entryPortal, "Create Portal Node");

            // 为每条边创建Exit Portal
            foreach (var edgeInfo in edgeGroup)
            {
                CreateExitPortalForEdge(graphView, edgeInfo, entryPortal, portalGroupId,
                    exitCountByInputPort, result);
            }

            // 连接Entry Portal到原始输出端口
            ConnectEntryToOutput(graphView, entryNodeView, firstEdge.outputPort, result);
        }

        private void CreateExitPortalForEdge(
            EditorGraphView graphView,
            EdgeConversionInfo edgeInfo,
            PortalNodeAsset entryPortal,
            string portalGroupId,
            Dictionary<string, int> exitCountByInputPort,
            PortalCreationResult result)
        {
            IEditorPortView inputPort = edgeInfo.inputPort;
            EditorOrientation orientation = inputPort.info.orientation;

            // 计算Exit Portal位置（考虑同一输入端口的多个Exit）
            string inputPortKey = PortalHelper.GetPortKey(inputPort);
            int exitIndex = exitCountByInputPort.GetValueOrDefault(inputPortKey, 0);
            exitCountByInputPort[inputPortKey] = exitIndex + 1;

            Vector2 exitPosition = CalculateExitPosition(graphView, inputPort, orientation, exitIndex);

            var exitNodeView = PortalHelper.CreatePortalNode(
                graphView, PortalDirection.Exit, exitPosition, portalGroupId, orientation, registerUndo: false);
            var exitPortal = exitNodeView.asset as PortalNodeAsset;
            result.nodeViews.Add(exitNodeView);

            Undo.RegisterCreatedObjectUndo(exitPortal, "Create Portal Node");

            // 建立Portal关联
            Undo.RecordObject(entryPortal, "Link Portal");
            Undo.RecordObject(exitPortal, "Link Portal");

            entryPortal.linkedPortalId = exitPortal.id;
            exitPortal.linkedPortalId = entryPortal.id;

            // 断开原边，创建新连接
            graphView.connectSystem.Disconnect(edgeInfo.edgeView);
            ConnectExitToInput(graphView, exitNodeView, inputPort, result);
        }

        private void ConnectEntryToOutput(
            EditorGraphView graphView,
            IEditorNodeView entryNodeView,
            IEditorPortView outputPort,
            PortalCreationResult result)
        {
            IEditorPortView entryPort = PortalHelper.GetPortalPort(entryNodeView);
            if (entryPort != null)
            {
                var edge = graphView.connectSystem.Connect(entryPort, outputPort);
                if (edge != null) result.edgeViews.Add(edge);
            }
        }

        private void ConnectExitToInput(
            EditorGraphView graphView,
            IEditorNodeView exitNodeView,
            IEditorPortView inputPort,
            PortalCreationResult result)
        {
            IEditorPortView exitPort = PortalHelper.GetPortalPort(exitNodeView);
            if (exitPort != null)
            {
                var edge = graphView.connectSystem.Connect(inputPort, exitPort);
                if (edge != null) result.edgeViews.Add(edge);
            }
        }

        private Vector2 CalculateEntryPosition(EditorGraphView graphView, IEditorPortView outputPort, EditorOrientation orientation)
        {
            Vector2 portPos = WorldToLocal(graphView, outputPort.portElement.worldBound.center);

            return orientation == EditorOrientation.Vertical
                ? new Vector2(portPos.x + PortalOffset, portPos.y + EntryOffsetY)
                : new Vector2(portPos.x + EntryOffsetX, portPos.y + PortalOffset);
        }

        private Vector2 CalculateExitPosition(EditorGraphView graphView, IEditorPortView inputPort, EditorOrientation orientation, int index)
        {
            Vector2 portPos = WorldToLocal(graphView, inputPort.portElement.worldBound.center);

            if (orientation == EditorOrientation.Vertical)
            {
                return new Vector2(
                    portPos.x - PortalOffset + index * PortalSpacing,
                    portPos.y + ExitOffsetY);
            }
            else
            {
                return new Vector2(
                    portPos.x + ExitOffsetX,
                    portPos.y - PortalOffset + index * PortalSpacing);
            }
        }

        private Vector2 WorldToLocal(EditorGraphView graphView, Vector2 worldPos)
        {
            return graphView.contentViewContainer.WorldToLocal(worldPos);
        }

        private void FinalizeConversion(EditorGraphView graphView, PortalCreationResult result)
        {
            // 更新边视图
            foreach (IEditorEdgeView edgeView in result.edgeViews)
                edgeView.ForceUpdateView();

            // 刷新Portal端口信息
            PortalHelper.RefreshPortalViews(result.nodeViews);

            // 选中新创建的Portal节点
            graphView.ClearSelection();
            foreach (var nodeView in result.nodeViews)
                graphView.AddToSelection(nodeView.element);

            graphView.UpdateSelected();
            graphView.graphSave.SetDirty();
        }
    }
}
