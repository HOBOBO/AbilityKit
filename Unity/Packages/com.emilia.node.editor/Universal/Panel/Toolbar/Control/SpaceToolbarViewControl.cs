using UnityEditor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏间隔控件
    /// </summary>
    public class SpaceToolbarViewControl : ToolbarViewControl
    {
        protected float size;
        protected bool leftSeparator;
        protected bool rightSeparator;

        public SpaceToolbarViewControl(float size, bool leftSeparator = false, bool rightSeparator = false)
        {
            this.size = size;
            this.leftSeparator = leftSeparator;
            this.rightSeparator = rightSeparator;
        }

        public override void OnDraw()
        {
            if (leftSeparator) EditorGUILayout.Separator();
            EditorGUILayout.Space(size);
            if (rightSeparator) EditorGUILayout.Separator();
        }
    }
}