using System;
using Emilia.Reflection.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 重写ContentZoomer
    /// </summary>
    public class GraphContentZoomer : Manipulator
    {
        public static readonly float DefaultReferenceScale = 1;
        public static readonly float DefaultMinScale = 0.25f;
        public static readonly float DefaultMaxScale = 1;
        public static readonly float DefaultScaleStep = 0.15f;

        public float referenceScale { get; set; } = DefaultReferenceScale;

        public float minScale { get; set; } = DefaultMinScale;
        public float maxScale { get; set; } = DefaultMaxScale;

        public float scaleStep { get; set; } = DefaultScaleStep;

        [Obsolete("ContentZoomer.keepPixelCacheOnZoom is deprecated and has no effect")]
        public bool keepPixelCacheOnZoom { get; set; }

        protected override void RegisterCallbacksOnTarget()
        {
            var graphView = target as GraphView;
            if (graphView == null)
            {
                throw new InvalidOperationException("Manipulator can only be added to a GraphView");
            }

            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }

        private static float CalculateNewZoom(float currentZoom, float wheelDelta, float zoomStep, float referenceZoom, float minZoom, float maxZoom)
        {
            if (minZoom <= 0)
            {
                Debug.LogError($"The minimum zoom ({minZoom}) must be greater than zero.");
                return currentZoom;
            }
            if (referenceZoom < minZoom)
            {
                Debug.LogError($"The reference zoom ({referenceZoom}) must be greater than or equal to the minimum zoom ({minZoom}).");
                return currentZoom;
            }
            if (referenceZoom > maxZoom)
            {
                Debug.LogError($"The reference zoom ({referenceZoom}) must be less than or equal to the maximum zoom ({maxZoom}).");
                return currentZoom;
            }
            if (zoomStep < 0)
            {
                Debug.LogError($"The zoom step ({zoomStep}) must be greater than or equal to zero.");
                return currentZoom;
            }

            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

            if (Mathf.Approximately(wheelDelta, 0))
            {
                return currentZoom;
            }

            double a = Math.Log(referenceZoom, 1 + zoomStep);
            double b = referenceZoom - Math.Pow(1 + zoomStep, a);

            double minWheel = Math.Log(minZoom - b, 1 + zoomStep) - a;
            double maxWheel = Math.Log(maxZoom - b, 1 + zoomStep) - a;
            double currentWheel = Math.Log(currentZoom - b, 1 + zoomStep) - a;

            wheelDelta = Math.Sign(wheelDelta);
            currentWheel += wheelDelta;

            if (currentWheel > maxWheel - 0.5)
            {
                return maxZoom;
            }
            if (currentWheel < minWheel + 0.5)
            {
                return minZoom;
            }

            currentWheel = Math.Round(currentWheel);
            return (float) (Math.Pow(1 + zoomStep, currentWheel + a) + b);
        }

        void OnWheel(WheelEvent evt)
        {
            EditorGraphView graphView = target as EditorGraphView;
            if (graphView == null) return;

            Rect rect = graphView.graphPanelSystem.graphRect;
            if (rect.Contains(evt.mousePosition) == false) return;

            if (graphView.isInitialized == false) return;
            if (EditorApplication.isCompiling) return;

            IPanel panel = (evt.target as VisualElement)?.panel;
            if (panel.GetCapturingElement(PointerId.mousePointerId) != null) return;

            Vector3 position = graphView.logicPosition;
            Vector3 scale = graphView.logicScale;

            Vector2 zoomCenter = target.ChangeCoordinatesTo(graphView.contentViewContainer, evt.localMousePosition);
            float x = zoomCenter.x + graphView.contentViewContainer.layout.x;
            float y = zoomCenter.y + graphView.contentViewContainer.layout.y;

            position += Vector3.Scale(new Vector3(x, y, 0), scale);

            float zoom = CalculateNewZoom(scale.y, -evt.delta.y, scaleStep, referenceScale, minScale, maxScale);
            scale.x = zoom;
            scale.y = zoom;
            scale.z = 1;

            position -= Vector3.Scale(new Vector3(x, y, 0), scale);
            position.x = GUIUtility_Internals.RoundToPixelGrid_Internals(position.x);
            position.y = GUIUtility_Internals.RoundToPixelGrid_Internals(position.y);

            graphView.SetViewTransform(position, scale);

            evt.StopPropagation();
        }
    }
}