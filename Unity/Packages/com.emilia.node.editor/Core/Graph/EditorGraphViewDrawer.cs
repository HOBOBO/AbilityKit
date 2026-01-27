using System;
using Sirenix.OdinInspector.Editor.Internal.UIToolkitIntegration;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// GraphView绘制器
    /// </summary>
    public class EditorGraphViewDrawer
    {
        private EditorGraphView _graphView;
        public OdinImGuiElement guiElement;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(EditorGraphView graphView)
        {
            _graphView = graphView;
            this.guiElement = new OdinImGuiElement(graphView);
        }

        /// <summary>
        /// 绘制
        /// </summary>
        public void Draw(float height, float width = -1)
        {
            if (guiElement == null) return;

            try
            {
                Rect rect = ImguiElementUtils.EmbedVisualElementAndDrawItHere(this.guiElement);

                float targetWidth = width <= 0 ? rect.width : width;
                if (targetWidth > 0) _graphView.style.width = targetWidth;

                float targetHeight = height;
                if (targetHeight > 0) _graphView.style.height = targetHeight;
            }
            catch (ArgumentException) { }
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            guiElement = null;
        }
    }
}