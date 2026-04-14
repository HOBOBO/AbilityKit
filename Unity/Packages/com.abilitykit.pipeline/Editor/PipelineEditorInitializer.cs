#if UNITY_EDITOR

using System;
using UnityEngine;
using AbilityKit.Pipeline;

namespace AbilityKit.Pipeline.Editor
{
    /// <summary>
    /// Editor 端管线系统初始化
    /// 替换 Runtime 的基础实现为 Editor 调试版本
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    public static class PipelineEditorInitializer
    {
        static PipelineEditorInitializer()
        {
            ReplaceWithEditorImplementation();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EditorInitialize()
        {
            ReplaceWithEditorImplementation();
        }

        private static void ReplaceWithEditorImplementation()
        {
            // 替换注册表为 Editor 版本
            Pipeline.SetRegistry(EditorPipelineRegistry.Instance);
            
            // 替换追踪记录器为 Editor 版本
            Pipeline.SetTraceRecorder(EditorPipelineTraceRecorder.Instance);
        }
    }
}

#endif
