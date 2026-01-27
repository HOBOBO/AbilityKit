using System.Collections;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataOutputProcess
    {
        public IEnumerator StartProcess(IBuildContainer container, IBuildArgs buildArgs)
        {
            var dataOutputs = DataOutputManager.instance.GetFinalizeBuildDisposeList(buildArgs);
            var amount = dataOutputs.Count;
            for (var i = 0; i < amount; i++)
            {
                IDataOutput dataOutput = dataOutputs[i];

                bool isFinish = false;

                dataOutput.Output(container, buildArgs, () => isFinish = true);

                while (isFinish == false) yield return 0;
            }
        }
    }
}