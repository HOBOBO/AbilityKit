using System;
using UnityEngine;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线运行时访问器
    /// 提供统一的访问入口
    /// </summary>
    public static class Pipeline
    {
        internal static IPipelineRegistry _registry;
        internal static IPipelineTraceRecorder _traceRecorder;
        private static bool _isInitialized;

        /// <summary>
        /// 设置注册表（Editor 初始化使用）
        /// </summary>
        public static void SetRegistry(IPipelineRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// 设置追踪记录器（Editor 初始化使用）
        /// </summary>
        public static void SetTraceRecorder(IPipelineTraceRecorder recorder)
        {
            _traceRecorder = recorder;
        }

        /// <summary>
        /// 管线注册表
        /// </summary>
        public static IPipelineRegistry Registry
        {
            get
            {
                EnsureInitialized();
                return _registry;
            }
        }

        /// <summary>
        /// 追踪记录器
        /// </summary>
        public static IPipelineTraceRecorder TraceRecorder
        {
            get
            {
                EnsureInitialized();
                return _traceRecorder;
            }
        }

        /// <summary>
        /// 是否启用调试追踪
        /// </summary>
        public static bool IsDebugEnabled => _traceRecorder?.IsEnabled ?? false;

        /// <summary>
        /// 初始化管线系统（Runtime 基础版本）
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_isInitialized) return;

            _registry = PipelineRegistry.Instance;
            _traceRecorder = NoOpPipelineTraceRecorder.Instance;
            _isInitialized = true;
        }

        /// <summary>
        /// 关闭管线系统
        /// </summary>
        public static void Shutdown()
        {
            if (_registry is PipelineRegistry runtime)
            {
                runtime.Shutdown();
            }
            _isInitialized = false;
        }

        /// <summary>
        /// 记录追踪数据
        /// </summary>
        public static void RecordTrace(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            _traceRecorder?.Record(owner, data);
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }
    }
}
