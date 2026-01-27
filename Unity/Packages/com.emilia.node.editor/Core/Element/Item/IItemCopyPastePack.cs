using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Item拷贝粘贴Pack接口
    /// </summary>
    public interface IItemCopyPastePack : ICopyPastePack
    {
        /// <summary>
        /// 拷贝的资源
        /// </summary>
        EditorItemAsset copyAsset { get; }

        /// <summary>
        /// 粘贴的资源
        /// </summary>
        EditorItemAsset pasteAsset { get; }
    }
}