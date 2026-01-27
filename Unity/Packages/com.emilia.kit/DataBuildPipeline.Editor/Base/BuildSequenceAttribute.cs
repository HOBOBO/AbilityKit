using System;

namespace Emilia.DataBuildPipeline.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildSequenceAttribute : Attribute
    {
        public int priority;

        public BuildSequenceAttribute(int priority)
        {
            this.priority = priority;
        }
    }
}