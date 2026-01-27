#if UNITY_EDITOR
using System.Collections.Generic;

namespace Emilia.Kit
{
    /// <summary>
    /// 拷贝粘贴上下文
    /// </summary>
    public struct CopyPasteContext
    {
        public object userData;

        /// <summary>
        /// 依赖的Pack
        /// </summary>
        public List<ICopyPastePack> dependency;

        /// <summary>
        /// 粘贴的内容
        /// </summary>
        public List<object> pasteContent;
    }
}
#endif