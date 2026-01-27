using System.Collections.Generic;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 选中绘制
    /// </summary>
    public abstract class GraphSelectedDrawer : IGraphSelectedDrawer
    {
        protected EditorGraphView graphView { get; private set; }

        public virtual void Initialize(EditorGraphView graphView)
        {
            this.graphView = graphView;
        }

        public virtual void SelectedUpdate(List<ISelectedHandle> selection) { }

        public virtual void Dispose() { }
    }
}