using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.HotReload
{
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
}
