using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Layer that handles pan and zoom navigation.
    /// </summary>
    public class GraphNavigationLayer : GraphLayer
    {
        private bool _isPanning;
        private Vector2 _lastMousePosition;

        public GraphNavigationLayer(EditorWindow editorWindow) : base(editorWindow)
        {
        }

        public override void ProcessEvent()
        {
            HandlePan();
            HandleZoom();
        }

        private void HandlePan()
        {
            Event e = Event.current;

            // Middle mouse button or Alt+Left click for panning
            if (e.button == 2 || (e.button == 0 && e.alt))
            {
                if (e.type == EventType.MouseDown)
                {
                    _isPanning = true;
                    _lastMousePosition = e.mousePosition;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && _isPanning)
                {
                    Vector2 delta = e.mousePosition - _lastMousePosition;
                    Context.PanOffset += delta;
                    _lastMousePosition = e.mousePosition;
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    _isPanning = false;
                    e.Use();
                }
            }
        }

        private void HandleZoom()
        {
            Event e = Event.current;

            if (e.type == EventType.ScrollWheel)
            {
                Vector2 mousePos = e.mousePosition;

                // Calculate zoom
                float oldZoom = Context.ZoomFactor;
                float newZoom = oldZoom * (1f - e.delta.y * 0.02f);
                newZoom = Mathf.Clamp(newZoom, 0.1f, 2f);

                // Calculate new pan offset to zoom towards mouse position
                // screenPos = contentPos * zoom + panOffset
                // Before zoom: mousePos = contentAtMouse * oldZoom + panOffset
                // After zoom: mousePos = contentAtMouse * newZoom + newPanOffset
                // Solving: newPanOffset = mousePos - contentAtMouse * newZoom
                Vector2 contentAtMouse = (mousePos - Context.PanOffset) / oldZoom;
                Context.PanOffset = mousePos - contentAtMouse * newZoom;

                Context.ZoomFactor = newZoom;

                e.Use();
            }
        }

        /// <summary>
        /// Resets the view to fit all nodes.
        /// </summary>
        public void ResetView()
        {
            Context.ZoomFactor = 1f;
            Context.PanOffset = new Vector2(50, 50);
        }

        /// <summary>
        /// Centers the view on a specific position.
        /// </summary>
        public void CenterOn(Vector2 contentPosition)
        {
            Context.PanOffset = new Vector2(
                ViewBounds.width / 2 - contentPosition.x * Context.ZoomFactor,
                ViewBounds.height / 2 - contentPosition.y * Context.ZoomFactor
            );
        }
    }
}
