using Emilia.Reflection.Editor;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点Vertical容器
    /// </summary>
    public class NodeVerticalContainer : VisualElement
    {
        public NodeVerticalContainer()
        {
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            style.justifyContent = Justify.SpaceBetween;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        protected void OnAttachToPanel(AttachToPanelEvent evt)
        {
            foreach (VisualElement child in Children()) child.style.flexGrow = 1;
            this.AddHierarchyChangedCallback_Internal(OnAddHierarchyChangedCallback);
        }

        protected void OnAddHierarchyChangedCallback(VisualElement visualElement, HierarchyChangeType_Internals hierarchyChangeTypeInternals)
        {
            foreach (VisualElement child in Children()) child.style.flexGrow = 1;
        }
    }
}