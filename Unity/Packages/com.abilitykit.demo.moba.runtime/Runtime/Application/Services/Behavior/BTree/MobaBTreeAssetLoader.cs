using System;
using System.IO;
using AbilityKit.Core.Logging;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// 行为树 JSON 资源加载器。
    ///
    /// 按树名（不含扩展名）解析导出 JSON，搜索顺序：
    /// 1. Unity Resources（moba/bt/&lt;name&gt;.json，UNITY_2020_3_OR_NEWER 时）
    /// 2. 当前工作目录 Configs/moba/bt/&lt;name&gt;.json（Console/测试）
    ///
    /// 结果带进程内缓存——同一棵树只读盘一次，每个大脑实例各自反序列化。
    /// </summary>
    internal static class MobaBTreeAssetLoader
    {
        private static readonly System.Collections.Generic.Dictionary<string, string> s_cache = new();

        public static bool TryLoad(string treeName, out string json)
        {
            json = null;
            if (string.IsNullOrWhiteSpace(treeName)) return false;

            if (s_cache.TryGetValue(treeName, out json))
            {
                return true;
            }

            json = LoadFromUnityResources(treeName) ?? LoadFromConfigDir(treeName);
            if (json == null)
            {
                Log.Warning($"[MobaBTreeAssetLoader] behavior tree json not found: {treeName}");
                return false;
            }

            s_cache[treeName] = json;
            return true;
        }

        private static string LoadFromUnityResources(string treeName)
        {
#if UNITY_2020_3_OR_NEWER
            try
            {
                var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>($"moba/bt/{treeName}");
                return asset != null ? asset.text : null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBTreeAssetLoader] Resources.Load failed: {treeName}");
                return null;
            }
#else
            return null;
#endif
        }

        private static string LoadFromConfigDir(string treeName)
        {
            try
            {
                var path = Path.Combine("Configs", "moba", "bt", treeName + ".json");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBTreeAssetLoader] read config file failed: {treeName}");
                return null;
            }
        }
    }
}
