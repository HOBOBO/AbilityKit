using System.Collections.Generic;
using System.Linq;
using Emilia.Node.Editor;
using UnityEditor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 将Portal节点还原为普通边的操作。
    /// 选中任意Portal后，会还原整个Portal组（Entry和所有关联的Exit）。
    /// </summary>
    [Action("Convert/Revert Portals to Edges", 7001, OperateMenuTagDefine.UniversalActionTag)]
    public class RevertPortalsToEdgesAction : OperateMenuAction
    {
        /// <summary>
        /// 待创建的连接信息
        /// </summary>
        private struct ConnectionInfo
        {
            public IEditorPortView outputPort;
            public IEditorPortView inputPort;
        }

        public override OperateMenuActionValidity GetValidity(OperateMenuContext context)
        {
            var graphData = context.graphView.GetGraphData<UniversalGraphData>();
            if (graphData.graphSetting.disabledTransmitNode)
                return OperateMenuActionValidity.NotApplicable;

            return GetSelectedPortals(context.graphView).Count > 0
                ? OperateMenuActionValidity.Valid
                : OperateMenuActionValidity.NotApplicable;
        }

        public override void Execute(OperateMenuActionContext context)
        {
            RevertPortals(context.graphView);
        }

        private void RevertPortals(EditorGraphView graphView)
        {
            var selectedPortals = GetSelectedPortals(graphView);
            if (selectedPortals.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            graphView.RegisterCompleteObjectUndo("Revert Portals to Edges");

            var (connections, portalsToDelete) = CollectRevertData(graphView, selectedPortals);
            DeletePortals(graphView, portalsToDelete);
            CreateDirectConnections(graphView, connections);

            graphView.UpdateSelected();
            graphView.graphSave.SetDirty();

            Undo.CollapseUndoOperations(undoGroup);
            Undo.SetCurrentGroupName("Revert Portals to Edges");
        }

        private List<IEditorNodeView> GetSelectedPortals(EditorGraphView graphView)
        {
            return graphView.selection
                .OfType<IEditorNodeView>()
                .Where(n => n.asset is PortalNodeAsset)
                .ToList();
        }

        /// <summary>
        /// 收集还原操作所需的数据：要创建的连接和要删除的Portal
        /// </summary>
        private (List<ConnectionInfo> connections, List<IEditorNodeView> portals) CollectRevertData(
            EditorGraphView graphView,
            List<IEditorNodeView> selectedPortals)
        {
            var processedGroupIds = new HashSet<string>();
            var connections = new List<ConnectionInfo>();
            var portalsToDelete = new List<IEditorNodeView>();

            foreach (IEditorNodeView nodeView in selectedPortals)
            {
                if (nodeView.asset is not PortalNodeAsset portalAsset)
                    continue;

                if (processedGroupIds.Contains(portalAsset.portalGroupId))
                    continue;

                processedGroupIds.Add(portalAsset.portalGroupId);
                ProcessPortalGroup(graphView, portalAsset.portalGroupId, connections, portalsToDelete);
            }

            return (connections, portalsToDelete);
        }

        /// <summary>
        /// 处理单个Portal组，收集其连接信息和Portal节点
        /// </summary>
        private void ProcessPortalGroup(
            EditorGraphView graphView,
            string portalGroupId,
            List<ConnectionInfo> connections,
            List<IEditorNodeView> portalsToDelete)
        {
            var groupInfo = PortalHelper.FindPortalsInGroup(graphView, portalGroupId);

            CollectConnectionsFromGroup(groupInfo, connections);
            AddUniquePortals(groupInfo.GetAllPortals(), portalsToDelete);
        }

        /// <summary>
        /// 从Portal组收集需要重建的连接
        /// </summary>
        private void CollectConnectionsFromGroup(PortalGroupInfo groupInfo, List<ConnectionInfo> connections)
        {
            foreach (IEditorNodeView entryView in groupInfo.entryPortals)
            {
                IEditorPortView entryPort = PortalHelper.GetPortalPort(entryView);
                if (entryPort == null) continue;

                // 获取Entry Portal连接的源输出端口
                foreach (IEditorEdgeView entryEdge in entryPort.edges)
                {
                    IEditorPortView sourceOutput = entryEdge.outputPortView;
                    if (sourceOutput == null) continue;

                    // 收集所有Exit Portal连接的目标输入端口
                    CollectExitConnections(groupInfo.exitPortals, sourceOutput, connections);
                }
            }
        }

        /// <summary>
        /// 收集Exit Portal到目标节点的连接
        /// </summary>
        private void CollectExitConnections(
            List<IEditorNodeView> exitPortals,
            IEditorPortView sourceOutput,
            List<ConnectionInfo> connections)
        {
            foreach (IEditorNodeView exitView in exitPortals)
            {
                IEditorPortView exitPort = PortalHelper.GetPortalPort(exitView);
                if (exitPort == null) continue;

                foreach (IEditorEdgeView exitEdge in exitPort.edges)
                {
                    IEditorPortView targetInput = exitEdge.inputPortView;
                    if (targetInput != null)
                    {
                        connections.Add(new ConnectionInfo
                        {
                            outputPort = sourceOutput,
                            inputPort = targetInput
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 添加不重复的Portal到删除列表
        /// </summary>
        private void AddUniquePortals(List<IEditorNodeView> portals, List<IEditorNodeView> targetList)
        {
            foreach (var portal in portals)
            {
                if (!targetList.Contains(portal))
                    targetList.Add(portal);
            }
        }

        private void DeletePortals(EditorGraphView graphView, List<IEditorNodeView> portals)
        {
            graphView.ClearSelection();

            foreach (IEditorNodeView portal in portals)
                graphView.AddToSelection(portal.element);

            graphView.graphOperate.Delete();
        }

        private void CreateDirectConnections(EditorGraphView graphView, List<ConnectionInfo> connections)
        {
            foreach (var conn in connections)
            {
                if (graphView.connectSystem.CanConnect(conn.inputPort, conn.outputPort))
                {
                    graphView.connectSystem.Connect(conn.inputPort, conn.outputPort);
                }
            }
        }
    }
}
