namespace Emilia.Node.Editor
{
    /// <summary>
    /// 需要响应调整大小元素接口
    /// </summary>
    public interface IResizedGraphElement
    {
        /// <summary>
        /// 当GraphView的大小改变时
        /// </summary>
        void OnElementResized();
    }
}