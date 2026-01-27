using System;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏按钮控件
    /// </summary>
    public class ButtonToolbarViewControl : ToolbarViewControl
    {
        protected GUIContent content;
        protected Action onClick;

        public ButtonToolbarViewControl(string displayName, Action onClick)
        {
            this.content = new GUIContent(displayName);
            this.onClick = onClick;
        }

        public ButtonToolbarViewControl(GUIContent guiContent, Action onClick)
        {
            this.content = guiContent;
            this.onClick = onClick;
        }

        public override void OnDraw()
        {
            if (GUILayout.Button(content, EditorStyles.toolbarButton)) onClick?.Invoke();
        }
    }
}