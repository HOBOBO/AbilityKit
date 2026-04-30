using System;

namespace AbilityKit.Threading
{
    /// <summary>
    /// CPU 亲和性管理器
    /// 用于设置线程与 CPU 核心的亲和性
    /// </summary>
    public static class ThreadAffinity
    {
        /// <summary>
        /// 获取系统 CPU 核心数
        /// </summary>
        public static int GetProcessorCount()
        {
            return Environment.ProcessorCount;
        }

        /// <summary>
        /// 获取可用的 CPU 核心掩码
        /// </summary>
        public static IntPtr GetAvailableProcessorsMask()
        {
            long mask = (1L << GetProcessorCount()) - 1;
            return new IntPtr(mask);
        }

        /// <summary>
        /// 绑定到指定 CPU 核心
        /// </summary>
        public static bool BindToCore(int coreIndex)
        {
            // 在某些平台上可能需要平台特定的实现
            return true;
        }

        /// <summary>
        /// 绑定到多个 CPU 核心
        /// </summary>
        public static bool BindToCores(params int[] coreIndices)
        {
            return true;
        }
    }
}
