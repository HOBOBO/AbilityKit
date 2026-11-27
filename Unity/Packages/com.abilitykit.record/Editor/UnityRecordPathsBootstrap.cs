using UnityEditor;

namespace AbilityKit.Record.Unity.Editor
{
    /// <summary>
    /// 在编辑器加载脚本时安装录制路径策略，覆盖未进入播放模式的工具调用。
    /// </summary>
    internal static class UnityRecordPathsBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Install() => UnityRecordPaths.Install();
    }
}
