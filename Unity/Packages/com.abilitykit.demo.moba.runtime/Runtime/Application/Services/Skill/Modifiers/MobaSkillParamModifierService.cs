using System;
using System.Buffers;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 面向作用域的修饰器容器与解析器，用于技能和计划动作参数。
    /// </summary>
    [WorldService(typeof(MobaSkillParamModifierService))]
    public sealed class MobaSkillParamModifierService : IService, IWorldInitializable
    {
        private const int ResolutionTraceCapacity = 128;
        private readonly Dictionary<OwnerKey, List<ModifierData>> _modifiersByOwner = new Dictionary<OwnerKey, List<ModifierData>>();
        private readonly Dictionary<OwnerKey, List<MobaParameterSelector>> _selectorsByOwner =
            new Dictionary<OwnerKey, List<MobaParameterSelector>>();
        private readonly Dictionary<OwnerKey, Dictionary<ModifierKey, List<int>>> _indicesByOwner =
            new Dictionary<OwnerKey, Dictionary<ModifierKey, List<int>>>();
        private readonly ModifierCalculator _calculator = new ModifierCalculator { EnableCache = false };
        private readonly MobaParameterResolutionTrace[] _recentResolutions =
            new MobaParameterResolutionTrace[ResolutionTraceCapacity];
        private int _nextResolutionTrace;
        private int _resolutionTraceCount;
        private long _evictedResolutionTraceCount;
        private IFrameTime _frameTime;
        private IMobaModifierPrecisionPolicy _precision = PassThroughModifierPrecisionPolicy.Instance;

        public long EvictedResolutionTraceCount => _evictedResolutionTraceCount;

        public MobaSkillParamGroupResolver Skill { get; }
        public MobaProjectileParamGroupResolver Projectile { get; }
        public MobaSummonParamGroupResolver Summon { get; }
        public MobaBuffParamGroupResolver Buff { get; }
        public MobaAoeParamGroupResolver Aoe { get; }
        public MobaDamageParamGroupResolver Damage { get; }

        public MobaSkillParamModifierService()
        {
            Skill = new MobaSkillParamGroupResolver(this);
            Projectile = new MobaProjectileParamGroupResolver(this);
            Summon = new MobaSummonParamGroupResolver(this);
            Buff = new MobaBuffParamGroupResolver(this);
            Aoe = new MobaAoeParamGroupResolver(this);
            Damage = new MobaDamageParamGroupResolver(this);
        }

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _frameTime);
            if (services != null && services.TryResolve(out IMobaModifierPrecisionPolicy precision) &&
                precision != null)
                _precision = precision;
        }

        internal void AddModifier(int actorId, in ModifierData modifier)
        {
            AddModifier(MobaModifierOwnerRef.Actor(actorId), in modifier);
        }

        internal void AddModifier(MobaModifierOwnerRef owner, in ModifierData modifier)
        {
            AddModifier(owner, in modifier, default);
        }

        internal void AddModifier(MobaModifierOwnerRef owner, in ModifierData modifier, in MobaParameterSelector selector)
        {
            AddModifierCore(owner, in modifier, in selector);
        }

        private void AddModifierCore(MobaModifierOwnerRef owner, in ModifierData modifier,
            in MobaParameterSelector selector)
        {
            ValidateProjection(owner, in modifier, in selector);

            var key = new OwnerKey(owner.Scope, owner.Id);
            if (!_modifiersByOwner.TryGetValue(key, out var modifiers) || modifiers == null)
            {
                modifiers = new List<ModifierData>(4);
                _modifiersByOwner[key] = modifiers;
                _selectorsByOwner[key] = new List<MobaParameterSelector>(4);
                _indicesByOwner[key] = new Dictionary<ModifierKey, List<int>>();
            }

            var index = modifiers.Count;
            modifiers.Add(modifier);
            _selectorsByOwner[key].Add(selector);
            AddIndex(key, modifier.Key, index);
        }

        internal void ClearActor(int actorId)
        {
            ClearOwner(MobaModifierOwnerRef.Actor(actorId));
        }

        internal void ClearOwner(MobaModifierOwnerRef owner)
        {
            if (!owner.IsValid) return;
            _modifiersByOwner.Remove(new OwnerKey(owner.Scope, owner.Id));
            _selectorsByOwner.Remove(new OwnerKey(owner.Scope, owner.Id));
            _indicesByOwner.Remove(new OwnerKey(owner.Scope, owner.Id));
        }

        internal void ClearSource(int actorId, int sourceId)
        {
            ClearSource(MobaModifierOwnerRef.Actor(actorId), sourceId);
        }

        internal void ClearSource(MobaModifierOwnerRef owner, int sourceId)
        {
            if (!owner.IsValid) return;
            var key = new OwnerKey(owner.Scope, owner.Id);
            if (!_modifiersByOwner.TryGetValue(key, out var modifiers) || modifiers == null) return;

            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].SourceId == sourceId)
                {
                    modifiers.RemoveAt(i);
                    _selectorsByOwner[key].RemoveAt(i);
                }
            }

            if (modifiers.Count == 0)
            {
                _modifiersByOwner.Remove(key);
                _selectorsByOwner.Remove(key);
                _indicesByOwner.Remove(key);
            }
            else RebuildIndices(key, modifiers);
        }

        public MobaSkillParamModifierServiceSnapshot CaptureRollbackSnapshot()
        {
            var entries = new List<MobaSkillParamModifierSnapshotEntry>();
            foreach (var pair in _modifiersByOwner)
            {
                var modifiers = pair.Value;
                if (modifiers == null) continue;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    entries.Add(new MobaSkillParamModifierSnapshotEntry(
                        pair.Key.Scope, pair.Key.Id, CloneModifier(in modifier), _selectorsByOwner[pair.Key][i]));
                }
            }
            entries.Sort(MobaSkillParamModifierSnapshotEntry.Compare);
            return new MobaSkillParamModifierServiceSnapshot(entries.ToArray());
        }

        internal void RestoreRollbackSnapshot(in MobaSkillParamModifierServiceSnapshot snapshot)
        {
            ValidateRollbackSnapshot(in snapshot);
            _modifiersByOwner.Clear();
            _selectorsByOwner.Clear();
            _indicesByOwner.Clear();
            _nextResolutionTrace = 0;
            _resolutionTraceCount = 0;
            _evictedResolutionTraceCount = 0;
            var entries = snapshot.Entries;
            if (entries == null) return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var modifier = entry.Modifier;
                AddModifierCore(new MobaModifierOwnerRef(entry.Scope, entry.OwnerId),
                    CloneModifier(in modifier), entry.Selector);
            }
        }

        internal static void ValidateRollbackSnapshot(in MobaSkillParamModifierServiceSnapshot snapshot)
        {
            var entries = snapshot.Entries;
            if (entries == null) return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var modifier = entry.Modifier;
                var selector = entry.Selector;
                ValidateProjection(new MobaModifierOwnerRef(entry.Scope, entry.OwnerId),
                    in modifier, in selector);
            }
        }

        private static void ValidateProjection(MobaModifierOwnerRef owner, in ModifierData modifier,
            in MobaParameterSelector selector)
        {
            if (!owner.IsValid)
                throw new InvalidOperationException(
                    $"Modifier projection requires a positive Actor owner, got {owner.Scope}:{owner.Id}.");
            if (!selector.IsEmpty &&
                (!MobaSkillParamModifierKeys.TryGetResolveStage(modifier.Key, out var stage) ||
                 !selector.IsValidForStage(stage)))
                throw new InvalidOperationException($"Modifier selector is unavailable at parameter stage for {modifier.Key}.");
        }

        public int ResolveInt(MobaModifierOwnerRef owner, ModifierKey key, int baseValue,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            RequireActorOwner(owner);
            var resolved = ResolveRecorded(owner, key, baseValue, in record, context,
                out var matchedCount, out var missingFields);
            RequireFinite(key, baseValue, resolved);
            var result = RoundToInt(resolved);
            TrackResolution(owner, key, baseValue, result, in record, matchedCount, missingFields);
            return result;
        }

        public float ResolveFloat(MobaModifierOwnerRef owner, ModifierKey key, float baseValue,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            RequireActorOwner(owner);
            var result = ResolveRecorded(owner, key, baseValue, in record, context,
                out var matchedCount, out var missingFields);
            RequireFinite(key, baseValue, result);
            TrackResolution(owner, key, baseValue, result, in record, matchedCount, missingFields);
            return result;
        }

        /// <summary>
        /// 解析进入确定性模拟域的数值参数：在 float 解析结果上应用当前世界的精度策略。
        /// 直通策略下与 <see cref="ResolveFloat"/> 位等价（零换算）；
        /// 帧同步定点策略下对齐 Q32.32 权威网格，越界 fail-loud。
        /// </summary>
        public float ResolveSimFloat(MobaModifierOwnerRef owner, ModifierKey key, float baseValue,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            var result = ResolveFloat(owner, key, baseValue, in record, context);
            return _precision.QuantizeSimFloat(result);
        }

        internal void UsePrecisionPolicyForTesting(IMobaModifierPrecisionPolicy policy)
        {
            _precision = policy ?? PassThroughModifierPrecisionPolicy.Instance;
        }

        public void CopyRecentResolutionsTo(List<MobaParameterResolutionTrace> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            var first = (_nextResolutionTrace - _resolutionTraceCount + ResolutionTraceCapacity) %
                ResolutionTraceCapacity;
            for (var i = 0; i < _resolutionTraceCount; i++)
                destination.Add(_recentResolutions[(first + i) % ResolutionTraceCapacity]);
        }

        private void TrackResolution(MobaModifierOwnerRef owner, ModifierKey key, float baseValue,
            float finalValue, in MobaParameterRecord record, int matchedCount,
            MobaParameterIdentityFields missingFields)
        {
            if (!owner.IsValid || record.Stage == MobaParameterStage.None) return;
            _recentResolutions[_nextResolutionTrace] = new MobaParameterResolutionTrace(
                owner, key, record, baseValue, finalValue, matchedCount, missingFields,
                _frameTime?.Frame.Value ?? -1);
            _nextResolutionTrace = (_nextResolutionTrace + 1) % ResolutionTraceCapacity;
            if (_resolutionTraceCount < ResolutionTraceCapacity) _resolutionTraceCount++;
            else _evictedResolutionTraceCount++;
        }

        public MobaParameterResolutionExplanation ExplainRecorded(MobaModifierOwnerRef owner,
            ModifierKey key, float baseValue, in MobaParameterRecord record,
            IModifierContext context = null)
        {
            RequireActorOwner(owner);
            ValidateRecordStage(key, in record);
            RequireFinite(key, baseValue, baseValue);
            if (!owner.IsValid ||
                !_modifiersByOwner.TryGetValue(new OwnerKey(owner.Scope, owner.Id), out var modifiers))
                return new MobaParameterResolutionExplanation(record, baseValue, baseValue,
                    Array.Empty<ModifierSourceEntry>());

            var selectors = _selectorsByOwner[new OwnerKey(owner.Scope, owner.Id)];
            var matched = new List<ModifierData>();
            var missingFields = MobaParameterIdentityFields.None;
            if (_indicesByOwner.TryGetValue(new OwnerKey(owner.Scope, owner.Id), out var byKey) &&
                byKey.TryGetValue(key, out var indices))
                for (var i = 0; i < indices.Count; i++)
                {
                    missingFields |= selectors[indices[i]].MissingFields(in record);
                    if (selectors[indices[i]].Matches(in record))
                        matched.Add(modifiers[indices[i]]);
                }
            if (matched.Count == 0)
                return new MobaParameterResolutionExplanation(record, baseValue, baseValue,
                    Array.Empty<ModifierSourceEntry>(), missingFields);

            var ordered = matched.ToArray();
            var recorder = new DefaultRecorder(ordered.Length);
            var result = _calculator.Calculate(ordered.AsSpan(), baseValue, recorder, context);
            RequireFinite(key, baseValue, result.FinalValue);
            var contributions = new ModifierSourceEntry[recorder.Count];
            for (var i = 0; i < contributions.Length; i++)
                contributions[i] = recorder.GetEntry(i);
            return new MobaParameterResolutionExplanation(record, baseValue, result.FinalValue,
                contributions, missingFields);
        }

        private float ResolveRecorded(MobaModifierOwnerRef owner, ModifierKey key, float baseValue,
            in MobaParameterRecord record, IModifierContext context, out int matchedCount,
            out MobaParameterIdentityFields missingFields)
        {
            matchedCount = 0;
            missingFields = MobaParameterIdentityFields.None;
            ValidateRecordStage(key, in record);
            if (!owner.IsValid) return baseValue;
            var ownerKey = new OwnerKey(owner.Scope, owner.Id);
            if (!_modifiersByOwner.TryGetValue(ownerKey, out var modifiers)) return baseValue;
            if (!_indicesByOwner[ownerKey].TryGetValue(key, out var indices)) return baseValue;
            var selectors = _selectorsByOwner[ownerKey];
            var count = 0;
            for (var i = 0; i < indices.Count; i++)
            {
                missingFields |= selectors[indices[i]].MissingFields(in record);
                if (selectors[indices[i]].Matches(in record)) count++;
            }
            matchedCount = count;
            if (count == 0) return baseValue;

            var rented = ArrayPool<ModifierData>.Shared.Rent(count);
            try
            {
                var written = 0;
                for (var i = 0; i < indices.Count; i++)
                    if (selectors[indices[i]].Matches(in record))
                        rented[written++] = modifiers[indices[i]];
                return Compose(rented.AsSpan(0, written), baseValue, context);
            }
            finally
            {
                ArrayPool<ModifierData>.Shared.Return(rented, clearArray: true);
            }
        }

        private static void RequireActorOwner(MobaModifierOwnerRef owner)
        {
            if (owner.Scope != MobaModifierOwnerScope.Actor)
                throw new InvalidOperationException($"Modifier resolution requires an Actor owner, got {owner.Scope}.");
        }

        private void AddIndex(OwnerKey owner, ModifierKey key, int index)
        {
            var byKey = _indicesByOwner[owner];
            if (!byKey.TryGetValue(key, out var indices))
                byKey[key] = indices = new List<int>(4);
            var modifiers = _modifiersByOwner[owner];
            var item = modifiers[index];
            var position = indices.Count;
            while (position > 0)
            {
                var previous = modifiers[indices[position - 1]];
                if (item.Priority < previous.Priority ||
                    item.Priority == previous.Priority && item.SourceId >= previous.SourceId)
                    break;
                position--;
            }
            indices.Insert(position, index);
        }

        private void RebuildIndices(OwnerKey owner, List<ModifierData> modifiers)
        {
            var byKey = _indicesByOwner[owner];
            byKey.Clear();
            for (var i = 0; i < modifiers.Count; i++)
                AddIndex(owner, modifiers[i].Key, i);
        }

        private static void ValidateRecordStage(ModifierKey key, in MobaParameterRecord record)
        {
            if (!MobaSkillParamModifierKeys.TryGetResolveStage(key, out var expected) ||
                record.Stage != expected)
                throw new InvalidOperationException(
                    $"Invalid modifier parameter stage for {key}: expected {expected}, actual {record.Stage}.");
        }

        private static int RoundToInt(float value)
        {
            if (value <= int.MinValue) return int.MinValue;
            if (value >= int.MaxValue) return int.MaxValue;
            return (int)Math.Round(value);
        }

        private static void RequireFinite(ModifierKey key, float baseValue, float result)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue) ||
                float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidOperationException(
                    $"Non-finite modifier parameter value for {key}: base={baseValue}, result={result}.");
        }

        public void Dispose()
        {
            _modifiersByOwner.Clear();
            _selectorsByOwner.Clear();
            _indicesByOwner.Clear();
            _nextResolutionTrace = 0;
            _resolutionTraceCount = 0;
            _evictedResolutionTraceCount = 0;
            _frameTime = null;
            _precision = PassThroughModifierPrecisionPolicy.Instance;
        }

        private static float Compose(ReadOnlySpan<ModifierData> modifiers, float baseValue,
            IModifierContext context)
        {
            return context == null
                ? OperatorComposer.Compose(modifiers, baseValue, 1f, null).FinalValue
                : new ModifierComputeCore(null).Compute(modifiers, baseValue, context.Level, context).FinalValue;
        }

        private readonly struct OwnerKey : IEquatable<OwnerKey>
        {
            public OwnerKey(MobaModifierOwnerScope scope, int id)
            {
                Scope = scope;
                Id = id;
            }

            internal MobaModifierOwnerScope Scope { get; }
            internal int Id { get; }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Scope * 397) ^ Id;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is OwnerKey other && Equals(other);
            }

            public bool Equals(OwnerKey other) => Scope == other.Scope && Id == other.Id;
        }

        private static ModifierData CloneModifier(in ModifierData source)
        {
            var clone = source;
            clone.Magnitude.ArrayData = Clone(source.Magnitude.ArrayData);
            var pipeline = clone.Magnitude.PipelineData;
            pipeline.Modifier0.Curve = Clone(pipeline.Modifier0.Curve);
            pipeline.Modifier1.Curve = Clone(pipeline.Modifier1.Curve);
            pipeline.Modifier2.Curve = Clone(pipeline.Modifier2.Curve);
            pipeline.Modifier3.Curve = Clone(pipeline.Modifier3.Curve);
            clone.Magnitude.PipelineData = pipeline;
            clone.CustomData.RawData = Clone(source.CustomData.RawData);
            return clone;
        }

        private static T[] Clone<T>(T[] source)
        {
            return source == null ? null : (T[])source.Clone();
        }
    }

    public readonly struct MobaParameterResolutionTrace
    {
        public MobaParameterResolutionTrace(MobaModifierOwnerRef owner, ModifierKey key,
            MobaParameterRecord record, float baseValue, float finalValue, int matchedCount,
            MobaParameterIdentityFields missingSelectorFields = MobaParameterIdentityFields.None,
            int frame = -1)
        {
            Owner = owner;
            Key = key;
            Record = record;
            BaseValue = baseValue;
            FinalValue = finalValue;
            MatchedCount = matchedCount;
            MissingSelectorFields = missingSelectorFields;
            Frame = frame;
        }

        public MobaModifierOwnerRef Owner { get; }
        public ModifierKey Key { get; }
        public MobaParameterRecord Record { get; }
        public float BaseValue { get; }
        public float FinalValue { get; }
        public int MatchedCount { get; }
        public MobaParameterIdentityFields MissingSelectorFields { get; }
        public int Frame { get; }
    }

    public readonly struct MobaParameterResolutionExplanation
    {
        public MobaParameterResolutionExplanation(MobaParameterRecord record, float baseValue,
            float finalValue, IReadOnlyList<ModifierSourceEntry> contributions,
            MobaParameterIdentityFields missingSelectorFields = MobaParameterIdentityFields.None)
        {
            Record = record;
            BaseValue = baseValue;
            FinalValue = finalValue;
            Contributions = contributions;
            MissingSelectorFields = missingSelectorFields;
        }

        public MobaParameterRecord Record { get; }
        public float BaseValue { get; }
        public float FinalValue { get; }
        public IReadOnlyList<ModifierSourceEntry> Contributions { get; }
        public MobaParameterIdentityFields MissingSelectorFields { get; }
    }

    public readonly struct MobaSkillParamModifierServiceSnapshot
    {
        public MobaSkillParamModifierServiceSnapshot(MobaSkillParamModifierSnapshotEntry[] entries)
        {
            Entries = entries ?? Array.Empty<MobaSkillParamModifierSnapshotEntry>();
        }

        public MobaSkillParamModifierSnapshotEntry[] Entries { get; }
    }

    public readonly struct MobaSkillParamModifierSnapshotEntry
    {
        public MobaSkillParamModifierSnapshotEntry(MobaModifierOwnerScope scope, int ownerId,
            ModifierData modifier, MobaParameterSelector selector = default)
        {
            Scope = scope;
            OwnerId = ownerId;
            Modifier = modifier;
            Selector = selector;
        }

        public MobaModifierOwnerScope Scope { get; }
        public int OwnerId { get; }
        public ModifierData Modifier { get; }
        public MobaParameterSelector Selector { get; }

        internal static int Compare(MobaSkillParamModifierSnapshotEntry left, MobaSkillParamModifierSnapshotEntry right)
        {
            var scope = left.Scope.CompareTo(right.Scope);
            if (scope != 0) return scope;
            var owner = left.OwnerId.CompareTo(right.OwnerId);
            if (owner != 0) return owner;
            var key = left.Modifier.Key.Packed.CompareTo(right.Modifier.Key.Packed);
            if (key != 0) return key;
            var priority = left.Modifier.Priority.CompareTo(right.Modifier.Priority);
            if (priority != 0) return priority;
            var source = left.Modifier.SourceId.CompareTo(right.Modifier.SourceId);
            if (source != 0) return source;
            var op = left.Modifier.Op.CompareTo(right.Modifier.Op);
            if (op != 0) return op;
            var slot = left.Selector.Slot.CompareTo(right.Selector.Slot);
            if (slot != 0) return slot;
            var baseSkill = left.Selector.BaseSkillId.CompareTo(right.Selector.BaseSkillId);
            if (baseSkill != 0) return baseSkill;
            var effectiveSkill = left.Selector.EffectiveSkillId.CompareTo(right.Selector.EffectiveSkillId);
            if (effectiveSkill != 0) return effectiveSkill;
            var launcher = left.Selector.LauncherTemplateId.CompareTo(right.Selector.LauncherTemplateId);
            if (launcher != 0) return launcher;
            var projectile = left.Selector.ProjectileTemplateId.CompareTo(right.Selector.ProjectileTemplateId);
            if (projectile != 0) return projectile;
            var area = left.Selector.AreaTemplateId.CompareTo(right.Selector.AreaTemplateId);
            if (area != 0) return area;
            var summon = left.Selector.SummonTemplateId.CompareTo(right.Selector.SummonTemplateId);
            if (summon != 0) return summon;
            return left.Selector.BuffTemplateId.CompareTo(right.Selector.BuffTemplateId);
        }
    }
}
