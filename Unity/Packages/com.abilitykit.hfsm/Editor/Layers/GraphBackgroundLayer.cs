using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Layer that renders the grid background of the graph view.
    /// </summary>
    public class GraphBackgroundLayer : GraphLayer
    {
        private const float GridSpacing = 20f;
        private const float MajorGridSpacing = 100f;

        private static readonly Color MinorGridColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        private static readonly Color MajorGridColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        public GraphBackgroundLayer(EditorWindow editorWindow) : base(editorWindow)
        {
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            if (Event.current.type != EventType.Repaint)
                return;

            // Draw minor grid
            DrawGrid(GridSpacing, MinorGridColor);

            // Draw major grid
            DrawGrid(MajorGridSpacing, MajorGridColor);
        }

        private void DrawGrid(float spacing, Color color)
        {
            Handles.BeginGUI();
            using (new Handles.DrawingScope(color))
            {
                // Calculate grid offset based on pan
                float offsetX = Context.PanOffset.x % spacing;
                float offsetY = Context.PanOffset.y % spacing;

                // Scale spacing by zoom
                float scaledSpacing = spacing * Context.ZoomFactor;

                // Draw vertical lines
                float x = -offsetX * Context.ZoomFactor;
                while (x < ViewBounds.width)
                {
                    Vector3 start = new Vector3(x, 0, 0);
                    Vector3 end = new Vector3(x, ViewBounds.height, 0);
                    Handles.DrawLine(start, end);
                    x += scaledSpacing;
                }

                // Draw horizontal lines
                float y = -offsetY * Context.ZoomFactor;
                while (y < ViewBounds.height)
                {
                    Vector3 start = new Vector3(0, y, 0);
                    Vector3 end = new Vector3(ViewBounds.width, y, 0);
                    Handles.DrawLine(start, end);
                    y += scaledSpacing;
                }
            }
            Handles.EndGUI();
        }
    }
}
