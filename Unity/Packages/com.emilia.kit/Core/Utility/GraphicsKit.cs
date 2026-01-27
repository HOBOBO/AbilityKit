#if UNITY_EDITOR
using Emilia.Reflection.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class GraphicsKit
    {
        public static void ShadowLabel(Rect rect, string text, GUIStyle style, Color textColor, Color shadowColor)
        {
            ShadowLabel(rect, GUIContent_Internals.Temp_Internal(text), style, textColor, shadowColor);
        }

        public static void ShadowLabel(Rect rect, GUIContent content, GUIStyle style, Color textColor, Color shadowColor)
        {
            var shadowRect = rect;
            shadowRect.xMin += 2.0f;
            shadowRect.yMin += 2.0f;
            style.normal.textColor = shadowColor;
            style.hover.textColor = shadowColor;
            GUI.Label(shadowRect, content, style);

            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            GUI.Label(rect, content, style);
        }

        public static void DrawLine(Vector3 p1, Vector3 p2, Color color)
        {
            Color c = Handles.color;
            Handles.color = color;
            Handles.DrawLine(p1, p2);
            Handles.color = c;
        }

        public static void DrawPolygonAA(Color color, Vector3[] vertices)
        {
            Color prevColor = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(vertices);
            Handles.color = prevColor;
        }

        public static void DrawDottedLine(Vector3 p1, Vector3 p2, float segmentsLength, Color col)
        {
            HandleUtility_Internals.ApplyWireMaterial_Internals();

            GL.Begin(GL.LINES);
            GL.Color(col);

            var length = Vector3.Distance(p1, p2); // ignore z component
            var count = Mathf.CeilToInt(length / segmentsLength);
            for (var i = 0; i < count; i += 2)
            {
                GL.Vertex(Vector3.Lerp(p1, p2, i * segmentsLength / length));
                GL.Vertex(Vector3.Lerp(p1, p2, (i + 1) * segmentsLength / length));
            }

            GL.End();
        }

        public static void DrawTextureRepeated(Rect area, Texture texture)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;

            GUI.BeginClip(area);
            int w = Mathf.CeilToInt(area.width / texture.width);
            int h = Mathf.CeilToInt(area.height / texture.height);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    GUI.DrawTexture(new Rect(x * texture.width, y * texture.height, texture.width, texture.height), texture);
                }
            }

            GUI.EndClip();
        }

        public static Rect CalculateTextBoxSize(Rect trackRect, GUIStyle font, GUIContent content, float padding)
        {
            Rect textRect = trackRect;
            textRect.width = font.CalcSize(content).x + padding;
            textRect.x += (trackRect.width - textRect.width) / 2f;
            textRect.height -= 4f;
            textRect.y += 2f;
            return textRect;
        }
    }
}
#endif