using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏固定间隔控件
    /// </summary>
    public class FlexibleSpaceToolbarViewControl : ToolbarViewControl
    {
        public override void OnDraw()
        {
            GUILayout.FlexibleSpace();
        }
    }
}