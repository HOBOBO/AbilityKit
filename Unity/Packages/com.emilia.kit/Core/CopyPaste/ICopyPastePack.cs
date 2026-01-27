#if UNITY_EDITOR
namespace Emilia.Kit
{
    /// <summary>
    /// 拷贝粘贴包
    /// </summary>
    public interface ICopyPastePack
    {
        /// <summary>
        /// 判断pack是否是自己的依赖
        /// </summary>
        bool CanDependency(ICopyPastePack pack);

        /// <summary>
        /// 粘贴
        /// </summary>
        void Paste(CopyPasteContext copyPasteContext);
    }
}
#endif