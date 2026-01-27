using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MG.MDV.CMD
{
    public static class MarkdownUniversalCMD
    {
        [MarkdownCMD("ExecuteMenuItem")]
        public static void ExecuteMenuItem(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            IList<MethodInfo> methods = TypeCache.GetMethodsWithAttribute<MenuItem>();
            int count = methods.Count;
            for (int i = 0; i < count; i++)
            {
                MethodInfo method = methods[i];
                MenuItem attribute = method.GetCustomAttribute<MenuItem>();
                if (attribute == null || attribute.menuItem != path) continue;
                method.Invoke(null, null);
                return;
            }
        }
    }
}