#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>注册同一稳定名称时采用的冲突处理方式。</summary>
    public enum NetworkSyncProfileRegistrationMode
    {
        /// <summary>稳定名称已存在时拒绝注册。</summary>
        RejectDuplicate = 0,

        /// <summary>稳定名称已存在时保留原顺序并替换档案。</summary>
        ReplaceExisting = 1
    }

    /// <summary>同步档案目录中的一个稳定条目。</summary>
    public readonly struct NetworkSyncProfileCatalogEntry
    {
        /// <summary>创建一个具备稳定名称的同步档案条目。</summary>
        public NetworkSyncProfileCatalogEntry(string name, in NetworkSyncProfile profile)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Profile name cannot be empty.", nameof(name));

            Name = name;
            Profile = profile;
        }

        /// <summary>用于配置、诊断和能力矩阵的稳定名称。</summary>
        public string Name { get; }

        /// <summary>该条目对应的兼容模型。</summary>
        public NetworkSyncModel Model => Profile.CompatibilityModel;

        /// <summary>同步能力档案。</summary>
        public NetworkSyncProfile Profile { get; }
    }

    /// <summary>
    /// 可由接入项目扩展的同步档案目录。注册操作使用写时复制发布新的只读快照，
    /// 因此解析与枚举不需要持有写锁；完成装配后可调用 <see cref="Freeze"/> 固定目录。
    /// </summary>
    public sealed class NetworkSyncProfileCatalog
    {
        private sealed class CatalogSnapshot
        {
            public static readonly CatalogSnapshot Empty = new CatalogSnapshot(
                Array.Empty<NetworkSyncProfileCatalogEntry>(),
                new Dictionary<NetworkSyncModel, int>(),
                new Dictionary<string, int>(StringComparer.Ordinal));

            public CatalogSnapshot(
                NetworkSyncProfileCatalogEntry[] entries,
                Dictionary<NetworkSyncModel, int> indexByModel,
                Dictionary<string, int> indexByName)
            {
                Entries = entries;
                EntryView = Array.AsReadOnly(entries);
                IndexByModel = indexByModel;
                IndexByName = indexByName;
            }

            public NetworkSyncProfileCatalogEntry[] Entries { get; }
            public IReadOnlyList<NetworkSyncProfileCatalogEntry> EntryView { get; }
            public Dictionary<NetworkSyncModel, int> IndexByModel { get; }
            public Dictionary<string, int> IndexByName { get; }
        }

        private readonly object _syncRoot = new();
        private volatile CatalogSnapshot _snapshot = CatalogSnapshot.Empty;
        private bool _isFrozen;

        /// <summary>当前已注册条目数量。</summary>
        public int Count => _snapshot.Entries.Length;

        /// <summary>目录是否已冻结。</summary>
        public bool IsFrozen
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isFrozen;
                }
            }
        }

        /// <summary>
        /// 注册一个同步档案。稳定名称是目录主键；同一兼容模型可注册多个命名变体。
        /// 某模型首次注册的条目自动成为该模型的默认档案，可通过 <see cref="SetDefault"/> 显式切换。
        /// </summary>
        public void Register(
            string name,
            in NetworkSyncProfile profile,
            NetworkSyncProfileRegistrationMode mode = NetworkSyncProfileRegistrationMode.RejectDuplicate)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Profile name cannot be empty.", nameof(name));
            if (!Enum.IsDefined(typeof(NetworkSyncProfileRegistrationMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));

            lock (_syncRoot)
            {
                ThrowIfFrozen();

                var snapshot = _snapshot;
                if (snapshot.IndexByName.TryGetValue(name, out var existingIndex))
                {
                    if (mode == NetworkSyncProfileRegistrationMode.RejectDuplicate)
                    {
                        throw new InvalidOperationException($"A profile named '{name}' is already registered.");
                    }

                    if (snapshot.Entries[existingIndex].Model != profile.CompatibilityModel)
                    {
                        throw new InvalidOperationException(
                            "Replacing a named profile cannot change its compatibility model.");
                    }

                    NetworkSyncConfigurationValidator.ValidateProfile(in profile).ThrowIfInvalid(name);
                    var replaced = CopyEntries(snapshot.Entries, snapshot.Entries.Length);
                    replaced[existingIndex] = new NetworkSyncProfileCatalogEntry(name, in profile);
                    _snapshot = new CatalogSnapshot(replaced, snapshot.IndexByModel, snapshot.IndexByName);
                    return;
                }

                NetworkSyncConfigurationValidator.ValidateProfile(in profile).ThrowIfInvalid(name);
                var appended = CopyEntries(snapshot.Entries, snapshot.Entries.Length + 1);
                appended[snapshot.Entries.Length] = new NetworkSyncProfileCatalogEntry(name, in profile);
                var modelIndexes = new Dictionary<NetworkSyncModel, int>(snapshot.IndexByModel)
                { };
                if (!modelIndexes.ContainsKey(profile.CompatibilityModel))
                {
                    modelIndexes[profile.CompatibilityModel] = snapshot.Entries.Length;
                }

                var nameIndexes = new Dictionary<string, int>(snapshot.IndexByName, StringComparer.Ordinal)
                {
                    [name] = snapshot.Entries.Length
                };
                _snapshot = new CatalogSnapshot(appended, modelIndexes, nameIndexes);
            }
        }

        /// <summary>
        /// 将指定命名档案设为其兼容模型的默认档案。该操作只影响按
        /// <see cref="NetworkSyncModel"/> 解析的结果，不影响命名档案及其枚举顺序。
        /// </summary>
        public void SetDefault(NetworkSyncModel model, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Profile name cannot be empty.", nameof(name));

            lock (_syncRoot)
            {
                ThrowIfFrozen();
                var snapshot = _snapshot;
                if (!snapshot.IndexByName.TryGetValue(name, out var index))
                {
                    throw new KeyNotFoundException($"Unknown network sync profile name '{name}'.");
                }

                if (snapshot.Entries[index].Model != model)
                {
                    throw new InvalidOperationException(
                        $"Profile '{name}' does not belong to compatibility model '{model}'.");
                }

                var modelIndexes = new Dictionary<NetworkSyncModel, int>(snapshot.IndexByModel)
                {
                    [model] = index
                };
                _snapshot = new CatalogSnapshot(snapshot.Entries, modelIndexes, snapshot.IndexByName);
            }
        }

        /// <summary>解析模型对应的同步档案；模型不存在时抛出异常。</summary>
        public NetworkSyncProfile Resolve(NetworkSyncModel model)
        {
            if (TryResolve(model, out var profile))
            {
                return profile;
            }

            throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown network sync compatibility model.");
        }

        /// <summary>尝试解析模型对应的同步档案。</summary>
        public bool TryResolve(NetworkSyncModel model, out NetworkSyncProfile profile)
        {
            var snapshot = _snapshot;
            if (snapshot.IndexByModel.TryGetValue(model, out var index))
            {
                profile = snapshot.Entries[index].Profile;
                return true;
            }

            profile = NetworkSyncProfiles.Unspecified;
            return false;
        }

        /// <summary>按稳定名称解析同步档案；名称不存在时抛出异常。</summary>
        public NetworkSyncProfile Resolve(string name)
        {
            if (TryResolve(name, out var profile))
            {
                return profile;
            }

            throw new KeyNotFoundException($"Unknown network sync profile name '{name}'.");
        }

        /// <summary>尝试按稳定名称解析同步档案，名称比较区分大小写。</summary>
        public bool TryResolve(string? name, out NetworkSyncProfile profile)
        {
            var snapshot = _snapshot;
            if (name != null && snapshot.IndexByName.TryGetValue(name, out var index))
            {
                profile = snapshot.Entries[index].Profile;
                return true;
            }

            profile = NetworkSyncProfiles.Unspecified;
            return false;
        }

        /// <summary>返回模型对应的稳定名称；模型不存在时抛出异常。</summary>
        public string GetName(NetworkSyncModel model)
        {
            if (TryGetName(model, out var name))
            {
                return name;
            }

            throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown network sync compatibility model.");
        }

        /// <summary>尝试返回模型对应的稳定名称。</summary>
        public bool TryGetName(NetworkSyncModel model, out string name)
        {
            var snapshot = _snapshot;
            if (snapshot.IndexByModel.TryGetValue(model, out var index))
            {
                name = snapshot.Entries[index].Name;
                return true;
            }

            name = string.Empty;
            return false;
        }

        /// <summary>返回注册条目的稳定快照，不受后续注册影响。</summary>
        public IReadOnlyList<NetworkSyncProfileCatalogEntry> Entries()
        {
            return _snapshot.EntryView;
        }

        /// <summary>创建包含当前条目的可变副本。</summary>
        public NetworkSyncProfileCatalog CreateMutableCopy()
        {
            var copy = new NetworkSyncProfileCatalog();
            var snapshot = _snapshot;
            var entries = snapshot.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                copy.Register(entries[i].Name, entries[i].Profile);
            }

            foreach (var defaultEntry in snapshot.IndexByModel)
            {
                copy.SetDefault(defaultEntry.Key, entries[defaultEntry.Value].Name);
            }

            return copy;
        }

        /// <summary>检查目录中全部命名档案，并一次返回所有配置问题。</summary>
        public NetworkSyncConfigurationReport Validate()
        {
            var issues = new List<NetworkSyncConfigurationIssue>();
            var entries = _snapshot.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                var profile = entries[i].Profile;
                NetworkSyncConfigurationValidator.AppendProfileIssues(
                    in profile,
                    issues,
                    $"Profiles[{entries[i].Name}]");
            }

            return new NetworkSyncConfigurationReport(issues);
        }

        /// <summary>校验并冻结目录；冻结后所有注册操作都会失败。</summary>
        public void Freeze()
        {
            lock (_syncRoot)
            {
                Validate().ThrowIfInvalid("网络同步档案目录");
                _isFrozen = true;
            }
        }

        private void ThrowIfFrozen()
        {
            if (_isFrozen)
            {
                throw new InvalidOperationException("The network sync profile catalog is frozen.");
            }
        }

        private static NetworkSyncProfileCatalogEntry[] CopyEntries(
            NetworkSyncProfileCatalogEntry[] source,
            int length)
        {
            var copy = new NetworkSyncProfileCatalogEntry[length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
