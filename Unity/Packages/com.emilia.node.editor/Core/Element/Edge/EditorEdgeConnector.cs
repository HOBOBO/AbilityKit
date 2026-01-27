using System;
using Emilia.Kit.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 重写EdgeConnector
    /// </summary>
    public class EditorEdgeConnector : EdgeConnector
    {
        public const float ConnectionDistanceTreshold = 10f;

        private Type edgeViewType;
        private EditorGraphView graphView;
        private EdgeDragHelper _edgeDragHelper;

        private IEditorEdgeView edgeViewCandidate;
        private bool active;
        private Vector2 downPosition;

        public override EdgeDragHelper edgeDragHelper => _edgeDragHelper;

        public virtual void Initialize(Type edgeViewType, GraphEdgeConnectorListener edgeConnectorListener)
        {
            this.edgeViewType = edgeViewType;
            this._edgeDragHelper = new EditorEdgeDragHelper(edgeViewType, edgeConnectorListener);
            this.graphView = edgeConnectorListener.graphView;

            activators.Add(new ManipulatorActivationFilter {button = MouseButton.LeftMouse});
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<KeyDownEvent>(OnKeyDown);
            target.RegisterCallback<MouseCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnCaptureOut);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (active)
            {
                evt.StopImmediatePropagation();
                return;
            }

            if (CanStartManipulation(evt) == false) return;

            Port graphElement = target as Port;
            if (graphElement == null) return;

            downPosition = evt.localMousePosition;

            this.edgeViewCandidate = ReflectUtility.CreateInstance(this.edgeViewType) as IEditorEdgeView;
            edgeDragHelper.draggedPort = graphElement;
            edgeDragHelper.edgeCandidate = this.edgeViewCandidate.edgeElement;
            edgeViewCandidate.Initialize(graphView, null);

            if (edgeDragHelper.HandleMouseDown(evt))
            {
                active = true;
                target.CaptureMouse();

                evt.StopPropagation();
            }
            else
            {
                edgeDragHelper.Reset();
                this.edgeViewCandidate = null;
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (active == false) return;

            edgeDragHelper.HandleMouseMove(evt);
            this.edgeViewCandidate.edgeElement.candidatePosition = evt.mousePosition;
            this.edgeViewCandidate.edgeElement.UpdateEdgeControl();
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (active == false) return;

            bool shouldStop = CanStopManipulation(evt) || evt.button == (int) MouseButton.RightMouse;
            if (shouldStop == false) return;

            if (evt.button == (int) MouseButton.RightMouse)
            {
                Abort();
                
                evt.StopImmediatePropagation();
            }
            else
            {
                bool canPerformConnection = Vector2.Distance(downPosition, evt.localMousePosition) > ConnectionDistanceTreshold;
                if (canPerformConnection) edgeDragHelper.HandleMouseUp(evt);
                else Abort();

                evt.StopPropagation();
            }

            active = false;
            this.edgeViewCandidate = null;
            target.ReleaseMouse();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || active == false) return;

            Abort();

            active = false;
            target.ReleaseMouse();
            evt.StopPropagation();
        }

        private void OnCaptureOut(MouseCaptureOutEvent evt)
        {
            active = false;
            if (this.edgeViewCandidate != null) Abort();
        }

        void Abort()
        {
            GraphView view = target?.GetFirstAncestorOfType<GraphView>();
            view?.RemoveElement(this.edgeViewCandidate.edgeElement);

            this.edgeViewCandidate.inputPortView = null;
            this.edgeViewCandidate.outputPortView = null;
            this.edgeViewCandidate = null;

            edgeDragHelper.Reset();
        }
    }
}