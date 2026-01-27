using Emilia.Node.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用浮动面板实现
    /// </summary>
    public class UniversalFloatView : GraphPanel
    {
        protected VisualElement floatContainer;

        protected Dragger _dragger;
        protected Resizer _resizer;

        /// <summary>
        /// 内容容器
        /// </summary>
        public override VisualElement contentContainer => floatContainer;

        public override GraphPanelCapabilities panelCapabilities
        {
            get => this._panelCapabilities;
            set
            {
                this._panelCapabilities = value;
                OnCapabilitiesChange();
            }
        }

        public UniversalFloatView()
        {
            floatContainer = new VisualElement();
            Add(this.floatContainer);

            _dragger = new Dragger {clampToParentEdges = true};
            _resizer = new Resizer(OnResize);
        }

        protected virtual void OnCapabilitiesChange()
        {
            if (capabilities.HasFlag(GraphPanelCapabilities.Movable))
            {
                if (_dragger.target != null) this.RemoveManipulator(this._dragger);
                this.AddManipulator(_dragger);
            }
            else
            {
                if (_dragger.target != null) this.RemoveManipulator(this._dragger);
            }

            if (capabilities.HasFlag(GraphPanelCapabilities.Resizable))
            {
                floatContainer.RemoveFromHierarchy();
                _resizer.RemoveFromHierarchy();

                hierarchy.Add(_resizer);
                _resizer.Add(this.floatContainer);
            }
            else
            {
                floatContainer.RemoveFromHierarchy();
                _resizer.RemoveFromHierarchy();

                hierarchy.Add(this.floatContainer);
            }
        }

        protected virtual void OnResize() { }
    }
}