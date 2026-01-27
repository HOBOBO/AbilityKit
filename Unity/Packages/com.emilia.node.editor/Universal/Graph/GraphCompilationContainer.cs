using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 编译中提示
    /// </summary>
    public class GraphCompilationContainer : VisualElement
    {
        public GraphCompilationContainer()
        {
            name = nameof(GraphLoadingContainer);
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.Flex;

            this.StretchToParentSize();

            Label label = new("Compilation...");
            label.style.position = Position.Absolute;

            label.style.left = 10;
            label.style.bottom = 10;

            Add(label);
        }
    }
}