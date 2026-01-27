using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class UniversalBuildPipeline : IBuildPipeline
    {
        protected BuildArgs buildArgs;
        protected IBuildContainer container;

        public virtual void Run(IBuildArgs args)
        {
            buildArgs = args as BuildArgs;
            RunInitialize();

            EditorCoroutineUtility.StartCoroutineOwnerless(RunTask());
        }

        protected virtual void RunInitialize() { }

        protected virtual IEnumerator RunTask()
        {
            DataCollect();

            DataDetection();

            yield return DataBuildProcess();

            yield return DataPostprocess();

            yield return DataOutputProcess();

            switch (this.buildArgs.outputReportMode)
            {
                case OutputReportMode.AllOutput:
                    BuildReportUtility.OutputReport(container.buildReport);
                    break;
                case OutputReportMode.ErrorOutput:
                    if (this.container.buildReport.result == BuildResult.Failed) BuildReportUtility.OutputReport(container.buildReport);
                    break;
            }

            buildArgs.onBuildComplete?.Invoke(container.buildReport);
        }

        protected virtual void DataCollect()
        {
            container = CreateContainer();
        }

        protected virtual IBuildContainer CreateContainer() => BuildContainerManager.instance.CreateBuildContainer(buildArgs);

        protected virtual void DataDetection()
        {
            List<IDataDetection> detections = DataDetectionManager.instance.GetDataDetectionList(this.buildArgs);
            int amount = detections.Count;
            for (int i = 0; i < amount; i++)
            {
                IDataDetection detection = detections[i];
                if (detection.Detection(this.container, this.buildArgs)) continue;
                container.buildReport.result = BuildResult.Failed;
                return;
            }
        }

        protected virtual IEnumerator DataBuildProcess()
        {
            if (this.container.buildReport.result == BuildResult.Failed) yield break;

            DataBuildProcess dataBuildProcess = new DataBuildProcess();
            yield return dataBuildProcess.StartProcess(this.container, this.buildArgs);
        }

        protected virtual IEnumerator DataPostprocess()
        {
            if (this.container.buildReport.result == BuildResult.Failed) yield break;

            DataPostproces dataPostproces = new DataPostproces();
            yield return dataPostproces.StartProcess(this.container, this.buildArgs);
        }

        protected virtual IEnumerator DataOutputProcess()
        {
            if (this.container.buildReport.result == BuildResult.Failed) yield break;
            container.buildReport.result = BuildResult.Succeeded;

            DataOutputProcess dataOutputProcess = new DataOutputProcess();
            yield return dataOutputProcess.StartProcess(this.container, this.buildArgs);
        }
    }
}