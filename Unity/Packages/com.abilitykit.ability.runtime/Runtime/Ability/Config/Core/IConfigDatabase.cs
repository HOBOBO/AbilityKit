using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置表接口，提供按 ID 查询配置的能力
    /// </summary>
    /// <typeparam name="TEntry">配置条目类型</typeparam>
    public interface IConfigTable<TEntry> where TEntry : class
    {
        /// <summary>
        /// 配置条目数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 根据 ID 获取配置条目
        /// </summary>
        TEntry Get(int id);

        /// <summary>
        /// 尝试获取配置条目
        /// </summary>
        bool TryGet(int id, out TEntry entry);

        /// <summary>
        /// 获取所有配置条目
        /// </summary>
        IEnumerable<TEntry> All();
    }

    /// <summary>
    /// 配置数据库接口，提供统一的配置存储和查询
    /// </summary>
    public interface IConfigDatabase
    {
        /// <summary>
        /// 当前版本号，每次加载/重新加载后递增
        /// </summary>
        long Version { get; }

        /// <summary>
        /// 获取配置表
        /// </summary>
        IConfigTable<TEntry> GetTable<TEntry>() where TEntry : class;

        /// <summary>
        /// 尝试获取配置表
        /// </summary>
        bool TryGetTable<TEntry>(out IConfigTable<TEntry> table) where TEntry : class;

        /// <summary>
        /// 从数据源加载配置
        /// </summary>
        ConfigReloadResult Load(IConfigSource source, string basePath = null);

        /// <summary>
        /// 重新加载配置
        /// </summary>
        ConfigReloadResult Reload(IConfigSource source, string basePath = null);
    }

    /// <summary>
    /// 配置重新加载结果
    /// </summary>
    public readonly struct ConfigReloadResult
    {
        public readonly string Key;
        public readonly long Version;
        public readonly bool Succeeded;
        public readonly bool FullReload;
        public readonly IReadOnlyList<int> ChangedIds;
        public readonly string Error;

        public ConfigReloadResult(string key, long version, bool succeeded, bool fullReload, IReadOnlyList<int> changedIds, string error)
        {
            Key = key ?? string.Empty;
            Version = version;
            Succeeded = succeeded;
            FullReload = fullReload;
            ChangedIds = changedIds;
            Error = error;
        }

        public static ConfigReloadResult Success(string key, long version, bool fullReload, IReadOnlyList<int> changedIds)
        {
            return new ConfigReloadResult(key, version, succeeded: true, fullReload: fullReload, changedIds: changedIds, error: null);
        }

        public static ConfigReloadResult Fail(string key, long version, string error)
        {
            return new ConfigReloadResult(key, version, succeeded: false, fullReload: true, changedIds: null, error: error);
        }
    }

    /// <summary>
    /// 配置重新加载结果发布总线
    /// </summary>
    public static class ConfigReloadBus
    {
        private static Action<ConfigReloadResult> _onPublished;

        public static event Action<ConfigReloadResult> OnPublished
        {
            add => _onPublished += value;
            remove => _onPublished -= value;
        }

        public static void Publish(ConfigReloadResult result)
        {
            _onPublished?.Invoke(result);
        }
    }
}
