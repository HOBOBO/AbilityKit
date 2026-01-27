namespace Emilia.DataBuildPipeline.Editor
{
    public class BuildContainer : IBuildContainer
    {
        public BuildReport buildReport { get; set; } = new BuildReport();
    }
}