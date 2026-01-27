using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Edge拷贝粘贴Pack接口
    /// </summary>
    public interface IEdgeCopyPastePack : ICopyPastePack
    {
        /// <summary>
        /// 拷贝的Edge资产
        /// </summary>
        EditorEdgeAsset copyAsset { get; }
        
        /// <summary>
        /// 粘贴的Edge资产
        /// </summary>
        EditorEdgeAsset pasteAsset { get; }
    }
}