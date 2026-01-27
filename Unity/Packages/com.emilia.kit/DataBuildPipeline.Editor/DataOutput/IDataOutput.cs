using System;

namespace Emilia.DataBuildPipeline.Editor
{
    public interface IDataOutput
    {
        void Output(IBuildContainer buildContainer, IBuildArgs buildArgs, Action onFinished);
    }
}