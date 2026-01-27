using System;

namespace Emilia.DataBuildPipeline.Editor
{
    public abstract class BuildArgs : IBuildArgs
    {
        public OutputReportMode outputReportMode { get; set; }
        public Action<BuildReport> onBuildComplete { get; set; }
    }
}