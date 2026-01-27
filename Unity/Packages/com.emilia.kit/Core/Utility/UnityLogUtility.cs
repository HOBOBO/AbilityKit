#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Emilia.Kit
{
    public static class UnityLogUtility
    {
        public static string ToUnityLogString(this Exception e)
        {
            return PathToUnityPathLog(e.ToString());
        }

        public static string PathToUnityPathLog(string pathString)
        {
            List<string> pathContentList = new List<string>();

            string pattern = @"[a-zA-Z]:\\(?:[\w-]+\\)*[\w-]+\.\w+:\d+";
            MatchCollection matches = Regex.Matches(pathString, pattern);
            foreach (Match match in matches)
            {
                if (match.Success == false) continue;
                pathContentList.Add(match.Value);
            }

            string dataPathNoAssets = Directory.GetParent(Application.dataPath).ToString();

            foreach (string pathContent in pathContentList)
            {
                string outputPath = pathContent.Replace(dataPathNoAssets + "\\", "");
                outputPath = outputPath.Replace("\\", "/");

                string[] pathAndLine = outputPath.Split(':');
                string path = pathAndLine[0];
                string line = pathAndLine[1];

                string text = $"<a href=\"{path}\" line=\"{line}\">{path}:{line}</a>";
                pathString = pathString.Replace(pathContent, text);
            }

            return pathString;
        }
    }
}
#endif