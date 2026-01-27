using System.Collections.Generic;
using Emilia.Node.Editor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// Portal节点方向枚举
    /// </summary>
    public enum PortalDirection
    {
        /// <summary>
        /// 入口Portal，接收来自其他节点输出端口的连接
        /// </summary>
        Entry,

        /// <summary>
        /// 出口Portal，将连接发送到其他节点的输入端口
        /// </summary>
        Exit
    }

    /// <summary>
    /// Portal节点资产，用于将长距离边连接转换为Portal传送连接。
    /// Portal是透传节点，在图遍历时逻辑透明，不影响实际的节点连接关系。
    /// Entry和Exit Portal成对存在，通过portalGroupId关联。
    /// </summary>
    [HideMonoScript]
    public class PortalNodeAsset : UniversalNodeAsset
    {
        [SerializeField, HideInInspector]
        private PortalDirection _direction;

        [SerializeField, HideInInspector]
        private string _portalGroupId;

        [SerializeField, HideInInspector]
        private string _linkedPortalId;

        [SerializeField, HideInInspector]
        private EditorOrientation _portOrientation = EditorOrientation.Horizontal;

        /// <summary>
        /// Portal方向（Entry或Exit）
        /// </summary>
        public PortalDirection direction
        {
            get => _direction;
            set => _direction = value;
        }

        /// <summary>
        /// Portal组ID，同一组的Portal可以互相透传连接
        /// </summary>
        public string portalGroupId
        {
            get => _portalGroupId;
            set => _portalGroupId = value;
        }

        /// <summary>
        /// 关联的Portal节点ID（Entry关联Exit，Exit关联Entry）
        /// </summary>
        public string linkedPortalId
        {
            get => _linkedPortalId;
            set => _linkedPortalId = value;
        }

        /// <summary>
        /// 端口方向（水平或垂直）
        /// </summary>
        public EditorOrientation portOrientation
        {
            get => _portOrientation;
            set => _portOrientation = value;
        }

        protected override string defaultDisplayName => direction == PortalDirection.Entry ? "Portal Entry" : "Portal Exit";

        public override string title => string.IsNullOrEmpty(displayName) ? defaultDisplayName : displayName;

        /// <summary>
        /// 获取逻辑输出节点，实现Portal的透传遍历。
        /// Entry Portal会跳转到关联的Exit Portal获取其输出节点。
        /// Exit Portal会获取其直接连接的目标节点。
        /// </summary>
        public override List<EditorNodeAsset> GetLogicalOutputNodes(HashSet<string> visited = null)
        {
            if (graphAsset == null) return new List<EditorNodeAsset>();

            visited ??= new HashSet<string>();
            if (visited.Contains(id)) return new List<EditorNodeAsset>();
            visited.Add(id);

            var result = new List<EditorNodeAsset>();

            if (direction == PortalDirection.Entry)
            {
                AppendLinkedPortalOutputs(result, visited);
            }
            else
            {
                AppendDirectOutputs(result, visited);
            }

            return result;
        }

        /// <summary>
        /// 获取逻辑输入节点，实现Portal的透传遍历。
        /// Exit Portal会跳转到关联的Entry Portal获取其输入节点。
        /// Entry Portal会获取其直接连接的源节点。
        /// </summary>
        public override List<EditorNodeAsset> GetLogicalInputNodes(HashSet<string> visited = null)
        {
            if (graphAsset == null) return new List<EditorNodeAsset>();

            visited ??= new HashSet<string>();
            if (visited.Contains(id)) return new List<EditorNodeAsset>();
            visited.Add(id);

            var result = new List<EditorNodeAsset>();

            if (direction == PortalDirection.Exit)
            {
                AppendLinkedPortalInputs(result, visited);
            }
            else
            {
                AppendDirectInputs(result, visited);
            }

            return result;
        }

        /// <summary>
        /// 通过关联Portal获取输出节点（Entry Portal使用）
        /// </summary>
        private void AppendLinkedPortalOutputs(List<EditorNodeAsset> result, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(linkedPortalId)) return;

            EditorNodeAsset linkedPortal = graphAsset.nodeMap.GetValueOrDefault(linkedPortalId);
            if (linkedPortal != null)
            {
                result.AddRange(linkedPortal.GetLogicalOutputNodes(visited));
            }
        }

        /// <summary>
        /// 获取直接连接的输出节点（Exit Portal使用）
        /// </summary>
        private void AppendDirectOutputs(List<EditorNodeAsset> result, HashSet<string> visited)
        {
            List<EditorEdgeAsset> edges = graphAsset.GetOutputEdges(this);
            foreach (EditorEdgeAsset edge in edges)
            {
                EditorNodeAsset targetNode = graphAsset.nodeMap.GetValueOrDefault(edge.inputNodeId);
                if (targetNode != null)
                {
                    result.AddRange(targetNode.GetLogicalOutputNodes(visited));
                }
            }
        }

        /// <summary>
        /// 通过关联Portal获取输入节点（Exit Portal使用）
        /// </summary>
        private void AppendLinkedPortalInputs(List<EditorNodeAsset> result, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(linkedPortalId)) return;

            EditorNodeAsset linkedPortal = graphAsset.nodeMap.GetValueOrDefault(linkedPortalId);
            if (linkedPortal != null)
            {
                result.AddRange(linkedPortal.GetLogicalInputNodes(visited));
            }
        }

        /// <summary>
        /// 获取直接连接的输入节点（Entry Portal使用）
        /// </summary>
        private void AppendDirectInputs(List<EditorNodeAsset> result, HashSet<string> visited)
        {
            List<EditorEdgeAsset> edges = graphAsset.GetInputEdges(this);
            foreach (EditorEdgeAsset edge in edges)
            {
                EditorNodeAsset sourceNode = graphAsset.nodeMap.GetValueOrDefault(edge.outputNodeId);
                if (sourceNode != null)
                {
                    result.AddRange(sourceNode.GetLogicalInputNodes(visited));
                }
            }
        }
    }
}
