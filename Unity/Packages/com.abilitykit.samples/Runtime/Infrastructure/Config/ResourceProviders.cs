#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 璧勬簮閰嶇疆鎻愪緵鑰呭叏灞€璁块棶鍣?
    /// 通过依赖注入/Setter注入来切换不同的 IResourceProvider 实现
    /// </summary>
    public static class ResourceProviders
    {
        private static IResourceProvider _current;

        /// <summary>
        /// 褰撳墠璧勬簮閰嶇疆鎻愪緵鑰?
        /// </summary>
        public static IResourceProvider Current
        {
            get => _current ??= CreateDefault();
            set => _current = value;
        }

        /// <summary>
        /// 鍒涘缓榛樿鐨勮祫婧愭彁渚涜€?
        /// </summary>
        public static IResourceProvider CreateDefault()
        {
            // 鍦ㄨ繍琛屾椂鑷姩妫€娴嬬幆澧?
            // TODO: 鍚庣画鍙互閫氳繃鐜鍙橀噺鎴栧惎鍔ㄥ弬鏁板垏鎹?
            // 内容层不再自行构造文件系统实现，否则会把 System.IO 的文件访问拖进 Unity 包。
            // 必须由宿主安装：.NET 宿主装文件系统实现，Unity 宿主装 TextAsset 实现。
            return new NullResourceProvider();
        }

        /// <summary>
        /// 璁剧疆璧勬簮鎻愪緵鑰?
        /// </summary>
        /// <typeparam name="T">璧勬簮鎻愪緵鑰呯被鍨?/typeparam>
        public static void Set<T>() where T : IResourceProvider, new()
        {
            _current = new T();
        }

        /// <summary>
        /// 閲嶇疆涓洪粯璁よ祫婧愭彁渚涜€?
        /// </summary>
        public static void Reset()
        {
            _current = CreateDefault();
        }

        /// <summary>
        /// 宿主尚未安装实现时的占位：不读任何资源，也不抛异常。
        /// LoadText 抛 FileNotFoundException 以保持既有回退语义——
        /// SampleConfig 正是靠捕获它退回内置默认配置。
        /// </summary>
        private sealed class NullResourceProvider : IResourceProvider
        {
            public string LoadText(string path) =>
                throw new System.IO.FileNotFoundException("No resource provider installed; cannot load " + path);

            public bool TryLoadText(string path, out string content)
            {
                content = null;
                return false;
            }

            public bool Exists(string path) => false;
        }

    }
}
