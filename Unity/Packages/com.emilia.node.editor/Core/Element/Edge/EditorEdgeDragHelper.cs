using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit.Editor;
using Emilia.Reflection.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 重写EdgeDragHelper
    /// </summary>
    public class EditorEdgeDragHelper : EdgeDragHelper
    {
        public const int PanInterval = 10;
        public const int PanAreaWidth = 100;
        public const int PanSpeed = 4;
        public const float MaxSpeedFactor = 2.5f;
        public const float MaxPanSpeed = MaxSpeedFactor * PanSpeed;

        protected static NodeAdapter nodeAdapter = new();

        private Type edgeViewType;
        protected List<IEditorPortView> portViews = new();
        protected Dictionary<IEditorNodeView, List<IEditorPortView>> nodeByPort = new();

        protected Dictionary<IEditorPortView, IEditorEdgeView> preConnectEdgeViews = new();

        protected IEditorEdgeView ghostEdge;
        protected EditorGraphView graphView;
        protected GraphEdgeConnectorListener listener;

        protected IVisualElementScheduledItem panScheduled;
        protected Vector3 panDiff;
        protected bool wasPanned;

        protected IEditorEdgeView edgeCandidateView;
        protected IEditorPortView draggedPortView;

        protected IEditorPortView disconnectPortView;

        public bool resetPositionOnPan { get; set; }

        public override Edge edgeCandidate
        {
            get => edgeCandidateView?.edgeElement;
            set => edgeCandidateView = value as IEditorEdgeView;
        }

        public override Port draggedPort
        {
            get => draggedPortView?.portElement;
            set => draggedPortView = value as IEditorPortView;
        }

        public EditorEdgeDragHelper(Type edgeViewType, GraphEdgeConnectorListener edgeConnectorListener)
        {
            this.edgeViewType = edgeViewType;
            listener = edgeConnectorListener;
            resetPositionOnPan = true;
            Reset();
        }

        public override void Reset(bool didConnect = false)
        {
            if (graphView != null) graphView.ports.ForEach((p) => p.OnStopEdgeDragging());
            if (portViews.Count > 0) portViews.Clear();
            if (ghostEdge != null && graphView != null) graphView.RemoveElement(ghostEdge.edgeElement);

            if (wasPanned)
            {
                if (resetPositionOnPan == false || didConnect)
                {
                    Vector3 p = graphView.contentViewContainer.transform.position;
                    Vector3 s = graphView.contentViewContainer.transform.scale;
                    graphView.UpdateViewTransform(p, s);
                }
            }

            if (panScheduled != null) panScheduled.Pause();

            if (ghostEdge != null)
            {
                ghostEdge.inputPortView = null;
                ghostEdge.outputPortView = null;
            }

            if (draggedPort != null && didConnect == false)
            {
                draggedPort.portCapLit = false;
                draggedPort = null;
            }

            if (edgeCandidate != null) edgeCandidate.SetEnabled(true);

            foreach (var itemPair in preConnectEdgeViews) itemPair.Value.edgeElement.RemoveFromHierarchy();
            preConnectEdgeViews.Clear();

            ghostEdge = null;
            edgeCandidate = null;
        }

        public override bool HandleMouseDown(MouseDownEvent evt)
        {
            Vector2 mousePosition = evt.mousePosition;

            if (draggedPort == null || edgeCandidate == null) return false;

            GraphView view = draggedPort.GetFirstAncestorOfType<GraphView>();
            graphView = view as EditorGraphView;

            if (graphView == null) return false;

            if (edgeCandidate.parent == null) graphView.AddElement(edgeCandidate);

            edgeCandidate.candidatePosition = mousePosition;
            edgeCandidate.SetEnabled(false);
            edgeCandidateView.isDrag = true;

            if (edgeCandidateView.outputPortView != null && edgeCandidateView.inputPortView != null)
            {
                if (edgeCandidateView.outputPortView == this.draggedPortView)
                {
                    disconnectPortView = edgeCandidateView.inputPortView;
                    edgeCandidateView.inputPortView = null;
                }
                else
                {
                    disconnectPortView = edgeCandidateView.outputPortView;
                    edgeCandidateView.outputPortView = null;
                }
            }
            else
            {
                bool startFromOutput = draggedPortView.portDirection == EditorPortDirection.Output || draggedPortView.portDirection == EditorPortDirection.Any;

                if (startFromOutput)
                {
                    edgeCandidateView.outputPortView = draggedPortView;
                    edgeCandidateView.inputPortView = null;
                }
                else
                {
                    edgeCandidateView.outputPortView = null;
                    edgeCandidateView.inputPortView = draggedPortView;
                }
            }

            draggedPort.portCapLit = true;

            bool canMultiConnect = draggedPortView.info.canMultiConnect || draggedPortView.edges.Count <= 0;
            bool isRedirection = disconnectPortView != null;

            if (canMultiConnect || isRedirection)
            {
                var compatiblePorts = graphView.GetCompatiblePorts(draggedPort, nodeAdapter).OfType<IEditorPortView>();
                var filterCanMultiConnect = compatiblePorts.Where((port) => port.info.canMultiConnect || port.edges.Count <= 0);
                portViews.AddRange(filterCanMultiConnect);
            }

            graphView.ports.ForEach((p) => p.OnStartEdgeDragging());

            foreach (IEditorPortView compatiblePort in portViews)
            {
                compatiblePort.portElement.highlight = true;

                IEditorNodeView compatiblePortMaster = compatiblePort.master;
                if (this.nodeByPort.ContainsKey(compatiblePortMaster) == false) nodeByPort.Add(compatiblePortMaster, new List<IEditorPortView>());
                nodeByPort[compatiblePortMaster].Add(compatiblePort);
            }

            edgeCandidate.UpdateEdgeControl();

            if (panScheduled == null)
            {
                panScheduled = this.graphView.schedule.Execute(Pan).Every(PanInterval).StartingIn(PanInterval);
                panScheduled.Pause();
            }

            void Pan(TimerState timerState)
            {
                this.graphView.viewTransform.position -= this.panDiff;
                edgeCandidate.ForceUpdateEdgeControl_Internal();
                wasPanned = true;
            }

            wasPanned = false;

            edgeCandidate.layer = int.MaxValue;

            return true;
        }

        public override void HandleMouseMove(MouseMoveEvent evt)
        {
            VisualElement element = (VisualElement) evt.target;
            Vector2 gvMousePos = element.ChangeCoordinatesTo(this.graphView.contentContainer, evt.localMousePosition);
            graphView?.viewTransformChanged(graphView);
            panDiff = GetEffectivePanSpeed(gvMousePos);

            if (panDiff != Vector3.zero) panScheduled.Resume();
            else panScheduled.Pause();

            Vector2 mousePosition = evt.mousePosition;

            edgeCandidate.candidatePosition = mousePosition;

            IEditorPortView endPort = GetEndPort(mousePosition);

            if (endPort != null)
            {
                if (evt.shiftKey && this.disconnectPortView == null)
                {
                    if (preConnectEdgeViews.ContainsKey(endPort) == false)
                    {
                        IEditorEdgeView preConnectEdgeView = ReflectUtility.CreateInstance(this.edgeViewType) as IEditorEdgeView;
                        preConnectEdgeView.edgeElement.isGhostEdge = true;
                        preConnectEdgeView.edgeElement.pickingMode = PickingMode.Ignore;
                        this.graphView.AddElement(preConnectEdgeView.edgeElement);

                        if (this.edgeCandidateView.outputPortView == null)
                        {
                            preConnectEdgeView.inputPortView = edgeCandidateView.inputPortView;
                            if (preConnectEdgeView.outputPortView != null) preConnectEdgeView.outputPortView.portElement.portCapLit = false;
                            preConnectEdgeView.outputPortView = endPort.portElement as IEditorPortView;
                            preConnectEdgeView.outputPortView.portElement.portCapLit = true;
                        }
                        else
                        {
                            if (preConnectEdgeView.inputPortView != null) preConnectEdgeView.inputPortView.portElement.portCapLit = false;
                            preConnectEdgeView.inputPortView = endPort.portElement as IEditorPortView;
                            preConnectEdgeView.inputPortView.portElement.portCapLit = true;
                            preConnectEdgeView.outputPortView = edgeCandidateView.outputPortView;
                        }

                        preConnectEdgeViews[endPort] = preConnectEdgeView;
                    }
                }

                if (ghostEdge == null)
                {
                    ghostEdge = ReflectUtility.CreateInstance(this.edgeViewType) as IEditorEdgeView;
                    ghostEdge.edgeElement.isGhostEdge = true;
                    ghostEdge.edgeElement.pickingMode = PickingMode.Ignore;
                    this.graphView.AddElement(ghostEdge.edgeElement);
                }

                if (this.edgeCandidateView.outputPortView == null)
                {
                    ghostEdge.inputPortView = edgeCandidateView.inputPortView;
                    if (ghostEdge.outputPortView != null) ghostEdge.outputPortView.portElement.portCapLit = false;
                    ghostEdge.outputPortView = endPort.portElement as IEditorPortView;
                    ghostEdge.outputPortView.portElement.portCapLit = true;
                }
                else
                {
                    if (ghostEdge.inputPortView != null) ghostEdge.inputPortView.portElement.portCapLit = false;
                    ghostEdge.inputPortView = endPort.portElement as IEditorPortView;
                    ghostEdge.inputPortView.portElement.portCapLit = true;
                    ghostEdge.outputPortView = edgeCandidateView.outputPortView;
                }
            }
            else if (ghostEdge != null)
            {
                if (edgeCandidate.input == null)
                {
                    if (ghostEdge.inputPortView != null) ghostEdge.inputPortView.portElement.portCapLit = false;
                }
                else
                {
                    if (ghostEdge.outputPortView != null) ghostEdge.outputPortView.portElement.portCapLit = false;
                }

                this.graphView.RemoveElement(ghostEdge.edgeElement);
                ghostEdge.inputPortView = null;
                ghostEdge.outputPortView = null;
                ghostEdge = null;
            }
        }

        public virtual Vector2 GetEffectivePanSpeed(Vector2 mousePos)
        {
            Vector2 effectiveSpeed = Vector2.zero;

            if (mousePos.x <= PanAreaWidth) effectiveSpeed.x = -((PanAreaWidth - mousePos.x) / PanAreaWidth + 0.5f) * PanSpeed;
            else if (mousePos.x >= this.graphView.contentContainer.layout.width - PanAreaWidth)
            {
                effectiveSpeed.x = ((mousePos.x - (this.graphView.contentContainer.layout.width - PanAreaWidth)) / PanAreaWidth + 0.5f) * PanSpeed;
            }

            if (mousePos.y <= PanAreaWidth) effectiveSpeed.y = -((PanAreaWidth - mousePos.y) / PanAreaWidth + 0.5f) * PanSpeed;
            else if (mousePos.y >= graphView.contentContainer.layout.height - PanAreaWidth)
            {
                effectiveSpeed.y = ((mousePos.y - (this.graphView.contentContainer.layout.height - PanAreaWidth)) / PanAreaWidth + 0.5f) * PanSpeed;
            }

            effectiveSpeed = Vector2.ClampMagnitude(effectiveSpeed, MaxPanSpeed);

            return effectiveSpeed;
        }

        public override void HandleMouseUp(MouseUpEvent evt)
        {
            bool didConnect = false;

            Vector2 mousePosition = evt.mousePosition;

            this.graphView.ports.ForEach((p) => p.OnStopEdgeDragging());

            IEditorPortView endPort = GetEndPort(mousePosition);

            if (disconnectPortView != null) disconnectPortView.portElement.Disconnect(edgeCandidateView.edgeElement);

            if (endPort == null && listener != null && preConnectEdgeViews.Count == 0) listener.OnDropOutsidePort(edgeCandidate, mousePosition);

            edgeCandidate.SetEnabled(true);

            if (edgeCandidate.input != null) edgeCandidate.input.portCapLit = false;
            if (edgeCandidate.output != null) edgeCandidate.output.portCapLit = false;

            this.graphView.RemoveElement(edgeCandidate);

            IEditorPortView port = draggedPort as IEditorPortView;

            IEditorPortView originalInputPort = edgeCandidateView.inputPortView;
            IEditorPortView originalOutputPort = edgeCandidateView.outputPortView;

            foreach (var itemPair in preConnectEdgeViews)
            {
                edgeCandidateView.inputPortView = originalInputPort;
                edgeCandidateView.outputPortView = originalOutputPort;

                if (edgeCandidateView.inputPortView == null) edgeCandidateView.inputPortView = itemPair.Key;
                else edgeCandidateView.outputPortView = itemPair.Key;

                listener.OnDrop(graphView, edgeCandidateView.edgeElement);
                didConnect = true;

                itemPair.Value.edgeElement.RemoveFromHierarchy();
            }

            if (port != null && endPort != null && preConnectEdgeViews.Count == 0)
            {
                if (edgeCandidateView.inputPortView == null) edgeCandidateView.inputPortView = endPort;
                else edgeCandidateView.outputPortView = endPort;

                listener.OnDrop(graphView, edgeCandidateView.edgeElement);
                didConnect = true;
            }
            else
            {
                edgeCandidateView.outputPortView?.portElement.Disconnect(edgeCandidateView.edgeElement);
                edgeCandidateView.inputPortView?.portElement.Disconnect(edgeCandidateView.edgeElement);
                edgeCandidateView.outputPortView = null;
                edgeCandidateView.inputPortView = null;
            }

            if (ghostEdge != null)
            {
                if (ghostEdge.inputPortView != null) ghostEdge.inputPortView.portElement.portCapLit = false;
                if (ghostEdge.outputPortView != null) ghostEdge.outputPortView.portElement.portCapLit = false;

                graphView.RemoveElement(ghostEdge.edgeElement);
                ghostEdge.inputPortView = null;
                ghostEdge.outputPortView = null;
                ghostEdge = null;
            }

            edgeCandidateView.isDrag = false;
            edgeCandidate.ResetLayer();

            preConnectEdgeViews.Clear();
            portViews.Clear();
            edgeCandidate = null;
            disconnectPortView = null;
            Reset(didConnect);
        }

        protected virtual IEditorPortView GetEndPort(Vector2 mousePosition)
        {
            if (this.graphView == null) return null;

            IEditorPortView endPort = null;

            Dictionary<IEditorPortView, Rect> portBonds = new();

            for (var i = 0; i < this.portViews.Count; i++)
            {
                IEditorPortView compatiblePort = this.portViews[i];
                Rect bounds = compatiblePort.portElement.worldBound;
                portBonds[compatiblePort] = bounds;
            }

            foreach (var pair in this.nodeByPort)
            {
                Rect nodeBounds = pair.Key.element.worldBound;
                if (nodeBounds.Contains(mousePosition) == false) continue;
                IEditorPortView nearestPort = GetNearestPort(pair.Value);
                if (nearestPort != null) endPort = nearestPort;
            }
            
            IEditorPortView GetNearestPort(List<IEditorPortView> ports)//获取最近的端口
            {
                IEditorPortView nearestPort = null;
                float minDistance = int.MaxValue;
                foreach (IEditorPortView port in ports)
                {
                    if (portBonds.TryGetValue(port, out Rect bounds) == false) continue;

                    bool isContains = bounds.Contains(mousePosition);
                    if (isContains)
                    {
                        nearestPort = port;
                        break;
                    }

                    Vector2 nearestPoint = mousePosition;
                    nearestPoint.x = Mathf.Clamp(nearestPoint.x, bounds.xMin, bounds.xMax);
                    nearestPoint.y = Mathf.Clamp(nearestPoint.y, bounds.yMin, bounds.yMax);

                    float distance = Vector2.Distance(mousePosition, nearestPoint);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPort = port;
                    }
                }

                return nearestPort;
            }

            return endPort;
        }
    }
}