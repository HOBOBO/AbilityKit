using System;
using System.IO;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>
    /// 将历史英雄资源审计归档到独立运行目录，不覆盖已存在的报告。
    /// </summary>
    internal static class MobaHeroInspectionReport
    {
        internal static string Write(string fileName, string contents)
        {
            const string option = "-mobaResourceFactsOutput";
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, option);
            string directory;
            if (index >= 0)
            {
                if (index == arguments.Length - 1 || index != Array.LastIndexOf(arguments, option) ||
                    string.IsNullOrWhiteSpace(arguments[index + 1]) || !Path.IsPathFullyQualified(arguments[index + 1]))
                    throw new ArgumentException("Specify -mobaResourceFactsOutput once with an absolute directory under local/Logs/.");

                directory = arguments[index + 1];
            }
            else
                directory = Path.Combine(Application.dataPath, "../../local/Logs/moba-resource-facts",
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");

            var destination = Path.Combine(Directory.CreateDirectory(directory).FullName, fileName);
            using var writer = new StreamWriter(new FileStream(destination, FileMode.CreateNew));
            writer.Write(contents);
            return destination;
        }
    }
}
