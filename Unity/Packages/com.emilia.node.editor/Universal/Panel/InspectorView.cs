using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Node.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 内置Inspector面板
    /// </summary>
    public class InspectorView : GraphPanel
    {
        protected PropertyTree propertyTree;
        protected List<Object> selectedObjects;

        public InspectorView()
        {
            name = nameof(InspectorView);
            Add(new IMGUIContainer(OnImGUI));
        }

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);

            UpdateTransform();

            graphView.UnregisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
            graphView.RegisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
        }

        /// <summary>
        /// 设置显示的对象
        /// </summary>
        public void SetObjects(List<Object> selected)
        {
            selectedObjects = selected;

            style.display = DisplayStyle.None;

            Type targetType = null;

            for (int i = 0; i < selectedObjects.Count; i++)
            {
                Type otherType;
                object target = selectedObjects[i];

                if (ReferenceEquals(target, null)) return;

                if (i == 0)
                {
                    targetType = target.GetType();
                }
                else if (targetType != (otherType = target.GetType()))
                {
                    if (targetType.IsAssignableFrom(otherType)) continue;
                    if (otherType.IsAssignableFrom(targetType))
                    {
                        targetType = otherType;
                        continue;
                    }

                    return;
                }
            }

            if (propertyTree != null) propertyTree.Dispose();
            propertyTree = PropertyTree.Create(selectedObjects);
            style.display = DisplayStyle.Flex;
        }

        protected void OnGeometryChangedEvent(GeometryChangedEvent evt)
        {
            UpdateTransform();
        }

        protected void UpdateTransform()
        {
            Rect worldRect = graphView.graphPanelSystem.graphRect;
            Vector2 localPosition = graphView.WorldToLocal(worldRect.position);
            transform.position = localPosition;

            Rect layoutRect = graphView.graphPanelSystem.graphLayoutRect;
            float width = layoutRect.width / 3f;
            width = Mathf.Max(width, 225);
            style.width = width;
        }

        protected void OnImGUI()
        {
            string label = GetLabel();
            if (string.IsNullOrEmpty(label) == false) GUILayout.Label(label);
            this.propertyTree?.Draw();
        }

        protected string GetLabel()
        {
            if (this.selectedObjects.Count > 1) return "Multiple Objects";
            Object first = this.selectedObjects.FirstOrDefault();
            TitleAsset titleAsset = first as TitleAsset;
            if (titleAsset != null) return titleAsset.title;
            return null;
        }

        public override void Dispose()
        {
            base.Dispose();

            graphView.UnregisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
            graphView.UnregisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);

            if (propertyTree != null)
            {
                propertyTree.Dispose();
                propertyTree = null;
            }
        }
    }
}