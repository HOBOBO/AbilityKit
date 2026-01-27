using System;
using System.Collections.Generic;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// Portal组信息，包含同一组内的所有Entry和Exit Portal
    /// </summary>
    public struct PortalGroupInfo
    {
        /// <summary>
        /// 组内所有Entry Portal视图
        /// </summary>
        public List<IEditorNodeView> entryPortals;

        /// <summary>
        /// 组内所有Exit Portal视图
        /// </summary>
        public List<IEditorNodeView> exitPortals;

        /// <summary>
        /// 创建空的Portal组信息
        /// </summary>
        public static PortalGroupInfo Create()
        {
            return new PortalGroupInfo
            {
                entryPortals = new List<IEditorNodeView>(),
                exitPortals = new List<IEditorNodeView>()
            };
        }

        /// <summary>
        /// 获取组内所有Portal
        /// </summary>
        public List<IEditorNodeView> GetAllPortals()
        {
            var all = new List<IEditorNodeView>(entryPortals.Count + exitPortals.Count);
            all.AddRange(entryPortals);
            all.AddRange(exitPortals);
            return all;
        }

        /// <summary>
        /// 组是否为空
        /// </summary>
        public bool IsEmpty => entryPortals.Count == 0 && exitPortals.Count == 0;
    }

    /// <summary>
    /// Portal辅助工具类，提供Portal节点的创建、查找和管理功能
    /// </summary>
    public static class PortalHelper
    {
        /// <summary>
        /// Portal端口的固定ID
        /// </summary>
        public const string PortalPortId = "portal_port";

        /// <summary>
        /// 查找指定组内的所有Portal节点
        /// </summary>
        /// <param name="graphView">图视图</param>
        /// <param name="portalGroupId">Portal组ID</param>
        /// <returns>包含Entry和Exit Portal的组信息</returns>
        public static PortalGroupInfo FindPortalsInGroup(EditorGraphView graphView, string portalGroupId)
        {
            var groupInfo = PortalGroupInfo.Create();

            if (string.IsNullOrEmpty(portalGroupId)) return groupInfo;

            foreach (var kvp in graphView.graphElementCache.nodeViewById)
            {
                if (kvp.Value.asset is PortalNodeAsset portal && portal.portalGroupId == portalGroupId)
                {
                    if (portal.direction == PortalDirection.Entry)
                        groupInfo.entryPortals.Add(kvp.Value);
                    else
                        groupInfo.exitPortals.Add(kvp.Value);
                }
            }

            return groupInfo;
        }

        /// <summary>
        /// 查找指定Portal的关联Portal节点
        /// </summary>
        /// <param name="graphView">图视图</param>
        /// <param name="portalAsset">源Portal资产</param>
        /// <param name="targetDirection">目标Portal方向</param>
        /// <returns>符合条件的Portal节点视图列表</returns>
        public static List<IEditorNodeView> FindLinkedPortals(
            EditorGraphView graphView,
            PortalNodeAsset portalAsset,
            PortalDirection targetDirection)
        {
            var result = new List<IEditorNodeView>();

            if (portalAsset == null || string.IsNullOrEmpty(portalAsset.portalGroupId))
                return result;

            foreach (var kvp in graphView.graphElementCache.nodeViewById)
            {
                if (kvp.Value.asset is PortalNodeAsset otherPortal &&
                    otherPortal.portalGroupId == portalAsset.portalGroupId &&
                    otherPortal.direction == targetDirection)
                {
                    result.Add(kvp.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// 通过ID查找Portal节点视图
        /// </summary>
        public static IEditorNodeView FindPortalById(EditorGraphView graphView, string portalId)
        {
            if (string.IsNullOrEmpty(portalId)) return null;
            return graphView.graphElementCache.nodeViewById.GetValueOrDefault(portalId);
        }

        /// <summary>
        /// 获取Portal节点的端口视图
        /// </summary>
        public static IEditorPortView GetPortalPort(IEditorNodeView portalNodeView)
        {
            return portalNodeView?.GetPortView(PortalPortId);
        }

        /// <summary>
        /// 生成端口的唯一标识键
        /// </summary>
        /// <param name="portView">端口视图</param>
        /// <returns>格式为 "nodeId_portId" 的唯一键</returns>
        public static string GetPortKey(IEditorPortView portView)
        {
            if (portView?.master?.asset == null) return string.Empty;
            return $"{portView.master.asset.id}_{portView.info.id}";
        }

        /// <summary>
        /// 创建Portal节点
        /// </summary>
        /// <param name="graphView">图视图</param>
        /// <param name="direction">Portal方向</param>
        /// <param name="position">节点位置</param>
        /// <param name="portalGroupId">Portal组ID</param>
        /// <param name="portOrientation">端口方向</param>
        /// <param name="registerUndo">是否注册撤销（默认为true）</param>
        /// <returns>创建的Portal节点视图</returns>
        public static IEditorNodeView CreatePortalNode(
            EditorGraphView graphView,
            PortalDirection direction,
            Vector2 position,
            string portalGroupId,
            EditorOrientation portOrientation,
            bool registerUndo = true)
        {
            PortalNodeAsset portalAsset = ScriptableObject.CreateInstance<PortalNodeAsset>();
            portalAsset.id = Guid.NewGuid().ToString();
            portalAsset.position = new Rect(position, new Vector2(100, 60));
            portalAsset.direction = direction;
            portalAsset.portalGroupId = portalGroupId;
            portalAsset.portOrientation = portOrientation;

            if (registerUndo)
            {
                Undo.RegisterCreatedObjectUndo(portalAsset, "Create Portal Node");
                graphView.RegisterCompleteObjectUndo("Create Portal Node");
            }

            return graphView.AddNode(portalAsset);
        }

        /// <summary>
        /// 创建一对关联的Portal节点
        /// </summary>
        /// <param name="graphView">图视图</param>
        /// <param name="entryPosition">Entry Portal位置</param>
        /// <param name="exitPosition">Exit Portal位置</param>
        /// <param name="portOrientation">端口方向</param>
        /// <returns>Entry和Exit Portal节点视图的元组</returns>
        public static (IEditorNodeView entry, IEditorNodeView exit) CreatePortalPair(
            EditorGraphView graphView,
            Vector2 entryPosition,
            Vector2 exitPosition,
            EditorOrientation portOrientation)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            string portalGroupId = Guid.NewGuid().ToString();

            var entryNodeView = CreatePortalNode(graphView, PortalDirection.Entry, entryPosition, portalGroupId, portOrientation);
            var exitNodeView = CreatePortalNode(graphView, PortalDirection.Exit, exitPosition, portalGroupId, portOrientation);

            var entryPortal = entryNodeView.asset as PortalNodeAsset;
            var exitPortal = exitNodeView.asset as PortalNodeAsset;

            if (entryPortal != null && exitPortal != null)
            {
                Undo.RecordObject(entryPortal, "Link Portal Pair");
                Undo.RecordObject(exitPortal, "Link Portal Pair");

                entryPortal.linkedPortalId = exitPortal.id;
                exitPortal.linkedPortalId = entryPortal.id;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Undo.SetCurrentGroupName("Create Portal Pair");

            return (entryNodeView, exitNodeView);
        }

        /// <summary>
        /// 连接Portal到指定端口
        /// </summary>
        /// <param name="graphView">图视图</param>
        /// <param name="portalNodeView">Portal节点视图</param>
        /// <param name="targetPort">目标端口</param>
        /// <returns>创建的边视图，失败返回null</returns>
        public static IEditorEdgeView ConnectPortalToPort(
            EditorGraphView graphView,
            IEditorNodeView portalNodeView,
            IEditorPortView targetPort)
        {
            IEditorPortView portalPort = GetPortalPort(portalNodeView);
            if (portalPort == null || targetPort == null) return null;

            var portalAsset = portalNodeView.asset as PortalNodeAsset;
            if (portalAsset == null) return null;

            // Entry Portal连接：targetPort(output) -> portalPort(input)
            // Exit Portal连接：portalPort(output) -> targetPort(input)
            if (portalAsset.direction == PortalDirection.Entry)
                return graphView.connectSystem.Connect(portalPort, targetPort);
            else
                return graphView.connectSystem.Connect(targetPort, portalPort);
        }

        /// <summary>
        /// 刷新Portal视图的端口信息
        /// </summary>
        public static void RefreshPortalView(IEditorNodeView portalNodeView)
        {
            if (portalNodeView is PortalEditorNodeView portalView)
            {
                portalView.RefreshPortFromConnections();
            }
        }

        /// <summary>
        /// 刷新多个Portal视图
        /// </summary>
        public static void RefreshPortalViews(IEnumerable<IEditorNodeView> portalNodeViews)
        {
            foreach (var nodeView in portalNodeViews)
            {
                RefreshPortalView(nodeView);
            }
        }
    }
}
