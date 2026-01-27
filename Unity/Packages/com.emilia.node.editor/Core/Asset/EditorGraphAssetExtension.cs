using System.Collections.Generic;

namespace Emilia.Node.Editor
{
    public static class EditorGraphAssetExtension
    {
        /// <summary>
        /// 获取实际直接连接的Output节点
        /// </summary>
        public static List<EditorNodeAsset> GetActualOutputNodes(this EditorGraphAsset graphAsset, EditorNodeAsset nodeAsset)
        {
            List<EditorNodeAsset> outputNodes = new();

            int edgeCount = graphAsset.edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                EditorEdgeAsset edgeAsset = graphAsset.edges[i];

                if (edgeAsset.outputNodeId != nodeAsset.id) continue;

                EditorNodeAsset outputNode = graphAsset.nodeMap.GetValueOrDefault(edgeAsset.inputNodeId);
                if (outputNode == null) continue;

                outputNodes.Add(outputNode);
            }

            return outputNodes;
        }

        /// <summary>
        /// 获取实际直接连接的Input节点
        /// </summary>
        public static List<EditorNodeAsset> GetActualInputNodes(this EditorGraphAsset graphAsset, EditorNodeAsset nodeAsset)
        {
            List<EditorNodeAsset> inputNodes = new();

            int edgeCount = graphAsset.edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                EditorEdgeAsset edgeAsset = graphAsset.edges[i];

                if (edgeAsset.inputNodeId != nodeAsset.id) continue;

                EditorNodeAsset inputNode = graphAsset.nodeMap.GetValueOrDefault(edgeAsset.outputNodeId);
                if (inputNode == null) continue;

                inputNodes.Add(inputNode);
            }

            return inputNodes;
        }

        /// <summary>
        /// 获取所有Output逻辑节点
        /// </summary>
        public static List<EditorNodeAsset> GetAllOutputNodes(EditorNodeAsset nodeAsset)
        {
            List<EditorNodeAsset> outputNodes = new();
            HashSet<string> visited = new();
            visited.Add(nodeAsset.id);

            CollectAllLogicalOutputNodes(nodeAsset, outputNodes, visited);

            return outputNodes;
        }

        private static void CollectAllLogicalOutputNodes(
            EditorNodeAsset nodeAsset,
            List<EditorNodeAsset> outputNodes,
            HashSet<string> visited)
        {
            List<EditorNodeAsset> directOutputs = nodeAsset.GetLogicalOutputNodes();

            foreach (EditorNodeAsset outputNode in directOutputs)
            {
                if (visited.Add(outputNode.id) == false) continue;

                outputNodes.Add(outputNode);
                CollectAllLogicalOutputNodes(outputNode, outputNodes, visited);
            }
        }

        /// <summary>
        /// 获取所有Input逻辑节点
        /// </summary>
        public static List<EditorNodeAsset> GetAllInputNodes(EditorNodeAsset nodeAsset)
        {
            List<EditorNodeAsset> inputNodes = new();
            HashSet<string> visited = new();
            visited.Add(nodeAsset.id);

            CollectAllLogicalInputNodes(nodeAsset, inputNodes, visited);

            return inputNodes;
        }

        private static void CollectAllLogicalInputNodes(
            EditorNodeAsset nodeAsset,
            List<EditorNodeAsset> inputNodes,
            HashSet<string> visited)
        {
            List<EditorNodeAsset> directInputs = nodeAsset.GetLogicalInputNodes(visited);

            foreach (EditorNodeAsset inputNode in directInputs)
            {
                if (visited.Add(inputNode.id) == false) continue;

                inputNodes.Add(inputNode);
                CollectAllLogicalInputNodes(inputNode, inputNodes, visited);
            }
        }

        /// <summary>
        /// 获取所有实际直接连接的Output节点
        /// </summary>
        public static List<EditorNodeAsset> GetAllActualOutputNodes(this EditorGraphAsset graphAsset, EditorNodeAsset nodeAsset)
        {
            List<EditorNodeAsset> outputNodes = new();
            HashSet<string> visited = new();
            visited.Add(nodeAsset.id);

            CollectAllActualOutputNodes(graphAsset, nodeAsset, outputNodes, visited);

            return outputNodes;
        }

        private static void CollectAllActualOutputNodes(
            EditorGraphAsset graphAsset,
            EditorNodeAsset nodeAsset,
            List<EditorNodeAsset> outputNodes,
            HashSet<string> visited)
        {
            List<EditorNodeAsset> directOutputs = graphAsset.GetActualOutputNodes(nodeAsset);

            foreach (EditorNodeAsset outputNode in directOutputs)
            {
                if (visited.Contains(outputNode.id)) continue;
                visited.Add(outputNode.id);

                outputNodes.Add(outputNode);
                CollectAllActualOutputNodes(graphAsset, outputNode, outputNodes, visited);
            }
        }

        /// <summary>
        /// 获取所有实际直接连接的Input节点
        /// </summary>
        public static List<EditorNodeAsset> GetAllActualInputNodes(this EditorGraphAsset graphAsset, EditorNodeAsset nodeAsset)
        {
            List<EditorNodeAsset> inputNodes = new();
            HashSet<string> visited = new();
            visited.Add(nodeAsset.id);

            CollectAllActualInputNodes(graphAsset, nodeAsset, inputNodes, visited);

            return inputNodes;
        }

        private static void CollectAllActualInputNodes(
            EditorGraphAsset graphAsset,
            EditorNodeAsset nodeAsset,
            List<EditorNodeAsset> inputNodes,
            HashSet<string> visited)
        {
            List<EditorNodeAsset> directInputs = graphAsset.GetActualInputNodes(nodeAsset);

            foreach (EditorNodeAsset inputNode in directInputs)
            {
                if (visited.Contains(inputNode.id)) continue;
                visited.Add(inputNode.id);

                inputNodes.Add(inputNode);
                CollectAllActualInputNodes(graphAsset, inputNode, inputNodes, visited);
            }
        }

        /// <summary>
        /// 获取所有Output的Edge
        /// </summary>
        public static List<EditorEdgeAsset> GetOutputEdges(this EditorGraphAsset graphAsset, EditorNodeAsset nodeAsset)
        {
            List<EditorEdgeAsset> outputEdges = new();

            int edgeCount = graphAsset.edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                EditorEdgeAsset edgeAsset = graphAsset.edges[i];
                if (edgeAsset.outputNodeId != nodeAsset.id) continue;
                outputEdges.Add(edgeAsset);
            }

            return outputEdges;
        }

        /// <summary>
        /// 获取所有Input的Edge
        /// </summary>
        public static List<EditorEdgeAsset> GetInputEdges(this EditorGraphAsset graphAsset, EditorNodeAsset nodeAsset)
        {
            List<EditorEdgeAsset> inputEdges = new();

            int edgeCount = graphAsset.edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                EditorEdgeAsset edgeAsset = graphAsset.edges[i];
                if (edgeAsset.inputNodeId != nodeAsset.id) continue;
                inputEdges.Add(edgeAsset);
            }

            return inputEdges;
        }

        /// <summary>
        /// 获取指定端口的所有Edge
        /// </summary>
        public static List<EditorEdgeAsset> GetEdges(this EditorGraphAsset graphAsset, string nodeId, string portId)
        {
            List<EditorEdgeAsset> edges = new();

            int edgeCount = graphAsset.edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                EditorEdgeAsset edgeAsset = graphAsset.edges[i];
                if (edgeAsset.inputNodeId == nodeId && edgeAsset.inputPortId == portId)
                {
                    edges.Add(edgeAsset);
                    continue;
                }

                if (edgeAsset.outputNodeId == nodeId && edgeAsset.outputPortId == portId)
                {
                    edges.Add(edgeAsset);
                    continue;
                }
            }

            return edges;
        }
    }
}