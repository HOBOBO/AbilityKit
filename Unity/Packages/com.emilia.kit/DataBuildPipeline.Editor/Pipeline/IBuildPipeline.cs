namespace Emilia.DataBuildPipeline.Editor
{
    public interface IBuildPipeline
    {
        void Run(IBuildArgs args);
    }
}