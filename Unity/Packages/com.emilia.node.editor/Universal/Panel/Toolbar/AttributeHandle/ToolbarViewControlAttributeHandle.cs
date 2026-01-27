using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏视图控件特性处理基类
    /// </summary>
    public abstract class ToolbarViewControlAttributeHandle
    {
        public abstract void OnHandle(ToolbarView toolbarView, EditorGraphView editorGraphView);
    }
}