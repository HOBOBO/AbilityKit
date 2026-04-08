using UnityEngine;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// Unity 时间提供者实现
    /// </summary>
    public sealed class UnityTimeProvider : ITimeProvider
    {
        public float RealtimeSinceStartup => Time.realtimeSinceStartup;
    }
}
