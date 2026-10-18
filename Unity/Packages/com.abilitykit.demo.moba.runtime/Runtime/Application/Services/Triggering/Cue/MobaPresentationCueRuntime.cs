using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Runtime;
using PresentationCueSemantics = AbilityKit.Demo.Moba.Share.PresentationCueSemantics;
using PresentationCueTransition = AbilityKit.Demo.Moba.Share.PresentationCueTransition;

namespace AbilityKit.Demo.Moba.Triggering
{
    /// <summary>
    /// MOBA 表现 Cue 定义：业务层可在此扩展表现资源映射，framework cue core 仍只依赖 TriggerCueDescriptor。
    /// </summary>
    public readonly struct MobaPresentationCueDefinition
    {
        public MobaPresentationCueDefinition(
            string cueId,
            string kind,
            int templateId,
            int vfxId,
            int sfxId,
            string primaryAssetId,
            string secondaryAssetId,
            string payload)
        {
            CueId = cueId;
            Kind = kind;
            TemplateId = templateId;
            VfxId = vfxId;
            SfxId = sfxId;
            PrimaryAssetId = primaryAssetId;
            SecondaryAssetId = secondaryAssetId;
            Payload = payload;
        }

        public string CueId { get; }
        public string Kind { get; }
        public int TemplateId { get; }
        public int VfxId { get; }
        public int SfxId { get; }
        public string PrimaryAssetId { get; }
        public string SecondaryAssetId { get; }
        public string Payload { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(CueId) && string.IsNullOrWhiteSpace(Kind) && TemplateId == 0 && VfxId == 0 && SfxId == 0;
    }

    public interface IMobaPresentationCueRegistry : IService
    {
        void Register(in MobaPresentationCueDefinition definition);
        bool TryGet(string cueId, out MobaPresentationCueDefinition definition);
    }

    public interface IMobaPresentationCueResolver : IService
    {
        MobaPresentationCueDefinition Resolve(in TriggerCueDescriptor descriptor);
    }

    [WorldService(typeof(IMobaPresentationCueRegistry))]
    [WorldService(typeof(MobaPresentationCueRegistry))]
    public sealed class MobaPresentationCueRegistry : IMobaPresentationCueRegistry
    {
        private readonly Dictionary<string, MobaPresentationCueDefinition> _definitions = new Dictionary<string, MobaPresentationCueDefinition>(StringComparer.OrdinalIgnoreCase);

        public void Register(in MobaPresentationCueDefinition definition)
        {
            var key = definition.CueId;
            if (string.IsNullOrWhiteSpace(key)) key = definition.Kind;
            if (string.IsNullOrWhiteSpace(key)) return;

            _definitions[key] = definition;
        }

        public bool TryGet(string cueId, out MobaPresentationCueDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(cueId) && _definitions.TryGetValue(cueId, out definition))
            {
                return true;
            }

            definition = default;
            return false;
        }

        public void Dispose()
        {
            _definitions.Clear();
        }
    }

    [WorldService(typeof(IMobaPresentationCueResolver))]
    [WorldService(typeof(MobaPresentationCueResolver))]
    public sealed class MobaPresentationCueResolver : IMobaPresentationCueResolver
    {
        private readonly IMobaPresentationCueRegistry _registry;

        public MobaPresentationCueResolver(IMobaPresentationCueRegistry registry = null)
        {
            _registry = registry;
        }

        public MobaPresentationCueDefinition Resolve(in TriggerCueDescriptor descriptor)
        {
            if (!descriptor.IsEmpty)
            {
                if (_registry != null)
                {
                    if (_registry.TryGet(descriptor.CueId, out var byCueId)) return Merge(in descriptor, in byCueId);
                    if (_registry.TryGet(descriptor.Kind, out var byKind)) return Merge(in descriptor, in byKind);
                }

                return FromDescriptor(in descriptor);
            }

            return default;
        }

        private static MobaPresentationCueDefinition Merge(in TriggerCueDescriptor descriptor, in MobaPresentationCueDefinition registered)
        {
            var fromDescriptor = FromDescriptor(in descriptor);
            return new MobaPresentationCueDefinition(
                string.IsNullOrWhiteSpace(fromDescriptor.CueId) ? registered.CueId : fromDescriptor.CueId,
                string.IsNullOrWhiteSpace(fromDescriptor.Kind) ? registered.Kind : fromDescriptor.Kind,
                fromDescriptor.TemplateId != 0 ? fromDescriptor.TemplateId : registered.TemplateId,
                fromDescriptor.VfxId != 0 ? fromDescriptor.VfxId : registered.VfxId,
                fromDescriptor.SfxId != 0 ? fromDescriptor.SfxId : registered.SfxId,
                string.IsNullOrWhiteSpace(fromDescriptor.PrimaryAssetId) ? registered.PrimaryAssetId : fromDescriptor.PrimaryAssetId,
                string.IsNullOrWhiteSpace(fromDescriptor.SecondaryAssetId) ? registered.SecondaryAssetId : fromDescriptor.SecondaryAssetId,
                string.IsNullOrWhiteSpace(fromDescriptor.Payload) ? registered.Payload : fromDescriptor.Payload);
        }

        public static MobaPresentationCueDefinition FromDescriptor(in TriggerCueDescriptor descriptor)
        {
            return new MobaPresentationCueDefinition(
                descriptor.CueId,
                descriptor.Kind,
                ParseId(descriptor.CueId ?? descriptor.Kind),
                ParseId(descriptor.PrimaryAssetId),
                ParseId(descriptor.SecondaryAssetId),
                descriptor.PrimaryAssetId,
                descriptor.SecondaryAssetId,
                descriptor.Payload);
        }

        private static int ParseId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            return int.TryParse(value, out var id) ? id : 0;
        }

        public void Dispose()
        {
        }
    }

    public readonly struct MobaActivePresentationCue
    {
        public MobaActivePresentationCue(string key, MobaPresentationCueSnapshotEntry entry, int revision)
        {
            Key = key;
            Entry = entry;
            Revision = revision;
        }

        public string Key { get; }
        public MobaPresentationCueSnapshotEntry Entry { get; }
        public int Revision { get; }
    }

    public enum MobaPresentationCueObserveResult : byte
    {
        Ignored = 0,
        Transient = 1,
        Started = 2,
        Updated = 3,
        Stopped = 4,
        MissingStableKey = 5,
    }

    public sealed class MobaActivePresentationCueStore
    {
        private readonly Dictionary<string, MobaActivePresentationCue> _active = new Dictionary<string, MobaActivePresentationCue>(StringComparer.Ordinal);
        private int _revision;

        public IReadOnlyDictionary<string, MobaActivePresentationCue> Active => _active;
        public int Count => _active.Count;

        public MobaPresentationCueObserveResult Observe(in MobaPresentationCueSnapshotEntry entry)
        {
            var stage = (MobaPresentationCueStage)entry.Stage;
            if (PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.None)
            {
                return MobaPresentationCueObserveResult.Ignored;
            }

            var lifetime = MobaPresentationCueKeys.ResolveLifetime(in entry);
            if (lifetime == AbilityKit.Demo.Moba.Share.PresentationCueLifetime.TransientEvent)
            {
                return MobaPresentationCueObserveResult.Transient;
            }

            var key = MobaPresentationCueKeys.GetStableKey(in entry);
            if (string.IsNullOrWhiteSpace(key)) return MobaPresentationCueObserveResult.MissingStableKey;

            if (MobaPresentationCueStages.ShouldStop(stage))
            {
                _active.Remove(key);
                return MobaPresentationCueObserveResult.Stopped;
            }

            if (MobaPresentationCueStages.ShouldStart(stage) || MobaPresentationCueStages.ShouldKeepActive(stage))
            {
                var wasActive = _active.ContainsKey(key);
                _active[key] = new MobaActivePresentationCue(key, entry, ++_revision);
                return wasActive ? MobaPresentationCueObserveResult.Updated : MobaPresentationCueObserveResult.Started;
            }

            return MobaPresentationCueObserveResult.Ignored;
        }

        public bool TryGet(string key, out MobaActivePresentationCue active)
        {
            if (!string.IsNullOrWhiteSpace(key) && _active.TryGetValue(key, out active))
            {
                return true;
            }

            active = default;
            return false;
        }

        public bool TryGet(in MobaPresentationCueSnapshotEntry entry, out MobaActivePresentationCue active)
        {
            return TryGet(MobaPresentationCueKeys.GetStableKey(in entry), out active);
        }

        public bool Contains(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _active.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _active.Remove(key);
        }

        public int CopyActiveTo(ICollection<MobaActivePresentationCue> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            foreach (var cue in _active.Values)
            {
                destination.Add(cue);
            }

            return _active.Count;
        }

        public void Clear()
        {
            _active.Clear();
            _revision = 0;
        }
    }

    public sealed class MobaPresentationCueEntryPool
    {
        private readonly Dictionary<int, Stack<MobaPresentationCueSnapshotEntry[]>> _exactArrays = new Dictionary<int, Stack<MobaPresentationCueSnapshotEntry[]>>();
        private readonly int _defaultCapacity;
        private readonly int _maxRetainedPerLength;

        public MobaPresentationCueEntryPool(int defaultCapacity = 32, int maxRetainedPerLength = 8)
        {
            _defaultCapacity = defaultCapacity > 0 ? defaultCapacity : 32;
            _maxRetainedPerLength = maxRetainedPerLength > 0 ? maxRetainedPerLength : 8;
        }

        public int AvailableCount
        {
            get
            {
                var count = 0;
                foreach (var bucket in _exactArrays.Values)
                {
                    count += bucket.Count;
                }

                return count;
            }
        }

        public MobaPresentationCueSnapshotEntry[] Rent(int minCapacity)
        {
            var length = Math.Max(_defaultCapacity, minCapacity);
            return RentExact(length);
        }

        public MobaPresentationCueSnapshotEntry[] RentExact(int length)
        {
            if (length <= 0) return Array.Empty<MobaPresentationCueSnapshotEntry>();

            if (_exactArrays.TryGetValue(length, out var bucket))
            {
                while (bucket.Count > 0)
                {
                    var array = bucket.Pop();
                    if (array != null && array.Length == length) return array;
                }
            }

            return new MobaPresentationCueSnapshotEntry[length];
        }

        public void Return(MobaPresentationCueSnapshotEntry[] array)
        {
            if (array == null || array.Length == 0) return;
            Array.Clear(array, 0, array.Length);

            if (!_exactArrays.TryGetValue(array.Length, out var bucket))
            {
                bucket = new Stack<MobaPresentationCueSnapshotEntry[]>();
                _exactArrays[array.Length] = bucket;
            }

            if (bucket.Count < _maxRetainedPerLength)
            {
                bucket.Push(array);
            }
        }

        public void Clear()
        {
            _exactArrays.Clear();
        }
    }

    public sealed class MobaPresentationCueReplicationPolicy
    {
        public bool ShouldReplicate(in MobaPresentationCueSnapshotEntry entry)
        {
            return entry.Stage != (int)MobaPresentationCueStage.None;
        }

        public void ApplyDefaults(ref MobaPresentationCueSnapshotEntry entry)
        {
            if (entry.ReplicationMode == 0)
            {
                entry.ReplicationMode = (int)MobaPresentationCueReplicationMode.ReliableForLifecycle;
            }

            if (string.IsNullOrWhiteSpace(entry.ReplicationId))
            {
                entry.ReplicationId = MobaPresentationCueKeys.GetStableKey(in entry);
            }
        }
    }

    public sealed class MobaPresentationCuePredictionService
    {
        public void ApplyServerAuthoritativeDefaults(ref MobaPresentationCueSnapshotEntry entry)
        {
            if (entry.PredictionKey == 0)
            {
                entry.PredictionKey = MobaPresentationCueKeys.StableHash(entry.ReplicationId ?? entry.RequestKey);
            }

            if (entry.PredictionState == 0)
            {
                entry.PredictionState = (int)MobaPresentationCuePredictionState.ServerConfirmed;
            }
        }
    }

    public static class MobaPresentationCueStages
    {
        // 迁移分类的唯一权威在 AbilityKit.Demo.Moba.Share.PresentationCueSemantics。
        // 这里保留转发，供逻辑侧既有的 MobaPresentationCueStage 枚举类型使用；
        // 客户端的三份镜像枚举同样转发到那一份实现，因此不再存在多表不同步的可能。
        public static bool ShouldStart(MobaPresentationCueStage stage)
        {
            return AbilityKit.Demo.Moba.Share.PresentationCueSemantics.Classify((int)stage)
                == AbilityKit.Demo.Moba.Share.PresentationCueTransition.Start;
        }

        public static bool ShouldKeepActive(MobaPresentationCueStage stage)
        {
            return AbilityKit.Demo.Moba.Share.PresentationCueSemantics.Classify((int)stage)
                == AbilityKit.Demo.Moba.Share.PresentationCueTransition.KeepActive;
        }

        public static bool ShouldStop(MobaPresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage)
                == PresentationCueTransition.Stop;
        }
    }

    public static class MobaPresentationCueKeys
    {
        public static AbilityKit.Demo.Moba.Share.PresentationCueLifetime ResolveLifetime(in MobaPresentationCueSnapshotEntry entry)
        {
            if (entry.Lifetime != 0)
            {
                return (AbilityKit.Demo.Moba.Share.PresentationCueLifetime)entry.Lifetime;
            }

            var domain = entry.Domain != 0
                ? (AbilityKit.Demo.Moba.Share.PresentationCueDomain)entry.Domain
                : PresentationCueSemantics.ResolveDomain(entry.OwnerKind ?? entry.CueKind);
            // Pre-P0 payloads did not carry domain/lifetime. A stable key is
            // the compatibility signal that the producer intended a retained
            // presentation object rather than a one-shot event.
            if (domain == AbilityKit.Demo.Moba.Share.PresentationCueDomain.None &&
                !string.IsNullOrWhiteSpace(GetStableKey(in entry)))
            {
                return AbilityKit.Demo.Moba.Share.PresentationCueLifetime.PersistentState;
            }
            return PresentationCueSemantics.ResolveLifetime(
                domain,
                (AbilityKit.Demo.Moba.Share.PresentationCueStage)entry.Stage);
        }

        public static string GetStableKey(in MobaPresentationCueSnapshotEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.InstanceKey)) return entry.InstanceKey;
            if (!string.IsNullOrWhiteSpace(entry.RequestKey)) return entry.RequestKey;
            return null;
        }

        public static int StableHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            unchecked
            {
                var hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return (int)(hash & 0x7fffffff);
            }
        }
    }
}
