using System;

namespace Emilia.DataBuildPipeline.Editor
{
    public interface IBuildArgs
    {
        Action<BuildReport> onBuildComplete { get; }
    }
}