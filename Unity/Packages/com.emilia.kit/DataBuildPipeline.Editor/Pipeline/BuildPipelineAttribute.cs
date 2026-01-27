using System;

namespace Emilia.DataBuildPipeline.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildPipelineAttribute : Attribute
    {
        public Type argsType;

        public BuildPipelineAttribute(Type argsType)
        {
            this.argsType = argsType;
        }
    }
}