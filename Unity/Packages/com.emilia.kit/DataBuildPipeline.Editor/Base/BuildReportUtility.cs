using UnityEditor;
using UnityEngine;

namespace Emilia.DataBuildPipeline.Editor
{
    public static class BuildReportUtility
    {
        public static void OutputReport(BuildReport report)
        {
            int amount = report.messages.Count;
            for (int i = 0; i < amount; i++)
            {
                BuildReportMessage reportMessage = report.messages[i];
                string outputMessage = $"{report.buildDescription} {reportMessage.message}";
                if (reportMessage.logType == LogType.Warning) Debug.LogError($"<color=#FFFF00>警告：{outputMessage}</color>");
                else Debug.unityLogger.Log(reportMessage.logType, outputMessage);
            }

            bool isBuildFailed = report.result == BuildResult.Failed;

            string buildInfo = isBuildFailed ? "构建失败" : "构建成功";
            string outputInfo = $"{report.buildDescription} {buildInfo}";

            if (isBuildFailed) Debug.LogError(outputInfo);
            else Debug.Log(outputInfo);

            bool isError = report.ContainErrorMessage();
            string tipInfo = buildInfo;
            if (isBuildFailed == false && isError) tipInfo = "构建有错误";
            if (isError || isBuildFailed) EditorUtility.DisplayDialog(tipInfo, $"{tipInfo} 请在 控制台（Console）查看详细", "确认");
        }
    }
}