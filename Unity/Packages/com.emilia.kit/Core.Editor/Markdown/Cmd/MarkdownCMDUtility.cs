using System;
using System.Collections.Generic;
using System.Reflection;
using Emilia.Kit;
using UnityEditor;
using UnityEngine;

namespace MG.MDV.CMD
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MarkdownCMDAttribute : Attribute
    {
        public string cmdName;

        public MarkdownCMDAttribute(string cmdName)
        {
            this.cmdName = cmdName;
        }
    }

    public static class MarkdownCMDUtility
    {
        public const string CMDIdentifier = "{CMD}";

        public static bool IsCmd(string content) => content.StartsWith(CMDIdentifier);

        public static void CmdDispose(string content)
        {
            string[] cmd = content.Split(new char[] {'='});
            if (cmd.Length != 2) return;

            string cmdContent = cmd[1];

            string[] cmdContents = cmdContent.Split('-');
            if (cmdContents.Length != 2) return;
            string cmdType = cmdContents[0];
            string cmdArgs = cmdContents[1];
            ExecutionCmd(cmdType, cmdArgs);
        }

        public static void ExecutionCmd(string cmdName, string args)
        {
            IList<MethodInfo> methodInfos = TypeCache.GetMethodsWithAttribute<MarkdownCMDAttribute>();

            int count = methodInfos.Count;
            for (int i = 0; i < count; i++)
            {
                MethodInfo methodInfo = methodInfos[i];

                MarkdownCMDAttribute cmdAttribute = methodInfo.GetCustomAttribute<MarkdownCMDAttribute>();
                if (cmdAttribute == null) continue;

                if (cmdAttribute.cmdName != cmdName) continue;

                try { methodInfo.Invoke(null, new object[] {args}); }
                catch (Exception e) { Debug.LogError($"执行命令失败: {e.ToUnityLogString()}"); }
            }

        }
    }
}