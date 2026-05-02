#if UNITY_EDITOR
using System;
using UnityEngine;
using AbilityKit.Editor.Framework;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 溯源树窗口配置
    /// </summary>
    [Serializable]
    public class TraceTreeConfig : IWindowConfig
    {
        /// <summary>
        /// 自动刷新间隔（秒）
        /// </summary>
        public float AutoRefreshInterval = 0.5f;

        /// <summary>
        /// 是否自动刷新
        /// </summary>
        public bool AutoRefresh = true;

        /// <summary>
        /// 树可视化缩放级别
        /// </summary>
        [Range(0.3f, 3.0f)]
        public float ZoomLevel = 1.0f;

        /// <summary>
        /// 是否显示已结束的节点
        /// </summary>
        public bool ShowEndedNodes = true;

        public void Validate()
        {
            if (AutoRefreshInterval < 0.1f)
                AutoRefreshInterval = 0.1f;
            if (AutoRefreshInterval > 10f)
                AutoRefreshInterval = 10f;

            ZoomLevel = Mathf.Clamp(ZoomLevel, 0.3f, 3.0f);
        }

        public string ToJson()
        {
            // 简单的序列化，不依赖 JsonUtility
            return $"{{\"AutoRefreshInterval\":{AutoRefreshInterval},\"AutoRefresh\":{AutoRefresh},\"ZoomLevel\":{ZoomLevel},\"ShowEndedNodes\":{ShowEndedNodes}}}";
        }

        public void FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                // 简单的反序列化
                var parts = json.Trim('{', '}').Split(',');
                foreach (var part in parts)
                {
                    var keyValue = part.Split(':');
                    if (keyValue.Length != 2) continue;

                    var key = keyValue[0].Trim('"');
                    var value = keyValue[1].Trim();

                    switch (key)
                    {
                        case "AutoRefreshInterval":
                            if (float.TryParse(value, out var interval))
                                AutoRefreshInterval = interval;
                            break;
                        case "AutoRefresh":
                            if (bool.TryParse(value, out var autoRefresh))
                                AutoRefresh = autoRefresh;
                            break;
                        case "ZoomLevel":
                            if (float.TryParse(value, out var zoom))
                                ZoomLevel = zoom;
                            break;
                        case "ShowEndedNodes":
                            if (bool.TryParse(value, out var showEnded))
                                ShowEndedNodes = showEnded;
                            break;
                    }
                }
            }
            catch
            {
                // 忽略解析错误
            }
        }
    }
}
#endif
