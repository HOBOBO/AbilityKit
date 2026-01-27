using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 节点颜色
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeColorAttribute : Attribute
    {
        public float r;
        public float g;
        public float b;

        public NodeColorAttribute(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }
}