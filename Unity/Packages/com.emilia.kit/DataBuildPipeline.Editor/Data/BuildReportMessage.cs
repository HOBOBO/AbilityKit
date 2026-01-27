using UnityEngine;

namespace Emilia.DataBuildPipeline.Editor
{
    public class BuildReportMessage
    {
        public LogType logType;
        public string message;

        public BuildReportMessage(LogType logType, string message)
        {
            this.logType = logType;
            this.message = message;
        }
    }
}