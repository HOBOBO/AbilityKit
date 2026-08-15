using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.ECS;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 诊断状态采样器：从 MOBA Runtime 的活动对象注册表采样当前帧的世界/角色状态，
    /// 写入平台无关的 <see cref="IBattleDiagnosticStateStore"/>。
    /// 采样失败的单个角色不会中断整批采样。
    /// </summary>
    [WorldService(typeof(MobaBattleDiagnosticStateSampler), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticStateSampler : IService
    {
        private readonly MobaActorRegistry _registry;
        private readonly IBattleDiagnosticStateStore _stateStore;
        private readonly Func<int> _frameProvider;
        private readonly Func<long> _timestampProvider;
        private long _sampleFailureCount;
        private int _lastSuccessfulSampleFrame = BattleDiagnosticFrames.Invalid;
        private string _lastSampleError = string.Empty;

        [WorldInject(required: false)]
        private IFrameTime _frameTime = null;

        [WorldInject(required: false)]
        private MobaSkillCastRuntimeService _skillRuntimes = null;

        [WorldInject(required: false)]
        private MobaTraceRegistry _traceRegistry = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorAttributeStore _attributeStore = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorBuffStore _buffStore = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorTagStore _tagStore = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorEffectStore _effectStore = null;

        [WorldInject(required: false)]
        private IUnitResolver _unitResolver = null;

        public MobaBattleDiagnosticStateSampler(
            MobaActorRegistry registry,
            IBattleDiagnosticStateStore stateStore)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _timestampProvider = System.Diagnostics.Stopwatch.GetTimestamp;
        }

        public MobaBattleDiagnosticStateSampler(
            MobaActorRegistry registry,
            IBattleDiagnosticStateStore stateStore,
            Func<int> frameProvider,
            Func<long> timestampProvider = null)
            : this(registry, stateStore)
        {
            _frameProvider = frameProvider;
            _timestampProvider = timestampProvider ?? System.Diagnostics.Stopwatch.GetTimestamp;
        }

        /// <summary>
        /// 成功写入 State Store 的最后一帧；尚未成功采样时为 Invalid。
        /// </summary>
        public int LastSuccessfulSampleFrame => _lastSuccessfulSampleFrame;

        /// <summary>
        /// 进程内累计的采样失败次数，用于区分数据未产出与采样执行失败。
        /// </summary>
        public long SampleFailureCount => _sampleFailureCount;

        /// <summary>
        /// 最近一次采样异常的简短消息；成功采样后清空。
        /// </summary>
        public string LastSampleError => _lastSampleError;

        /// <summary>
        /// 执行一次完整的世界 + 角色状态采样。
        /// </summary>
        public bool Sample()
        {
            if (_stateStore.IsFrozen)
            {
                return false;
            }

            try
            {
                var frame = ResolveFrame();
                var timestamp = _timestampProvider();
                var scope = _stateStore.Scope;
                var actors = new List<BattleDiagnosticActorSummary>();
                var actorIds = _attributeStore != null || _buffStore != null ||
                               _tagStore != null || _effectStore != null
                    ? new List<long>()
                    : null;
                var attributes = _attributeStore != null
                    ? new List<BattleDiagnosticActorAttribute>()
                    : null;
                var modifiers = _attributeStore != null
                    ? new List<BattleDiagnosticActorAttributeModifier>()
                    : null;
                var buffs = _buffStore != null
                    ? new List<BattleDiagnosticActorBuff>()
                    : null;
                var tags = _tagStore != null
                    ? new List<BattleDiagnosticActorTag>()
                    : null;
                var effects = _effectStore != null
                    ? new List<BattleDiagnosticActorEffect>()
                    : null;

                if (_registry != null)
                {
                    foreach (var entry in _registry.Entries)
                    {
                        var actorId = entry.Key;
                        var entity = entry.Value;

                        if (!TrySampleActor(scope, frame, actorId, entity, out var summary))
                        {
                            continue;
                        }

                        actors.Add(summary);
                        actorIds?.Add(actorId);
                        if (_attributeStore != null)
                        {
                            TrySampleActorAttributes(
                                scope,
                                frame,
                                actorId,
                                entity,
                                attributes,
                                modifiers);
                        }
                        if (_buffStore != null)
                        {
                            TrySampleActorBuffs(
                                scope,
                                frame,
                                actorId,
                                entity,
                                buffs);
                        }
                        if ((_tagStore != null || _effectStore != null) &&
                            _unitResolver != null &&
                            _unitResolver.TryResolve(new EcsEntityId(actorId), out var unit))
                        {
                            if (_tagStore != null)
                            {
                                TrySampleActorTags(scope, frame, actorId, unit, tags);
                            }
                            if (_effectStore != null)
                            {
                                TrySampleActorEffects(scope, frame, actorId, unit, effects);
                            }
                        }
                    }
                }

                var world = new BattleDiagnosticWorldSummary(
                    scope,
                    frame,
                    timestamp,
                    actors.Count,
                    _skillRuntimes?.Count ?? 0,
                    CountActiveTraceRoots());

                if (!_stateStore.TryReplaceSnapshot(world, actors))
                {
                    return false;
                }

                if (_attributeStore != null &&
                    !_attributeStore.IsFrozen &&
                    !_attributeStore.TryReplaceSnapshot(frame, actorIds, attributes, modifiers))
                {
                    return false;
                }

                if (_buffStore != null &&
                    !_buffStore.IsFrozen &&
                    !_buffStore.TryReplaceSnapshot(frame, actorIds, buffs))
                {
                    return false;
                }

                if (_tagStore != null &&
                    !_tagStore.IsFrozen &&
                    !_tagStore.TryReplaceSnapshot(frame, actorIds, tags))
                {
                    return false;
                }

                if (_effectStore != null &&
                    !_effectStore.IsFrozen &&
                    !_effectStore.TryReplaceSnapshot(frame, actorIds, effects))
                {
                    return false;
                }

                _lastSuccessfulSampleFrame = frame;
                _lastSampleError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                _sampleFailureCount++;
                _lastSampleError = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// 将 <see cref="EntityMainType"/> + <see cref="UnitSubType"/> 映射为诊断 ActorKind。
        /// public static 使聚焦测试可在不构造完整 World 的情况下验证映射。
        /// </summary>
        public static BattleDiagnosticActorKind ResolveActorKind(
            EntityMainType mainType,
            UnitSubType unitSubType)
        {
            switch (mainType)
            {
                case EntityMainType.Unit:
                    switch (unitSubType)
                    {
                        case UnitSubType.Hero:
                            return BattleDiagnosticActorKind.Hero;
                        case UnitSubType.Minion:
                            return BattleDiagnosticActorKind.Minion;
                        case UnitSubType.Neutral:
                        case UnitSubType.Boss:
                            return BattleDiagnosticActorKind.Monster;
                        case UnitSubType.Tower:
                        case UnitSubType.Base:
                            return BattleDiagnosticActorKind.Building;
                        default:
                            return BattleDiagnosticActorKind.Hero;
                    }

                case EntityMainType.Projectile:
                    return BattleDiagnosticActorKind.Projectile;

                case EntityMainType.Summon:
                    return BattleDiagnosticActorKind.Summon;

                case EntityMainType.SceneObject:
                    return BattleDiagnosticActorKind.Area;

                default:
                    return BattleDiagnosticActorKind.Unknown;
            }
        }

        /// <summary>
        /// 从单个 ActorEntity 安全采样角色摘要。public static 使聚焦测试可验证映射。
        /// </summary>
        public static bool TrySampleActor(
            BattleDiagnosticSessionScope scope,
            int frame,
            int actorId,
            object entityObj,
            out BattleDiagnosticActorSummary summary)
        {
            summary = default;

            if (entityObj == null || actorId <= 0)
            {
                return false;
            }

            try
            {
                return TrySampleActorCore(scope, frame, actorId, entityObj, out summary);
            }
            catch
            {
                summary = default;
                return false;
            }
        }

        private static bool TrySampleActorCore(
            BattleDiagnosticSessionScope scope,
            int frame,
            int actorId,
            object entityObj,
            out BattleDiagnosticActorSummary summary)
        {
            summary = default;
            var entity = (ActorEntity)entityObj;

            if (entity == null || !entity.isEnabled)
            {
                return false;
            }

            var mainType = entity.hasEntityMainType
                ? entity.entityMainType.Value
                : EntityMainType.None;

            var unitSubType = entity.hasUnitSubType
                ? entity.unitSubType.Value
                : UnitSubType.None;

            var kind = ResolveActorKind(mainType, unitSubType);

            var teamId = entity.hasTeam ? (int)entity.team.Value : 0;

            var configId = entity.hasModelId ? entity.modelId.Value : 0;

            float posX = 0f, posY = 0f, posZ = 0f;
            if (entity.hasTransform)
            {
                var pos = entity.transform.Value.Position;
                posX = pos.X;
                posY = pos.Y;
                posZ = pos.Z;
            }

            float health = 0f;
            float maxHealth = 0f;
            bool isAlive = entity.isEnabled;

            if (entity.hasResourceContainer && entity.resourceContainer.Value != null)
            {
                var container = entity.resourceContainer.Value;
                if (container.Map != null &&
                    container.Map.TryGetValue(ResourceType.Hp, out var hpState) &&
                    hpState != null)
                {
                    health = MobaResourceFixedConvert.ToSingle(hpState.Current);
                }
            }

            if (entity.hasAttributeGroup && entity.attributeGroup.Group != null)
            {
                maxHealth = entity.attributeGroup.Group.GetValue(MobaAttributeIds.MAX_HP);
            }

            if (health <= 0f && maxHealth > 0f)
            {
                isAlive = false;
            }

            summary = new BattleDiagnosticActorSummary(
                scope,
                frame,
                actorId,
                kind,
                configId,
                teamId,
                posX,
                posY,
                posZ,
                health,
                maxHealth,
                isAlive);

            return true;
        }

        public static bool TrySampleActorAttributes(
            BattleDiagnosticSessionScope scope,
            int frame,
            int actorId,
            object entityObj,
            ICollection<BattleDiagnosticActorAttribute> attributes,
            ICollection<BattleDiagnosticActorAttributeModifier> modifiers)
        {
            if (entityObj == null || actorId <= 0 || attributes == null || modifiers == null)
            {
                return false;
            }

            try
            {
                var entity = (ActorEntity)entityObj;
                if (entity == null || !entity.isEnabled ||
                    !entity.hasAttributeGroup || entity.attributeGroup.Group == null)
                {
                    return false;
                }

                foreach (var entry in entity.attributeGroup.Group.Attributes)
                {
                    var attributeId = entry.Key;
                    var instance = entry.Value;
                    if (attributeId <= 0 || instance == null)
                    {
                        continue;
                    }

                    var activeModifiers = instance.GetActiveModifierData();
                    attributes.Add(new BattleDiagnosticActorAttribute(
                        scope,
                        frame,
                        actorId,
                        attributeId,
                        instance.BaseValue,
                        instance.Value,
                        activeModifiers.Length,
                        instance.Id.Name));

                    for (var i = 0; i < activeModifiers.Length; i++)
                    {
                        var modifier = activeModifiers[i];
                        modifiers.Add(new BattleDiagnosticActorAttributeModifier(
                            scope,
                            frame,
                            actorId,
                            attributeId,
                            (int)modifier.Op,
                            modifier.Magnitude.BaseValue,
                            modifier.Priority,
                            modifier.SourceId,
                            (int)modifier.Magnitude.Type));
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySampleActorBuffs(
            BattleDiagnosticSessionScope scope,
            int frame,
            int actorId,
            object entityObj,
            ICollection<BattleDiagnosticActorBuff> buffs)
        {
            if (entityObj == null || actorId <= 0 || buffs == null)
            {
                return false;
            }

            try
            {
                var entity = (ActorEntity)entityObj;
                if (entity == null || !entity.isEnabled)
                {
                    return false;
                }

                var active = entity.hasBuffs ? entity.buffs.Active : null;
                if (active == null)
                {
                    return true;
                }

                for (var i = 0; i < active.Count; i++)
                {
                    var runtime = active[i];
                    if (runtime == null || runtime.BuffId <= 0)
                    {
                        continue;
                    }

                    var continuous = runtime.Continuous;
                    var remaining = continuous != null
                        ? continuous.RemainingSeconds
                        : runtime.Remaining;
                    var intervalRemaining = continuous != null
                        ? continuous.IntervalRemainingSeconds
                        : runtime.IntervalRemainingSeconds;
                    var sourceActorId = runtime.ContextSource.SourceActorId != 0
                        ? runtime.ContextSource.SourceActorId
                        : runtime.SourceId;
                    var rootContextId = runtime.ContextSource.RootContextId != 0
                        ? runtime.ContextSource.RootContextId
                        : runtime.Origin.EffectiveRootContextId;
                    var skillHandle = runtime.SkillRuntimeHandle.IsValid
                        ? runtime.SkillRuntimeHandle
                        : runtime.ContextSource.SkillRuntimeHandle;

                    buffs.Add(new BattleDiagnosticActorBuff(
                        scope,
                        frame,
                        actorId,
                        runtime.BuffId,
                        sourceActorId,
                        runtime.StackCount < 0 ? 0 : runtime.StackCount,
                        NormalizeTime(remaining),
                        NormalizeTime(intervalRemaining),
                        runtime.SourceContextId,
                        runtime.RuntimeContextId,
                        runtime.RuntimeContextVersion < 0 ? 0 : runtime.RuntimeContextVersion,
                        skillHandle.IsValid
                            ? new BattleDiagnosticRuntimeHandle(skillHandle.RuntimeId, skillHandle.Generation)
                            : default,
                        rootContextId,
                        runtime.ModifierBindings?.Count ?? 0));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySampleActorTags(
            BattleDiagnosticSessionScope scope,
            int frame,
            int actorId,
            IUnitFacade unit,
            ICollection<BattleDiagnosticActorTag> tags)
        {
            if (actorId <= 0 || unit?.Tags == null || tags == null)
            {
                return false;
            }

            try
            {
                var sorted = new List<(int TagId, string Name)>();
                foreach (var tag in unit.Tags)
                {
                    if (!tag.IsValid || tag.Value <= 0)
                    {
                        continue;
                    }

                    sorted.Add((tag.Value, tag.TagName ?? string.Empty));
                }

                sorted.Sort((left, right) => left.TagId.CompareTo(right.TagId));
                var sampled = new BattleDiagnosticActorTag[sorted.Count];
                for (var i = 0; i < sorted.Count; i++)
                {
                    sampled[i] = new BattleDiagnosticActorTag(
                        scope,
                        frame,
                        actorId,
                        sorted[i].TagId,
                        sorted[i].Name);
                }

                for (var i = 0; i < sampled.Length; i++)
                {
                    tags.Add(sampled[i]);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySampleActorEffects(
            BattleDiagnosticSessionScope scope,
            int frame,
            int actorId,
            IUnitFacade unit,
            ICollection<BattleDiagnosticActorEffect> effects)
        {
            if (actorId <= 0 || unit?.Effects?.Active == null || effects == null)
            {
                return false;
            }

            try
            {
                var active = unit.Effects.Active;
                var sorted = new List<AbilityKit.Ability.Share.Effect.EffectInstance>(active.Count);
                for (var i = 0; i < active.Count; i++)
                {
                    var instance = active[i];
                    if (instance?.Spec == null || instance.Id <= 0)
                    {
                        continue;
                    }

                    sorted.Add(instance);
                }

                sorted.Sort((left, right) => left.Id.CompareTo(right.Id));
                var sampled = new BattleDiagnosticActorEffect[sorted.Count];
                for (var i = 0; i < sorted.Count; i++)
                {
                    var instance = sorted[i];
                    var spec = instance.Spec;
                    if (!TryMapDurationPolicy(spec.DurationPolicy, out var durationPolicy))
                    {
                        return false;
                    }

                    var hasRemainingTime = spec.DurationPolicy ==
                                           AbilityKit.Ability.Share.Effect.EffectDurationPolicy.Duration;
                    var hasPeriodicTick = spec.PeriodSeconds > 0f &&
                                          !float.IsNaN(spec.PeriodSeconds) &&
                                          !float.IsInfinity(spec.PeriodSeconds);
                    sampled[i] = new BattleDiagnosticActorEffect(
                        scope,
                        frame,
                        actorId,
                        instance.Id,
                        durationPolicy,
                        instance.StackCount < 0 ? 0 : instance.StackCount,
                        NormalizeTime(instance.ElapsedSeconds),
                        NormalizeTime(instance.RemainingSeconds),
                        hasRemainingTime,
                        NormalizeTime(instance.NextTickInSeconds),
                        hasPeriodicTick,
                        NormalizeTime(spec.DurationSeconds),
                        NormalizeTime(spec.PeriodSeconds),
                        spec.Components?.Count ?? 0,
                        spec.ExecutePeriodicOnApply);
                }

                for (var i = 0; i < sampled.Length; i++)
                {
                    effects.Add(sampled[i]);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMapDurationPolicy(
            AbilityKit.Ability.Share.Effect.EffectDurationPolicy policy,
            out BattleDiagnosticEffectDurationPolicy mapped)
        {
            switch (policy)
            {
                case AbilityKit.Ability.Share.Effect.EffectDurationPolicy.Instant:
                    mapped = BattleDiagnosticEffectDurationPolicy.Instant;
                    return true;
                case AbilityKit.Ability.Share.Effect.EffectDurationPolicy.Duration:
                    mapped = BattleDiagnosticEffectDurationPolicy.Duration;
                    return true;
                case AbilityKit.Ability.Share.Effect.EffectDurationPolicy.Infinite:
                    mapped = BattleDiagnosticEffectDurationPolicy.Infinite;
                    return true;
                default:
                    mapped = default;
                    return false;
            }
        }

        private static float NormalizeTime(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return 0f;
            return value;
        }

        private int CountActiveTraceRoots()
        {
            if (_traceRegistry == null) return 0;

            var activeRootCount = 0;
            foreach (var root in _traceRegistry.GetActiveRoots())
            {
                if (root.ActiveCount > 0)
                {
                    activeRootCount++;
                }
            }

            return activeRootCount;
        }

        private int ResolveFrame()
        {
            if (_frameProvider != null)
            {
                return _frameProvider();
            }

            return _frameTime != null ? _frameTime.Frame.Value : 0;
        }
    }
}
