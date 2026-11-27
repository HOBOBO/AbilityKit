using AbilityKit.Core.Recording;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace AbilityKit.Record.Unity
{
    /// <summary>
    /// 为录制包装配 Unity 的相对路径基目录，不改变绝对路径或录制格式。
    /// </summary>
    public static class UnityRecordPaths
    {
        /// <summary>
        /// 安装 Unity 路径策略。正常启动时自动安装；早于启动回调的调用方可显式安装。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install() => RecordPathResolver.SetBaseDirectoryProvider(GetBaseDirectory);

        private static string GetBaseDirectory(bool persistentDataOnly)
        {
            var persistentPath = Application.persistentDataPath;
            return !persistentDataOnly && string.IsNullOrEmpty(persistentPath) ? Application.dataPath : persistentPath;
        }
    }
}
