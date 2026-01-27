using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Node拷贝粘贴Pack接口
    /// </summary>
    public interface INodeCopyPastePack : ICopyPastePack
    {
        /// <summary>
        /// 拷贝的Asset
        /// </summary>
        EditorNodeAsset copyAsset { get; }

        /// <summary>
        /// 粘贴的Asset
        /// </summary>
        EditorNodeAsset pasteAsset { get; }
    }
}