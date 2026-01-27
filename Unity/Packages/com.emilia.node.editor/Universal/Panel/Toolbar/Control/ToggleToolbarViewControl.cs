using System;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏Toggle控件
    /// </summary>
    public class ToggleToolbarViewControl : ToolbarViewControl
    {
        protected GUIContent content;

        protected Func<bool> getter;
        protected Action<bool> setter;

        public ToggleToolbarViewControl(string displayName, Func<bool> getter, Action<bool> setter)
        {
            this.content = new GUIContent(displayName);
            this.getter = getter;
            this.setter = setter;
        }

        public ToggleToolbarViewControl(GUIContent guiContent, Func<bool> getter, Action<bool> setter)
        {
            this.content = guiContent;
            this.getter = getter;
            this.setter = setter;
        }

        public override void OnDraw()
        {
            bool value = this.getter?.Invoke() ?? false;
            value = GUILayout.Toggle(getter.Invoke(), content, EditorStyles.toolbarButton);
            setter?.Invoke(value);
        }
    }
}