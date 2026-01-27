using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏标签控件
    /// </summary>
    public class LabelToolbarViewControl : ToolbarViewControl
    {
        /// <summary>
        /// 标签内容
        /// </summary>
        public GUIContent content;

        public LabelToolbarViewControl(string text)
        {
            this.content = new GUIContent(text);
        }

        public LabelToolbarViewControl(GUIContent guiContent)
        {
            this.content = guiContent;
        }

        public override void OnDraw()
        {
            GUILayout.Label(content);
        }
    }
}