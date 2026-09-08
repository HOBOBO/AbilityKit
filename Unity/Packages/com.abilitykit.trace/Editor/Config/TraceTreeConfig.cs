#if UNITY_EDITOR
using UnityEngine;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 溯源树窗口配置（内存态，不持久化）。
    /// </summary>
    public class TraceTreeConfig
    {
        public float AutoRefreshInterval = 0.5f;
        public bool AutoRefresh = true;

        [Range(0.3f, 3.0f)]
        public float ZoomLevel = 1.0f;

        public bool ShowEndedNodes = true;

        public void Validate()
        {
            if (AutoRefreshInterval < 0.1f)
                AutoRefreshInterval = 0.1f;
            if (AutoRefreshInterval > 10f)
                AutoRefreshInterval = 10f;

            ZoomLevel = Mathf.Clamp(ZoomLevel, 0.3f, 3.0f);
        }
    }
}
#endif
