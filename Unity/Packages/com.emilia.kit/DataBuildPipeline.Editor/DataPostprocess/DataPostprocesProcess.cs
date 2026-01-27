using System;
using System.Collections;
using System.Collections.Generic;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataPostproces
    {
        public IEnumerator StartProcess(IBuildContainer container, IBuildArgs args)
        {
            List<IDataPostprocess> postprocessList = DataPostprocessManager.instance.GetDataPostprocess(args);
            int amount = postprocessList.Count;
            for (int i = 0; i < amount; i++)
            {
                IDataPostprocess postprocess = postprocessList[i];

                bool isFinish = false;

                try
                {
                    postprocess.Postprocess(container, args, () => isFinish = true);
                }
                catch (Exception e)
                {
                    isFinish = true;
                    container.buildReport.result = BuildResult.Failed;
                    container.buildReport.AddErrorMessage($"DataPostproces:{postprocess} 出现错误： {e.Message}\n{e.StackTrace}");
                }

                while (isFinish == false) yield return 0;
            }
        }
    }
}