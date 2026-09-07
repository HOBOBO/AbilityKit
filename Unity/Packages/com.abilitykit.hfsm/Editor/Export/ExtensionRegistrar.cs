// ============================================================================
// Extension Registration Helper - 扩展注册辅助类
// 提供静态初始化方法，用于包外代码注册扩展
// ============================================================================

using System;
using UnityEngine;

namespace AbilityKit.HFSM.Editor.Export
{
    /// <summary>
    /// 扩展注册辅助类
    /// 在包的初始化代码中调用，确保默认扩展被注册
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    public static class ExtensionRegistrar
    {
        static ExtensionRegistrar()
        {
            // 在编辑器启动时初始化默认扩展
            ExtensionRegistry.Initialize();
        }

        /// <summary>
        /// 初始化入口（运行时调用）
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            ExtensionRegistry.Initialize();
        }
    }
}
