//------------------------------------------------------------
//        File:  NodePort.cs
//       Brief:  NodePort
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-05
//============================================================

using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTCore.Editor
{
    public class NodePort : Port
    {
        /// <summary>
        /// 主要是监听端口边的连接
        /// 源码地址：https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/GraphViewEditor/Elements/Port.cs
        /// </summary>
        private class DefaultEdgeConnectorListener : IEdgeConnectorListener
        {
            private GraphViewChange m_GraphViewChange;
            private List<Edge> m_EdgesToCreate; 
            private List<GraphElement> m_EdgesToDelete;

            public DefaultEdgeConnectorListener()
            {
                m_EdgesToCreate = new List<Edge>();
                m_EdgesToDelete = new List<GraphElement>();

                m_GraphViewChange.edgesToCreate = m_EdgesToCreate;
            }

            /// <summary>
            /// 当边拖拽到空区域，非端口位置
            /// </summary>
            /// <param name="edge"></param>
            /// <param name="position"></param>
            public void OnDropOutsidePort(Edge edge, Vector2 position) {
                var nodeEditor = (BTNodeView) null;
                var isAsParent = false;
                if (edge.output != null) {
                    nodeEditor = edge.output.node as BTNodeView;
                    isAsParent = true;
                }

                if (edge.input != null) {
                    nodeEditor = edge.input.node as BTNodeView;
                }
                
                // 打开节点搜索窗口
                NodeSearchWindow.Show(position, nodeEditor, isAsParent);
            }
            
            public void OnDrop(GraphView graphView, Edge edge)
            {
                m_EdgesToCreate.Clear();
                m_EdgesToCreate.Add(edge);

                // We can't just add these edges to delete to the m_GraphViewChange
                // because we want the proper deletion code in GraphView to also
                // be called. Of course, that code (in DeleteElements) also
                // sends a GraphViewChange.
                m_EdgesToDelete.Clear();
                if (edge.input.capacity == Capacity.Single)
                    foreach (Edge edgeToDelete in edge.input.connections)
                        if (edgeToDelete != edge)
                            m_EdgesToDelete.Add(edgeToDelete);
                if (edge.output.capacity == Capacity.Single)
                    foreach (Edge edgeToDelete in edge.output.connections)
                        if (edgeToDelete != edge)
                            m_EdgesToDelete.Add(edgeToDelete);
                if (m_EdgesToDelete.Count > 0)
                    graphView.DeleteElements(m_EdgesToDelete);

                var edgesToCreate = m_EdgesToCreate;
                if (graphView.graphViewChanged != null)
                {
                    edgesToCreate = graphView.graphViewChanged(m_GraphViewChange).edgesToCreate;
                }

                foreach (Edge e in edgesToCreate)
                {
                    graphView.AddElement(e);
                    edge.input.Connect(e);
                    edge.output.Connect(e);
                }
            }
        }
        
        public NodePort(Direction portDirection, Capacity portCapacity) 
            : base(Orientation.Vertical, portDirection, portCapacity, typeof(bool)) {
            var connectorListener = new DefaultEdgeConnectorListener();
            m_EdgeConnector = new EdgeConnector<Edge>(connectorListener);
            this.AddManipulator(m_EdgeConnector);
        }
    }
}
