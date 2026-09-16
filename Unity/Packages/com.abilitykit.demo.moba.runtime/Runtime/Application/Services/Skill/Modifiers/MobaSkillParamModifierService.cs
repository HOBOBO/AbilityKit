using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 面向作用域的修饰器容器与解析器，用于技能和计划动作参数。
    /// </summary>
    [WorldService(typeof(MobaSkillParamModifierService))]
    public sealed class MobaSkillParamModifierService : IService
    {
        private readonly Dictionary<OwnerKey, List<ModifierData>> _modifiersByOwner = new Dictionary<OwnerKey, List<ModifierData>>();
        private readonly ModifierCalculator _calculator = new ModifierCalculator { EnableCache = false };

        public MobaSkillParamGroupResolver Skill { get; }
        public MobaProjectileParamGroupResolver Projectile { get; }
        public MobaSummonParamGroupResolver Summon { get; }

        public MobaSkillParamModifierService()
        {
            Skill = new MobaSkillParamGroupResolver(this);
            Projectile = new MobaProjectileParamGroupResolver(this);
            Summon = new MobaSummonParamGroupResolver(this);
        }

        public void AddModifier(int actorId, in ModifierData modifier)
        {
            AddModifier(MobaModifierOwnerRef.Actor(actorId), in modifier);
        }

        public void AddModifier(MobaModifierOwnerRef owner, in ModifierData modifier)
        {
            if (!owner.IsValid) return;

            var key = new OwnerKey(owner.Scope, owner.Id);
            if (!_modifiersByOwner.TryGetValue(key, out var modifiers) || modifiers == null)
            {
                modifiers = new List<ModifierData>(4);
                _modifiersByOwner[key] = modifiers;
            }

            modifiers.Add(modifier);
        }

        public void AddFixed(int actorId, ModifierKey key, ModifierOp op, float value, int sourceId = 0, int priority = 10)
        {
            AddFixed(MobaModifierOwnerRef.Actor(actorId), key, op, value, sourceId, priority);
        }

        public void AddFixed(MobaModifierOwnerRef owner, ModifierKey key, ModifierOp op, float value, int sourceId = 0, int priority = 10)
        {
            AddModifier(owner, new ModifierData
            {
                Key = key,
                Op = op,
                Magnitude = MagnitudeSource.Fixed(value),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = -1,
                Metadata = ModifierMetadata.CreateByIndex(-1, 0, sourceId)
            });
        }

        public void ClearActor(int actorId)
        {
            ClearOwner(MobaModifierOwnerRef.Actor(actorId));
        }

        public void ClearOwner(MobaModifierOwnerRef owner)
        {
            if (!owner.IsValid) return;
            _modifiersByOwner.Remove(new OwnerKey(owner.Scope, owner.Id));
        }

        public void ClearSource(int actorId, int sourceId)
        {
            ClearSource(MobaModifierOwnerRef.Actor(actorId), sourceId);
        }

        public void ClearSource(MobaModifierOwnerRef owner, int sourceId)
        {
            if (!owner.IsValid) return;
            var key = new OwnerKey(owner.Scope, owner.Id);
            if (!_modifiersByOwner.TryGetValue(key, out var modifiers) || modifiers == null) return;

            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].SourceId == sourceId)
                {
                    modifiers.RemoveAt(i);
                }
            }

            if (modifiers.Count == 0)
            {
                _modifiersByOwner.Remove(key);
            }
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
                        pair.Key.Scope, pair.Key.Id, CloneModifier(in modifier)));
                }
            }
            entries.Sort(MobaSkillParamModifierSnapshotEntry.Compare);
            return new MobaSkillParamModifierServiceSnapshot(entries.ToArray());
        }

        public void RestoreRollbackSnapshot(in MobaSkillParamModifierServiceSnapshot snapshot)
        {
            _modifiersByOwner.Clear();
            var entries = snapshot.Entries;
            if (entries == null) return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var modifier = entry.Modifier;
                AddModifier(new MobaModifierOwnerRef(entry.Scope, entry.OwnerId), CloneModifier(in modifier));
            }
        }

        public int ResolveInt(int actorId, ModifierKey key, int baseValue, IModifierContext context = null)
        {
            return ResolveInt(MobaModifierOwnerRef.Actor(actorId), key, baseValue, context);
        }

        public int ResolveInt(MobaModifierOwnerRef owner, ModifierKey key, int baseValue, IModifierContext context = null)
        {
            if (!owner.IsValid) return baseValue;
            return ResolveInt(new[] { owner }, key, baseValue, context);
        }

        public int ResolveInt(MobaModifierOwnerRef[] ownerChain, ModifierKey key, int baseValue, IModifierContext context = null)
        {
            var filtered = CollectMatchingModifiers(ownerChain, key);
            if (filtered.Count == 0) return baseValue;

            var value = _calculator.Calculate(filtered.ToArray(), baseValue, context).FinalValue;
            if (value <= int.MinValue) return int.MinValue;
            if (value >= int.MaxValue) return int.MaxValue;
            return (int)Math.Round(value);
        }

        public float ResolveFloat(int actorId, ModifierKey key, float baseValue, IModifierContext context = null)
        {
            return ResolveFloat(MobaModifierOwnerRef.Actor(actorId), key, baseValue, context);
        }

        public float ResolveFloat(MobaModifierOwnerRef owner, ModifierKey key, float baseValue, IModifierContext context = null)
        {
            if (!owner.IsValid) return baseValue;
            return ResolveFloat(new[] { owner }, key, baseValue, context);
        }

        public float ResolveFloat(MobaModifierOwnerRef[] ownerChain, ModifierKey key, float baseValue, IModifierContext context = null)
        {
            var filtered = CollectMatchingModifiers(ownerChain, key);
            if (filtered.Count == 0) return baseValue;
            return _calculator.Calculate(filtered.ToArray(), baseValue, context).FinalValue;
        }

        private List<ModifierData> CollectMatchingModifiers(MobaModifierOwnerRef[] ownerChain, ModifierKey key)
        {
            var filtered = new List<ModifierData>();
            if (ownerChain == null || ownerChain.Length == 0) return filtered;

            for (int i = 0; i < ownerChain.Length; i++)
            {
                var owner = ownerChain[i];
                if (!owner.IsValid) continue;

                var ownerKey = new OwnerKey(owner.Scope, owner.Id);
                if (!_modifiersByOwner.TryGetValue(ownerKey, out var modifiers) || modifiers == null || modifiers.Count == 0) continue;

                for (int j = 0; j < modifiers.Count; j++)
                {
                    var modifier = modifiers[j];
                    if (modifier.Key.Equals(key))
                    {
                        filtered.Add(modifier);
                    }
                }
            }

            return filtered;
        }

        public void Dispose()
        {
            _modifiersByOwner.Clear();
        }

        private readonly struct OwnerKey
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
                return obj is OwnerKey other && Scope == other.Scope && Id == other.Id;
            }
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
        public MobaSkillParamModifierSnapshotEntry(MobaModifierOwnerScope scope, int ownerId, ModifierData modifier)
        {
            Scope = scope;
            OwnerId = ownerId;
            Modifier = modifier;
        }

        public MobaModifierOwnerScope Scope { get; }
        public int OwnerId { get; }
        public ModifierData Modifier { get; }

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
            return left.Modifier.Op.CompareTo(right.Modifier.Op);
        }
    }
}
