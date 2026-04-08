using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 时间提供者的静态访问器，使用依赖注入模式
    /// </summary>
    public static class TimeProvider
    {
        private static ITimeProvider _instance = new DefaultTimeProvider();

        /// <summary>
        /// 获取或设置当前的时间提供者实例
        /// </summary>
        public static ITimeProvider Instance
        {
            get => _instance;
            set => _instance = value ?? new DefaultTimeProvider();
        }

        /// <summary>
        /// 获取自系统启动以来的实时时间（秒）
        /// </summary>
        public static float RealtimeSinceStartup => Instance.RealtimeSinceStartup;

        /// <summary>
        /// 默认的时间提供者实现（基于 DateTime）
        /// </summary>
        private sealed class DefaultTimeProvider : ITimeProvider
        {
            private readonly DateTime _startTime = DateTime.UtcNow;

            public float RealtimeSinceStartup => (float)(DateTime.UtcNow - _startTime).TotalSeconds;
        }
    }
}
