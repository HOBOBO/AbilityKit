using System;
using Emilia.Reflection.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 重写ContentDragger
    /// </summary>
    public class GraphContentDragger : MouseManipulator
    {
        protected Vector2 m_Start;
        public Vector2 panSpeed { get; set; }

        public bool clampToParentEdges { get; set; }

        protected bool m_Active;

        public GraphContentDragger()
        {
            m_Active = false;
            activators.Add(new ManipulatorActivationFilter {button = MouseButton.LeftMouse, modifiers = EventModifiers.Alt});
            activators.Add(new ManipulatorActivationFilter {button = MouseButton.MiddleMouse});
            panSpeed = new Vector2(1, 1);
            clampToParentEdges = false;
        }

        protected Rect CalculatePosition(float x, float y, float width, float height)
        {
            var rect = new Rect(x, y, width, height);

            if (clampToParentEdges)
            {
                Rect shadowRect = target.hierarchy.parent.GetRect_Internal();
                if (rect.x < shadowRect.xMin) rect.x = shadowRect.xMin;
                else if (rect.xMax > shadowRect.xMax) rect.x = shadowRect.xMax - rect.width;

                if (rect.y < shadowRect.yMin) rect.y = shadowRect.yMin;
                else if (rect.yMax > shadowRect.yMax) rect.y = shadowRect.yMax - rect.height;

                // Reset size, we never intended to change them in the first place
                rect.width = width;
                rect.height = height;
            }

            return rect;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            var graphView = target as GraphView;
            if (graphView == null)
            {
                throw new InvalidOperationException("Manipulator can only be added to a GraphView");
            }

            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected void OnMouseDown(MouseDownEvent e)
        {
            if (m_Active)
            {
                e.StopImmediatePropagation();
                return;
            }

            if (! CanStartManipulation(e)) return;

            EditorGraphView graphView = target as EditorGraphView;
            if (graphView == null) return;

            Rect rect = graphView.graphPanelSystem.graphRect;
            if (rect.Contains(e.mousePosition) == false) return;

            m_Start = graphView.ChangeCoordinatesTo(graphView.contentViewContainer, e.localMousePosition);

            m_Active = true;
            target.CaptureMouse();
            e.StopImmediatePropagation();
        }

        protected void OnMouseMove(MouseMoveEvent e)
        {
            if (! m_Active) return;

            var graphView = target as GraphView;
            if (graphView == null) return;

            Vector2 diff = graphView.ChangeCoordinatesTo(graphView.contentViewContainer, e.localMousePosition) - m_Start;

            // During the drag update only the view
            Vector3 s = graphView.contentViewContainer.transform.scale;
            graphView.viewTransform.position += Vector3.Scale(diff, s);

            e.StopPropagation();
        }

        protected void OnMouseUp(MouseUpEvent e)
        {
            if (! m_Active || ! CanStopManipulation(e)) return;

            var graphView = target as GraphView;
            if (graphView == null) return;

            Vector3 p = graphView.contentViewContainer.transform.position;
            Vector3 s = graphView.contentViewContainer.transform.scale;

            graphView.UpdateViewTransform(p, s);

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }
    }
}