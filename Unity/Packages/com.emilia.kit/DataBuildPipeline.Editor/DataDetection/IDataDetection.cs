namespace Emilia.DataBuildPipeline.Editor
{
    public interface IDataDetection
    {
        bool Detection(IBuildContainer buildContainer, IBuildArgs buildArgs);
    }
}