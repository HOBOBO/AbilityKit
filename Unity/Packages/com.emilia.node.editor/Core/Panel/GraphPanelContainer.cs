using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 面板容器
    /// </summary>
    public class GraphPanelContainer : VisualElement
    {
        public GraphPanelContainer()
        {
            pickingMode = PickingMode.Ignore;
            style.flexGrow = 1;
            style.position = Position.Absolute;

            this.StretchToParentSize();
        }
    }
}