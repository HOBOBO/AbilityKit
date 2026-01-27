using System;
using System.Collections;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataBuildProcess
    {
        public IEnumerator StartProcess(IBuildContainer container, IBuildArgs args)
        {
            var disposes = DataBuildManager.instance.GetDataBuildList(args);
            var amount = disposes.Count;
            for (var i = 0; i < amount; i++)
            {
                IDataBuild build = disposes[i];

                bool isFinish = false;

                try
                {
                    build.Build(container, args, () => isFinish = true);
                }
                catch (Exception e)
                {
                    isFinish = true;
                    container.buildReport.result = BuildResult.Failed;
                    container.buildReport.AddErrorMessage($"DataBuildProcess:{build} 出现错误： {e.Message}\n{e.StackTrace}");
                }

                while (isFinish == false) yield return 0;
            }
        }
    }
}