using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 节点Tips
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeTipsAttribute : Attribute
    {
        public string tips;

        public NodeTipsAttribute(string tips)
        {
            this.tips = tips;
        }
    }
}