using System.Collections.Generic;
using UnityEngine;

namespace Emilia.DataBuildPipeline.Editor
{
    public class BuildReport
    {
        public object product;
        public BuildResult result;
        public string buildDescription;
        public List<BuildReportMessage> messages = new List<BuildReportMessage>();

        public void AddInfoMessage(string message)
        {
            BuildReportMessage reportMessage = new BuildReportMessage(LogType.Log, message);
            this.messages.Add(reportMessage);
        }

        public void AddWarningMessage(string message)
        {
            BuildReportMessage reportMessage = new BuildReportMessage(LogType.Warning, message);
            this.messages.Add(reportMessage);
        }

        public void AddErrorMessage(string message)
        {
            BuildReportMessage reportMessage = new BuildReportMessage(LogType.Error, message);
            this.messages.Add(reportMessage);
        }

        public bool ContainErrorMessage()
        {
            int amount = this.messages.Count;
            for (int i = 0; i < amount; i++)
            {
                BuildReportMessage message = this.messages[i];
                if (message.logType == LogType.Error) return true;
            }
            return false;
        }
    }
}