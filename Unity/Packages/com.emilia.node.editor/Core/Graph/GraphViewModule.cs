namespace Emilia.Node.Editor
{
    /// <summary>
    /// 基础模块
    /// </summary>
    public abstract class BasicGraphViewModule : GraphViewModule { }

    /// <summary>
    /// 自定义模块
    /// </summary>
    public abstract class CustomGraphViewModule : GraphViewModule { }

    /// <summary>
    /// GraphView子模块
    /// </summary>
    public abstract class GraphViewModule
    {
        protected EditorGraphView graphView;

        /// <summary>
        /// 顺序
        /// </summary>
        public virtual int order => 0;

        /// <summary>
        /// 初始化模块
        /// </summary>
        public virtual void Initialize(EditorGraphView graphView)
        {
            this.graphView = graphView;
        }

        /// <summary>
        /// 所有模块初始化成功时调用
        /// </summary>
        public virtual void AllModuleInitializeSuccess() { }

        /// <summary>
        /// 销毁模块
        /// </summary>
        public virtual void Dispose()
        {
            graphView = null;
        }
    }
}