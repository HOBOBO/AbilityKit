using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 可以拷贝粘贴的元素接口
    /// </summary>
    public interface IGraphCopyPasteElement
    {
        /// <summary>
        /// 获取复制粘贴包
        /// </summary>
        ICopyPastePack GetPack();
    }
}