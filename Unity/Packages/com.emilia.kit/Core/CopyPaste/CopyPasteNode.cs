#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.Serialization;

namespace Emilia.Kit
{
    /// <summary>
    /// 拷贝粘贴节点
    /// </summary>
    [Serializable]
    public class CopyPasteNode
    {
        /// <summary>
        /// 自身
        /// </summary>
        [OdinSerialize, NonSerialized]
        public ICopyPastePack pack;

        /// <summary>
        /// 输出方向所有节点
        /// </summary>
        [OdinSerialize, NonSerialized]
        public List<CopyPasteNode> output;

        /// <summary>
        /// 输入方向所有节点
        /// </summary>
        [OdinSerialize, NonSerialized]
        public List<CopyPasteNode> input;

        public CopyPasteNode(ICopyPastePack pack)
        {
            this.pack = pack;

            this.output = new List<CopyPasteNode>();
            this.input = new List<CopyPasteNode>();
        }
    }
}
#endif