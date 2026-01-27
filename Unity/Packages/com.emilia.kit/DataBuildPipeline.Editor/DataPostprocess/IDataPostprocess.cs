using System;

namespace Emilia.DataBuildPipeline.Editor
{
    public interface IDataPostprocess
    {
        void Postprocess(IBuildContainer buildContainer, IBuildArgs buildArgs, Action onFinished);
    }
}