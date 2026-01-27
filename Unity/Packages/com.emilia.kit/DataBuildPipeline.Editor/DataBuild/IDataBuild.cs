using System;

namespace Emilia.DataBuildPipeline.Editor
{
    public interface IDataBuild
    {
        void Build(IBuildContainer buildContainer, IBuildArgs buildArgs, Action onFinished);
    }
}